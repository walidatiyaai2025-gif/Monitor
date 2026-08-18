[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$target = Join-Path $PSScriptRoot 'Invoke-Rc61DurablePromotion.ps1'
if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    throw "RC.61 promotion operator helper not found: $target"
}

$global:Rc61PromotionHelperMock = [ordered]@{
    Mode = 'happy-url'
    Commands = [System.Collections.Generic.List[string]]::new()
    DispatchCount = 0
    RunPollCount = 0
    ExistingRunId = 50
    PromotionRunId = 900
    Actor = 'mock-operator'
}

$repository = 'walidatiyaai2025-gif/Monitor'
$repositoryId = 1329517438
$version = '0.1.0-rc.61'
$sourceRunId = 31667721306
$sourceArtifactId = 9168574442
$outerDigest = 'sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382'
$productSha = 'd0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5'
$sourceCommit = 'e28158da67b36dfc5dbf8f4c38b5c43d99c7c728'
$testedMerge = '158148d8bfd05f724014541bc7a0b1eab5dae1b5'
$releaseTag = 'v0.1.0-rc.61'

function global:gh {
    $state = $global:Rc61PromotionHelperMock
    $command = ($args | ForEach-Object { [string]$_ }) -join ' '
    $state.Commands.Add($command)
    $global:LASTEXITCODE = 0

    if ($command -eq 'auth status') {
        'mock authenticated'
        return
    }

    if ($command -like 'repo view walidatiyaai2025-gif/Monitor*') {
        [ordered]@{
            nameWithOwner = 'walidatiyaai2025-gif/Monitor'
            defaultBranchRef = [ordered]@{ name = 'main' }
        } | ConvertTo-Json -Depth 8 -Compress
        return
    }

    if ($command -eq 'api repos/walidatiyaai2025-gif/Monitor/actions/runs/31667721306') {
        [ordered]@{
            id = 31667721306
            status = 'completed'
            conclusion = 'success'
            path = '.github/workflows/production-candidate.yml'
            head_sha = 'e28158da67b36dfc5dbf8f4c38b5c43d99c7c728'
            repository = [ordered]@{ id = 1329517438 }
            head_repository = [ordered]@{ id = 1329517438 }
        } | ConvertTo-Json -Depth 8 -Compress
        return
    }

    if ($command -eq 'api repos/walidatiyaai2025-gif/Monitor/actions/artifacts/9168574442') {
        [ordered]@{
            id = 9168574442
            name = 'Monitor-0.1.0-rc.61-win-x64'
            expired = $false
            digest = 'sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382'
            size_in_bytes = 4824061
            workflow_run = [ordered]@{
                id = 31667721306
                head_sha = 'e28158da67b36dfc5dbf8f4c38b5c43d99c7c728'
                repository_id = 1329517438
                head_repository_id = 1329517438
            }
        } | ConvertTo-Json -Depth 8 -Compress
        return
    }

    if ($command -eq 'api repos/walidatiyaai2025-gif/Monitor/releases/tags/v0.1.0-rc.61') {
        if ($state.Mode -eq 'existing-state') {
            [ordered]@{ id = 777; tag_name = 'v0.1.0-rc.61' } | ConvertTo-Json -Compress
            return
        }
        'HTTP 404: Not Found'
        $global:LASTEXITCODE = 1
        return
    }

    if ($command -eq 'api repos/walidatiyaai2025-gif/Monitor/git/ref/tags/v0.1.0-rc.61') {
        'HTTP 404: Not Found'
        $global:LASTEXITCODE = 1
        return
    }

    if ($command -eq 'api user') {
        [ordered]@{ login = $state.Actor } | ConvertTo-Json -Compress
        return
    }

    if ($command -eq 'api repos/walidatiyaai2025-gif/Monitor/actions/workflows/promote-existing-candidate.yml/runs?event=workflow_dispatch&branch=main&per_page=50') {
        $runs = @(
            [ordered]@{
                id = $state.ExistingRunId
                event = 'workflow_dispatch'
                head_branch = 'main'
                path = '.github/workflows/promote-existing-candidate.yml'
                created_at = '2026-08-18T02:00:00Z'
            }
        )
        if ($state.DispatchCount -gt 0 -and $state.Mode -in @('fallback', 'ambiguous')) {
            $runs += [ordered]@{
                id = 901
                event = 'workflow_dispatch'
                head_branch = 'main'
                path = '.github/workflows/promote-existing-candidate.yml'
                created_at = '2026-08-18T03:26:00Z'
            }
            if ($state.Mode -eq 'ambiguous') {
                $runs += [ordered]@{
                    id = 902
                    event = 'workflow_dispatch'
                    head_branch = 'main'
                    path = '.github/workflows/promote-existing-candidate.yml'
                    created_at = '2026-08-18T03:26:01Z'
                }
            }
        }
        [ordered]@{ workflow_runs = $runs } | ConvertTo-Json -Depth 8 -Compress
        return
    }

    if ($command -like 'workflow run promote-existing-candidate.yml --repo walidatiyaai2025-gif/Monitor --ref main *') {
        $state.DispatchCount++
        if ($state.Mode -in @('fallback', 'ambiguous')) {
            'Created workflow_dispatch event for promote-existing-candidate.yml'
        }
        else {
            "https://github.com/walidatiyaai2025-gif/Monitor/actions/runs/$($state.PromotionRunId)"
        }
        return
    }

    if ($command -match '^api repos/walidatiyaai2025-gif/Monitor/actions/runs/(?<id>900|901)$') {
        $runId = [long]$Matches['id']
        $state.RunPollCount++
        $status = 'completed'
        $conclusion = 'success'
        if ($state.Mode -eq 'happy-url' -and $state.RunPollCount -eq 1) {
            $status = 'in_progress'
            $conclusion = $null
        }
        if ($state.Mode -eq 'promotion-failure') {
            $status = 'completed'
            $conclusion = 'failure'
        }
        [ordered]@{
            id = $runId
            status = $status
            conclusion = $conclusion
            event = 'workflow_dispatch'
            head_branch = 'main'
            path = '.github/workflows/promote-existing-candidate.yml'
            head_sha = ('a' * 40)
            html_url = "https://github.com/walidatiyaai2025-gif/Monitor/actions/runs/$runId"
            actor = [ordered]@{ login = $state.Actor }
            repository = [ordered]@{ id = 1329517438 }
            head_repository = [ordered]@{ id = 1329517438 }
        } | ConvertTo-Json -Depth 8 -Compress
        return
    }

    $global:LASTEXITCODE = 1
    "unexpected mock gh command: $command"
}

function Reset-Mock {
    param([Parameter(Mandatory = $true)][string]$Mode)

    $state = $global:Rc61PromotionHelperMock
    $state.Mode = $Mode
    $state.Commands.Clear()
    $state.DispatchCount = 0
    $state.RunPollCount = 0
    $state.PromotionRunId = 900
}

function Assert-NoVerificationDispatch {
    $state = $global:Rc61PromotionHelperMock
    foreach ($command in $state.Commands) {
        if ($command -match '^workflow run verify-durable-release\.yml') {
            throw "Operator helper automatically dispatched the independent verifier: $command"
        }
        if ($command -match '(?i)^release create|--method\s+(POST|PATCH|PUT|DELETE)|^git\s+(tag|push)') {
            throw "Operator helper used a forbidden direct mutation command: $command"
        }
    }
}

function Assert-FailsClosed {
    param(
        [Parameter(Mandatory = $true)][string]$Mode,
        [Parameter(Mandatory = $true)][string]$Pattern
    )

    Reset-Mock -Mode $Mode
    $failed = $false
    try {
        & $target `
            -AcknowledgePromotion `
            -RunDiscoveryAttempts 2 `
            -RunDiscoveryPollSeconds 0 `
            -RunCompletionAttempts 3 `
            -RunCompletionPollSeconds 0 | Out-Null
    }
    catch {
        $failed = $true
        if ($_.Exception.Message -notmatch $Pattern) {
            throw "Mode '$Mode' failed for the wrong reason: $($_.Exception.Message)"
        }
    }
    if (-not $failed) {
        throw "Mode '$Mode' unexpectedly passed the RC.61 promotion operator helper."
    }
    Assert-NoVerificationDispatch
}

try {
    Reset-Mock -Mode 'happy-url'
    $preview = & $target
    if ($preview.Status -cne 'READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT') { throw 'Preview status drifted.' }
    if ($preview.WorkflowDispatchPerformed) { throw 'Preview path dispatched a workflow without acknowledgement.' }
    if ($global:Rc61PromotionHelperMock.DispatchCount -ne 0) { throw 'Preview path issued a promotion dispatch.' }
    Assert-NoVerificationDispatch

    Reset-Mock -Mode 'happy-url'
    $result = & $target `
        -AcknowledgePromotion `
        -RunDiscoveryAttempts 2 `
        -RunDiscoveryPollSeconds 0 `
        -RunCompletionAttempts 3 `
        -RunCompletionPollSeconds 0
    if ($result.Status -cne 'PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED') { throw 'Happy-path promotion status drifted.' }
    if ($result.PromotionRunId -ne 900) { throw 'Happy path lost exact promotion run ID.' }
    if ($global:Rc61PromotionHelperMock.DispatchCount -ne 1) { throw 'Happy path did not dispatch exactly once.' }
    if ($result.IndependentVerificationDispatched) { throw 'Happy path claimed independent verification was dispatched.' }
    if ($result.IndependentVerificationCommand -notmatch '^gh workflow run verify-durable-release\.yml ') { throw 'Happy path lost separate verifier command.' }
    if ($result.PostVerificationReadinessCommand -notmatch 'PromotionRunId 900.+VerificationRunId <VERIFICATION_RUN_ID>') { throw 'Happy path lost post-verification readiness handoff.' }
    Assert-NoVerificationDispatch

    Reset-Mock -Mode 'fallback'
    $fallback = & $target `
        -AcknowledgePromotion `
        -RunDiscoveryAttempts 2 `
        -RunDiscoveryPollSeconds 0 `
        -RunCompletionAttempts 2 `
        -RunCompletionPollSeconds 0
    if ($fallback.PromotionRunId -ne 901) { throw 'Fallback run discovery did not capture the unique new run.' }
    if ($global:Rc61PromotionHelperMock.DispatchCount -ne 1) { throw 'Fallback path dispatched more than once.' }
    Assert-NoVerificationDispatch

    Assert-FailsClosed -Mode 'ambiguous' -Pattern 'Do not redispatch'
    if ($global:Rc61PromotionHelperMock.DispatchCount -ne 1) { throw 'Ambiguous discovery path dispatched more than once.' }

    Assert-FailsClosed -Mode 'promotion-failure' -Pattern 'Do not redispatch'
    if ($global:Rc61PromotionHelperMock.DispatchCount -ne 1) { throw 'Failed promotion path dispatched more than once.' }

    Reset-Mock -Mode 'existing-state'
    $failedExisting = $false
    try {
        & $target -AcknowledgePromotion | Out-Null
    }
    catch {
        $failedExisting = $true
        if ($_.Exception.Message -notmatch 'preflight is not ready|durable state already exists') {
            throw "Existing-state preflight failed for the wrong reason: $($_.Exception.Message)"
        }
    }
    if (-not $failedExisting) { throw 'Existing durable state unexpectedly allowed promotion dispatch.' }
    if ($global:Rc61PromotionHelperMock.DispatchCount -ne 0) { throw 'Existing durable state issued a promotion dispatch.' }
    Assert-NoVerificationDispatch

    [pscustomobject]@{
        Status = 'PASS'
        PreviewNoMutation = $true
        HappyPath = $true
        UrlFallback = $true
        FailClosedCases = 3
        IndependentVerifierAutoDispatches = 0
    }
}
finally {
    Remove-Item Function:\gh -ErrorAction SilentlyContinue
    Remove-Variable Rc61PromotionHelperMock -Scope Global -ErrorAction SilentlyContinue
}
