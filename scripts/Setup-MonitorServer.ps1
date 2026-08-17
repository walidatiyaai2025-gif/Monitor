[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$HostName,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Fa-f0-9 ]+$')]
    [string]$CertificateThumbprint,

    [string]$SiteName = 'Monitor',
    [string]$AppPoolName = 'Monitor',
    [string]$ReleaseRoot = 'C:\Program Files\Monitor\releases',
    [string]$StateRoot = 'C:\ProgramData\Monitor\App_Data',
    [string]$BootstrapSiteRoot = 'C:\ProgramData\Monitor\bootstrap-site',

    [ValidateRange(1, 65535)]
    [int]$HttpsPort = 443,

    [switch]$Offline,

    [string]$HostingBundlePath,
    [Uri]$HostingBundleUri = 'https://aka.ms/dotnet/8.0/dotnet-hosting-win.exe',
    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string]$HostingBundleSha256,

    [string]$PowerShellMsiPath,
    [Uri]$PowerShellMsiUri = 'https://github.com/PowerShell/PowerShell/releases/download/v7.4.16/PowerShell-7.4.16-win-x64.msi',
    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string]$PowerShellMsiSha256 = '2C0C2036B0032375AD4F7809A92D0B6FA4A8E4EE89A75211514C4CF55AE22495',

    [string]$CertificatePfxPath,
    [Security.SecureString]$CertificatePfxPassword,

    [switch]$AllowIisServiceRestart,
    [switch]$Apply,
    [switch]$PassThru
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$requiredIisFeatures = @(
    'Web-Server',
    'Web-WebServer',
    'Web-Common-Http',
    'Web-Default-Doc',
    'Web-Static-Content',
    'Web-Http-Errors',
    'Web-Http-Logging',
    'Web-Request-Monitor',
    'Web-Filtering',
    'Web-Stat-Compression',
    'Web-Mgmt-Tools',
    'Web-Scripting-Tools'
)

function Test-WindowsPlatform {
    [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
}

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Applying Monitor server setup requires an elevated PowerShell session.'
    }
}

function Normalize-Thumbprint {
    param([string]$Value)
    ($Value -replace '[^A-Fa-f0-9]', '').ToUpperInvariant()
}

function Get-PowerShell7Path {
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        $current = Join-Path $PSHOME 'pwsh.exe'
        if (Test-Path -LiteralPath $current -PathType Leaf) { return $current }
    }

    $command = Get-Command pwsh.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }

    $candidate = Join-Path $env:ProgramFiles 'PowerShell\7\pwsh.exe'
    if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    $null
}

function Get-DotNetPath {
    $command = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }

    $candidate = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    $null
}

function Get-HostingState {
    $dotnetPath = Get-DotNetPath
    $runtime = $null
    if (-not [string]::IsNullOrWhiteSpace([string]$dotnetPath)) {
        try {
            $runtime = & $dotnetPath --list-runtimes 2>$null |
                Where-Object { $_ -match '^Microsoft\.AspNetCore\.App 8\.' } |
                Select-Object -First 1
        }
        catch { $runtime = $null }
    }

    $ancmCandidates = @((Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'))
    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $ancmCandidates += (Join-Path $programFilesX86 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll')
    }
    $ancm = $ancmCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1

    [pscustomobject]@{
        DotNetPath = $dotnetPath
        AspNetCoreRuntime = [string]$runtime
        RuntimeReady = -not [string]::IsNullOrWhiteSpace([string]$runtime)
        AncmPath = [string]$ancm
        AncmReady = -not [string]::IsNullOrWhiteSpace([string]$ancm)
    }
}

function Assert-DownloadUri {
    param([Uri]$Uri, [string[]]$AllowedHosts, [string]$Label)
    if ($null -eq $Uri -or -not $Uri.IsAbsoluteUri -or $Uri.Scheme -ne 'https') {
        throw "$Label must use an absolute HTTPS URI."
    }
    if (-not ($AllowedHosts -contains $Uri.DnsSafeHost.ToLowerInvariant())) {
        throw "$Label host '$($Uri.DnsSafeHost)' is not approved."
    }
}

function Assert-InstallerHash {
    param([string]$Path, [string]$ExpectedSha256, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($ExpectedSha256)) { return }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actual -ne $ExpectedSha256.ToUpperInvariant()) {
        throw "$Label SHA-256 mismatch. Expected $($ExpectedSha256.ToUpperInvariant()) but calculated $actual."
    }
}

function Assert-MicrosoftSignedInstaller {
    param([string]$Path, [string]$Label)

    $signature = Get-AuthenticodeSignature -FilePath $Path
    if ($null -eq $signature -or [string]$signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate) {
        throw "$Label does not have a valid Authenticode signature."
    }

    $subject = [string]$signature.SignerCertificate.Subject
    if ($subject -notmatch '(?i)(^|,\s*)O=Microsoft Corporation(,|$)') {
        throw "$Label is not signed by Microsoft Corporation."
    }
}

function Get-InstallerFile {
    param(
        [string]$LocalPath,
        [Uri]$DownloadUri,
        [string]$ExpectedSha256,
        [string[]]$AllowedHosts,
        [string]$Label,
        [string]$Extension
    )

    if (-not [string]::IsNullOrWhiteSpace($LocalPath)) {
        if (-not (Test-Path -LiteralPath $LocalPath -PathType Leaf)) { throw "$Label was not found: $LocalPath" }
        Assert-InstallerHash -Path $LocalPath -ExpectedSha256 $ExpectedSha256 -Label $Label
        Assert-MicrosoftSignedInstaller -Path $LocalPath -Label $Label
        return [pscustomobject]@{ Path = (Resolve-Path -LiteralPath $LocalPath).Path; Temporary = $false }
    }

    if ($Offline) { throw "$Label is required in Offline mode. Supply its local path." }
    Assert-DownloadUri -Uri $DownloadUri -AllowedHosts $AllowedHosts -Label $Label

    $downloadPath = Join-Path ([IO.Path]::GetTempPath()) ("monitor-$([Guid]::NewGuid().ToString('N'))$Extension")
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $DownloadUri.AbsoluteUri -OutFile $downloadPath
        if (-not (Test-Path -LiteralPath $downloadPath -PathType Leaf) -or (Get-Item -LiteralPath $downloadPath).Length -le 0) {
            throw "$Label download produced no installer bytes."
        }
        Assert-InstallerHash -Path $downloadPath -ExpectedSha256 $ExpectedSha256 -Label $Label
        Assert-MicrosoftSignedInstaller -Path $downloadPath -Label $Label
        [pscustomobject]@{ Path = $downloadPath; Temporary = $true }
    }
    catch {
        Remove-Item -LiteralPath $downloadPath -Force -ErrorAction SilentlyContinue
        throw
    }
}

function Install-PowerShell7 {
    $existing = Get-PowerShell7Path
    if (-not [string]::IsNullOrWhiteSpace([string]$existing)) {
        return [pscustomobject]@{ Path = $existing; Changed = $false; RestartRequired = $false }
    }
    if (-not $Apply) { return [pscustomobject]@{ Path = $null; Changed = $false; RestartRequired = $false } }

    $installer = Get-InstallerFile `
        -LocalPath $PowerShellMsiPath `
        -DownloadUri $PowerShellMsiUri `
        -ExpectedSha256 $PowerShellMsiSha256 `
        -AllowedHosts @('github.com') `
        -Label 'PowerShell 7 MSI' `
        -Extension '.msi'
    try {
        $arguments = @('/i', ('"' + $installer.Path + '"'), '/qn', '/norestart', 'ADD_PATH=1', 'USE_MU=1', 'ENABLE_MU=1')
        $process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $arguments -Wait -PassThru
        if ($process.ExitCode -notin @(0, 3010)) { throw "PowerShell 7 MSI failed with exit code $($process.ExitCode)." }
        $restartRequired = $process.ExitCode -eq 3010
    }
    finally {
        if ($installer.Temporary) { Remove-Item -LiteralPath $installer.Path -Force -ErrorAction SilentlyContinue }
    }

    $installed = Get-PowerShell7Path
    if ([string]::IsNullOrWhiteSpace([string]$installed)) { throw 'PowerShell 7 installation completed but pwsh.exe could not be located.' }
    [pscustomobject]@{ Path = $installed; Changed = $true; RestartRequired = $restartRequired }
}

function Get-IisFeatureState {
    Import-Module ServerManager -ErrorAction Stop
    @($requiredIisFeatures | ForEach-Object {
        $feature = Get-WindowsFeature -Name $_ -ErrorAction Stop
        [pscustomobject]@{ Name = $_; Installed = [bool]$feature.Installed }
    })
}

function Install-IisFeatures {
    $before = Get-IisFeatureState
    $missing = @($before | Where-Object { -not $_.Installed } | ForEach-Object { $_.Name })
    if ($missing.Count -eq 0) { return [pscustomobject]@{ Changed = $false; RestartRequired = $false; MissingBefore = @() } }
    if (-not $Apply) { return [pscustomobject]@{ Changed = $false; RestartRequired = $false; MissingBefore = $missing } }

    $result = Install-WindowsFeature -Name $missing -IncludeManagementTools -ErrorAction Stop
    if (-not $result.Success) { throw "IIS feature installation failed: $($missing -join ', ')" }
    [pscustomobject]@{
        Changed = $true
        RestartRequired = ([string]$result.RestartNeeded -ne 'No')
        MissingBefore = $missing
    }
}

function Install-HostingBundle {
    param([bool]$ForceRepair)

    $before = Get-HostingState
    $needed = $ForceRepair -or -not $before.RuntimeReady -or -not $before.AncmReady
    if (-not $needed) { return [pscustomobject]@{ Changed = $false; RestartRequired = $false; Before = $before; After = $before } }
    if (-not $Apply) { return [pscustomobject]@{ Changed = $false; RestartRequired = $false; Before = $before; After = $before } }

    $installer = Get-InstallerFile `
        -LocalPath $HostingBundlePath `
        -DownloadUri $HostingBundleUri `
        -ExpectedSha256 $HostingBundleSha256 `
        -AllowedHosts @('aka.ms', 'dotnet.microsoft.com', 'download.visualstudio.microsoft.com', 'builds.dotnet.microsoft.com') `
        -Label '.NET 8 Hosting Bundle' `
        -Extension '.exe'
    try {
        $process = Start-Process -FilePath $installer.Path -ArgumentList @('/install', '/quiet', '/norestart') -Wait -PassThru
        if ($process.ExitCode -notin @(0, 3010)) { throw ".NET 8 Hosting Bundle failed with exit code $($process.ExitCode)." }
        $restartRequired = $process.ExitCode -eq 3010
    }
    finally {
        if ($installer.Temporary) { Remove-Item -LiteralPath $installer.Path -Force -ErrorAction SilentlyContinue }
    }

    $after = Get-HostingState
    if (-not $after.RuntimeReady -or -not $after.AncmReady) {
        throw '.NET 8 Hosting Bundle completed but the ASP.NET Core 8 runtime or ANCM v2 is still unavailable.'
    }
    [pscustomobject]@{ Changed = $true; RestartRequired = $restartRequired; Before = $before; After = $after }
}

function Assert-PfxContainsApprovedCertificate {
    param([string]$Path, [string]$ExpectedThumbprint)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Certificate PFX was not found: $Path" }

    $pfx = if ($null -ne $CertificatePfxPassword) {
        Get-PfxData -FilePath $Path -Password $CertificatePfxPassword -ErrorAction Stop
    }
    else { Get-PfxData -FilePath $Path -ErrorAction Stop }

    $certificates = @($pfx.EndEntityCertificates) + @($pfx.OtherCertificates)
    if (@($certificates | Where-Object { (Normalize-Thumbprint $_.Thumbprint) -eq $ExpectedThumbprint }).Count -eq 0) {
        throw "The supplied PFX does not contain approved certificate '$ExpectedThumbprint'."
    }
}

function Get-ApprovedCertificate {
    param([string]$ExpectedThumbprint)
    $path = "Cert:\LocalMachine\My\$ExpectedThumbprint"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    $certificate = Get-Item -LiteralPath $path
    if (-not $certificate.HasPrivateKey) { throw 'The approved HTTPS certificate has no accessible private key.' }
    if ($certificate.NotAfter -le (Get-Date).AddDays(1)) { throw 'The approved HTTPS certificate is expired or expires within 24 hours.' }
    $certificate
}

function Ensure-ApprovedCertificate {
    param([string]$ExpectedThumbprint)
    $certificate = Get-ApprovedCertificate -ExpectedThumbprint $ExpectedThumbprint
    if ($null -ne $certificate) { return [pscustomobject]@{ Changed = $false; Certificate = $certificate } }
    if (-not $Apply) { return [pscustomobject]@{ Changed = $false; Certificate = $null } }
    if ([string]::IsNullOrWhiteSpace($CertificatePfxPath)) {
        throw "Approved certificate '$ExpectedThumbprint' is missing. Install it in Cert:\LocalMachine\My or supply -CertificatePfxPath."
    }

    Assert-PfxContainsApprovedCertificate -Path $CertificatePfxPath -ExpectedThumbprint $ExpectedThumbprint
    if ($null -ne $CertificatePfxPassword) {
        Import-PfxCertificate -FilePath $CertificatePfxPath -CertStoreLocation 'Cert:\LocalMachine\My' -Password $CertificatePfxPassword | Out-Null
    }
    else { Import-PfxCertificate -FilePath $CertificatePfxPath -CertStoreLocation 'Cert:\LocalMachine\My' | Out-Null }

    $certificate = Get-ApprovedCertificate -ExpectedThumbprint $ExpectedThumbprint
    if ($null -eq $certificate) { throw 'PFX import completed but the approved certificate thumbprint was not found in LocalMachine\My.' }
    [pscustomobject]@{ Changed = $true; Certificate = $certificate }
}

function Get-SafeAppPoolIdentity {
    param([object]$Pool)
    $identityType = [string]$Pool.processModel.identityType
    if (@('LocalSystem', 'LocalService', 'NetworkService') -contains $identityType) {
        throw "Application pool '$AppPoolName' uses forbidden high/shared privilege identity type '$identityType'."
    }
    if ($identityType -eq 'ApplicationPoolIdentity') { return "IIS AppPool\$AppPoolName" }
    if ($identityType -eq 'SpecificUser' -and -not [string]::IsNullOrWhiteSpace([string]$Pool.processModel.userName)) {
        return [string]$Pool.processModel.userName
    }
    throw "Application pool '$AppPoolName' uses unsupported identity type '$identityType'."
}

function Assert-AppPoolOwnership {
    if (-not (Test-Path -LiteralPath "IIS:\AppPools\$AppPoolName")) { return }

    $otherSites = @(Get-Website | Where-Object {
        [string]$_.Name -ne $SiteName -and [string]$_.applicationPool -eq $AppPoolName
    })
    if ($otherSites.Count -gt 0) {
        $names = @($otherSites | ForEach-Object { [string]$_.Name }) -join ', '
        throw "Application pool '$AppPoolName' is shared by other IIS site(s): $names. Monitor bootstrap will not mutate a shared application pool."
    }
}

function Ensure-IisTopology {
    param([string]$ApprovedThumbprint)

    Import-Module WebAdministration -ErrorAction Stop
    Assert-AppPoolOwnership

    New-Item -ItemType Directory -Path $ReleaseRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $StateRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $BootstrapSiteRoot -Force | Out-Null

    $indexPath = Join-Path $BootstrapSiteRoot 'index.html'
    if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
        '<!doctype html><title>Monitor bootstrap</title><p>Monitor host prepared. Application release not deployed yet.</p>' |
            Set-Content -LiteralPath $indexPath -Encoding UTF8
    }

    $appPoolPath = "IIS:\AppPools\$AppPoolName"
    if (-not (Test-Path -LiteralPath $appPoolPath)) {
        New-WebAppPool -Name $AppPoolName | Out-Null
        Set-ItemProperty -LiteralPath $appPoolPath -Name managedRuntimeVersion -Value ''
        Set-ItemProperty -LiteralPath $appPoolPath -Name managedPipelineMode -Value 'Integrated'
        Set-ItemProperty -LiteralPath $appPoolPath -Name processModel.identityType -Value 'ApplicationPoolIdentity'
    }

    $pool = Get-Item -LiteralPath $appPoolPath
    if (-not [string]::IsNullOrWhiteSpace([string]$pool.managedRuntimeVersion)) {
        Set-ItemProperty -LiteralPath $appPoolPath -Name managedRuntimeVersion -Value ''
        $pool = Get-Item -LiteralPath $appPoolPath
    }
    $aclIdentity = Get-SafeAppPoolIdentity -Pool $pool

    $sitePath = "IIS:\Sites\$SiteName"
    if (-not (Test-Path -LiteralPath $sitePath)) {
        New-Website -Name $SiteName -PhysicalPath $BootstrapSiteRoot -ApplicationPool $AppPoolName -Port $HttpsPort -HostHeader $HostName -Ssl | Out-Null
    }

    $site = Get-Item -LiteralPath $sitePath
    if ([string]$site.applicationPool -ne $AppPoolName) {
        throw "Existing IIS site '$SiteName' is assigned to application pool '$($site.applicationPool)' and will not be hijacked."
    }

    $expectedSuffix = ":$HttpsPort`:$HostName"
    $binding = @(Get-WebBinding -Name $SiteName -Protocol https) |
        Where-Object { ([string]$_.bindingInformation).EndsWith($expectedSuffix, [StringComparison]::OrdinalIgnoreCase) } |
        Select-Object -First 1
    if ($null -eq $binding) {
        New-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $HostName -SslFlags 1 | Out-Null
        $binding = @(Get-WebBinding -Name $SiteName -Protocol https) |
            Where-Object { ([string]$_.bindingInformation).EndsWith($expectedSuffix, [StringComparison]::OrdinalIgnoreCase) } |
            Select-Object -First 1
    }
    if ($null -eq $binding) { throw 'Failed to create the approved HTTPS binding.' }
    $binding.AddSslCertificate($ApprovedThumbprint, 'My')

    & icacls.exe $StateRoot /grant "${aclIdentity}:(OI)(CI)M" /T /C | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to grant Modify permission on stable Monitor App_Data.' }
    & icacls.exe $ReleaseRoot /grant "${aclIdentity}:(OI)(CI)RX" /T /C | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to grant Read/Execute permission on Monitor release root.' }
    & icacls.exe $BootstrapSiteRoot /grant "${aclIdentity}:(OI)(CI)RX" /T /C | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to grant Read/Execute permission on Monitor bootstrap site root.' }

    [pscustomobject]@{ AppPoolIdentity = $aclIdentity; SitePhysicalPath = [string]$site.physicalPath }
}

function Restart-IisServicesSafely {
    param([bool]$IisWasInstalledBefore)
    if (-not $Apply) { return $false }
    if ($IisWasInstalledBefore -and -not $AllowIisServiceRestart) {
        throw 'The Hosting Bundle was installed/repaired on a server where IIS was already installed. Re-run with -AllowIisServiceRestart during an approved maintenance window, or restart WAS/W3SVC manually before deployment.'
    }

    $was = Get-Service -Name WAS -ErrorAction Stop
    if ($was.Status -ne 'Stopped') {
        & net.exe stop was /y | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Failed to stop Windows Process Activation Service after Hosting Bundle installation.' }
    }
    $w3svc = Get-Service -Name W3SVC -ErrorAction Stop
    if ($w3svc.Status -ne 'Running') {
        & net.exe start w3svc | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Failed to restart World Wide Web Publishing Service after Hosting Bundle installation.' }
    }
    $true
}

if (-not (Test-WindowsPlatform)) { throw 'Monitor IIS server setup is supported only on Windows Server.' }
if ($HostName -match '^[\s\.]*$' -or $HostName.Contains('*') -or $HostName.Contains('/') -or $HostName.Contains(':')) {
    throw 'HostName must be one concrete DNS host name without scheme, wildcard, path, or port.'
}

$os = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
if ([int]$os.ProductType -eq 1) { throw 'Monitor production IIS setup requires Windows Server, not a Windows client workstation.' }
if ($Apply) { Assert-Administrator }

$approvedThumbprint = Normalize-Thumbprint $CertificateThumbprint
$featureState = Get-IisFeatureState
$missingFeatures = @($featureState | Where-Object { -not $_.Installed } | ForEach-Object { $_.Name })
$hostingState = Get-HostingState
$pwshPath = Get-PowerShell7Path
$certificateBefore = Get-ApprovedCertificate -ExpectedThumbprint $approvedThumbprint

$plan = [ordered]@{
    mode = if ($Apply) { 'Apply' } else { 'PlanOnly' }
    hostName = $HostName
    siteName = $SiteName
    appPoolName = $AppPoolName
    httpsPort = $HttpsPort
    offline = [bool]$Offline
    windowsServer = [string]$os.Caption
    powerShell7Ready = -not [string]::IsNullOrWhiteSpace([string]$pwshPath)
    missingIisFeatures = $missingFeatures
    aspNetCore8RuntimeReady = [bool]$hostingState.RuntimeReady
    ancmV2Ready = [bool]$hostingState.AncmReady
    approvedCertificateReady = $null -ne $certificateBefore
    certificatePfxAvailable = -not [string]::IsNullOrWhiteSpace($CertificatePfxPath)
    releaseRoot = $ReleaseRoot
    stateRoot = $StateRoot
    bootstrapSiteRoot = $BootstrapSiteRoot
    actions = @(
        'Install PowerShell 7 when missing using a SHA-256 pinned, Microsoft-signed official MSI (or an operator-supplied offline MSI)',
        'Install missing IIS server roles/features including WebAdministration scripting tools',
        'Install or repair a Microsoft-signed .NET 8 Hosting Bundle after IIS so ASP.NET Core Runtime 8 and ANCM v2 are present',
        'Use only the explicitly approved certificate thumbprint; optionally import a supplied PFX containing that exact certificate',
        'Create/validate a dedicated Monitor No Managed Code application pool; refuse privileged identities and pools shared with other IIS sites',
        'Create/validate only the exact Monitor IIS site and HTTPS host binding',
        'Grant Modify only on stable App_Data and Read/Execute on release/bootstrap roots',
        'Run the existing authoritative Test-IisProductionPrerequisites.ps1 after setup'
    )
}

if (-not $Apply) {
    if ($PassThru) { return [pscustomobject]$plan }
    $plan | ConvertTo-Json -Depth 6
    Write-Host 'PLAN ONLY. No Windows features, downloads, installers, certificate store, IIS, filesystem or ACL changes were made.'
    return
}

$powerShellResult = Install-PowerShell7
$iisWasInstalledBefore = @($featureState | Where-Object { $_.Name -eq 'Web-Server' -and $_.Installed }).Count -gt 0
$iisResult = Install-IisFeatures
$hostingResult = Install-HostingBundle -ForceRepair ([bool]$iisResult.Changed)
$certificateResult = Ensure-ApprovedCertificate -ExpectedThumbprint $approvedThumbprint

$iisServicesRestarted = $false
if ($hostingResult.Changed) {
    $iisServicesRestarted = Restart-IisServicesSafely -IisWasInstalledBefore $iisWasInstalledBefore
}

$topology = Ensure-IisTopology -ApprovedThumbprint $approvedThumbprint
$restartRequired = [bool]$powerShellResult.RestartRequired -or [bool]$iisResult.RestartRequired -or [bool]$hostingResult.RestartRequired
$pwshPath = Get-PowerShell7Path
if ([string]::IsNullOrWhiteSpace([string]$pwshPath)) { throw 'PowerShell 7 is still unavailable after setup.' }

$result = [ordered]@{
    Mode = 'Apply'
    Ready = -not $restartRequired
    RestartRequired = $restartRequired
    PowerShell7Path = $pwshPath
    PowerShell7Changed = [bool]$powerShellResult.Changed
    IisChanged = [bool]$iisResult.Changed
    HostingBundleChanged = [bool]$hostingResult.Changed
    IisServicesRestarted = [bool]$iisServicesRestarted
    CertificateImported = [bool]$certificateResult.Changed
    CertificateThumbprint = $approvedThumbprint
    AppPoolIdentity = $topology.AppPoolIdentity
    SitePhysicalPath = $topology.SitePhysicalPath
}

if ($restartRequired) {
    Write-Warning 'A prerequisite installer or Windows feature reported that a server restart is required. Reboot, then run the same command again before deployment.'
}
else {
    $preflightScript = Join-Path $PSScriptRoot 'Test-IisProductionPrerequisites.ps1'
    if (-not (Test-Path -LiteralPath $preflightScript -PathType Leaf)) { throw "IIS preflight script was not found: $preflightScript" }
    & $pwshPath -NoProfile -File $preflightScript `
        -HostName $HostName `
        -CertificateThumbprint $approvedThumbprint `
        -SiteName $SiteName `
        -AppPoolName $AppPoolName `
        -HttpsPort $HttpsPort
    if ($LASTEXITCODE -ne 0) { throw 'Authoritative Monitor IIS preflight failed after bootstrap setup.' }
    Write-Host 'Monitor server prerequisites and IIS bootstrap passed. The application release has not been deployed by this script.'
}

if ($PassThru) { return [pscustomobject]$result }
[pscustomobject]$result | Format-List
