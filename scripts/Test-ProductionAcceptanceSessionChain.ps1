[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Recorder', 'Finalizer')]
    [string]$Mode
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function New-SyntheticAcceptanceSession {
    param([string]$Name)

    $root = Join-Path $env:RUNNER_TEMP $Name
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $root | Out-Null

    $version = '0.0.0-ci'
    $fileName = "Monitor-$version-win-x64.zip"
    $checksumName = "$fileName.sha256"
    $sourceRoot = Join-Path $root 'source'
    $payloadRoot = Join-Path $sourceRoot 'payload'
    New-Item -ItemType Directory -Force -Path $sourceRoot, $payloadRoot | Out-Null
    'candidate=synthetic;purpose=session-chain-contract' | Set-Content -LiteralPath (Join-Path $payloadRoot 'candidate.txt') -Encoding utf8NoBOM

    $artifactPath = Join-Path $sourceRoot $fileName
    $checksumPath = Join-Path $sourceRoot $checksumName
    Compress-Archive -Path (Join-Path $payloadRoot '*') -DestinationPath $artifactPath -CompressionLevel Optimal -Force
    $productHash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$productHash  $fileName" | Set-Content -LiteralPath $checksumPath -Encoding ascii
    $toolingCommit = ('c' * 40) -join ''

    $sessionRoot = Join-Path $root 'session'
    $session = ./scripts/New-ProductionAcceptanceSession.ps1 `
        -SessionRoot $sessionRoot `
        -ArtifactPath $artifactPath `
        -ChecksumPath $checksumPath `
        -CandidateVersion $version `
        -ExpectedProductSha256 $productHash `
        -SourceCommit (('a' * 40) -join '') `
        -TestedMergeCommit (('b' * 40) -join '') `
        -OperatorToolingCommit $toolingCommit `
        -HostName 'monitor.example.internal' `
        -SiteName 'Monitor' `
        -AppPoolName 'Monitor' `
        -AppPoolIdentity 'IIS AppPool\Monitor' `
        -CertificateThumbprint (('d' * 40) -join '') `
        -OperationalBackupId 'ci-backup-001' `
        -PreviousPhysicalPath 'C:\Program Files\Monitor\releases\previous' `
        -StateRoot 'C:\ProgramData\Monitor\App_Data'

    [pscustomobject]@{
        Root = $root
        SessionRoot = $sessionRoot
        EvidenceRoot = Join-Path $sessionRoot 'evidence'
        ProofRoot = Join-Path $sessionRoot 'evidence\proof'
        EvidencePath = Join-Path $sessionRoot 'evidence\p0-5-evidence-pack.json'
        ManifestPath = Join-Path $sessionRoot 'session-manifest.json'
        ManifestLockPath = Join-Path $sessionRoot 'session-manifest.sha256'
        ManifestSha256 = $session.ManifestSha256
        ProductSha256 = $productHash
        ToolingCommit = $toolingCommit
        SourceArtifact = $artifactPath
        CandidateArtifact = Join-Path $sessionRoot "candidate\$fileName"
    }
}

function Assert-Rejected {
    param([scriptblock]$Action, [string]$FailureMessage)
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw $FailureMessage }
}

if ($Mode -eq 'Recorder') {
    $context = New-SyntheticAcceptanceSession -Name 'monitor-p0-5-session-recorder-contract'
    $manifestHash = $context.ManifestSha256
    $packPath = $context.EvidencePath

    $binding = ./scripts/Test-ProductionAcceptanceSessionBinding.ps1 `
        -EvidencePath $packPath `
        -ExpectedSessionManifestSha256 $manifestHash
    if ($binding.OperatorToolingCommit -ne $context.ToolingCommit) {
        throw 'Session binding did not retain the acceptance-control sidecar tooling commit.'
    }

    $goodRelative = 'proof/artifactChecksumVerified.txt'
    $goodTarget = Join-Path $context.EvidenceRoot $goodRelative
    'gate=artifactChecksumVerified;result=PASS;source=windows-ci' | Set-Content -LiteralPath $goodTarget -Encoding utf8NoBOM
    ./scripts/Set-ProductionAcceptanceGate.ps1 `
        -EvidencePath $packPath `
        -ExpectedSessionManifestSha256 $manifestHash `
        -GateName artifactChecksumVerified `
        -EvidenceFile $goodRelative `
        -AcknowledgePass | Out-Null

    $recorded = Get-Content -LiteralPath $packPath -Raw | ConvertFrom-Json -Depth 20
    if (-not $recorded.gates.artifactChecksumVerified.passed) { throw 'Recorder did not mark the explicit gate PASS.' }
    if ($recorded.gates.artifactChecksumVerified.evidenceSha256 -ne (Get-FileHash -LiteralPath $goodTarget -Algorithm SHA256).Hash.ToLowerInvariant()) {
        throw 'Recorder did not bind the evidence SHA-256.'
    }
    if ($recorded.gates.iisPreflightPassed.passed) { throw 'Recorder mutated an unrelated production gate.' }
    if (-not [string]::IsNullOrWhiteSpace([string]$recorded.acceptedBy) -or $null -ne $recorded.acceptedAtUtc) {
        throw 'Recorder mutated final operator acceptance metadata.'
    }

    $secondRelative = 'proof/iisPreflightPassed.txt'
    $secondTarget = Join-Path $context.EvidenceRoot $secondRelative
    'gate=iisPreflightPassed;result=PASS;source=windows-ci' | Set-Content -LiteralPath $secondTarget -Encoding utf8NoBOM
    Assert-Rejected `
        -Action { ./scripts/Set-ProductionAcceptanceGate.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 $manifestHash -GateName iisPreflightPassed -EvidenceFile $secondRelative } `
        -FailureMessage 'Recorder without acknowledgement unexpectedly passed.'

    Assert-Rejected `
        -Action { ./scripts/Set-ProductionAcceptanceGate.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 $manifestHash -GateName iisPreflightPassed -EvidenceFile '../outside.txt' -AcknowledgePass } `
        -FailureMessage 'Traversal evidence unexpectedly passed.'

    $secretRelative = 'proof/secret.txt'
    'password=must-never-be-retained' | Set-Content -LiteralPath (Join-Path $context.EvidenceRoot $secretRelative) -Encoding utf8NoBOM
    Assert-Rejected `
        -Action { ./scripts/Set-ProductionAcceptanceGate.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 $manifestHash -GateName iisPreflightPassed -EvidenceFile $secretRelative -AcknowledgePass } `
        -FailureMessage 'Secret evidence unexpectedly passed.'

    Assert-Rejected `
        -Action { ./scripts/Set-ProductionAcceptanceGate.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 $manifestHash -GateName artifactChecksumVerified -EvidenceFile $goodRelative -AcknowledgePass } `
        -FailureMessage 'Duplicate PASS unexpectedly passed.'

    Assert-Rejected `
        -Action { ./scripts/Set-ProductionAcceptanceGate.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 (('0' * 64) -join '') -GateName iisPreflightPassed -EvidenceFile $secondRelative -AcknowledgePass } `
        -FailureMessage 'Recorder with wrong expected session-manifest hash unexpectedly passed.'

    $originalPackRaw = Get-Content -LiteralPath $packPath -Raw
    $tamperedPack = $originalPackRaw | ConvertFrom-Json -Depth 20
    $tamperedPack.candidate.sha256 = ('e' * 64) -join ''
    $tamperedPack | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $packPath -Encoding utf8NoBOM
    Assert-Rejected `
        -Action { ./scripts/Set-ProductionAcceptanceGate.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 $manifestHash -GateName iisPreflightPassed -EvidenceFile $secondRelative -AcknowledgePass } `
        -FailureMessage 'Recorder accepted an evidence pack whose candidate identity drifted from the locked session.'
    [IO.File]::WriteAllText($packPath, $originalPackRaw, [Text.UTF8Encoding]::new($false))

    $originalManifestRaw = Get-Content -LiteralPath $context.ManifestPath -Raw
    $tamperedManifest = $originalManifestRaw | ConvertFrom-Json -Depth 20
    $tamperedManifest.operatorToolingCommit = ('e' * 40) -join ''
    $tamperedManifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $context.ManifestPath -Encoding utf8NoBOM
    Assert-Rejected `
        -Action { ./scripts/Set-ProductionAcceptanceGate.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 $manifestHash -GateName iisPreflightPassed -EvidenceFile $secondRelative -AcknowledgePass } `
        -FailureMessage 'Recorder accepted a session manifest whose operator tooling identity drifted from the externally preserved manifest SHA-256.'
    [IO.File]::WriteAllText($context.ManifestPath, $originalManifestRaw, [Text.UTF8Encoding]::new($false))

    Copy-Item -LiteralPath $context.CandidateArtifact -Destination ($context.CandidateArtifact + '.backup') -Force
    Add-Content -LiteralPath $context.CandidateArtifact -Value 'tampered-candidate-bytes' -Encoding ascii
    Assert-Rejected `
        -Action { ./scripts/Set-ProductionAcceptanceGate.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 $manifestHash -GateName iisPreflightPassed -EvidenceFile $secondRelative -AcknowledgePass } `
        -FailureMessage 'Recorder accepted candidate bytes that drifted from the selected product SHA-256.'
    Move-Item -LiteralPath ($context.CandidateArtifact + '.backup') -Destination $context.CandidateArtifact -Force

    $finalBinding = ./scripts/Test-ProductionAcceptanceSessionBinding.ps1 `
        -EvidencePath $packPath `
        -ExpectedSessionManifestSha256 $manifestHash
    if ($finalBinding.OperatorToolingCommit -ne $context.ToolingCommit) {
        throw 'Final session binding did not retain the expected operator tooling commit.'
    }

    Write-Host 'Session-bound gate recorder contract passed: locked manifest, sidecar tooling identity, candidate bytes and evidence-pack identity enforced before PASS mutation.'
    return
}

$context = New-SyntheticAcceptanceSession -Name 'monitor-p0-5-session-finalizer-contract'
$manifestHash = $context.ManifestSha256
$packPath = $context.EvidencePath
$summaryPath = Join-Path $context.EvidenceRoot 'p0-5-closure-summary.json'

Assert-Rejected `
    -Action { ./scripts/Complete-ProductionAcceptance.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 $manifestHash -AcceptedBy 'ci-operator' -ClosureSummaryFile 'too-early.json' -AcknowledgeFinalAcceptance } `
    -FailureMessage 'Finalizer before all gates unexpectedly passed.'

$pack = Get-Content -LiteralPath $packPath -Raw | ConvertFrom-Json -Depth 20
foreach ($property in $pack.gates.PSObject.Properties) {
    $relative = "proof/$($property.Name).txt"
    $target = Join-Path $context.EvidenceRoot $relative
    "gate=$($property.Name);result=PASS;source=windows-ci" | Set-Content -LiteralPath $target -Encoding utf8NoBOM
    ./scripts/Set-ProductionAcceptanceGate.ps1 `
        -EvidencePath $packPath `
        -ExpectedSessionManifestSha256 $manifestHash `
        -GateName $property.Name `
        -EvidenceFile $relative `
        -AcknowledgePass | Out-Null
}

Assert-Rejected `
    -Action { ./scripts/Complete-ProductionAcceptance.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 $manifestHash -AcceptedBy 'ci-operator' -ClosureSummaryFile 'no-ack.json' } `
    -FailureMessage 'Finalizer without acknowledgement unexpectedly passed.'
Assert-Rejected `
    -Action { ./scripts/Complete-ProductionAcceptance.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 $manifestHash -AcceptedBy 'ci-operator' -ClosureSummaryFile '../escape.json' -AcknowledgeFinalAcceptance } `
    -FailureMessage 'Unsafe finalizer summary path unexpectedly passed.'
Assert-Rejected `
    -Action { ./scripts/Complete-ProductionAcceptance.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 $manifestHash -AcceptedBy 'password=must-never-be-retained' -ClosureSummaryFile 'unsafe-operator.json' -AcknowledgeFinalAcceptance } `
    -FailureMessage 'Unsafe finalizer operator identity unexpectedly passed.'
Assert-Rejected `
    -Action { ./scripts/Complete-ProductionAcceptance.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 (('0' * 64) -join '') -AcceptedBy 'ci-operator' -ClosureSummaryFile 'wrong-session.json' -AcknowledgeFinalAcceptance } `
    -FailureMessage 'Finalizer with wrong expected session-manifest hash unexpectedly passed.'

$allGatesPackRaw = Get-Content -LiteralPath $packPath -Raw
$tamperedIdentity = $allGatesPackRaw | ConvertFrom-Json -Depth 20
$tamperedIdentity.candidate.sourceCommit = ('e' * 40) -join ''
$tamperedIdentity | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $packPath -Encoding utf8NoBOM
Assert-Rejected `
    -Action { ./scripts/Complete-ProductionAcceptance.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 $manifestHash -AcceptedBy 'ci-operator' -ClosureSummaryFile 'tampered-session.json' -AcknowledgeFinalAcceptance } `
    -FailureMessage 'Finalizer accepted an evidence pack whose candidate identity drifted from the locked session.'
[IO.File]::WriteAllText($packPath, $allGatesPackRaw, [Text.UTF8Encoding]::new($false))

./scripts/Complete-ProductionAcceptance.ps1 `
    -EvidencePath $packPath `
    -ExpectedSessionManifestSha256 $manifestHash `
    -AcceptedBy 'ci-operator' `
    -ClosureSummaryFile 'p0-5-closure-summary.json' `
    -AcknowledgeFinalAcceptance | Out-Null

if (-not (Test-Path -LiteralPath $summaryPath -PathType Leaf)) { throw 'Positive finalizer did not create a closure summary.' }
$acceptedPack = Get-Content -LiteralPath $packPath -Raw | ConvertFrom-Json -Depth 20
if ($acceptedPack.acceptedBy -ne 'ci-operator' -or $null -eq $acceptedPack.acceptedAtUtc) {
    throw 'Positive finalizer did not record final operator acceptance metadata.'
}
$summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json -Depth 20
if ($summary.sessionManifestSha256 -ne $manifestHash -or $summary.selectedProductSha256 -ne $context.ProductSha256) {
    throw 'Closure summary did not retain the locked session-manifest and selected-product SHA-256 anchors.'
}
if ($summary.operatorToolingCommit -ne $context.ToolingCommit) {
    throw 'Closure summary did not retain the acceptance-control sidecar tooling commit.'
}

Assert-Rejected `
    -Action { ./scripts/Complete-ProductionAcceptance.ps1 -EvidencePath $packPath -ExpectedSessionManifestSha256 $manifestHash -AcceptedBy 'ci-operator' -ClosureSummaryFile 'second-summary.json' -AcknowledgeFinalAcceptance } `
    -FailureMessage 'Second finalization unexpectedly passed.'

./scripts/Test-ProductionAcceptanceEvidence.ps1 `
    -EvidencePath $packPath `
    -ExpectedSessionManifestSha256 $manifestHash `
    -ClosureSummaryPath (Join-Path $context.EvidenceRoot 'validator-recheck.json') | Out-Null

function Assert-StandaloneValidatorRejected {
    param([string]$Path, [string]$FailureMessage)
    Assert-Rejected `
        -Action { ./scripts/Test-ProductionAcceptanceEvidence.ps1 -EvidencePath $Path -EvidenceRoot $context.EvidenceRoot } `
        -FailureMessage $FailureMessage
}

$negativeGatePath = Join-Path $context.Root 'negative-gate.json'
$negativeGate = Get-Content -LiteralPath $packPath -Raw | ConvertFrom-Json -Depth 20
$negativeGate.gates.artifactChecksumVerified.passed = $false
$negativeGate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $negativeGatePath -Encoding utf8NoBOM
Assert-StandaloneValidatorRejected -Path $negativeGatePath -FailureMessage 'Negative gate unexpectedly passed.'

$tamperedHashPath = Join-Path $context.Root 'tampered-hash.json'
$tamperedHash = Get-Content -LiteralPath $packPath -Raw | ConvertFrom-Json -Depth 20
$tamperedHash.gates.iisPreflightPassed.evidenceSha256 = ('0' * 64) -join ''
$tamperedHash | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $tamperedHashPath -Encoding utf8NoBOM
Assert-StandaloneValidatorRejected -Path $tamperedHashPath -FailureMessage 'Tampered evidence hash unexpectedly passed.'

$secretPath = Join-Path $context.Root 'secret-bearing.json'
$secretPack = Get-Content -LiteralPath $packPath -Raw | ConvertFrom-Json -Depth 20
$secretPack | Add-Member -NotePropertyName 'password' -NotePropertyValue 'must-never-be-retained'
$secretPack | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $secretPath -Encoding utf8NoBOM
Assert-StandaloneValidatorRejected -Path $secretPath -FailureMessage 'Secret-bearing evidence unexpectedly passed.'

Write-Host 'Session-bound finalizer contract passed: all 15 gates, locked session + sidecar tooling anchors, authoritative validation and standalone schema negatives verified.'
