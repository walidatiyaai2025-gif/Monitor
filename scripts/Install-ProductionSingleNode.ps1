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

    [string]$CertificateThumbprint,
    [string]$PfxPath,
    [Security.SecureString]$PfxPassword,

    [ValidateSet('Auto', 'Online', 'Offline')]
    [string]$HostingBundleMode = 'Auto',

    [string]$HostingBundleInstallerPath,
    [Uri]$HostingBundleUrl,

    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string]$HostingBundleSha256,

    [string]$SiteName = 'Monitor',
    [string]$AppPoolName = 'Monitor',
    [string]$ReleaseRoot = 'C:\Program Files\Monitor\releases',
    [string]$StateRoot = 'C:\ProgramData\Monitor\App_Data',
    [string]$BootstrapSitePath = 'C:\ProgramData\Monitor\bootstrap-site',

    [ValidateRange(1, 65535)]
    [int]$HttpsPort = 443,

    [string]$EvidencePath,

    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-FileExists {
    param([string]$Path, [string]$Label)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label was not found: $Path"
    }
}

if (-not $IsWindows) {
    throw 'Monitor SingleNode installer/deploy entrypoint is supported only on Windows Server.'
}

Assert-FileExists -Path $ArtifactPath -Label 'Release artifact'
Assert-FileExists -Path $ChecksumPath -Label 'Checksum file'
Assert-FileExists -Path $ProductionConfigPath -Label 'Approved production configuration'
if ([string]::IsNullOrWhiteSpace($OperationalBackupId)) {
    throw 'OperationalBackupId is required. Create and validate the pre-cutover operational backup before deployment.'
}

$bootstrapScript = Join-Path $PSScriptRoot 'Initialize-IisProductionHost.ps1'
$preflightScript = Join-Path $PSScriptRoot 'Test-IisProductionPrerequisites.ps1'
$deployScript = Join-Path $PSScriptRoot 'Deploy-ProductionSingleNode.ps1'
Assert-FileExists -Path $bootstrapScript -Label 'IIS bootstrap script'
Assert-FileExists -Path $preflightScript -Label 'Authoritative IIS preflight script'
Assert-FileExists -Path $deployScript -Label 'SingleNode deployment script'

$bootstrapArgs = @{
    HostName = $HostName
    SiteName = $SiteName
    AppPoolName = $AppPoolName
    HttpsPort = $HttpsPort
    ReleaseRoot = $ReleaseRoot
    StateRoot = $StateRoot
    BootstrapSitePath = $BootstrapSitePath
    HostingBundleMode = $HostingBundleMode
    PassThru = $true
}
if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) { $bootstrapArgs['CertificateThumbprint'] = $CertificateThumbprint }
if (-not [string]::IsNullOrWhiteSpace($PfxPath)) { $bootstrapArgs['PfxPath'] = $PfxPath }
if ($null -ne $PfxPassword) { $bootstrapArgs['PfxPassword'] = $PfxPassword }
if (-not [string]::IsNullOrWhiteSpace($HostingBundleInstallerPath)) { $bootstrapArgs['HostingBundleInstallerPath'] = $HostingBundleInstallerPath }
if ($null -ne $HostingBundleUrl) { $bootstrapArgs['HostingBundleUrl'] = $HostingBundleUrl }
if (-not [string]::IsNullOrWhiteSpace($HostingBundleSha256)) { $bootstrapArgs['HostingBundleSha256'] = $HostingBundleSha256 }
if ($Apply) { $bootstrapArgs['Apply'] = $true }

# Phase 1: idempotent host bootstrap. PlanOnly unless this entrypoint also received -Apply.
$bootstrap = & $bootstrapScript @bootstrapArgs

$combinedPlan = [ordered]@{
    mode = if ($Apply) { 'Apply' } else { 'PlanOnly' }
    phase1 = 'Initialize-IisProductionHost.ps1'
    phase2 = 'Test-IisProductionPrerequisites.ps1'
    phase3 = 'Deploy-ProductionSingleNode.ps1'
    hostName = $HostName
    httpsPort = $HttpsPort
    siteName = $SiteName
    appPoolName = $AppPoolName
    releaseVersion = $ReleaseVersion
    artifact = [IO.Path]::GetFileName($ArtifactPath)
    bootstrapReadyForPreflight = [bool]$bootstrap.ReadyForPreflight
    certificateThumbprint = [string]$bootstrap.CertificateThumbprint
}

if (-not $Apply -and -not $bootstrap.ReadyForPreflight) {
    $combinedPlan | ConvertTo-Json -Depth 6
    $bootstrap.Plan | ConvertTo-Json -Depth 8
    Write-Host 'PLAN ONLY. Bootstrap changes are required before the authoritative IIS preflight can pass.'
    Write-Host 'No Windows feature, runtime, IIS, certificate, binding, filesystem, ACL, release, configuration or application state changes were made.'
    Write-Host 'After approving the bootstrap plan, rerun this same command with -Apply. The entrypoint will then run bootstrap -> authoritative preflight -> existing deployment.'
    return
}

$resolvedThumbprint = [string]$bootstrap.CertificateThumbprint
if ([string]::IsNullOrWhiteSpace($resolvedThumbprint)) {
    throw 'The bootstrap did not resolve an approved machine certificate thumbprint.'
}

# Phase 2: the existing fail-closed production preflight remains authoritative after bootstrap.
$preflight = & $preflightScript `
    -HostName $HostName `
    -CertificateThumbprint $resolvedThumbprint `
    -SiteName $SiteName `
    -AppPoolName $AppPoolName `
    -HttpsPort $HttpsPort `
    -PassThru

if (-not $preflight.Ready) {
    throw 'Authoritative IIS production preflight did not report Ready=true.'
}

$deployArgs = @{
    ArtifactPath = $ArtifactPath
    ChecksumPath = $ChecksumPath
    ReleaseVersion = $ReleaseVersion
    ProductionConfigPath = $ProductionConfigPath
    OperationalBackupId = $OperationalBackupId
    HostName = $HostName
    CertificateThumbprint = $resolvedThumbprint
    SiteName = $SiteName
    AppPoolName = $AppPoolName
    ReleaseRoot = $ReleaseRoot
    StateRoot = $StateRoot
    HttpsPort = $HttpsPort
}
if (-not [string]::IsNullOrWhiteSpace($EvidencePath)) { $deployArgs['EvidencePath'] = $EvidencePath }
if ($Apply) { $deployArgs['Apply'] = $true }

# Phase 3: delegate artifact SHA-256 validation, immutable release staging, durable App_Data,
# cutover acceptance and automatic application-path rollback to the existing deployment script.
& $deployScript @deployArgs

if (-not $Apply) {
    Write-Host 'PLAN ONLY. Bootstrap, authoritative preflight and existing SingleNode deployment plan completed without mutation.'
}
else {
    Write-Host 'Bootstrap, authoritative IIS preflight and existing SingleNode deployment entrypoint completed.'
    if ($bootstrap.RebootRequired) {
        Write-Warning 'The bootstrap reported RebootRequired=true. Follow platform policy and re-run authoritative preflight after any approved reboot.'
    }
}
