[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$requiredTools = @(
    'New-ProductionAcceptanceSession.ps1',
    'New-ProductionAcceptanceEvidencePack.ps1',
    'Test-ProductionAcceptanceSessionBinding.ps1',
    'Set-ProductionAcceptanceGate.ps1',
    'Complete-ProductionAcceptance.ps1',
    'Test-ProductionAcceptanceEvidence.ps1'
)

function Assert-Rejected {
    param([scriptblock]$Action, [string]$FailureMessage)
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw $FailureMessage }
}

$root = Join-Path $env:RUNNER_TEMP 'monitor-p0-5-sidecar-contract'
Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $root | Out-Null
$toolRoot = Join-Path $root 'acceptance-control-toolkit'
$sourceRoot = Join-Path $root 'source'
$payloadRoot = Join-Path $sourceRoot 'payload'
New-Item -ItemType Directory -Force -Path $toolRoot, $sourceRoot, $payloadRoot | Out-Null

foreach ($toolName in $requiredTools) {
    Copy-Item -LiteralPath (Join-Path 'scripts' $toolName) -Destination (Join-Path $toolRoot $toolName) -Force
}

$version = '0.0.0-sidecar-ci'
$fileName = "Monitor-$version-win-x64.zip"
$artifactPath = Join-Path $sourceRoot $fileName
$checksumPath = "$artifactPath.sha256"
'candidate=synthetic;purpose=sidecar-hash-contract' | Set-Content -LiteralPath (Join-Path $payloadRoot 'candidate.txt') -Encoding utf8NoBOM
Compress-Archive -Path (Join-Path $payloadRoot '*') -DestinationPath $artifactPath -CompressionLevel Optimal -Force
$productHash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$productHash  $fileName" | Set-Content -LiteralPath $checksumPath -Encoding ascii
$toolingCommit = ('c' * 40) -join ''

$sessionRoot = Join-Path $root 'session'
$session = & (Join-Path $toolRoot 'New-ProductionAcceptanceSession.ps1') `
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
    -OperationalBackupId 'ci-backup-sidecar-001' `
    -PreviousPhysicalPath 'C:\Program Files\Monitor\releases\previous' `
    -StateRoot 'C:\ProgramData\Monitor\App_Data'

if ($session.OperatorToolingCommit -ne $toolingCommit -or $session.OperatorToolingFileCount -ne $requiredTools.Count) {
    throw 'Initializer did not bind the exact acceptance-control sidecar identity/file count.'
}

$bindingPath = Join-Path $toolRoot 'Test-ProductionAcceptanceSessionBinding.ps1'
& $bindingPath `
    -EvidencePath $session.EvidencePath `
    -ExpectedSessionManifestSha256 $session.ManifestSha256 | Out-Null

$tamperedTool = Join-Path $toolRoot 'Complete-ProductionAcceptance.ps1'
$originalFinalizer = Get-Content -LiteralPath $tamperedTool -Raw
Add-Content -LiteralPath $tamperedTool -Value '# tampered after session initialization' -Encoding utf8NoBOM
Assert-Rejected `
    -Action { & $bindingPath -EvidencePath $session.EvidencePath -ExpectedSessionManifestSha256 $session.ManifestSha256 } `
    -FailureMessage 'Modified acceptance-control sidecar file unexpectedly passed locked-session binding.'
[IO.File]::WriteAllText($tamperedTool, $originalFinalizer, [Text.UTF8Encoding]::new($false))

$missingTool = Join-Path $toolRoot 'Set-ProductionAcceptanceGate.ps1'
Move-Item -LiteralPath $missingTool -Destination ($missingTool + '.missing') -Force
Assert-Rejected `
    -Action { & $bindingPath -EvidencePath $session.EvidencePath -ExpectedSessionManifestSha256 $session.ManifestSha256 } `
    -FailureMessage 'Missing acceptance-control sidecar file unexpectedly passed locked-session binding.'

Write-Host 'Acceptance Control Toolkit sidecar contract passed: exact commit identity/file set locked at initialization; modified and missing sidecar files rejected.'
