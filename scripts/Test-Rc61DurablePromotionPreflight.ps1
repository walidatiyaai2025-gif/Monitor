[CmdletBinding()]
param(
    [string]$Repository = 'walidatiyaai2025-gif/Monitor'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedRepository = 'walidatiyaai2025-gif/Monitor'
$expectedRepositoryId = '1329517438'
$version = '0.1.0-rc.61'
$sourceRunId = '31667721306'
$sourceArtifactId = '9168574442'
$outerDigest = 'sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382'
$productSha256 = 'd0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5'
$sourceCommit = 'e28158da67b36dfc5dbf8f4c38b5c43d99c7c728'
$testedMergeCommit = '158148d8bfd05f724014541bc7a0b1eab5dae1b5'
$releaseTag = 'v0.1.0-rc.61'

if ($Repository -cne $expectedRepository) {
    throw "RC.61 promotion is pinned to repository '$expectedRepository'; got '$Repository'."
}
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) is required for the read-only RC.61 promotion preflight.'
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
    if ([string]::IsNullOrWhiteSpace($text)) { throw "gh $($Arguments -join ' ') returned no JSON." }
    return $text | ConvertFrom-Json
}

function Test-GitHubResourceExists {
    param([Parameter(Mandatory = $true)][string]$ApiPath)

    $output = & gh api $ApiPath 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0) { return $true }

    $message = $output -join [Environment]::NewLine
    if ($message -match '(?i)(HTTP\s+404|Not Found)') { return $false }

    throw "GitHub resource probe failed for '$ApiPath'; refusing to treat the error as absence: $message"
}

Invoke-Gh -Arguments @('auth', 'status') | Out-Null
$repo = Invoke-GhJson -Arguments @('repo', 'view', $Repository, '--json', 'nameWithOwner,defaultBranchRef')
if ([string]$repo.nameWithOwner -cne $expectedRepository -or [string]$repo.defaultBranchRef.name -cne 'main') {
    throw 'Repository identity/default branch does not match the locked RC.61 promotion contract.'
}

$sourceRun = Invoke-GhJson -Arguments @('api', "repos/$Repository/actions/runs/$sourceRunId")
if ([string]$sourceRun.status -cne 'completed' -or [string]$sourceRun.conclusion -cne 'success') { throw 'Selected source run is not successful.' }
if ([string]$sourceRun.path -cne '.github/workflows/production-candidate.yml') { throw 'Selected source run is not production-candidate.yml.' }
if ([string]$sourceRun.head_sha -cne $sourceCommit) { throw 'Selected source run head SHA drifted.' }
if ([string]$sourceRun.repository.id -cne $expectedRepositoryId -or [string]$sourceRun.head_repository.id -cne $expectedRepositoryId) { throw 'Selected source run repository identity drifted.' }

$artifact = Invoke-GhJson -Arguments @('api', "repos/$Repository/actions/artifacts/$sourceArtifactId")
if ([string]$artifact.id -cne $sourceArtifactId) { throw 'Selected artifact ID drifted.' }
if ([string]$artifact.name -cne "Monitor-$version-win-x64") { throw 'Selected artifact name drifted.' }
if ([bool]$artifact.expired) { throw 'Selected RC.61 Actions artifact is expired.' }
if ([string]$artifact.digest -cne $outerDigest) { throw 'Selected artifact outer digest drifted.' }
if ([string]$artifact.workflow_run.id -cne $sourceRunId -or [string]$artifact.workflow_run.head_sha -cne $sourceCommit) { throw 'Selected artifact source provenance drifted.' }
if ([string]$artifact.workflow_run.repository_id -cne $expectedRepositoryId -or [string]$artifact.workflow_run.head_repository_id -cne $expectedRepositoryId) { throw 'Selected artifact repository provenance drifted.' }
if ([long]$artifact.size_in_bytes -le 0) { throw 'Selected artifact has invalid size.' }

$releaseExists = Test-GitHubResourceExists -ApiPath "repos/$Repository/releases/tags/$releaseTag"
$tagExists = Test-GitHubResourceExists -ApiPath "repos/$Repository/git/ref/tags/$releaseTag"

$promotionCommand = @(
    'gh workflow run promote-existing-candidate.yml',
    "--repo $Repository",
    '--ref main',
    "-f candidate_version=$version",
    "-f source_run_id=$sourceRunId",
    "-f source_artifact_id=$sourceArtifactId",
    "-f expected_outer_artifact_digest=$outerDigest",
    "-f expected_product_sha256=$productSha256",
    "-f source_commit=$sourceCommit",
    "-f tested_merge_commit=$testedMergeCommit",
    "-f release_tag=$releaseTag",
    '-f acknowledge_promotion=true'
) -join ' '

$verificationCommand = @(
    'gh workflow run verify-durable-release.yml',
    "--repo $Repository",
    '--ref main',
    "-f release_version=$version",
    "-f release_tag=$releaseTag",
    "-f expected_commit=$testedMergeCommit",
    "-f expected_product_sha256=$productSha256"
) -join ' '

$state = if ($releaseExists -or $tagExists) {
    'DURABLE_STATE_EXISTS_VERIFY_OR_INVESTIGATE'
}
else {
    'READY_FOR_EXPLICIT_MANUAL_PROMOTION'
}

[pscustomobject]@{
    Status = $state
    Repository = $Repository
    Version = $version
    SourceRunId = $sourceRunId
    SourceArtifactId = $sourceArtifactId
    ArtifactExpired = [bool]$artifact.expired
    OuterArtifactDigest = $outerDigest
    ProductSha256 = $productSha256
    SourceCommit = $sourceCommit
    TestedMergeCommit = $testedMergeCommit
    ReleaseTag = $releaseTag
    TagExists = $tagExists
    ReleaseExists = $releaseExists
    PromotionCommand = $promotionCommand
    IndependentVerificationCommand = $verificationCommand
    MutatedGitHubState = $false
}
