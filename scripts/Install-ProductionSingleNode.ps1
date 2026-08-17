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
    [string]$PowerShellMode = 'Online',

    [string]$PowerShellMsiInstallerPath,

    [uri]$PowerShellMsiDownloadUrl = 'https://github.com/PowerShell/PowerShell/releases/download/v7.4.16/PowerShell-7.4.16-win-x64.msi',

    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string]$PowerShellMsiSha256 = '2C0C2036B0032375AD4F7809A92D0B6FA4A8E4EE89A75211514C4CF55AE22495',

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

    [switch]$AllowIisServiceRestart,
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

function Get-PowerShell7Path {
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        $current = Join-Path $PSHOME 'pwsh.exe'
        if (Test-Path -LiteralPath $current -PathType Leaf) { return $current }
    }

    $command = Get-Command pwsh.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) { return [string]$command.Source }

    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $wellKnown = Join-Path $env:ProgramFiles 'PowerShell\7\pwsh.exe'
        if (Test-Path -LiteralPath $wellKnown -PathType Leaf) { return $wellKnown }
    }
    return $null
}

function Test-PowerShellDownloadUri {
    param([uri]$Uri)
    if ($null -eq $Uri -or -not $Uri.IsAbsoluteUri -or $Uri.Scheme -ne 'https') { return $false }
    if ($Uri.DnsSafeHost.ToLowerInvariant() -ne 'github.com') { return $false }
    return $Uri.AbsolutePath.StartsWith('/PowerShell/PowerShell/releases/download/', [StringComparison]::OrdinalIgnoreCase) -and
        $Uri.AbsolutePath.EndsWith('-win-x64.msi', [StringComparison]::OrdinalIgnoreCase)
}

function Assert-PowerShellInstallerIntegrity {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "PowerShell 7 MSI was not found: $Path" }

    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
    $expected = $PowerShellMsiSha256.ToUpperInvariant()
    if ($actual -ne $expected) {
        throw "PowerShell 7 MSI SHA-256 mismatch. Expected $expected but calculated $actual."
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
        $null -eq $signature.SignerCertificate -or
        [string]$signature.SignerCertificate.Subject -notmatch '(?i)(^|,\s*)O=Microsoft Corporation(,|$)') {
        throw 'PowerShell 7 MSI must have a valid Microsoft Corporation Authenticode signature.'
    }
}

function Ensure-PowerShell7 {
    $existing = Get-PowerShell7Path
    if (-not [string]::IsNullOrWhiteSpace($existing)) {
        return [pscustomobject]@{
            Ready = ($PSVersionTable.PSVersion.Major -ge 7)
            Installed = $true
            Changed = $false
            RequiresRelaunch = ($PSVersionTable.PSVersion.Major -lt 7)
            Path = $existing
            PlannedAction = if ($PSVersionTable.PSVersion.Major -lt 7) { 'Re-run this same installer from PowerShell 7 (pwsh) before any IIS mutation.' } else { $null }
        }
    }

    $planned = if ($PowerShellMode -eq 'Offline') {
        'Install PowerShell 7 from the operator-supplied offline MSI, then rerun this same installer under pwsh before any IIS mutation.'
    }
    else {
        'Install the SHA-256 pinned Microsoft-signed PowerShell 7 x64 MSI from the approved official PowerShell GitHub release, then rerun this same installer under pwsh before any IIS mutation.'
    }

    if (-not $Apply) {
        if ($PowerShellMode -eq 'Offline' -and [string]::IsNullOrWhiteSpace($PowerShellMsiInstallerPath)) {
            $planned += ' Supply -PowerShellMsiInstallerPath before Offline Apply.'
        }
        if ($PowerShellMode -eq 'Online' -and -not (Test-PowerShellDownloadUri -Uri $PowerShellMsiDownloadUrl)) {
            throw 'Online PowerShell MSI URL must be an explicit official PowerShell GitHub release x64 MSI URL.'
        }
        return [pscustomobject]@{
            Ready = $false
            Installed = $false
            Changed = $false
            RequiresRelaunch = $true
            Path = $null
            PlannedAction = $planned
        }
    }

    $installer = $null
    $downloadedInstaller = $false
    try {
        if ($PowerShellMode -eq 'Offline') {
            if ([string]::IsNullOrWhiteSpace($PowerShellMsiInstallerPath)) {
                throw 'Offline PowerShell 7 installation requires -PowerShellMsiInstallerPath.'
            }
            $installer = [IO.Path]::GetFullPath($PowerShellMsiInstallerPath)
        }
        else {
            if (-not (Test-PowerShellDownloadUri -Uri $PowerShellMsiDownloadUrl)) {
                throw 'Online PowerShell MSI URL must be an explicit official PowerShell GitHub release x64 MSI URL.'
            }
            $installer = Join-Path ([IO.Path]::GetTempPath()) ("powershell-7-$([Guid]::NewGuid().ToString('N')).msi")
            Invoke-WebRequest -Uri $PowerShellMsiDownloadUrl.AbsoluteUri -OutFile $installer -UseBasicParsing -MaximumRedirection 5
            $downloadedInstaller = $true
        }

        Assert-PowerShellInstallerIntegrity -Path $installer
        $arguments = @('/i', ('"' + $installer + '"'), '/qn', '/norestart', 'ADD_PATH=1', 'USE_MU=1', 'ENABLE_MU=1')
        $process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $arguments -Wait -PassThru
        if ($process.ExitCode -notin @(0, 3010)) {
            throw "PowerShell 7 MSI failed with exit code $($process.ExitCode)."
        }
        if ($process.ExitCode -eq 3010) {
            throw 'PowerShell 7 installation requested a reboot (3010). No IIS mutation was attempted. Reboot and rerun the same installer from pwsh.'
        }
    }
    finally {
        if ($downloadedInstaller -and $installer -and (Test-Path -LiteralPath $installer)) {
            Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
        }
    }

    $installedPath = Get-PowerShell7Path
    if ([string]::IsNullOrWhiteSpace($installedPath)) {
        throw 'PowerShell 7 MSI completed but pwsh.exe could not be located. No IIS mutation was attempted.'
    }

    return [pscustomobject]@{
        Ready = $false
        Installed = $true
        Changed = $true
        RequiresRelaunch = $true
        Path = $installedPath
        PlannedAction = 'PowerShell 7 was installed successfully. Re-run this same installer from pwsh; no IIS mutation was attempted in this Windows PowerShell process.'
    }
}

if ($Apply -and -not $AcknowledgeDurableReleasePrerequisite) {
    throw 'Apply is blocked until the operator confirms #162 durable RC publication + independent verification is complete. Rerun with -AcknowledgeDurableReleasePrerequisite only after #162 is actually complete. This switch does not itself satisfy or verify #162.'
}

$powerShell7 = Ensure-PowerShell7

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
if ($AllowIisServiceRestart) { $bootstrapParameters.AllowIisServiceRestart = $true }

if ($Apply -and [bool]$powerShell7.RequiresRelaunch) {
    $result = [pscustomobject]@{
        Mode = 'PREREQUISITE APPLY'
        Apply = $true
        PowerShell7 = $powerShell7
        Bootstrap = $null
        ProductionPreflightExecuted = $false
        DeploymentPlanExecuted = $false
        DeploymentApplied = $false
        NextStep = "Open an elevated PowerShell 7 console using '$([string]$powerShell7.Path)' and rerun the same approved Install-ProductionSingleNode.ps1 command. No IIS mutation was attempted."
        Boundary = '#162 acknowledgement was required before installing prerequisites. This prerequisite step does not close #162, #116 or #111.'
    }
    if ($PassThru) { return $result }
    $result | Format-List
    Write-Warning $result.NextStep
    return
}

if ($Apply) { $bootstrapParameters.Apply = $true }
$bootstrap = & $bootstrapScript @bootstrapParameters

if (-not $Apply -and ([bool]$powerShell7.RequiresRelaunch -or [bool]$bootstrap.RequiresChanges)) {
    $result = [pscustomobject]@{
        Mode = 'PLAN ONLY'
        Apply = $false
        PowerShell7 = $powerShell7
        Bootstrap = $bootstrap
        ProductionPreflightExecuted = $false
        DeploymentPlanExecuted = $false
        DeploymentApplied = $false
        NextStep = if ([bool]$powerShell7.RequiresRelaunch) {
            'Prepare PowerShell 7 first. After it is installed, rerun this same entrypoint from pwsh; then review/apply the remaining bootstrap plan.'
        } else {
            'Review the bootstrap plan. Apply it only after approval; then rerun this same entrypoint. Existing production preflight and deployment planning remain authoritative after bootstrap.'
        }
    }
    if ($PassThru) { return $result }
    $result | Format-List
    Write-Host 'Monitor SingleNode installer PLAN ONLY stopped before preflight/deploy because prerequisite/bootstrap changes are required. No mutation occurred.'
    return
}

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'The existing Monitor production preflight/deploy toolchain requires PowerShell 7. Re-run this same installer from pwsh before any IIS/application mutation.'
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
    PowerShell7 = $powerShell7
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
