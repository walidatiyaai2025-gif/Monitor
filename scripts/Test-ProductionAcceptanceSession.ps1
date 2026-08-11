[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Join-Path $env:RUNNER_TEMP 'monitor-p0-5-acceptance-session-contract'
Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $root | Out-Null

$version = '0.0.0-ci'
$fileName = "Monitor-$version-win-x64.zip"
$checksumName = "$fileName.sha256"
$sourceRoot = Join-Path $root 'source'
$payloadRoot = Join-Path $sourceRoot 'payload'
New-Item -ItemType Directory -Force -Path $sourceRoot, $payloadRoot | Out-Null
'candidate=synthetic;purpose=session-contract' | Set-Content -LiteralPath (Join-Path $payloadRoot 'candidate.txt') -Encoding utf8NoBOM

$artifactPath = Join-Path $sourceRoot $fileName
$checksumPath = Join-Path $sourceRoot $checksumName
Compress-Archive -Path (Join-Path $payloadRoot '*') -DestinationPath $artifactPath -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $fileName" | Set-Content -LiteralPath $checksumPath -Encoding ascii

$common = @{
    ArtifactPath = $artifactPath
    ChecksumPath = $checksumPath
    CandidateVersion = $version
    SourceCommit = ('a' * 40) -join ''
    TestedMergeCommit = ('b' * 40) -join ''
    HostName = 'monitor.example.internal'
    SiteName = 'Monitor'
    AppPoolName = 'Monitor'
    AppPoolIdentity = 'IIS AppPool\Monitor'
    CertificateThumbprint = ('d' * 40) -join ''
    OperationalBackupId = 'ci-backup-001'
    PreviousPhysicalPath = 'C:\Program Files\Monitor\releases\previous'
    StateRoot = 'C:\ProgramData\Monitor\App_Data'
}

$sessionRoot = Join-Path $root 'session-good'
$result = ./scripts/New-ProductionAcceptanceSession.ps1 @common -SessionRoot $sessionRoot
if (-not (Test-Path -LiteralPath $sessionRoot -PathType Container)) { throw 'Session initializer did not create the target workspace.' }
if ($result.ExternalGateCount -ne 15 -or $result.ExternalGatesPassed -ne 0 -or $result.ProductionAccepted) {
    throw 'Session initializer did not remain fail-closed at 0/15 external gates.'
}

$manifestPath = Join-Path $sessionRoot 'session-manifest.json'
$manifestLockPath = Join-Path $sessionRoot 'session-manifest.sha256'
$packPath = Join-Path $sessionRoot 'evidence\p0-5-evidence-pack.json'
$stepsPath = Join-Path $sessionRoot 'OPERATOR-NEXT-STEPS.txt'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $manifestLockPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $packPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $stepsPath -PathType Leaf)) {
    throw 'Session initializer did not create the required manifest/lock/evidence/next-steps files.'
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 20
if ($manifest.status -ne 'PreparedFailClosed' -or $manifest.externalGateCount -ne 15 -or $manifest.externalGatesPassed -ne 0) {
    throw 'Session manifest did not record the fail-closed 0/15 state.'
}
if ($manifest.artifactSha256 -ne $hash -or $manifest.artifactFileName -ne $fileName) {
    throw 'Session manifest is not bound to the selected candidate bytes.'
}

$lockLine = (Get-Content -LiteralPath $manifestLockPath -Raw).Trim()
$manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($lockLine -ne "$manifestHash  session-manifest.json") {
    throw 'Session manifest SHA-256 lock does not match the created manifest.'
}

$pack = Get-Content -LiteralPath $packPath -Raw | ConvertFrom-Json -Depth 20
$gates = @($pack.gates.PSObject.Properties)
if ($gates.Count -ne 15 -or @($gates | Where-Object { $_.Value.passed }).Count -ne 0) {
    throw 'Session evidence pack must contain exactly 15 gates with zero PASS values.'
}
if (-not [string]::IsNullOrWhiteSpace([string]$pack.acceptedBy) -or $null -ne $pack.acceptedAtUtc) {
    throw 'Session evidence pack unexpectedly contains final acceptance metadata.'
}

$copiedArtifact = Join-Path $sessionRoot "candidate\$fileName"
$copiedChecksum = Join-Path $sessionRoot "candidate\$checksumName"
if ((Get-FileHash -LiteralPath $copiedArtifact -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) {
    throw 'Session candidate copy does not match the selected artifact SHA-256.'
}
if (-not (Test-Path -LiteralPath $copiedChecksum -PathType Leaf)) {
    throw 'Session candidate checksum copy is missing.'
}

function Assert-SessionRejected {
    param([scriptblock]$Action, [string]$FailureMessage)
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw $FailureMessage }
}

Assert-SessionRejected `
    -Action { ./scripts/New-ProductionAcceptanceSession.ps1 @common -SessionRoot $sessionRoot } `
    -FailureMessage 'Reused session root unexpectedly passed.'

$tamperedRoot = Join-Path $root 'tampered-source'
New-Item -ItemType Directory -Force -Path $tamperedRoot | Out-Null
$tamperedArtifact = Join-Path $tamperedRoot $fileName
$tamperedChecksum = Join-Path $tamperedRoot $checksumName
Copy-Item -LiteralPath $artifactPath -Destination $tamperedArtifact
(('0' * 64) + "  $fileName") | Set-Content -LiteralPath $tamperedChecksum -Encoding ascii
$tamperedArgs = $common.Clone()
$tamperedArgs.ArtifactPath = $tamperedArtifact
$tamperedArgs.ChecksumPath = $tamperedChecksum
Assert-SessionRejected `
    -Action { ./scripts/New-ProductionAcceptanceSession.ps1 @tamperedArgs -SessionRoot (Join-Path $root 'session-tampered') } `
    -FailureMessage 'Tampered checksum unexpectedly passed.'

$nonZipRoot = Join-Path $root 'non-zip-source'
New-Item -ItemType Directory -Force -Path $nonZipRoot | Out-Null
$nonZipArtifact = Join-Path $nonZipRoot $fileName
$nonZipChecksum = Join-Path $nonZipRoot $checksumName
'not-a-zip' | Set-Content -LiteralPath $nonZipArtifact -Encoding ascii
$nonZipHash = (Get-FileHash -LiteralPath $nonZipArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
"$nonZipHash  $fileName" | Set-Content -LiteralPath $nonZipChecksum -Encoding ascii
$nonZipArgs = $common.Clone()
$nonZipArgs.ArtifactPath = $nonZipArtifact
$nonZipArgs.ChecksumPath = $nonZipChecksum
Assert-SessionRejected `
    -Action { ./scripts/New-ProductionAcceptanceSession.ps1 @nonZipArgs -SessionRoot (Join-Path $root 'session-non-zip') } `
    -FailureMessage 'Non-ZIP artifact unexpectedly passed.'

$secretArgs = $common.Clone()
$secretArgs.OperationalBackupId = 'password=must-never-be-retained'
Assert-SessionRejected `
    -Action { ./scripts/New-ProductionAcceptanceSession.ps1 @secretArgs -SessionRoot (Join-Path $root 'session-secret') } `
    -FailureMessage 'Secret-like session metadata unexpectedly passed.'

Assert-SessionRejected `
    -Action { ./scripts/New-ProductionAcceptanceSession.ps1 @common -SessionRoot 'relative-session' } `
    -FailureMessage 'Relative session root unexpectedly passed.'

$traversalRoot = "$root\segment\..\session-traversal"
Assert-SessionRejected `
    -Action { ./scripts/New-ProductionAcceptanceSession.ps1 @common -SessionRoot $traversalRoot } `
    -FailureMessage 'Traversal-bearing absolute session root unexpectedly passed.'

Write-Host 'Immutable production acceptance session initializer contract passed: candidate-bound workspace created at 0/15 gates; negative reuse/checksum/ZIP/secret/relative/traversal path cases rejected.'
