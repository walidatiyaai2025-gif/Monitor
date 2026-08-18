[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [long]::MaxValue)]
    [long]$PromotionRunId,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [long]::MaxValue)]
    [long]$VerificationRunId,

    [string]$Repository = 'walidatiyaai2025-gif/Monitor'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedRepository = 'walidatiyaai2025-gif/Monitor'
$expectedRepositoryId = '1329517438'
$version = '0.1.0-rc.61'
$releaseTag = 'v0.1.0-rc.61'
$testedMergeCommit = '158148d8bfd05f724014541bc7a0b1eab5dae1b5'
$productSha256 = 'd0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5'
$promotionWorkflowPath = '.github/workflows/promote-existing-candidate.yml'
$verificationWorkflowPath = '.github/workflows/verify-durable-release.yml'
$zipName = "Monitor-$version-win-x64.zip"
$checksumName = "$zipName.sha256"
$operatorToolingCommit = 'b422eaaee53d931a62a43b3c36a53b68cd4f3e27'
$operatorToolingFiles = @(
    'scripts/Export-ProductionAcceptanceToolkit.ps1',
    'scripts/Test-ProductionAcceptanceToolkit.ps1',
    'scripts/New-ProductionAcceptanceSession.ps1',
    'scripts/New-ProductionAcceptanceEvidencePack.ps1',
    'scripts/Test-ProductionAcceptanceSessionBinding.ps1',
    'scripts/Set-ProductionAcceptanceGate.ps1',
    'scripts/Complete-ProductionAcceptance.ps1',
    'scripts/Test-ProductionAcceptanceEvidence.ps1'
)

if ($Repository -cne $expectedRepository) {
    throw "RC.61 cutover readiness is pinned to repository '$expectedRepository'; got '$Repository'."
}
if ($PromotionRunId -eq $VerificationRunId) {
    throw 'PromotionRunId and VerificationRunId must identify two separate workflow runs.'
}
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) is required for the read-only RC.61 cutover readiness gate.'
}

function Invoke-Gh {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $output = & gh @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "gh $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }
    return ($output -join [Environment]::NewLine)
}

function Invoke-GhJson {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $text = Invoke-Gh -Arguments $Arguments
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "gh $($Arguments -join ' ') returned no JSON."
    }
    return $text | ConvertFrom-Json
}

function Assert-WorkflowRun {
    param(
        [Parameter(Mandatory = $true)]$Run,
        [Parameter(Mandatory = $true)][long]$ExpectedRunId,
        [Parameter(Mandatory = $true)][string]$ExpectedPath,
        [Parameter(Mandatory = $true)][string]$Role
    )

    if ([string]$Run.id -cne [string]$ExpectedRunId) { throw "$Role workflow run ID drifted." }
    if ([string]$Run.status -cne 'completed' -or [string]$Run.conclusion -cne 'success') { throw "$Role workflow run is not completed/success." }
    if ([string]$Run.event -cne 'workflow_dispatch') { throw "$Role workflow run was not started by workflow_dispatch." }
    if ([string]$Run.head_branch -cne 'main') { throw "$Role workflow run was not dispatched from main." }
    if ([string]$Run.path -cne $ExpectedPath) { throw "$Role workflow path does not match the locked contract." }
    if ([string]$Run.repository.id -cne $expectedRepositoryId -or [string]$Run.head_repository.id -cne $expectedRepositoryId) {
        throw "$Role workflow repository provenance drifted."
    }
    if ([string]$Run.head_sha -notmatch '^[a-f0-9]{40}$') { throw "$Role workflow head SHA is not canonical." }
    if ([string]$Run.html_url -notmatch '^https://github\.com/walidatiyaai2025-gif/Monitor/actions/runs/[1-9][0-9]*$') {
        throw "$Role workflow URL does not match the locked repository."
    }
}

Invoke-Gh -Arguments @('auth', 'status') | Out-Null
$repo = Invoke-GhJson -Arguments @('repo', 'view', $Repository, '--json', 'nameWithOwner,defaultBranchRef')
if ([string]$repo.nameWithOwner -cne $expectedRepository -or [string]$repo.defaultBranchRef.name -cne 'main') {
    throw 'Repository identity/default branch does not match the locked RC.61 cutover contract.'
}

$promotionRun = Invoke-GhJson -Arguments @('api', "repos/$Repository/actions/runs/$PromotionRunId")
$verificationRun = Invoke-GhJson -Arguments @('api', "repos/$Repository/actions/runs/$VerificationRunId")
Assert-WorkflowRun -Run $promotionRun -ExpectedRunId $PromotionRunId -ExpectedPath $promotionWorkflowPath -Role 'Promotion'
Assert-WorkflowRun -Run $verificationRun -ExpectedRunId $VerificationRunId -ExpectedPath $verificationWorkflowPath -Role 'Independent verification'

$promotionCompletedAt = [DateTimeOffset]::Parse([string]$promotionRun.updated_at, [Globalization.CultureInfo]::InvariantCulture)
$verificationCreatedAt = [DateTimeOffset]::Parse([string]$verificationRun.created_at, [Globalization.CultureInfo]::InvariantCulture)
if ($verificationCreatedAt -lt $promotionCompletedAt) {
    throw 'Independent verification run was created before the promotion run completed; the required sequence is invalid.'
}

$tagRef = Invoke-GhJson -Arguments @('api', "repos/$Repository/git/ref/tags/$releaseTag")
if ([string]$tagRef.ref -cne "refs/tags/$releaseTag") { throw 'Durable tag ref name drifted.' }
if ([string]$tagRef.object.type -notin @('commit', 'tag')) { throw 'Durable tag ref object type is invalid.' }
if ([string]$tagRef.object.sha -notmatch '^[a-f0-9]{40}$') { throw 'Durable tag ref object SHA is not canonical.' }

$resolvedTag = Invoke-GhJson -Arguments @('api', "repos/$Repository/commits/$releaseTag")
if ([string]$resolvedTag.sha -cne $testedMergeCommit) {
    throw 'Durable RC.61 tag does not resolve to the approved tested merge commit.'
}

$release = Invoke-GhJson -Arguments @('api', "repos/$Repository/releases/tags/$releaseTag")
if ([long]$release.id -le 0) { throw 'Durable release ID is invalid.' }
if ([string]$release.tag_name -cne $releaseTag) { throw 'Durable release tag metadata drifted.' }
if ([string]$release.name -cne "Monitor $version") { throw 'Durable release title drifted.' }
if ([bool]$release.draft) { throw 'Durable release must not be a draft.' }
if (-not [bool]$release.prerelease) { throw 'RC.61 durable release must be marked prerelease.' }

$assets = @($release.assets)
if ($assets.Count -ne 2) { throw 'Durable release must contain exactly two assets.' }
$assetNames = @($assets | ForEach-Object { [string]$_.name } | Sort-Object)
$expectedNames = @($checksumName, $zipName | Sort-Object)
if (($assetNames -join "`n") -cne ($expectedNames -join "`n")) {
    throw 'Durable release asset names do not match the exact RC.61 ZIP/checksum contract.'
}

$zipAsset = @($assets | Where-Object { [string]$_.name -ceq $zipName })
$checksumAsset = @($assets | Where-Object { [string]$_.name -ceq $checksumName })
if ($zipAsset.Count -ne 1 -or $checksumAsset.Count -ne 1) { throw 'Expected durable assets are not uniquely represented.' }
$zipAsset = $zipAsset[0]
$checksumAsset = $checksumAsset[0]

foreach ($asset in @($zipAsset, $checksumAsset)) {
    if ([string]$asset.state -cne 'uploaded') { throw "Durable asset '$($asset.name)' is not fully uploaded." }
    if ([long]$asset.id -le 0 -or [long]$asset.size -le 0) { throw "Durable asset '$($asset.name)' has invalid ID or size." }
    if ([string]$asset.digest -notmatch '^sha256:[a-f0-9]{64}$') { throw "Durable asset '$($asset.name)' has a non-canonical API digest." }
    $expectedUrl = "https://github.com/$Repository/releases/download/$releaseTag/$($asset.name)"
    if ([string]$asset.browser_download_url -cne $expectedUrl) { throw "Durable asset '$($asset.name)' download URL drifted." }
}
if ([long]$zipAsset.id -eq [long]$checksumAsset.id) { throw 'Durable ZIP and checksum assets must have distinct IDs.' }
if ([string]$zipAsset.digest -cne "sha256:$productSha256") {
    throw 'Durable ZIP API digest does not match the approved RC.61 product SHA-256.'
}

$toolingCommit = Invoke-GhJson -Arguments @('api', "repos/$Repository/commits/$operatorToolingCommit")
if ([string]$toolingCommit.sha -cne $operatorToolingCommit) { throw 'Acceptance Control Toolkit source commit is unavailable or drifted.' }
foreach ($path in $operatorToolingFiles) {
    $encodedPath = [Uri]::EscapeDataString($path).Replace('%2F', '/')
    $file = Invoke-GhJson -Arguments @('api', "repos/$Repository/contents/$encodedPath`?ref=$operatorToolingCommit")
    if ([string]$file.type -cne 'file' -or [string]::IsNullOrWhiteSpace([string]$file.sha)) {
        throw "Acceptance Control Toolkit source file '$path' is unavailable at the locked commit."
    }
}

[pscustomobject]@{
    Status = 'READY_FOR_P0_5_PRE_CUTOVER_PREPARATION'
    Repository = $Repository
    Version = $version
    ReleaseTag = $releaseTag
    TestedMergeCommit = $testedMergeCommit
    ProductSha256 = $productSha256
    PromotionRunId = $PromotionRunId
    PromotionRunUrl = [string]$promotionRun.html_url
    VerificationRunId = $VerificationRunId
    VerificationRunUrl = [string]$verificationRun.html_url
    ReleaseId = [long]$release.id
    ZipAssetId = [long]$zipAsset.id
    ZipAssetDigest = [string]$zipAsset.digest
    ChecksumAssetId = [long]$checksumAsset.id
    ChecksumAssetDigest = [string]$checksumAsset.digest
    OperatorToolingCommit = $operatorToolingCommit
    DurableReleasePrerequisiteSatisfied = $true
    ExternalGatesPassed = 0
    ProductionMutationPerformed = $false
    MutatedGitHubState = $false
}
