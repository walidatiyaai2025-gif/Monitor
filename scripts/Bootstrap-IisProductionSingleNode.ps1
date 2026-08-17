[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9.-]+$')]
    [string]$HostName,

    [string]$CertificateThumbprint,

    [string]$CertificatePfxPath,

    [Security.SecureString]$CertificatePfxPassword,

    [ValidateSet('Online', 'Offline')]
    [string]$HostingBundleMode = 'Offline',

    [string]$HostingBundleInstallerPath,

    [uri]$HostingBundleDownloadUrl,

    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string]$HostingBundleSha256,

    [string]$SiteName = 'Monitor',

    [string]$AppPoolName = 'Monitor',

    [ValidateRange(1, 65535)]
    [int]$HttpsPort = 443,

    [string]$ReleaseRoot = 'C:\Program Files\Monitor\releases',

    [string]$StateRoot = 'C:\ProgramData\Monitor\App_Data',

    [string]$BootstrapSiteRoot = 'C:\ProgramData\Monitor\bootstrap-site',

    [switch]$Apply,

    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$plannedActions = [Collections.Generic.List[string]]::new()
$appliedActions = [Collections.Generic.List[string]]::new()
$requiredWindowsFeatures = @(
    'Web-Server',
    'Web-Static-Content',
    'Web-Http-Logging',
    'Web-Filtering',
    'Web-Mgmt-Tools',
    'Web-Scripting-Tools'
)
$forbiddenIdentityTypes = @('LocalSystem', 'LocalService', 'NetworkService')

function Add-PlannedAction {
    param([string]$Action)
    if (-not $plannedActions.Contains($Action)) { $plannedActions.Add($Action) }
}

function Add-AppliedAction {
    param([string]$Action)
    if (-not $appliedActions.Contains($Action)) { $appliedActions.Add($Action) }
}

function Assert-WindowsServer {
    if ($env:OS -ne 'Windows_NT') {
        throw 'Monitor IIS bootstrap is supported only on Windows Server.'
    }
}

function Assert-ElevatedWhenApplying {
    if (-not $Apply) { return }
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Apply requires an elevated Administrator PowerShell session.'
    }
}

function Normalize-Thumbprint {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    $normalized = ($Value -replace '[^A-Fa-f0-9]', '').ToUpperInvariant()
    if ($normalized -notmatch '^[A-F0-9]{40}$') {
        throw 'Certificate thumbprint must resolve to exactly 40 hexadecimal characters.'
    }
    return $normalized
}

function Normalize-FullPath {
    param([string]$Value, [string]$Name)
    if ([string]::IsNullOrWhiteSpace($Value)) { throw "$Name is required." }
    if (-not [IO.Path]::IsPathRooted($Value)) { throw "$Name must be an absolute path." }
    if ($Value -match '[\r\n\x00-\x1F]') { throw "$Name contains invalid control characters." }
    if ($Value -match '(?:^|[\\/])\.\.?([\\/]|$)') { throw "$Name must not contain path traversal segments." }
    $full = [IO.Path]::GetFullPath($Value).TrimEnd('\', '/')
    if ($full -eq [IO.Path]::GetPathRoot($full).TrimEnd('\', '/')) { throw "$Name must not be a filesystem root." }
    return $full
}

function Get-DotNetExecutable {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $command) { return [string]$command.Source }
    $wellKnown = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $wellKnown -PathType Leaf) { return $wellKnown }
    return $null
}

function Get-AspNetCoreRuntime8 {
    $dotnet = Get-DotNetExecutable
    if ([string]::IsNullOrWhiteSpace($dotnet)) { return $null }
    $runtimes = & $dotnet --list-runtimes 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return @($runtimes | Where-Object { $_ -match '^Microsoft\.AspNetCore\.App 8\.' } | Select-Object -First 1)[0]
}

function Get-AncmPath {
    $candidates = @(
        (Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'),
        (Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2_outofprocess.dll')
    )
    return @($candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1)[0]
}

function Test-MicrosoftDownloadUri {
    param([uri]$Uri)
    if ($null -eq $Uri -or -not $Uri.IsAbsoluteUri -or $Uri.Scheme -ne 'https') { return $false }
    $host = $Uri.DnsSafeHost.ToLowerInvariant()
    return ($host -eq 'microsoft.com' -or $host.EndsWith('.microsoft.com', [StringComparison]::Ordinal))
}

function Assert-InstallerIntegrity {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Hosting Bundle installer was not found: $Path"
    }
    if ($HostingBundleSha256) {
        $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $HostingBundleSha256.ToLowerInvariant()) {
            throw "Hosting Bundle SHA-256 mismatch. Expected $($HostingBundleSha256.ToLowerInvariant()) but calculated $actual."
        }
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne [Management.Automation.SignatureStatus]::Valid -or
        $null -eq $signature.SignerCertificate -or
        [string]$signature.SignerCertificate.Subject -notmatch 'Microsoft') {
        throw 'Hosting Bundle installer must have a valid Microsoft Authenticode signature.'
    }
}

function Install-HostingBundleIfRequired {
    $runtime = Get-AspNetCoreRuntime8
    $ancm = Get-AncmPath
    if ($null -ne $runtime -and $null -ne $ancm) {
        return [pscustomobject]@{ Changed = $false; RebootRequired = $false }
    }

    Add-PlannedAction 'Install .NET 8 ASP.NET Core Hosting Bundle because runtime and/or ANCM v2 is missing.'
    if (-not $Apply) {
        return [pscustomobject]@{ Changed = $false; RebootRequired = $false }
    }

    $installer = $null
    $downloadedInstaller = $false
    try {
        if ($HostingBundleMode -eq 'Offline') {
            if ([string]::IsNullOrWhiteSpace($HostingBundleInstallerPath)) {
                throw 'Offline Hosting Bundle installation requires -HostingBundleInstallerPath when runtime/ANCM is missing.'
            }
            $installer = [IO.Path]::GetFullPath($HostingBundleInstallerPath)
        }
        else {
            if ($null -eq $HostingBundleDownloadUrl) {
                throw 'Online Hosting Bundle installation requires an explicit -HostingBundleDownloadUrl.'
            }
            if (-not (Test-MicrosoftDownloadUri -Uri $HostingBundleDownloadUrl)) {
                throw 'Online Hosting Bundle URL must be an explicit HTTPS URL hosted on microsoft.com.'
            }
            $installer = Join-Path $env:TEMP ("dotnet-hosting-8-" + [Guid]::NewGuid().ToString('N') + '.exe')
            Invoke-WebRequest -Uri $HostingBundleDownloadUrl.AbsoluteUri -OutFile $installer -UseBasicParsing
            $downloadedInstaller = $true
        }

        Assert-InstallerIntegrity -Path $installer
        $process = Start-Process -FilePath $installer -ArgumentList @('/install', '/quiet', '/norestart') -Wait -PassThru
        if ($process.ExitCode -notin @(0, 3010)) {
            throw "Hosting Bundle installer failed with exit code $($process.ExitCode)."
        }
        Add-AppliedAction 'Installed the operator-approved .NET 8 ASP.NET Core Hosting Bundle.'
        return [pscustomobject]@{ Changed = $true; RebootRequired = ($process.ExitCode -eq 3010) }
    }
    finally {
        if ($downloadedInstaller -and $installer -and (Test-Path -LiteralPath $installer)) {
            Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
        }
    }
}

function Get-PfxLeafCertificate {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Certificate PFX was not found: $Path" }
    $parameters = @{ FilePath = $Path }
    if ($null -ne $CertificatePfxPassword) { $parameters.Password = $CertificatePfxPassword }
    $data = Get-PfxData @parameters
    $leaf = @($data.EndEntityCertificates)
    if ($leaf.Count -ne 1) { throw 'Certificate PFX must contain exactly one end-entity certificate.' }
    return $leaf[0]
}

function Resolve-ApprovedCertificateThumbprint {
    $approvedThumbprint = Normalize-Thumbprint $CertificateThumbprint
    $hasPfx = -not [string]::IsNullOrWhiteSpace($CertificatePfxPath)
    if ($null -eq $approvedThumbprint -and -not $hasPfx) {
        throw 'Supply either an existing -CertificateThumbprint or an explicit -CertificatePfxPath.'
    }

    if ($hasPfx) {
        $pfxPath = [IO.Path]::GetFullPath($CertificatePfxPath)
        $leaf = Get-PfxLeafCertificate -Path $pfxPath
        $pfxThumbprint = Normalize-Thumbprint $leaf.Thumbprint
        if ($approvedThumbprint -and $approvedThumbprint -ne $pfxThumbprint) {
            throw 'CertificatePfxPath does not match the independently supplied CertificateThumbprint.'
        }
        $approvedThumbprint = $pfxThumbprint
        $storePath = "Cert:\LocalMachine\My\$approvedThumbprint"
        if (-not (Test-Path -LiteralPath $storePath)) {
            Add-PlannedAction "Import approved PFX certificate $approvedThumbprint into LocalMachine\\My."
            if ($Apply) {
                $importParameters = @{
                    FilePath = $pfxPath
                    CertStoreLocation = 'Cert:\LocalMachine\My'
                }
                if ($null -ne $CertificatePfxPassword) { $importParameters.Password = $CertificatePfxPassword }
                Import-PfxCertificate @importParameters | Out-Null
                Add-AppliedAction "Imported approved PFX certificate $approvedThumbprint into LocalMachine\\My."
            }
        }
    }

    return $approvedThumbprint
}

function Assert-CertificateReady {
    param([string]$Thumbprint, [switch]$AllowMissingForPlan)
    $path = "Cert:\LocalMachine\My\$Thumbprint"
    if (-not (Test-Path -LiteralPath $path)) {
        if ($AllowMissingForPlan) { return $false }
        throw "Certificate '$Thumbprint' was not found in Cert:\LocalMachine\My after bootstrap."
    }
    $certificate = Get-Item -LiteralPath $path
    if (-not $certificate.HasPrivateKey) { throw 'The production HTTPS certificate has no accessible private key.' }
    if ($certificate.NotAfter -le (Get-Date).AddDays(1)) { throw 'The production HTTPS certificate is expired or expires within 24 hours.' }
    return $true
}

function Ensure-Directory {
    param([string]$Path, [string]$Purpose)
    if (Test-Path -LiteralPath $Path) {
        if (-not (Test-Path -LiteralPath $Path -PathType Container)) { throw "$Purpose path exists but is not a directory: $Path" }
        return
    }
    Add-PlannedAction "Create $Purpose directory '$Path'."
    if ($Apply) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
        Add-AppliedAction "Created $Purpose directory '$Path'."
    }
}

function Test-AclContainsRights {
    param([string]$Path, [string]$Identity, [Security.AccessControl.FileSystemRights]$Rights)
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return $false }
    try {
        $acl = Get-Acl -LiteralPath $Path
        foreach ($rule in $acl.Access) {
            if ($rule.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow -and
                [string]$rule.IdentityReference -ieq $Identity -and
                (($rule.FileSystemRights -band $Rights) -eq $Rights)) {
                return $true
            }
        }
    }
    catch { return $false }
    return $false
}

function Ensure-Acl {
    param([string]$Path, [string]$Identity, [string]$Grant, [Security.AccessControl.FileSystemRights]$Rights)
    if (Test-AclContainsRights -Path $Path -Identity $Identity -Rights $Rights) { return }
    Add-PlannedAction "Grant '$Identity' $Grant on '$Path'."
    if ($Apply) {
        $output = & icacls.exe $Path /grant "$Identity`:$Grant" 2>&1
        if ($LASTEXITCODE -ne 0) { throw "icacls failed for '$Path': $($output -join [Environment]::NewLine)" }
        Add-AppliedAction "Granted '$Identity' $Grant on '$Path'."
    }
}

Assert-WindowsServer
Assert-ElevatedWhenApplying

$releaseRootFull = Normalize-FullPath -Value $ReleaseRoot -Name 'ReleaseRoot'
$stateRootFull = Normalize-FullPath -Value $StateRoot -Name 'StateRoot'
$bootstrapRootFull = Normalize-FullPath -Value $BootstrapSiteRoot -Name 'BootstrapSiteRoot'
if ($stateRootFull.StartsWith($releaseRootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    $stateRootFull.Equals($releaseRootFull, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'StateRoot must be outside ReleaseRoot.'
}

$windowsFeatureCommand = Get-Command Get-WindowsFeature -ErrorAction SilentlyContinue
if ($null -eq $windowsFeatureCommand) {
    if ($Apply) { throw 'Get-WindowsFeature is unavailable. Run on a supported Windows Server with ServerManager.' }
    Add-PlannedAction 'Validate/install Windows Server IIS features after ServerManager becomes available.'
}
else {
    $missingFeatures = @()
    foreach ($featureName in $requiredWindowsFeatures) {
        $feature = Get-WindowsFeature -Name $featureName
        if ($null -eq $feature -or -not $feature.Installed) { $missingFeatures += $featureName }
    }
    if ($missingFeatures.Count -gt 0) {
        Add-PlannedAction ("Install missing IIS Windows features: " + ($missingFeatures -join ', ') + '.')
        if ($Apply) {
            $installResult = Install-WindowsFeature -Name $missingFeatures -IncludeManagementTools
            if (-not $installResult.Success) { throw 'Install-WindowsFeature did not report success.' }
            Add-AppliedAction ("Installed IIS Windows features: " + ($missingFeatures -join ', ') + '.')
        }
    }
}

$hostingBundleResult = Install-HostingBundleIfRequired
if ($hostingBundleResult.RebootRequired) {
    throw 'Hosting Bundle installation requested a reboot (3010). Reboot the server, then rerun the same bootstrap/install command; all completed steps are idempotent.'
}

$approvedCertificateThumbprint = Resolve-ApprovedCertificateThumbprint
$certificateReady = Assert-CertificateReady -Thumbprint $approvedCertificateThumbprint -AllowMissingForPlan:(-not $Apply)

$webAdministrationAvailable = $null -ne (Get-Module -ListAvailable -Name WebAdministration)
if (-not $webAdministrationAvailable) {
    if ($Apply) { throw 'WebAdministration is unavailable after IIS feature installation.' }
    Add-PlannedAction 'Create/validate Monitor app pool, IIS site and HTTPS binding after WebAdministration is available.'
}
else {
    Import-Module WebAdministration -ErrorAction Stop
    $poolPath = "IIS:\AppPools\$AppPoolName"
    if (-not (Test-Path -LiteralPath $poolPath)) {
        Add-PlannedAction "Create application pool '$AppPoolName' with No Managed Code and ApplicationPoolIdentity."
        if ($Apply) {
            New-WebAppPool -Name $AppPoolName | Out-Null
            Set-ItemProperty -LiteralPath $poolPath -Name managedRuntimeVersion -Value ''
            Set-ItemProperty -LiteralPath $poolPath -Name processModel.identityType -Value 'ApplicationPoolIdentity'
            Add-AppliedAction "Created application pool '$AppPoolName' with No Managed Code and ApplicationPoolIdentity."
        }
    }

    if (Test-Path -LiteralPath $poolPath) {
        $pool = Get-Item -LiteralPath $poolPath
        if (-not [string]::IsNullOrWhiteSpace([string]$pool.managedRuntimeVersion)) {
            throw "Existing application pool '$AppPoolName' must already use No Managed Code; bootstrap will not silently rewrite an unexpected pool."
        }
        $identityType = [string]$pool.processModel.identityType
        if ($forbiddenIdentityTypes -contains $identityType) {
            throw "Existing application pool '$AppPoolName' uses forbidden identity '$identityType'. Bootstrap will not elevate or replace identities."
        }
        if ($identityType -notin @('ApplicationPoolIdentity', 'SpecificUser')) {
            throw "Existing application pool '$AppPoolName' identity '$identityType' is not an approved ApplicationPoolIdentity/SpecificUser configuration."
        }
        if ($identityType -eq 'SpecificUser' -and [string]::IsNullOrWhiteSpace([string]$pool.processModel.userName)) {
            throw "Existing SpecificUser application pool '$AppPoolName' has no configured user name."
        }
    }

    $sitePath = "IIS:\Sites\$SiteName"
    if (-not (Test-Path -LiteralPath $sitePath)) {
        Add-PlannedAction "Create IIS site '$SiteName' with HTTPS-only binding for $HostName`:$HttpsPort."
        if ($Apply) {
            if (-not (Test-Path -LiteralPath $bootstrapRootFull -PathType Container)) {
                New-Item -ItemType Directory -Path $bootstrapRootFull -Force | Out-Null
            }
            New-Website -Name $SiteName -PhysicalPath $bootstrapRootFull -ApplicationPool $AppPoolName -Port $HttpsPort -HostHeader $HostName -Ssl | Out-Null
            Add-AppliedAction "Created IIS site '$SiteName' with HTTPS binding for $HostName`:$HttpsPort."
        }
    }

    if (Test-Path -LiteralPath $sitePath) {
        $site = Get-Item -LiteralPath $sitePath
        if ([string]$site.applicationPool -ne $AppPoolName) {
            throw "Existing IIS site '$SiteName' is assigned to '$([string]$site.applicationPool)' instead of '$AppPoolName'. Bootstrap will not silently reassign it."
        }

        $bindingSuffix = ":$HttpsPort`:$HostName"
        $binding = @(Get-WebBinding -Name $SiteName -Protocol https | Where-Object {
            ([string]$_.bindingInformation).EndsWith($bindingSuffix, [StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1)[0]
        if ($null -eq $binding) {
            Add-PlannedAction "Create HTTPS binding for $HostName`:$HttpsPort on site '$SiteName'."
            if ($Apply) {
                New-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $HostName -SslFlags 1
                $binding = @(Get-WebBinding -Name $SiteName -Protocol https | Where-Object {
                    ([string]$_.bindingInformation).EndsWith($bindingSuffix, [StringComparison]::OrdinalIgnoreCase)
                } | Select-Object -First 1)[0]
                Add-AppliedAction "Created HTTPS binding for $HostName`:$HttpsPort on site '$SiteName'."
            }
        }

        if ($null -ne $binding) {
            $currentBindingThumbprint = Normalize-Thumbprint ([string]$binding.certificateHash)
            if ($currentBindingThumbprint -and $currentBindingThumbprint -ne $approvedCertificateThumbprint) {
                throw "Existing HTTPS binding for $HostName`:$HttpsPort uses a different certificate. Bootstrap will not overwrite an unexpected binding certificate."
            }
            if (-not $currentBindingThumbprint) {
                Add-PlannedAction "Bind approved certificate $approvedCertificateThumbprint to $HostName`:$HttpsPort."
                if ($Apply) {
                    if (-not $certificateReady) { throw 'Approved certificate is not ready for HTTPS binding.' }
                    $binding.AddSslCertificate($approvedCertificateThumbprint, 'My')
                    Add-AppliedAction "Bound approved certificate $approvedCertificateThumbprint to $HostName`:$HttpsPort."
                }
            }
        }
    }
}

Ensure-Directory -Path $releaseRootFull -Purpose 'immutable release root'
Ensure-Directory -Path $stateRootFull -Purpose 'stable App_Data state root'
Ensure-Directory -Path $bootstrapRootFull -Purpose 'bootstrap site root'

if ($Apply -and -not (Test-Path -LiteralPath "IIS:\AppPools\$AppPoolName")) {
    throw "Application pool '$AppPoolName' is unavailable after bootstrap."
}

if ($webAdministrationAvailable -or $Apply) {
    if (-not (Get-Module WebAdministration)) { Import-Module WebAdministration -ErrorAction Stop }
    $pool = Get-Item -LiteralPath "IIS:\AppPools\$AppPoolName"
    $aclIdentity = if ([string]$pool.processModel.identityType -eq 'SpecificUser') {
        [string]$pool.processModel.userName
    } else {
        "IIS AppPool\$AppPoolName"
    }
    if ([string]::IsNullOrWhiteSpace($aclIdentity)) { throw 'Could not resolve the approved app-pool identity for ACL provisioning.' }

    Ensure-Acl -Path $stateRootFull -Identity $aclIdentity -Grant '(OI)(CI)M' -Rights ([Security.AccessControl.FileSystemRights]::Modify)
    Ensure-Acl -Path $releaseRootFull -Identity $aclIdentity -Grant '(OI)(CI)RX' -Rights ([Security.AccessControl.FileSystemRights]::ReadAndExecute)
    Ensure-Acl -Path $bootstrapRootFull -Identity $aclIdentity -Grant '(OI)(CI)RX' -Rights ([Security.AccessControl.FileSystemRights]::ReadAndExecute)
}

$runtimeAfter = Get-AspNetCoreRuntime8
$ancmAfter = Get-AncmPath
$requiresChanges = $plannedActions.Count -gt 0

if (-not $Apply) {
    $result = [pscustomobject]@{
        Mode = 'PLAN ONLY'
        Apply = $false
        HostName = $HostName
        HttpsPort = $HttpsPort
        SiteName = $SiteName
        AppPoolName = $AppPoolName
        CertificateThumbprint = $approvedCertificateThumbprint
        HostingBundleMode = $HostingBundleMode
        AspNetCoreRuntime = [string]$runtimeAfter
        AspNetCoreModulePath = [string]$ancmAfter
        RequiresChanges = $requiresChanges
        PlannedActions = @($plannedActions)
        AppliedActions = @()
    }
    if ($PassThru) { return $result }
    $result | Format-List
    if ($requiresChanges) {
        Write-Host 'Monitor IIS bootstrap PLAN ONLY completed. No IIS, Windows feature, runtime, certificate, filesystem or ACL changes were made.'
    } else {
        Write-Host 'Monitor IIS bootstrap PLAN ONLY found no bootstrap changes to apply. Existing production preflight is still authoritative.'
    }
    return
}

$preflightScript = Join-Path $PSScriptRoot 'Test-IisProductionPrerequisites.ps1'
if (-not (Test-Path -LiteralPath $preflightScript -PathType Leaf)) {
    throw 'Test-IisProductionPrerequisites.ps1 is required beside the bootstrap script.'
}
$preflight = & $preflightScript `
    -HostName $HostName `
    -CertificateThumbprint $approvedCertificateThumbprint `
    -SiteName $SiteName `
    -AppPoolName $AppPoolName `
    -HttpsPort $HttpsPort `
    -PassThru

$result = [pscustomobject]@{
    Mode = 'APPLY'
    Apply = $true
    HostName = $HostName
    HttpsPort = $HttpsPort
    SiteName = $SiteName
    AppPoolName = $AppPoolName
    CertificateThumbprint = $approvedCertificateThumbprint
    HostingBundleMode = $HostingBundleMode
    AspNetCoreRuntime = [string]$preflight.AspNetCoreRuntime
    AspNetCoreModulePath = [string]$preflight.AspNetCoreModulePath
    RequiresChanges = $false
    PlannedActions = @($plannedActions)
    AppliedActions = @($appliedActions)
    ProductionPreflightReady = [bool]$preflight.Ready
}
if ($PassThru) { return $result }
$result | Format-List
Write-Host 'Monitor IIS bootstrap APPLY completed and the existing production preflight passed. This does not satisfy #162 or any #116 production acceptance gate.'
