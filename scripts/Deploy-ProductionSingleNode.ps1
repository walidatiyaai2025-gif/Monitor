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
    [string]$HostName,

    [Parameter(Mandatory = $true)]
    [string]$CertificateThumbprint,

    [string]$SiteName = 'Monitor',
    [string]$AppPoolName = 'Monitor',
    [string]$ReleaseRoot = 'C:\Program Files\Monitor\releases',
    [string]$StateRoot = 'C:\ProgramData\Monitor\App_Data',

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

function Assert-ArtifactChecksum {
    param([string]$ZipPath, [string]$ShaPath)

    $expected = ((Get-Content -LiteralPath $ShaPath -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
    if ($expected -notmatch '^[a-f0-9]{64}$') {
        throw 'Checksum file does not contain a valid SHA-256 value.'
    }

    $actual = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "Release artifact SHA-256 mismatch. Expected $expected but calculated $actual."
    }
    return $actual
}

function Assert-ProductionConfiguration {
    param([string]$Path, [string]$ExpectedHost)

    $text = Get-Content -LiteralPath $Path -Raw
    $json = $text | ConvertFrom-Json -Depth 20
    if ($null -eq $json.Deployment -or [string]$json.Deployment.Mode -ne 'SingleNode') {
        throw 'Production configuration must set Deployment:Mode to SingleNode.'
    }
    if ($text -match '(?i)"DevelopmentAdmin"\s*:') {
        throw 'Production configuration must not contain DevelopmentAdmin credential material; supply the production administrator verifier through approved environment variables.'
    }
    if ($text -match '(?i)"(Password|HashBase64|SaltBase64|ConnectionString)"\s*:') {
        throw 'Production configuration contains credential/connection material that must be supplied through approved environment variables.'
    }

    $allowedHosts = [string]$json.AllowedHosts
    $allowed = @($allowedHosts -split '[;,]' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($allowed -contains '*') {
        throw 'Production AllowedHosts must not use a wildcard.'
    }
    if (-not ($allowed | Where-Object { $_.Equals($ExpectedHost, [StringComparison]::OrdinalIgnoreCase) })) {
        throw "Production AllowedHosts must include '$ExpectedHost'."
    }
}

function Assert-CleanCandidateArchive {
    param([string]$Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Path).Path)
    try {
        $names = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\\', '/') })
        if (-not ($names -contains 'Monitor.Web.dll')) {
            throw 'Candidate archive does not contain Monitor.Web.dll at the package root.'
        }
        if (-not ($names -contains 'web.config')) {
            throw 'Candidate archive does not contain the generated IIS web.config.'
        }
        if ($names | Where-Object { $_ -match '^(?i)App_Data/' }) {
            throw 'Candidate archive contains App_Data. Durable production state must never ship inside a replaceable release package.'
        }
        if ($names | Where-Object { $_ -match '^(?i)appsettings\.Development\.json$' }) {
            throw 'Candidate archive contains appsettings.Development.json.'
        }
        if ($names | Where-Object { $_ -match '^(?i)appsettings\.Production\.json$' }) {
            throw 'Candidate archive contains an environment-specific appsettings.Production.json. Use the separately approved production configuration.'
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Write-AtomicJson {
    param([string]$Path, [object]$Value)
    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $temp = "$Path.$([Guid]::NewGuid().ToString('N')).tmp"
    $Value | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $temp -Encoding utf8NoBOM
    Move-Item -LiteralPath $temp -Destination $Path -Force
}

if (-not $IsWindows) {
    throw 'Production IIS deployment is supported only on Windows Server.'
}
if ([string]::IsNullOrWhiteSpace($OperationalBackupId)) {
    throw 'OperationalBackupId is required. Create and validate the pre-cutover operational backup before deployment.'
}

Assert-FileExists -Path $ArtifactPath -Label 'Release artifact'
Assert-FileExists -Path $ChecksumPath -Label 'Checksum file'
Assert-FileExists -Path $ProductionConfigPath -Label 'Approved production configuration'
Assert-ProductionConfiguration -Path $ProductionConfigPath -ExpectedHost $HostName
Assert-CleanCandidateArchive -Path $ArtifactPath
$artifactSha256 = Assert-ArtifactChecksum -ZipPath $ArtifactPath -ShaPath $ChecksumPath

$preflightScript = Join-Path $PSScriptRoot 'Test-IisProductionPrerequisites.ps1'
Assert-FileExists -Path $preflightScript -Label 'IIS preflight script'
$preflight = & $preflightScript `
    -HostName $HostName `
    -CertificateThumbprint $CertificateThumbprint `
    -SiteName $SiteName `
    -AppPoolName $AppPoolName `
    -HttpsPort $HttpsPort `
    -PassThru

$releaseRootFull = [IO.Path]::GetFullPath($ReleaseRoot).TrimEnd('\')
$stateRootFull = [IO.Path]::GetFullPath($StateRoot).TrimEnd('\')
if ($stateRootFull.StartsWith("$releaseRootFull\", [StringComparison]::OrdinalIgnoreCase)) {
    throw 'StateRoot must be outside ReleaseRoot so application upgrades cannot replace registrations, secrets, key rings, backups or operational state.'
}

$releasePath = Join-Path $releaseRootFull $ReleaseVersion
if (Test-Path -LiteralPath $releasePath) {
    throw "Release path already exists and will not be overwritten: $releasePath"
}

if ([string]::IsNullOrWhiteSpace($EvidencePath)) {
    $EvidencePath = Join-Path $stateRootFull "deployment-evidence\$ReleaseVersion.json"
}

$baseUri = if ($HttpsPort -eq 443) { [Uri]"https://$HostName/" } else { [Uri]"https://$HostName`:$HttpsPort/" }
$plan = [ordered]@{
    mode = if ($Apply) { 'Apply' } else { 'PlanOnly' }
    releaseVersion = $ReleaseVersion
    artifact = [IO.Path]::GetFileName($ArtifactPath)
    artifactSha256 = $artifactSha256
    site = $SiteName
    appPool = $AppPoolName
    appPoolIdentityType = $preflight.AppPoolIdentityType
    baseUri = $baseUri.AbsoluteUri
    previousPhysicalPath = $preflight.SitePhysicalPath
    releasePath = $releasePath
    stableStateRoot = $stateRootFull
    operationalBackupId = $OperationalBackupId
    evidencePath = $EvidencePath
    actions = @(
        'Extract candidate into a new immutable versioned release directory',
        'Copy only the separately approved secret-free appsettings.Production.json',
        'Create release App_Data as a junction to the stable state root',
        'Grant the existing application-pool identity Modify on stable App_Data and Read/Execute on the release',
        'Stop only the Monitor application pool, switch IIS physicalPath, and start it again',
        'Run HTTPS artifact/health acceptance; automatically switch IIS back to the previous physicalPath if acceptance fails',
        'Write deployment pointer/evidence without credential values'
    )
}

if (-not $Apply) {
    $plan | ConvertTo-Json -Depth 6
    Write-Host 'PLAN ONLY. No IIS, filesystem, ACL, application-pool, binding, certificate, configuration or state changes were made.'
    Write-Host 'Re-run with -Apply only after reviewing this plan and the pre-cutover operational backup.'
    return
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Applying an IIS production deployment requires an elevated PowerShell session.'
}

Import-Module WebAdministration -ErrorAction Stop
New-Item -ItemType Directory -Path $releaseRootFull -Force | Out-Null
New-Item -ItemType Directory -Path $stateRootFull -Force | Out-Null

$stagingPath = Join-Path $releaseRootFull ('.staging-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
$switched = $false
$previousPhysicalPath = [string]$preflight.SitePhysicalPath

try {
    Expand-Archive -LiteralPath $ArtifactPath -DestinationPath $stagingPath -Force
    if (-not (Test-Path -LiteralPath (Join-Path $stagingPath 'Monitor.Web.dll') -PathType Leaf)) {
        throw 'Extracted candidate is missing Monitor.Web.dll.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $stagingPath 'web.config') -PathType Leaf)) {
        throw 'Extracted candidate is missing web.config.'
    }
    if (Test-Path -LiteralPath (Join-Path $stagingPath 'App_Data')) {
        throw 'Extracted candidate unexpectedly contains App_Data; deployment stopped before IIS cutover.'
    }

    Copy-Item -LiteralPath $ProductionConfigPath -Destination (Join-Path $stagingPath 'appsettings.Production.json') -Force
    Move-Item -LiteralPath $stagingPath -Destination $releasePath
    $stagingPath = $null

    $releaseStatePath = Join-Path $releasePath 'App_Data'
    New-Item -ItemType Junction -Path $releaseStatePath -Target $stateRootFull | Out-Null

    $aclIdentity = if ($preflight.AppPoolIdentityType -eq 'ApplicationPoolIdentity') {
        "IIS AppPool\$AppPoolName"
    }
    else {
        $pool = Get-Item -LiteralPath "IIS:\AppPools\$AppPoolName"
        [string]$pool.processModel.userName
    }
    if ([string]::IsNullOrWhiteSpace($aclIdentity)) {
        throw 'Could not resolve the application-pool filesystem identity.'
    }

    & icacls.exe $stateRootFull /grant "${aclIdentity}:(OI)(CI)M" /T /C | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to grant Modify permission on the stable App_Data root.' }
    & icacls.exe $releasePath /grant "${aclIdentity}:(OI)(CI)RX" /T /C | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to grant Read/Execute permission on the versioned release.' }

    Stop-WebAppPool -Name $AppPoolName
    Set-ItemProperty -LiteralPath "IIS:\Sites\$SiteName" -Name physicalPath -Value $releasePath
    $switched = $true
    Start-WebAppPool -Name $AppPoolName

    $acceptScript = Join-Path $PSScriptRoot 'Accept-ProductionSingleNode.ps1'
    Assert-FileExists -Path $acceptScript -Label 'Production acceptance script'
    & $acceptScript `
        -BaseUri $baseUri `
        -ArtifactPath $ArtifactPath `
        -ChecksumPath $ChecksumPath `
        -EvidencePath $EvidencePath

    $pointer = [ordered]@{
        schemaVersion = 1
        deployedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        releaseVersion = $ReleaseVersion
        artifact = [IO.Path]::GetFileName($ArtifactPath)
        artifactSha256 = $artifactSha256
        siteName = $SiteName
        appPoolName = $AppPoolName
        hostName = $HostName
        httpsPort = $HttpsPort
        currentPhysicalPath = $releasePath
        previousPhysicalPath = $previousPhysicalPath
        stableStateRoot = $stateRootFull
        operationalBackupId = $OperationalBackupId
        acceptanceEvidence = $EvidencePath
    }
    Write-AtomicJson -Path (Join-Path $stateRootFull 'deployment-current.json') -Value $pointer

    Write-Host "Monitor SingleNode candidate '$ReleaseVersion' is active on $($baseUri.AbsoluteUri)."
    Write-Host "Previous IIS physicalPath retained for rollback: $previousPhysicalPath"
    Write-Host 'Repository/cutover health acceptance passed. P0.5 still requires the documented IIS recycle, registration/credential durability, deployed least-privilege, and rollback rehearsal gates.'
}
catch {
    if ($switched) {
        try {
            Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
            Set-ItemProperty -LiteralPath "IIS:\Sites\$SiteName" -Name physicalPath -Value $previousPhysicalPath
            Start-WebAppPool -Name $AppPoolName
            Write-Warning "Deployment failed; IIS physicalPath was restored to the previous release: $previousPhysicalPath"
        }
        catch {
            Write-Warning 'Automatic application-path rollback also failed. Keep the site drained and follow docs/ROLLBACK_RUNBOOK.md.'
        }
    }
    throw
}
finally {
    if (-not [string]::IsNullOrWhiteSpace([string]$stagingPath) -and (Test-Path -LiteralPath $stagingPath)) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}
