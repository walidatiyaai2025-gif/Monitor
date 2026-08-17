[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,

    [Parameter(Mandatory = $true)]
    [string]$ChecksumPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:[-.][0-9A-Za-z.-]+)?$')]
    [string]$ReleaseVersion,

    [Parameter(Mandatory = $true)]
    [string]$ProductionConfigPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9._:-]{1,128}$')]
    [string]$OperationalBackupId,

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

    [string]$EvidencePath = 'C:\ProgramData\Monitor\acceptance\latest.json',

    [switch]$AcknowledgeDurableReleasePrerequisite,

    [switch]$Apply,

    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$bootstrapScript = Join-Path $PSScriptRoot 'Bootstrap-IisProductionSingleNode.ps1'
$preflightScript = Join-Path $PSScriptRoot 'Test-IisProductionPrerequisites.ps1'
$deployScript = Join-Path $PSScriptRoot 'Deploy-ProductionSingleNode.ps1'
foreach ($requiredScript in @($bootstrapScript, $preflightScript, $deployScript)) {
    if (-not (Test-Path -LiteralPath $requiredScript -PathType Leaf)) {
        throw "Required production operation script is missing: $requiredScript"
    }
}

if ($Apply -and -not $AcknowledgeDurableReleasePrerequisite) {
    throw 'Apply is blocked until the operator confirms #162 durable RC publication + independent verification is complete. Rerun with -AcknowledgeDurableReleasePrerequisite only after #162 is actually complete. This switch does not itself satisfy or verify #162.'
}

$bootstrapParameters = @{
    HostName = $HostName
    HostingBundleMode = $HostingBundleMode
    SiteName = $SiteName
    AppPoolName = $AppPoolName
    HttpsPort = $HttpsPort
    ReleaseRoot = $ReleaseRoot
    StateRoot = $StateRoot
    BootstrapSiteRoot = $BootstrapSiteRoot
    PassThru = $true
}
if ($CertificateThumbprint) { $bootstrapParameters.CertificateThumbprint = $CertificateThumbprint }
if ($CertificatePfxPath) { $bootstrapParameters.CertificatePfxPath = $CertificatePfxPath }
if ($null -ne $CertificatePfxPassword) { $bootstrapParameters.CertificatePfxPassword = $CertificatePfxPassword }
if ($HostingBundleInstallerPath) { $bootstrapParameters.HostingBundleInstallerPath = $HostingBundleInstallerPath }
if ($null -ne $HostingBundleDownloadUrl) { $bootstrapParameters.HostingBundleDownloadUrl = $HostingBundleDownloadUrl }
if ($HostingBundleSha256) { $bootstrapParameters.HostingBundleSha256 = $HostingBundleSha256 }
if ($Apply) { $bootstrapParameters.Apply = $true }

$bootstrap = & $bootstrapScript @bootstrapParameters

if (-not $Apply -and [bool]$bootstrap.RequiresChanges) {
    $result = [pscustomobject]@{
        Mode = 'PLAN ONLY'
        Apply = $false
        Bootstrap = $bootstrap
        ProductionPreflightExecuted = $false
        DeploymentPlanExecuted = $false
        DeploymentApplied = $false
        NextStep = 'Review the bootstrap plan. Apply it only after approval; then rerun this same entrypoint. Existing production preflight and deployment planning remain authoritative after bootstrap.'
    }
    if ($PassThru) { return $result }
    $result | Format-List
    Write-Host 'Monitor SingleNode installer PLAN ONLY stopped before preflight/deploy because bootstrap changes are required. No mutation occurred.'
    return
}

$approvedThumbprint = [string]$bootstrap.CertificateThumbprint
if ([string]::IsNullOrWhiteSpace($approvedThumbprint)) {
    throw 'Bootstrap did not resolve an approved machine certificate thumbprint.'
}

$preflight = & $preflightScript `
    -HostName $HostName `
    -CertificateThumbprint $approvedThumbprint `
    -SiteName $SiteName `
    -AppPoolName $AppPoolName `
    -HttpsPort $HttpsPort `
    -PassThru

if (-not [bool]$preflight.Ready) {
    throw 'Existing production IIS preflight did not report Ready=true.'
}

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
    EvidencePath = $EvidencePath
}
if ($Apply) { $deployParameters.Apply = $true }

$deployment = & $deployScript @deployParameters

$result = [pscustomobject]@{
    Mode = if ($Apply) { 'APPLY' } else { 'PLAN ONLY' }
    Apply = [bool]$Apply
    Bootstrap = $bootstrap
    ProductionPreflightExecuted = $true
    ProductionPreflightReady = [bool]$preflight.Ready
    DeploymentPlanExecuted = $true
    DeploymentApplied = [bool]$Apply
    CertificateThumbprint = $approvedThumbprint
    ReleaseVersion = $ReleaseVersion
    DeploymentResult = $deployment
    Boundary = if ($Apply) {
        '#162 acknowledgement was required before mutation. This entrypoint does not close #162, #116 or #111 and does not manufacture external acceptance evidence.'
    } else {
        'PLAN ONLY. No IIS, Windows feature, runtime, certificate, filesystem, ACL, application-pool, binding, configuration or state changes were requested.'
    }
}

if ($PassThru) { return $result }
$result | Format-List
