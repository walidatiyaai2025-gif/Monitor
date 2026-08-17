[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,

    [Parameter(Mandatory = $true)]
    [string]$ChecksumPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z._-]{0,79}$')]
    [string]$ReleaseVersion,

    [Parameter(Mandatory = $true)]
    [string]$ProductionConfigPath,

    [Parameter(Mandatory = $true)]
    [string]$OperationalBackupId,

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

    [string]$EvidencePath,

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

    [Parameter(DontShow = $true)]
    [switch]$ContinueAfterBootstrap
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-FileExists {
    param([string]$Path, [string]$Label)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "$Label was not found: $Path" }
}

function Normalize-Thumbprint {
    param([string]$Value)
    ($Value -replace '[^A-Fa-f0-9]', '').ToUpperInvariant()
}

function Assert-PackageInputs {
    Assert-FileExists -Path $ArtifactPath -Label 'Release artifact'
    Assert-FileExists -Path $ChecksumPath -Label 'Checksum file'
    Assert-FileExists -Path $ProductionConfigPath -Label 'Approved production configuration'

    $expected = ((Get-Content -LiteralPath $ChecksumPath -Raw).Trim() -split '\s+')[0].ToUpperInvariant()
    if ($expected -notmatch '^[A-F0-9]{64}$') { throw 'Checksum file does not contain a valid SHA-256 value.' }
    $actual = (Get-FileHash -LiteralPath $ArtifactPath -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actual -ne $expected) { throw "Release artifact SHA-256 mismatch. Expected $expected but calculated $actual." }
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

function Invoke-AuthoritativeDeployment {
    $preflightScript = Join-Path $PSScriptRoot 'Test-IisProductionPrerequisites.ps1'
    $deployScript = Join-Path $PSScriptRoot 'Deploy-ProductionSingleNode.ps1'
    Assert-FileExists -Path $preflightScript -Label 'IIS preflight script'
    Assert-FileExists -Path $deployScript -Label 'SingleNode deployment script'

    $approvedThumbprint = Normalize-Thumbprint $CertificateThumbprint
    & $preflightScript `
        -HostName $HostName `
        -CertificateThumbprint $approvedThumbprint `
        -SiteName $SiteName `
        -AppPoolName $AppPoolName `
        -HttpsPort $HttpsPort

    $deployParameters = @{
        ArtifactPath = $ArtifactPath
        ChecksumPath = $ChecksumPath
        ReleaseVersion = $ReleaseVersion
        ProductionConfigPath = $ProductionConfigPath
        OperationalBackupId = $OperationalBackupId
        HostName = $HostName
        CertificateThumbprint = $approvedThumbprint
        SiteName = $SiteName
        AppPoolName = $AppPoolName
        ReleaseRoot = $ReleaseRoot
        StateRoot = $StateRoot
        HttpsPort = $HttpsPort
        Apply = $true
    }
    if (-not [string]::IsNullOrWhiteSpace($EvidencePath)) { $deployParameters['EvidencePath'] = $EvidencePath }
    & $deployScript @deployParameters
}

function Invoke-UnderPowerShell7 {
    param([string]$PowerShellPath)

    $arguments = @(
        '-NoProfile',
        '-File', $PSCommandPath,
        '-ArtifactPath', $ArtifactPath,
        '-ChecksumPath', $ChecksumPath,
        '-ReleaseVersion', $ReleaseVersion,
        '-ProductionConfigPath', $ProductionConfigPath,
        '-OperationalBackupId', $OperationalBackupId,
        '-HostName', $HostName,
        '-CertificateThumbprint', (Normalize-Thumbprint $CertificateThumbprint),
        '-SiteName', $SiteName,
        '-AppPoolName', $AppPoolName,
        '-ReleaseRoot', $ReleaseRoot,
        '-StateRoot', $StateRoot,
        '-BootstrapSiteRoot', $BootstrapSiteRoot,
        '-HttpsPort', [string]$HttpsPort,
        '-Apply',
        '-ContinueAfterBootstrap'
    )
    if (-not [string]::IsNullOrWhiteSpace($EvidencePath)) { $arguments += @('-EvidencePath', $EvidencePath) }

    & $PowerShellPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "PowerShell 7 deployment continuation failed with exit code $LASTEXITCODE." }
}

Assert-PackageInputs

if ($ContinueAfterBootstrap) {
    if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Internal deployment continuation requires PowerShell 7 or later.' }
    Invoke-AuthoritativeDeployment
    return
}

$setupScript = Join-Path $PSScriptRoot 'Setup-MonitorServer.ps1'
Assert-FileExists -Path $setupScript -Label 'Monitor server setup script'

$setupParameters = @{
    HostName = $HostName
    CertificateThumbprint = (Normalize-Thumbprint $CertificateThumbprint)
    SiteName = $SiteName
    AppPoolName = $AppPoolName
    ReleaseRoot = $ReleaseRoot
    StateRoot = $StateRoot
    BootstrapSiteRoot = $BootstrapSiteRoot
    HttpsPort = $HttpsPort
    HostingBundleUri = $HostingBundleUri
    PowerShellMsiUri = $PowerShellMsiUri
    PowerShellMsiSha256 = $PowerShellMsiSha256
    PassThru = $true
}
if ($Offline) { $setupParameters['Offline'] = $true }
if (-not [string]::IsNullOrWhiteSpace($HostingBundlePath)) { $setupParameters['HostingBundlePath'] = $HostingBundlePath }
if (-not [string]::IsNullOrWhiteSpace($HostingBundleSha256)) { $setupParameters['HostingBundleSha256'] = $HostingBundleSha256 }
if (-not [string]::IsNullOrWhiteSpace($PowerShellMsiPath)) { $setupParameters['PowerShellMsiPath'] = $PowerShellMsiPath }
if (-not [string]::IsNullOrWhiteSpace($CertificatePfxPath)) { $setupParameters['CertificatePfxPath'] = $CertificatePfxPath }
if ($null -ne $CertificatePfxPassword) { $setupParameters['CertificatePfxPassword'] = $CertificatePfxPassword }
if ($AllowIisServiceRestart) { $setupParameters['AllowIisServiceRestart'] = $true }
if ($Apply) { $setupParameters['Apply'] = $true }

$setupResult = & $setupScript @setupParameters

if (-not $Apply) {
    $plan = [ordered]@{
        mode = 'PlanOnly'
        releaseVersion = $ReleaseVersion
        artifact = [IO.Path]::GetFileName($ArtifactPath)
        artifactSha256 = (Get-FileHash -LiteralPath $ArtifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
        hostName = $HostName
        siteName = $SiteName
        appPoolName = $AppPoolName
        httpsPort = $HttpsPort
        bootstrap = $setupResult
        deployment = [ordered]@{
            willRunAfterBootstrap = $true
            authoritativePreflight = 'Test-IisProductionPrerequisites.ps1'
            authoritativeDeployment = 'Deploy-ProductionSingleNode.ps1'
            immutableReleaseRoot = $ReleaseRoot
            stableStateRoot = $StateRoot
            operationalBackupId = $OperationalBackupId
            applyRequired = $true
        }
    }
    $plan | ConvertTo-Json -Depth 8
    Write-Host 'PLAN ONLY. Package integrity and prerequisite state were inspected; no server, IIS, certificate, ACL or application deployment changes were made.'
    Write-Host 'Re-run the same command with -Apply only after the plan, certificate, production configuration and operational backup are approved.'
    return
}

if ($null -eq $setupResult) { throw 'Monitor server setup returned no result.' }
if ([bool]$setupResult.RestartRequired) {
    Write-Warning 'Monitor server prerequisites were installed but a server restart is required. No application release was deployed. Reboot and rerun the same command with -Apply.'
    return
}

if ($PSVersionTable.PSVersion.Major -lt 7) {
    $pwshPath = [string]$setupResult.PowerShell7Path
    if ([string]::IsNullOrWhiteSpace($pwshPath)) { $pwshPath = Get-PowerShell7Path }
    if ([string]::IsNullOrWhiteSpace($pwshPath)) {
        throw 'PowerShell 7 is required by the existing production deployment toolchain and could not be located after bootstrap.'
    }
    Invoke-UnderPowerShell7 -PowerShellPath $pwshPath
    return
}

Invoke-AuthoritativeDeployment
