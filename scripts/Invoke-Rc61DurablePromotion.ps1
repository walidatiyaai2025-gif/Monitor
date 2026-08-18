[CmdletBinding()]
param(
    [switch]$AcknowledgePromotion,

    [string]$Repository = 'walidatiyaai2025-gif/Monitor',

    [ValidateRange(1, 120)]
    [int]$RunDiscoveryAttempts = 20,

    [ValidateRange(0, 30)]
    [int]$RunDiscoveryPollSeconds = 2,

    [ValidateRange(1, 600)]
    [int]$RunCompletionAttempts = 120,

    [ValidateRange(0, 60)]
    [int]$RunCompletionPollSeconds = 5
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedRepository = 'walidatiyaai2025-gif/Monitor'
$expectedRepositoryId = '1329517438'
$promotionWorkflow = 'promote-existing-candidate.yml'
$promotionWorkflowPath = '.github/workflows/promote-existing-candidate.yml'
$version = '0.1.0-rc.61'
$sourceRunId = '31667721306'
$sourceArtifactId = '9168574442'
$outerArtifactDigest = 'sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382'
$productSha256 = 'd0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5'
$sourceCommit = 'e28158da67b36dfc5dbf8f4c38b5c43d99c7c728'
$testedMergeCommit = '158148d8bfd05f724014541bc7a0b1eab5dae1b5'
$releaseTag = 'v0.1.0-rc.61'

if ($Repository -cne $expectedRepository) {
    throw "RC.61 promotion is pinned to repository '$expectedRepository'; got '$Repository'."
}

$preflightScript = Join-Path $PSScriptRoot 'Test-Rc61DurablePromotionPreflight.ps1'
if (-not (Test-Path -LiteralPath $preflightScript -PathType Leaf)) {
    throw "Required read-only RC.61 preflight script was not found: $preflightScript"
}
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) is required for the explicit RC.61 promotion operator helper.'
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

function Assert-LockedPreflight {
    param([Parameter(Mandatory = $true)]$Preflight)

    if ([string]$Preflight.Status -cne 'READY_FOR_EXPLICIT_MANUAL_PROMOTION') {
        throw "RC.61 preflight is not ready for promotion: $($Preflight.Status)"
    }
    if ([bool]$Preflight.MutatedGitHubState) { throw 'Read-only RC.61 preflight unexpectedly reported GitHub mutation.' }
    if ([bool]$Preflight.TagExists -or [bool]$Preflight.ReleaseExists) {
        throw 'RC.61 durable state already exists; refuse promotion dispatch and investigate/verify instead.'
    }
    if ([string]$Preflight.Repository -cne $expectedRepository) { throw 'RC.61 preflight repository drifted.' }
    if ([string]$Preflight.Version -cne $version) { throw 'RC.61 preflight version drifted.' }
    if ([string]$Preflight.SourceRunId -cne $sourceRunId) { throw 'RC.61 preflight source run drifted.' }
    if ([string]$Preflight.SourceArtifactId -cne $sourceArtifactId) { throw 'RC.61 preflight source artifact drifted.' }
    if ([string]$Preflight.OuterArtifactDigest -cne $outerArtifactDigest) { throw 'RC.61 preflight outer artifact digest drifted.' }
    if ([string]$Preflight.ProductSha256 -cne $productSha256) { throw 'RC.61 preflight product SHA-256 drifted.' }
    if ([string]$Preflight.SourceCommit -cne $sourceCommit) { throw 'RC.61 preflight source commit drifted.' }
    if ([string]$Preflight.TestedMergeCommit -cne $testedMergeCommit) { throw 'RC.61 preflight tested merge commit drifted.' }
    if ([string]$Preflight.ReleaseTag -cne $releaseTag) { throw 'RC.61 preflight release tag drifted.' }
    if ([bool]$Preflight.ArtifactExpired) { throw 'RC.61 source artifact expired after preflight validation.' }
}

function Get-PromotionRunSnapshot {
    $snapshot = Invoke-GhJson -Arguments @(
        'api',
        "repos/$Repository/actions/workflows/$promotionWorkflow/runs?event=workflow_dispatch&branch=main&per_page=50"
    )
    return @($snapshot.workflow_runs)
}

function Assert-PromotionRunIdentity {
    param(
        [Parameter(Mandatory = $true)]$Run,
        [Parameter(Mandatory = $true)][long]$ExpectedRunId
    )

    if ([string]$Run.id -cne [string]$ExpectedRunId) { throw 'Captured promotion workflow run ID drifted.' }
    if ([string]$Run.event -cne 'workflow_dispatch') { throw 'Captured promotion run was not started by workflow_dispatch.' }
    if ([string]$Run.head_branch -cne 'main') { throw 'Captured promotion run was not dispatched from main.' }
    if ([string]$Run.path -cne $promotionWorkflowPath) { throw 'Captured promotion workflow path drifted.' }
    if ([string]$Run.repository.id -cne $expectedRepositoryId -or [string]$Run.head_repository.id -cne $expectedRepositoryId) {
        throw 'Captured promotion run repository provenance drifted.'
    }
    if ([string]$Run.head_sha -notmatch '^[a-f0-9]{40}$') { throw 'Captured promotion run head SHA is not canonical.' }
    $expectedUrl = "https://github.com/$Repository/actions/runs/$ExpectedRunId"
    if ([string]$Run.html_url -cne $expectedUrl) { throw 'Captured promotion run URL drifted.' }
}

function Resolve-NewPromotionRunId {
    param(
        [Parameter(Mandatory = $true)][string[]]$BeforeIds,
        [Parameter(Mandatory = $true)][DateTimeOffset]$DispatchStartedAt
    )

    for ($attempt = 1; $attempt -le $RunDiscoveryAttempts; $attempt++) {
        $runs = Get-PromotionRunSnapshot
        $newRuns = @(
            $runs | Where-Object {
                $id = [string]$_.id
                $createdAt = [DateTimeOffset]::Parse([string]$_.created_at, [Globalization.CultureInfo]::InvariantCulture)
                $BeforeIds -cnotcontains $id -and
                    [string]$_.event -ceq 'workflow_dispatch' -and
                    [string]$_.head_branch -ceq 'main' -and
                    [string]$_.path -ceq $promotionWorkflowPath -and
                    $createdAt -ge $DispatchStartedAt.AddMinutes(-1)
            }
        )

        if ($newRuns.Count -eq 1) {
            return [long]$newRuns[0].id
        }
        if ($newRuns.Count -gt 1) {
            $ids = ($newRuns | ForEach-Object { [string]$_.id } | Sort-Object) -join ', '
            throw "Promotion dispatch succeeded but run discovery is ambiguous ($ids). Do not redispatch; inspect these exact runs."
        }

        if ($attempt -lt $RunDiscoveryAttempts -and $RunDiscoveryPollSeconds -gt 0) {
            Start-Sleep -Seconds $RunDiscoveryPollSeconds
        }
    }

    throw 'Promotion dispatch succeeded but its exact workflow run could not be discovered. Do not redispatch; inspect recent promote-existing-candidate workflow_dispatch runs.'
}

$preflight = & $preflightScript -Repository $Repository
Assert-LockedPreflight -Preflight $preflight

if (-not $AcknowledgePromotion) {
    [pscustomobject]@{
        Status = 'READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT'
        Repository = $Repository
        Version = $version
        ReleaseTag = $releaseTag
        ProductSha256 = $productSha256
        PromotionWorkflow = $promotionWorkflow
        AcknowledgementRequired = $true
        PromotionCommand = '.\scripts\Invoke-Rc61DurablePromotion.ps1 -AcknowledgePromotion'
        WorkflowDispatchPerformed = $false
        IndependentVerificationDispatched = $false
        ProductionMutationPerformed = $false
        MutatedGitHubState = $false
    }
    return
}

Invoke-Gh -Arguments @('auth', 'status') | Out-Null
$actor = Invoke-GhJson -Arguments @('api', 'user')
$actorLogin = [string]$actor.login
if ([string]::IsNullOrWhiteSpace($actorLogin)) { throw 'Authenticated GitHub actor identity is unavailable.' }

$beforeRuns = Get-PromotionRunSnapshot
$beforeIds = @($beforeRuns | ForEach-Object { [string]$_.id })
$dispatchStartedAt = [DateTimeOffset]::UtcNow

$dispatchArguments = @(
    'workflow', 'run', $promotionWorkflow,
    '--repo', $Repository,
    '--ref', 'main',
    '-f', "candidate_version=$version",
    '-f', "source_run_id=$sourceRunId",
    '-f', "source_artifact_id=$sourceArtifactId",
    '-f', "expected_outer_artifact_digest=$outerArtifactDigest",
    '-f', "expected_product_sha256=$productSha256",
    '-f', "source_commit=$sourceCommit",
    '-f', "tested_merge_commit=$testedMergeCommit",
    '-f', "release_tag=$releaseTag",
    '-f', 'acknowledge_promotion=true'
)

$dispatchOutput = Invoke-Gh -Arguments $dispatchArguments
$escapedRepository = [regex]::Escape($Repository)
$urlMatch = [regex]::Match(
    $dispatchOutput,
    "https://github\.com/$escapedRepository/actions/runs/(?<id>[1-9][0-9]*)"
)

if ($urlMatch.Success) {
    $promotionRunId = [long]$urlMatch.Groups['id'].Value
}
else {
    $promotionRunId = Resolve-NewPromotionRunId -BeforeIds $beforeIds -DispatchStartedAt $dispatchStartedAt
}

$promotionRun = Invoke-GhJson -Arguments @('api', "repos/$Repository/actions/runs/$promotionRunId")
Assert-PromotionRunIdentity -Run $promotionRun -ExpectedRunId $promotionRunId
if ([string]$promotionRun.actor.login -cne $actorLogin) {
    throw "Captured promotion run actor '$($promotionRun.actor.login)' does not match authenticated operator '$actorLogin'. Do not redispatch; inspect run $promotionRunId."
}

$promotionRunUrl = "https://github.com/$Repository/actions/runs/$promotionRunId"
$lastStatus = [string]$promotionRun.status
$lastConclusion = [string]$promotionRun.conclusion

for ($attempt = 1; $attempt -le $RunCompletionAttempts; $attempt++) {
    if ($lastStatus -ceq 'completed') { break }

    if ($attempt -lt $RunCompletionAttempts -and $RunCompletionPollSeconds -gt 0) {
        Start-Sleep -Seconds $RunCompletionPollSeconds
    }

    $promotionRun = Invoke-GhJson -Arguments @('api', "repos/$Repository/actions/runs/$promotionRunId")
    Assert-PromotionRunIdentity -Run $promotionRun -ExpectedRunId $promotionRunId
    if ([string]$promotionRun.actor.login -cne $actorLogin) {
        throw "Promotion run actor drifted while monitoring exact run $promotionRunId."
    }
    $lastStatus = [string]$promotionRun.status
    $lastConclusion = [string]$promotionRun.conclusion
}

if ($lastStatus -cne 'completed') {
    [pscustomobject]@{
        Status = 'PROMOTION_DISPATCHED_CHECK_EXACT_RUN'
        Repository = $Repository
        Version = $version
        ReleaseTag = $releaseTag
        PromotionRunId = $promotionRunId
        PromotionRunUrl = $promotionRunUrl
        PromotionRunStatus = $lastStatus
        PromotionRunConclusion = $lastConclusion
        CheckExactRunCommand = "gh run view $promotionRunId --repo $Repository"
        RedispatchAllowed = $false
        WorkflowDispatchPerformed = $true
        IndependentVerificationDispatched = $false
        ProductionMutationPerformed = $false
    }
    return
}

if ($lastConclusion -cne 'success') {
    throw "Exact promotion run $promotionRunId completed with conclusion '$lastConclusion'. Do not redispatch; inspect $promotionRunUrl."
}

$independentVerificationCommand = [string]$preflight.IndependentVerificationCommand
if ([string]::IsNullOrWhiteSpace($independentVerificationCommand) -or
    $independentVerificationCommand -notmatch '^gh workflow run verify-durable-release\.yml ') {
    throw 'Independent verification command from the locked preflight is missing or drifted.'
}

[pscustomobject]@{
    Status = 'PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED'
    Repository = $Repository
    Version = $version
    ReleaseTag = $releaseTag
    ProductSha256 = $productSha256
    PromotionRunId = $promotionRunId
    PromotionRunUrl = $promotionRunUrl
    PromotionRunStatus = $lastStatus
    PromotionRunConclusion = $lastConclusion
    IndependentVerificationCommand = $independentVerificationCommand
    PostVerificationReadinessCommand = ".\scripts\Test-Rc61CutoverReadiness.ps1 -PromotionRunId $promotionRunId -VerificationRunId <VERIFICATION_RUN_ID>"
    WorkflowDispatchPerformed = $true
    IndependentVerificationDispatched = $false
    ProductionMutationPerformed = $false
    DirectTagOrReleaseMutationPerformedByHelper = $false
}
