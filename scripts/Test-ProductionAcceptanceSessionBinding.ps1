[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EvidencePath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{64}$')]
    [string]$ExpectedSessionManifestSha256
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-ExactProperties {
    param([object]$Value, [string[]]$Allowed, [string]$Path)
    if ($null -eq $Value) { throw "$Path is required." }
    $names = @($Value.PSObject.Properties.Name)
    $missing = @($Allowed | Where-Object { $_ -cnotin $names })
    $unknown = @($names | Where-Object { $_ -cnotin $Allowed })
    if ($missing.Count -gt 0) { throw "$Path is missing required properties: $($missing -join ', ')." }
    if ($unknown.Count -gt 0) { throw "$Path contains unknown properties: $($unknown -join ', ')." }
}

function Resolve-SessionRelativePath {
    param([string]$SessionRoot, [string]$RelativePath, [string]$Name)

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        [IO.Path]::IsPathRooted($RelativePath) -or
        $RelativePath -match '(^|[\\/])\.\.?([\\/]|$)' -or
        $RelativePath -match '[?#]' -or
        $RelativePath.Length -gt 260) {
        throw "$Name must be a bounded relative local path inside the acceptance session."
    }

    $rootFull = [IO.Path]::GetFullPath($SessionRoot).TrimEnd('\', '/')
    $rootPrefix = $rootFull + [IO.Path]::DirectorySeparatorChar
    $normalizedRelative = $RelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $targetFull = [IO.Path]::GetFullPath((Join-Path $rootFull $normalizedRelative))
    if (-not $targetFull.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Name escapes the acceptance session root."
    }
    return $targetFull
}

function Read-CanonicalChecksum {
    param([string]$Path, [string]$ExpectedFileName)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Candidate checksum was not found: $Path"
    }
    $line = (Get-Content -LiteralPath $Path -Raw).Trim()
    $escaped = [Regex]::Escape($ExpectedFileName)
    if ($line -notmatch "^(?<hash>[a-fA-F0-9]{64})\s+\*?$escaped$") {
        throw "Candidate checksum must contain exactly '<64-hex-sha256>  $ExpectedFileName'."
    }
    return $Matches['hash'].ToLowerInvariant()
}

$requiredOperatorToolingFiles = @(
    'New-ProductionAcceptanceSession.ps1',
    'New-ProductionAcceptanceEvidencePack.ps1',
    'Test-ProductionAcceptanceSessionBinding.ps1',
    'Set-ProductionAcceptanceGate.ps1',
    'Complete-ProductionAcceptance.ps1',
    'Test-ProductionAcceptanceEvidence.ps1'
)

if (-not (Test-Path -LiteralPath $EvidencePath -PathType Leaf)) {
    throw "Evidence pack was not found: $EvidencePath"
}

$resolvedEvidencePath = (Resolve-Path -LiteralPath $EvidencePath).Path
if ([IO.Path]::GetFileName($resolvedEvidencePath) -cne 'p0-5-evidence-pack.json') {
    throw 'Session-bound evidence pack file name must be exactly p0-5-evidence-pack.json.'
}

$evidenceRoot = Split-Path -Parent $resolvedEvidencePath
if ([IO.Path]::GetFileName($evidenceRoot) -cne 'evidence') {
    throw 'Session-bound evidence pack must reside directly beneath the acceptance session evidence directory.'
}
$sessionRoot = Split-Path -Parent $evidenceRoot
if ([string]::IsNullOrWhiteSpace($sessionRoot) -or -not (Test-Path -LiteralPath $sessionRoot -PathType Container)) {
    throw 'Acceptance session root was not found.'
}

$manifestPath = Join-Path $sessionRoot 'session-manifest.json'
$manifestLockPath = Join-Path $sessionRoot 'session-manifest.sha256'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or -not (Test-Path -LiteralPath $manifestLockPath -PathType Leaf)) {
    throw 'Session manifest and session-manifest.sha256 are required for gate recording and finalization.'
}

$expectedManifestHash = $ExpectedSessionManifestSha256.ToLowerInvariant()
$actualManifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualManifestHash -ne $expectedManifestHash) {
    throw 'Session manifest SHA-256 does not match the externally preserved expected session-manifest SHA-256.'
}
$manifestLockLine = (Get-Content -LiteralPath $manifestLockPath -Raw).Trim()
if ($manifestLockLine -cne "$expectedManifestHash  session-manifest.json") {
    throw 'session-manifest.sha256 does not match the externally preserved expected session-manifest SHA-256.'
}

try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 20
}
catch {
    throw 'Session manifest is not valid JSON.'
}

Assert-ExactProperties -Value $manifest -Allowed @(
    'schemaVersion',
    'createdAtUtc',
    'status',
    'deploymentMode',
    'candidateVersion',
    'artifactFileName',
    'artifactSha256',
    'selectedProductSha256',
    'sourceCommit',
    'testedMergeCommit',
    'operatorToolingCommit',
    'operatorToolingFiles',
    'hostName',
    'siteName',
    'appPoolName',
    'appPoolIdentity',
    'certificateThumbprint',
    'operationalBackupId',
    'previousPhysicalPath',
    'stateRoot',
    'candidateArtifactRelativePath',
    'candidateChecksumRelativePath',
    'evidencePackRelativePath',
    'proofRootRelativePath',
    'externalGateCount',
    'externalGatesPassed',
    'note'
) -Path '$manifest'

if ([int]$manifest.schemaVersion -ne 1) { throw 'Session manifest schemaVersion must be exactly 1.' }
if ([string]$manifest.status -cne 'PreparedFailClosed') { throw 'Session manifest status must remain PreparedFailClosed.' }
if ([string]$manifest.deploymentMode -cne 'SingleNode') { throw 'Session manifest deploymentMode must be exactly SingleNode.' }
if ([int]$manifest.externalGateCount -ne 15 -or [int]$manifest.externalGatesPassed -ne 0) {
    throw 'Immutable session manifest must remain the original fail-closed 0/15 anchor.'
}

$selectedProductHash = ([string]$manifest.selectedProductSha256).ToLowerInvariant()
if ($selectedProductHash -notmatch '^[a-f0-9]{64}$') { throw 'Session manifest selectedProductSha256 is invalid.' }
if (([string]$manifest.artifactSha256).ToLowerInvariant() -ne $selectedProductHash) {
    throw 'Session manifest artifactSha256 does not match selectedProductSha256.'
}
if ([string]$manifest.sourceCommit -notmatch '^[a-fA-F0-9]{40}$' -or [string]$manifest.testedMergeCommit -notmatch '^[a-fA-F0-9]{40}$') {
    throw 'Session manifest source/tested-merge identity is invalid.'
}
$operatorToolingCommit = ([string]$manifest.operatorToolingCommit).ToLowerInvariant()
if ($operatorToolingCommit -notmatch '^[a-f0-9]{40}$') {
    throw 'Session manifest operatorToolingCommit must be a full 40-hex repository commit SHA.'
}

Assert-ExactProperties -Value $manifest.operatorToolingFiles -Allowed $requiredOperatorToolingFiles -Path '$manifest.operatorToolingFiles'
foreach ($toolName in $requiredOperatorToolingFiles) {
    $expectedToolHash = ([string]$manifest.operatorToolingFiles.PSObject.Properties[$toolName].Value).ToLowerInvariant()
    if ($expectedToolHash -notmatch '^[a-f0-9]{64}$') {
        throw "Session manifest operatorToolingFiles.$toolName must be a 64-hex SHA-256."
    }

    $actualToolPath = Join-Path $PSScriptRoot $toolName
    if (-not (Test-Path -LiteralPath $actualToolPath -PathType Leaf)) {
        throw "Acceptance Control Toolkit sidecar file is missing: $toolName"
    }
    $actualToolHash = (Get-FileHash -LiteralPath $actualToolPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualToolHash -ne $expectedToolHash) {
        throw "Acceptance Control Toolkit sidecar file hash does not match the locked session manifest: $toolName"
    }
}

$expectedArtifactName = "Monitor-$($manifest.candidateVersion)-win-x64.zip"
if ([string]$manifest.artifactFileName -cne $expectedArtifactName) {
    throw "Session manifest artifactFileName must be exactly '$expectedArtifactName'."
}
$expectedArtifactRelative = "candidate/$expectedArtifactName"
$expectedChecksumRelative = "$expectedArtifactRelative.sha256"
if ([string]$manifest.candidateArtifactRelativePath -cne $expectedArtifactRelative -or
    [string]$manifest.candidateChecksumRelativePath -cne $expectedChecksumRelative -or
    [string]$manifest.evidencePackRelativePath -cne 'evidence/p0-5-evidence-pack.json' -or
    [string]$manifest.proofRootRelativePath -cne 'evidence/proof') {
    throw 'Session manifest canonical candidate/evidence relative paths were changed.'
}

$candidateArtifactPath = Resolve-SessionRelativePath -SessionRoot $sessionRoot -RelativePath ([string]$manifest.candidateArtifactRelativePath) -Name 'candidateArtifactRelativePath'
$candidateChecksumPath = Resolve-SessionRelativePath -SessionRoot $sessionRoot -RelativePath ([string]$manifest.candidateChecksumRelativePath) -Name 'candidateChecksumRelativePath'
$manifestEvidencePath = Resolve-SessionRelativePath -SessionRoot $sessionRoot -RelativePath ([string]$manifest.evidencePackRelativePath) -Name 'evidencePackRelativePath'
$proofRoot = Resolve-SessionRelativePath -SessionRoot $sessionRoot -RelativePath ([string]$manifest.proofRootRelativePath) -Name 'proofRootRelativePath'

if (-not $manifestEvidencePath.Equals($resolvedEvidencePath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Evidence pack path does not match the locked session manifest.'
}
if (-not (Test-Path -LiteralPath $candidateArtifactPath -PathType Leaf)) { throw 'Session candidate artifact is missing.' }
if (-not (Test-Path -LiteralPath $proofRoot -PathType Container)) { throw 'Session evidence/proof directory is missing.' }

$candidateHash = (Get-FileHash -LiteralPath $candidateArtifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($candidateHash -ne $selectedProductHash) {
    throw 'Session candidate artifact bytes no longer match the selected product SHA-256.'
}
$checksumHash = Read-CanonicalChecksum -Path $candidateChecksumPath -ExpectedFileName $expectedArtifactName
if ($checksumHash -ne $selectedProductHash) {
    throw 'Session candidate checksum no longer matches the selected product SHA-256.'
}

try {
    $record = Get-Content -LiteralPath $resolvedEvidencePath -Raw | ConvertFrom-Json -Depth 20
}
catch {
    throw 'Evidence pack is not valid JSON.'
}

Assert-ExactProperties -Value $record.candidate -Allowed @('version', 'sourceCommit', 'testedMergeCommit', 'artifactFileName', 'sha256') -Path '$.candidate'
Assert-ExactProperties -Value $record.environment -Allowed @('hostName', 'siteName', 'appPoolName', 'appPoolIdentity', 'certificateThumbprint', 'deploymentMode', 'operationalBackupId', 'previousPhysicalPath', 'stateRoot') -Path '$.environment'

$identityPairs = @(
    @('candidate.version', [string]$record.candidate.version, [string]$manifest.candidateVersion),
    @('candidate.sourceCommit', ([string]$record.candidate.sourceCommit).ToLowerInvariant(), ([string]$manifest.sourceCommit).ToLowerInvariant()),
    @('candidate.testedMergeCommit', ([string]$record.candidate.testedMergeCommit).ToLowerInvariant(), ([string]$manifest.testedMergeCommit).ToLowerInvariant()),
    @('candidate.artifactFileName', [string]$record.candidate.artifactFileName, [string]$manifest.artifactFileName),
    @('candidate.sha256', ([string]$record.candidate.sha256).ToLowerInvariant(), $selectedProductHash),
    @('environment.hostName', ([string]$record.environment.hostName).ToLowerInvariant(), ([string]$manifest.hostName).ToLowerInvariant()),
    @('environment.siteName', [string]$record.environment.siteName, [string]$manifest.siteName),
    @('environment.appPoolName', [string]$record.environment.appPoolName, [string]$manifest.appPoolName),
    @('environment.appPoolIdentity', [string]$record.environment.appPoolIdentity, [string]$manifest.appPoolIdentity),
    @('environment.certificateThumbprint', ([string]$record.environment.certificateThumbprint).ToUpperInvariant(), ([string]$manifest.certificateThumbprint).ToUpperInvariant()),
    @('environment.deploymentMode', [string]$record.environment.deploymentMode, [string]$manifest.deploymentMode),
    @('environment.operationalBackupId', [string]$record.environment.operationalBackupId, [string]$manifest.operationalBackupId),
    @('environment.previousPhysicalPath', [string]$record.environment.previousPhysicalPath, [string]$manifest.previousPhysicalPath),
    @('environment.stateRoot', [string]$record.environment.stateRoot, [string]$manifest.stateRoot)
)
foreach ($pair in $identityPairs) {
    if ([string]$pair[1] -cne [string]$pair[2]) {
        throw "Evidence pack $($pair[0]) does not match the locked acceptance session."
    }
}

[pscustomobject]@{
    SessionRoot = $sessionRoot
    SessionManifestPath = $manifestPath
    SessionManifestSha256 = $expectedManifestHash
    SelectedProductSha256 = $selectedProductHash
    OperatorToolingCommit = $operatorToolingCommit
    OperatorToolingFileCount = $requiredOperatorToolingFiles.Count
    CandidateArtifact = $candidateArtifactPath
    EvidencePack = $resolvedEvidencePath
    ProofRoot = $proofRoot
}
