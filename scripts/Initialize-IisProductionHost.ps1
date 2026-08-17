[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$HostName,

    [string]$CertificateThumbprint,
    [string]$PfxPath,
    [Security.SecureString]$PfxPassword,

    [ValidateSet('Auto', 'Online', 'Offline')]
    [string]$HostingBundleMode = 'Auto',

    [string]$HostingBundleInstallerPath,
    [Uri]$HostingBundleUrl,

    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string]$HostingBundleSha256,

    [ValidateNotNullOrEmpty()]
    [string]$SiteName = 'Monitor',

    [ValidateNotNullOrEmpty()]
    [string]$AppPoolName = 'Monitor',

    [ValidateRange(1, 65535)]
    [int]$HttpsPort = 443,

    [string]$ReleaseRoot = 'C:\Program Files\Monitor\releases',
    [string]$StateRoot = 'C:\ProgramData\Monitor\App_Data',
    [string]$BootstrapSitePath = 'C:\ProgramData\Monitor\bootstrap-site',

    [switch]$Apply,
    [switch]$PassThru
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Normalize-Thumbprint {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    return ($Value -replace '[^A-Fa-f0-9]', '').ToUpperInvariant()
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Assert-ConcreteHostName {
    param([string]$Value)
    if ($Value -match '^[\s\.]*$' -or $Value.Contains('*') -or $Value.Contains('/') -or $Value.Contains(':')) {
        throw 'HostName must be one concrete DNS host name without scheme, wildcard, path, or port.'
    }
}

function Get-HostingBundleState {
    $runtime = $null
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $dotnet) {
        $runtime = & $dotnet.Source --list-runtimes 2>$null |
            Where-Object { $_ -match '^Microsoft\.AspNetCore\.App 8\.' } |
            Select-Object -First 1
    }

    $ancmCandidates = @()
    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $ancmCandidates += (Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll')
    }
    $programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $ancmCandidates += (Join-Path $programFilesX86 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll')
    }
    $ancmPath = $ancmCandidates |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1

    return [pscustomobject]@{
        Runtime = [string]$runtime
        AncmPath = [string]$ancmPath
        Ready = (-not [string]::IsNullOrWhiteSpace([string]$runtime)) -and (-not [string]::IsNullOrWhiteSpace([string]$ancmPath))
    }
}

function Assert-MicrosoftHostingBundleUrl {
    param([Uri]$Uri)
    if ($null -eq $Uri) { throw 'HostingBundleUrl is required for Online mode.' }
    if ($Uri.Scheme -ne 'https') { throw 'HostingBundleUrl must use HTTPS.' }

    $allowedHosts = @(
        'download.visualstudio.microsoft.com',
        'builds.dotnet.microsoft.com'
    )
    if (-not ($allowedHosts -contains $Uri.DnsSafeHost.ToLowerInvariant())) {
        throw "HostingBundleUrl host '$($Uri.DnsSafeHost)' is not an approved Microsoft download host."
    }
}

function Assert-FileSha256 {
    param([string]$Path, [string]$ExpectedSha256, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($ExpectedSha256)) { return }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    $expected = $ExpectedSha256.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "$Label SHA-256 mismatch. Expected $expected but calculated $actual."
    }
}

function Get-CertificateByThumbprint {
    param([string]$Thumbprint)
    $normalized = Normalize-Thumbprint $Thumbprint
    if ([string]::IsNullOrWhiteSpace($normalized)) { return $null }
    $path = "Cert:\LocalMachine\My\$normalized"
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    return Get-Item -LiteralPath $path
}

function Assert-UsableMachineCertificate {
    param([object]$Certificate)
    if ($null -eq $Certificate) {
        throw 'The approved production certificate was not found in Cert:\LocalMachine\My.'
    }
    if (-not $Certificate.HasPrivateKey) {
        throw 'The production HTTPS certificate has no accessible private key.'
    }
    if ($Certificate.NotAfter -le (Get-Date).AddDays(1)) {
        throw 'The production HTTPS certificate is expired or expires within 24 hours.'
    }
}

function Get-IisInfrastructureState {
    param([string]$ExpectedThumbprint)

    $state = [ordered]@{
        ModuleAvailable = $false
        AppPoolExists = $false
        SiteExists = $false
        BindingExists = $false
        BindingMatchesCertificate = $false
        AppPoolIdentityType = $null
        AppPoolIdentityName = $null
        SitePhysicalPath = $null
    }

    if (-not (Get-Module -ListAvailable -Name WebAdministration)) {
        return [pscustomobject]$state
    }

    Import-Module WebAdministration -ErrorAction Stop
    $state.ModuleAvailable = $true

    $appPoolPath = "IIS:\AppPools\$AppPoolName"
    if (Test-Path -LiteralPath $appPoolPath) {
        $state.AppPoolExists = $true
        $pool = Get-Item -LiteralPath $appPoolPath
        if (-not [string]::IsNullOrWhiteSpace([string]$pool.managedRuntimeVersion)) {
            throw "Application pool '$AppPoolName' must use No Managed Code (empty managedRuntimeVersion)."
        }
        $identityType = [string]$pool.processModel.identityType
        if (@('LocalSystem', 'LocalService', 'NetworkService') -contains $identityType) {
            throw "Application pool '$AppPoolName' uses forbidden high/shared privilege identity type '$identityType'."
        }
        if ($identityType -eq 'SpecificUser' -and [string]::IsNullOrWhiteSpace([string]$pool.processModel.userName)) {
            throw "Application pool '$AppPoolName' uses SpecificUser without an approved user name."
        }
        $state.AppPoolIdentityType = $identityType
        $state.AppPoolIdentityName = if ($identityType -eq 'ApplicationPoolIdentity') { "IIS AppPool\$AppPoolName" } else { [string]$pool.processModel.userName }
    }

    $sitePath = "IIS:\Sites\$SiteName"
    if (Test-Path -LiteralPath $sitePath) {
        $state.SiteExists = $true
        $site = Get-Item -LiteralPath $sitePath
        $state.SitePhysicalPath = [string]$site.physicalPath
        if ([string]$site.applicationPool -ne $AppPoolName) {
            throw "IIS site '$SiteName' is assigned to application pool '$([string]$site.applicationPool)' instead of required '$AppPoolName'."
        }

        $expectedBindingSuffix = ":$HttpsPort`:$HostName"
        $binding = @(Get-WebBinding -Name $SiteName -Protocol https) |
            Where-Object { ([string]$_.bindingInformation).EndsWith($expectedBindingSuffix, [StringComparison]::OrdinalIgnoreCase) } |
            Select-Object -First 1
        if ($null -ne $binding) {
            $state.BindingExists = $true
            if (-not [string]::IsNullOrWhiteSpace($ExpectedThumbprint)) {
                $bindingThumbprint = Normalize-Thumbprint ([string]$binding.certificateHash)
                $state.BindingMatchesCertificate = $bindingThumbprint -eq (Normalize-Thumbprint $ExpectedThumbprint)
                if (-not $state.BindingMatchesCertificate) {
                    throw 'Existing HTTPS binding certificate does not match the approved certificate thumbprint. Refusing to replace it implicitly.'
                }
            }
        }
    }

    return [pscustomobject]$state
}

if (-not $IsWindows) { throw 'Monitor IIS bootstrap is supported only on Windows Server.' }
Assert-ConcreteHostName -Value $HostName

$usingThumbprint = -not [string]::IsNullOrWhiteSpace($CertificateThumbprint)
$usingPfx = -not [string]::IsNullOrWhiteSpace($PfxPath)
if ($usingThumbprint -eq $usingPfx) {
    throw 'Specify exactly one certificate source: CertificateThumbprint or PfxPath.'
}
if ($usingPfx -and -not (Test-Path -LiteralPath $PfxPath -PathType Leaf)) {
    throw "PFX file was not found: $PfxPath"
}
if ($Apply -and -not (Test-IsAdministrator)) {
    throw 'Applying the IIS host bootstrap requires an elevated PowerShell session.'
}

$requiredFeatures = @(
    'Web-Server',
    'Web-WebServer',
    'Web-Static-Content',
    'Web-Http-Errors',
    'Web-Filtering',
    'Web-Mgmt-Tools',
    'Web-Mgmt-Console',
    'Web-Scripting-Tools'
)
if (-not (Get-Command Get-WindowsFeature -ErrorAction SilentlyContinue)) {
    throw 'Get-WindowsFeature is unavailable. Run this bootstrap on a supported Windows Server with ServerManager.'
}
$featureState = @(Get-WindowsFeature -Name $requiredFeatures)
$missingFeatures = @($featureState | Where-Object { -not $_.Installed } | ForEach-Object { $_.Name })

$hostingState = Get-HostingBundleState
$resolvedBundleMode = $HostingBundleMode
if ($resolvedBundleMode -eq 'Auto') {
    if (-not [string]::IsNullOrWhiteSpace($HostingBundleInstallerPath)) { $resolvedBundleMode = 'Offline' }
    elseif ($null -ne $HostingBundleUrl) { $resolvedBundleMode = 'Online' }
}
if (-not $hostingState.Ready) {
    if ($resolvedBundleMode -eq 'Offline') {
        if ([string]::IsNullOrWhiteSpace($HostingBundleInstallerPath) -or -not (Test-Path -LiteralPath $HostingBundleInstallerPath -PathType Leaf)) {
            throw 'Offline Hosting Bundle installation requires an existing HostingBundleInstallerPath.'
        }
        Assert-FileSha256 -Path $HostingBundleInstallerPath -ExpectedSha256 $HostingBundleSha256 -Label 'Offline Hosting Bundle installer'
    }
    elseif ($resolvedBundleMode -eq 'Online') {
        Assert-MicrosoftHostingBundleUrl -Uri $HostingBundleUrl
    }
    else {
        throw '.NET 8 ASP.NET Core Runtime / ANCM is missing. Supply an Offline installer path or an explicit approved Microsoft HostingBundleUrl.'
    }
}

$resolvedThumbprint = Normalize-Thumbprint $CertificateThumbprint
$certificate = if ($usingThumbprint) { Get-CertificateByThumbprint -Thumbprint $resolvedThumbprint } else { $null }
if ($usingThumbprint -and $null -ne $certificate) {
    Assert-UsableMachineCertificate -Certificate $certificate
}

$initialIisState = Get-IisInfrastructureState -ExpectedThumbprint $resolvedThumbprint
$changes = [System.Collections.Generic.List[string]]::new()
if ($missingFeatures.Count -gt 0) { $changes.Add("Install missing IIS roles/features: $($missingFeatures -join ', ')") }
if (-not $hostingState.Ready) {
    if ($resolvedBundleMode -eq 'Offline') { $changes.Add('Install .NET 8 Hosting Bundle from the operator-supplied local installer') }
    else { $changes.Add("Download the .NET 8 Hosting Bundle only from $($HostingBundleUrl.AbsoluteUri) and install it") }
}
if ($usingThumbprint -and $null -eq $certificate) {
    throw "Certificate '$resolvedThumbprint' was not found in Cert:\LocalMachine\My. Supply the correct thumbprint or use PfxPath."
}
if ($usingPfx) { $changes.Add('Import the explicitly supplied PFX into Cert:\LocalMachine\My without exporting or logging its password') }
if (-not $initialIisState.AppPoolExists) { $changes.Add("Create application pool '$AppPoolName' as No Managed Code using ApplicationPoolIdentity") }
if (-not $initialIisState.SiteExists) { $changes.Add("Create IIS site '$SiteName' on HTTPS $HostName`:$HttpsPort using the bootstrap physical path") }
elseif (-not $initialIisState.BindingExists) { $changes.Add("Create the missing SNI HTTPS binding for $HostName`:$HttpsPort") }
$changes.Add("Ensure filesystem roots exist: '$ReleaseRoot', '$StateRoot', '$BootstrapSitePath'")
$changes.Add("Apply least-privilege ACLs for the '$AppPoolName' filesystem identity")

$plan = [ordered]@{
    mode = if ($Apply) { 'Apply' } else { 'PlanOnly' }
    hostName = $HostName
    httpsPort = $HttpsPort
    siteName = $SiteName
    appPoolName = $AppPoolName
    certificateSource = if ($usingThumbprint) { 'ExistingMachineThumbprint' } else { 'OperatorSuppliedPfx' }
    certificateThumbprint = if ($usingThumbprint) { $resolvedThumbprint } else { '<resolved-after-pfx-import>' }
    hostingBundleMode = $resolvedBundleMode
    hostingBundleReady = $hostingState.Ready
    missingIisFeatures = $missingFeatures
    actions = @($changes)
}

if (-not $Apply) {
    $requiresInfrastructureApply = ($missingFeatures.Count -gt 0) -or (-not $hostingState.Ready) -or $usingPfx -or (-not $initialIisState.AppPoolExists) -or (-not $initialIisState.SiteExists) -or (-not $initialIisState.BindingExists)
    $result = [pscustomobject]@{
        Mode = 'PlanOnly'
        Applied = $false
        HostName = $HostName
        HttpsPort = $HttpsPort
        SiteName = $SiteName
        AppPoolName = $AppPoolName
        CertificateThumbprint = $resolvedThumbprint
        HostingBundleReady = $hostingState.Ready
        MissingIisFeatures = $missingFeatures
        ReadyForPreflight = -not $requiresInfrastructureApply
        RebootRequired = $false
        Plan = $plan
    }
    if (-not $PassThru) {
        $plan | ConvertTo-Json -Depth 8
        Write-Host 'PLAN ONLY. No Windows feature, runtime, IIS, certificate, binding, filesystem or ACL changes were made.'
        Write-Host 'Re-run with -Apply only after reviewing this plan and the certificate/Hosting Bundle inputs.'
    }
    return $result
}

$rebootRequired = $false
if ($missingFeatures.Count -gt 0) {
    $installResult = Install-WindowsFeature -Name $missingFeatures -IncludeManagementTools
    if (-not $installResult.Success) { throw 'Failed to install all required IIS roles/features.' }
    if ([string]$installResult.RestartNeeded -eq 'Yes') { $rebootRequired = $true }
}

$hostingState = Get-HostingBundleState
if (-not $hostingState.Ready) {
    $installerPath = $HostingBundleInstallerPath
    $downloadedInstaller = $false
    try {
        if ($resolvedBundleMode -eq 'Online') {
            Assert-MicrosoftHostingBundleUrl -Uri $HostingBundleUrl
            $installerPath = Join-Path ([IO.Path]::GetTempPath()) ("dotnet-hosting-8-$([Guid]::NewGuid().ToString('N')).exe")
            Invoke-WebRequest -Uri $HostingBundleUrl.AbsoluteUri -OutFile $installerPath -UseBasicParsing -MaximumRedirection 5
            $downloadedInstaller = $true
        }
        Assert-FileSha256 -Path $installerPath -ExpectedSha256 $HostingBundleSha256 -Label '.NET 8 Hosting Bundle installer'
        $process = Start-Process -FilePath $installerPath -ArgumentList @('/install', '/quiet', '/norestart') -Wait -PassThru
        if (@(0, 3010) -notcontains $process.ExitCode) {
            throw ".NET 8 Hosting Bundle installer failed with exit code $($process.ExitCode)."
        }
        if ($process.ExitCode -eq 3010) { $rebootRequired = $true }
    }
    finally {
        if ($downloadedInstaller -and -not [string]::IsNullOrWhiteSpace($installerPath) -and (Test-Path -LiteralPath $installerPath)) {
            Remove-Item -LiteralPath $installerPath -Force -ErrorAction SilentlyContinue
        }
    }
    $hostingState = Get-HostingBundleState
    if (-not $hostingState.Ready) {
        throw '.NET 8 Hosting Bundle completed but ASP.NET Core Runtime / ANCM is still not detectable. Reboot if required, then rerun the bootstrap.'
    }
}

if ($usingPfx) {
    $importArgs = @{
        FilePath = $PfxPath
        CertStoreLocation = 'Cert:\LocalMachine\My'
        Exportable = $false
    }
    if ($null -ne $PfxPassword) { $importArgs['Password'] = $PfxPassword }
    $imported = Import-PfxCertificate @importArgs
    if ($null -eq $imported) { throw 'PFX import did not return an imported certificate.' }
    $resolvedThumbprint = Normalize-Thumbprint ([string]$imported.Thumbprint)
    $certificate = Get-CertificateByThumbprint -Thumbprint $resolvedThumbprint
}
Assert-UsableMachineCertificate -Certificate $certificate

Import-Module WebAdministration -ErrorAction Stop
$appPoolPath = "IIS:\AppPools\$AppPoolName"
if (-not (Test-Path -LiteralPath $appPoolPath)) {
    New-WebAppPool -Name $AppPoolName | Out-Null
    Set-ItemProperty -LiteralPath $appPoolPath -Name managedRuntimeVersion -Value ''
    Set-ItemProperty -LiteralPath $appPoolPath -Name processModel.identityType -Value 'ApplicationPoolIdentity'
}
$pool = Get-Item -LiteralPath $appPoolPath
if (-not [string]::IsNullOrWhiteSpace([string]$pool.managedRuntimeVersion)) {
    throw "Application pool '$AppPoolName' must use No Managed Code."
}
$identityType = [string]$pool.processModel.identityType
if (@('LocalSystem', 'LocalService', 'NetworkService') -contains $identityType) {
    throw "Application pool '$AppPoolName' uses forbidden identity type '$identityType'."
}
$aclIdentity = if ($identityType -eq 'ApplicationPoolIdentity') { "IIS AppPool\$AppPoolName" } else { [string]$pool.processModel.userName }
if ([string]::IsNullOrWhiteSpace($aclIdentity)) { throw 'Could not resolve the approved application-pool filesystem identity.' }

foreach ($directory in @($ReleaseRoot, $StateRoot, $BootstrapSitePath)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
$bootstrapMarker = Join-Path $BootstrapSitePath 'bootstrap.txt'
if (-not (Test-Path -LiteralPath $bootstrapMarker -PathType Leaf)) {
    'Monitor bootstrap placeholder. Deploy-ProductionSingleNode.ps1 replaces the site physicalPath with an immutable release.' |
        Set-Content -LiteralPath $bootstrapMarker -Encoding utf8NoBOM
}

$sitePath = "IIS:\Sites\$SiteName"
if (-not (Test-Path -LiteralPath $sitePath)) {
    New-Website -Name $SiteName -PhysicalPath $BootstrapSitePath -ApplicationPool $AppPoolName -Port $HttpsPort -HostHeader $HostName -Ssl | Out-Null
    $createdBindingInfo = "*:$HttpsPort`:$HostName"
    Set-WebBinding -Name $SiteName -BindingInformation $createdBindingInfo -PropertyName sslFlags -Value 1
}
$site = Get-Item -LiteralPath $sitePath
if ([string]$site.applicationPool -ne $AppPoolName) {
    throw "Existing IIS site '$SiteName' is not assigned to required application pool '$AppPoolName'."
}

$expectedBindingSuffix = ":$HttpsPort`:$HostName"
$binding = @(Get-WebBinding -Name $SiteName -Protocol https) |
    Where-Object { ([string]$_.bindingInformation).EndsWith($expectedBindingSuffix, [StringComparison]::OrdinalIgnoreCase) } |
    Select-Object -First 1
if ($null -eq $binding) {
    New-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $HostName -SslFlags 1 | Out-Null
    $binding = @(Get-WebBinding -Name $SiteName -Protocol https) |
        Where-Object { ([string]$_.bindingInformation).EndsWith($expectedBindingSuffix, [StringComparison]::OrdinalIgnoreCase) } |
        Select-Object -First 1
}
if ($null -eq $binding) { throw 'Failed to create or locate the required HTTPS binding.' }
$bindingThumbprint = Normalize-Thumbprint ([string]$binding.certificateHash)
if ([string]::IsNullOrWhiteSpace($bindingThumbprint)) {
    $binding.AddSslCertificate($resolvedThumbprint, 'my')
}
elseif ($bindingThumbprint -ne $resolvedThumbprint) {
    throw 'Existing HTTPS binding certificate does not match the approved certificate thumbprint. Refusing to replace it implicitly.'
}

& icacls.exe $StateRoot /grant "${aclIdentity}:(OI)(CI)M" /T /C | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Failed to grant Modify permission on the stable App_Data root.' }
& icacls.exe $ReleaseRoot /grant "${aclIdentity}:(OI)(CI)RX" /T /C | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Failed to grant Read/Execute permission on the release root.' }
& icacls.exe $BootstrapSitePath /grant "${aclIdentity}:(OI)(CI)RX" /T /C | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Failed to grant Read/Execute permission on the bootstrap site root.' }

$finalIisState = Get-IisInfrastructureState -ExpectedThumbprint $resolvedThumbprint
if (-not ($finalIisState.AppPoolExists -and $finalIisState.SiteExists -and $finalIisState.BindingExists -and $finalIisState.BindingMatchesCertificate)) {
    throw 'IIS bootstrap completed but the required app pool/site/HTTPS binding state did not verify.'
}

$result = [pscustomobject]@{
    Mode = 'Apply'
    Applied = $true
    HostName = $HostName
    HttpsPort = $HttpsPort
    SiteName = $SiteName
    AppPoolName = $AppPoolName
    AppPoolIdentityType = $finalIisState.AppPoolIdentityType
    CertificateThumbprint = $resolvedThumbprint
    HostingBundleReady = $hostingState.Ready
    MissingIisFeatures = @()
    ReadyForPreflight = $true
    RebootRequired = $rebootRequired
    Plan = $plan
}

if ($PassThru) { return $result }
$result | Format-List
Write-Host 'Monitor IIS host bootstrap is ready for the authoritative Test-IisProductionPrerequisites.ps1 preflight.'
if ($rebootRequired) {
    Write-Warning 'A Windows feature or Hosting Bundle installer requested a reboot. Perform the approved reboot before production cutover if required by platform policy.'
}
