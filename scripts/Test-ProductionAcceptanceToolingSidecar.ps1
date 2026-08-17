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

function Invoke-Git {
    param([string]$Root, [string[]]$Arguments)
    $output = & git -C $Root @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Git fixture command failed: git -C '$Root' $($Arguments -join ' ')`n$($output -join [Environment]::NewLine)"
    }
    return (($output | ForEach-Object { [string]$_ }) -join "`n").Trim()
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is required for the Acceptance Control Toolkit provenance runtime.'
}

$root = Join-Path $env:RUNNER_TEMP 'monitor-p0-5-sidecar-contract'
Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $root | Out-Null
$fixtureRoot = Join-Path $root 'source-checkout'
$fixtureScripts = Join-Path $fixtureRoot 'scripts'
$toolRoot = Join-Path $root 'acceptance-control-toolkit'
$sourceRoot = Join-Path $root 'candidate-source'
$payloadRoot = Join-Path $sourceRoot 'payload'
New-Item -ItemType Directory -Force -Path $fixtureScripts, $sourceRoot, $payloadRoot | Out-Null

foreach ($fileName in @($requiredTools + @('Export-ProductionAcceptanceToolkit.ps1', 'Test-ProductionAcceptanceToolkit.ps1'))) {
    Copy-Item -LiteralPath (Join-Path 'scripts' $fileName) -Destination (Join-Path $fixtureScripts $fileName) -Force
}

Invoke-Git -Root $fixtureRoot -Arguments @('init') | Out-Null
Invoke-Git -Root $fixtureRoot -Arguments @('config', 'user.name', 'Monitor CI') | Out-Null
Invoke-Git -Root $fixtureRoot -Arguments @('config', 'user.email', 'monitor-ci@example.invalid') | Out-Null
Invoke-Git -Root $fixtureRoot -Arguments @('add', '--', 'scripts') | Out-Null
Invoke-Git -Root $fixtureRoot -Arguments @('commit', '-m', 'Synthetic reviewed acceptance toolkit') | Out-Null
$toolingCommit = (Invoke-Git -Root $fixtureRoot -Arguments @('rev-parse', '--verify', 'HEAD')).ToLowerInvariant()

$exporterPath = Join-Path $fixtureScripts 'Export-ProductionAcceptanceToolkit.ps1'
$verifierPath = Join-Path $fixtureScripts 'Test-ProductionAcceptanceToolkit.ps1'
$export = & $exporterPath -ExpectedToolingCommit $toolingCommit -OutputDirectory $toolRoot
$toolkitManifestHash = $export.ToolkitManifestSha256
& $verifierPath `
    -ToolkitRoot $toolRoot `
    -ExpectedToolingCommit $toolingCommit `
    -ExpectedToolkitManifestSha256 $toolkitManifestHash | Out-Null

$wrongCommit = if ($toolingCommit -ceq (('0' * 40) -join '')) { (('1' * 40) -join '') } else { (('0' * 40) -join '') }
Assert-Rejected `
    -Action { & $exporterPath -ExpectedToolingCommit $wrongCommit -OutputDirectory (Join-Path $root 'wrong-commit-export') } `
    -FailureMessage 'wrong expected Git commit unexpectedly passed Acceptance Control Toolkit export.'

$dirtyTracked = Join-Path $fixtureScripts 'New-ProductionAcceptanceEvidencePack.ps1'
Add-Content -LiteralPath $dirtyTracked -Value '# deliberate tracked fixture drift' -Encoding utf8NoBOM
Assert-Rejected `
    -Action { & $exporterPath -ExpectedToolingCommit $toolingCommit -OutputDirectory (Join-Path $root 'dirty-export') } `
    -FailureMessage 'dirty tracked checkout unexpectedly passed Acceptance Control Toolkit export.'
Invoke-Git -Root $fixtureRoot -Arguments @('checkout', '--', 'scripts/New-ProductionAcceptanceEvidencePack.ps1') | Out-Null

$manifestPath = Join-Path $toolRoot 'toolkit-manifest.json'
$originalManifest = Get-Content -LiteralPath $manifestPath -Raw
Add-Content -LiteralPath $manifestPath -Value ' ' -Encoding utf8NoBOM
Assert-Rejected `
    -Action { & $verifierPath -ToolkitRoot $toolRoot -ExpectedToolingCommit $toolingCommit -ExpectedToolkitManifestSha256 $toolkitManifestHash } `
    -FailureMessage 'Tampered Acceptance Control Toolkit manifest unexpectedly passed independent verification.'
[IO.File]::WriteAllText($manifestPath, $originalManifest, [Text.UTF8Encoding]::new($false))

$extraPath = Join-Path $toolRoot 'unexpected.txt'
'extra' | Set-Content -LiteralPath $extraPath -Encoding utf8NoBOM
Assert-Rejected `
    -Action { & $verifierPath -ToolkitRoot $toolRoot -ExpectedToolingCommit $toolingCommit -ExpectedToolkitManifestSha256 $toolkitManifestHash } `
    -FailureMessage 'Extra Acceptance Control Toolkit file unexpectedly passed independent verification.'
Remove-Item -LiteralPath $extraPath -Force

$version = '0.0.0-sidecar-ci'
$fileName = "Monitor-$version-win-x64.zip"
$artifactPath = Join-Path $sourceRoot $fileName
$checksumPath = "$artifactPath.sha256"
'candidate=synthetic;purpose=sidecar-hash-contract' | Set-Content -LiteralPath (Join-Path $payloadRoot 'candidate.txt') -Encoding utf8NoBOM
Compress-Archive -Path (Join-Path $payloadRoot '*') -DestinationPath $artifactPath -CompressionLevel Optimal -Force
$productHash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$productHash  $fileName" | Set-Content -LiteralPath $checksumPath -Encoding ascii

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
    -ExpectedOperatorToolkitManifestSha256 $toolkitManifestHash `
    -HostName 'monitor.example.internal' `
    -SiteName 'Monitor' `
    -AppPoolName 'Monitor' `
    -AppPoolIdentity 'IIS AppPool\Monitor' `
    -CertificateThumbprint (('d' * 40) -join '') `
    -OperationalBackupId 'ci-backup-sidecar-001' `
    -PreviousPhysicalPath 'C:\Program Files\Monitor\releases\previous' `
    -StateRoot 'C:\ProgramData\Monitor\App_Data'

if ($session.OperatorToolingCommit -ne $toolingCommit -or
    $session.OperatorToolkitManifestSha256 -ne $toolkitManifestHash -or
    $session.OperatorToolingFileCount -ne $requiredTools.Count) {
    throw 'Initializer did not bind the exact acceptance-control toolkit commit/manifest/file count.'
}

$bindingPath = Join-Path $toolRoot 'Test-ProductionAcceptanceSessionBinding.ps1'
& $bindingPath `
    -EvidencePath $session.EvidencePath `
    -ExpectedSessionManifestSha256 $session.ManifestSha256 | Out-Null

$originalToolkitManifest = Get-Content -LiteralPath $manifestPath -Raw
Add-Content -LiteralPath $manifestPath -Value '# manifest drift after session creation' -Encoding utf8NoBOM
Assert-Rejected `
    -Action { & $bindingPath -EvidencePath $session.EvidencePath -ExpectedSessionManifestSha256 $session.ManifestSha256 } `
    -FailureMessage 'Tampered Acceptance Control Toolkit manifest unexpectedly passed locked-session binding.'
[IO.File]::WriteAllText($manifestPath, $originalToolkitManifest, [Text.UTF8Encoding]::new($false))

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

Write-Host 'Acceptance Control Toolkit provenance contract passed: exact clean commit export, independent manifest verification, manifest/file tamper negatives and locked-session binding all enforced.'
