[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$target = Join-Path $PSScriptRoot 'Test-Rc61CutoverReadiness.ps1'
if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    throw "RC.61 cutover readiness target not found: $target"
}

$script:MockMode = 'success'
$script:ObservedCommands = [System.Collections.Generic.List[string]]::new()
$testedMerge = '158148d8bfd05f724014541bc7a0b1eab5dae1b5'
$productSha = 'd0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5'
$toolingCommit = 'b422eaaee53d931a62a43b3c36a53b68cd4f3e27'
$zipName = 'Monitor-0.1.0-rc.61-win-x64.zip'
$checksumName = "$zipName.sha256"
$releaseTag = 'v0.1.0-rc.61'
$repository = 'walidatiyaai2025-gif/Monitor'

function ConvertTo-MockJson {
    param([Parameter(Mandatory = $true)]$Value)
    return ($Value | ConvertTo-Json -Depth 12 -Compress)
}

function global:gh {
    $command = ($args | ForEach-Object { [string]$_ }) -join ' '
    $script:ObservedCommands.Add($command)
    $global:LASTEXITCODE = 0

    if ($command -eq 'auth status') {
        'mock authenticated'
        return
    }
    if ($command -like 'repo view walidatiyaai2025-gif/Monitor*') {
        ConvertTo-MockJson ([ordered]@{
            nameWithOwner = $repository
            defaultBranchRef = [ordered]@{ name = 'main' }
        })
        return
    }
    if ($command -eq 'api repos/walidatiyaai2025-gif/Monitor/actions/runs/100') {
        ConvertTo-MockJson ([ordered]@{
            id = 100
            status = 'completed'
            conclusion = 'success'
            event = 'workflow_dispatch'
            head_branch = 'main'
            path = '.github/workflows/promote-existing-candidate.yml'
            head_sha = ('a' * 40)
            html_url = 'https://github.com/walidatiyaai2025-gif/Monitor/actions/runs/100'
            created_at = '2026-08-18T02:58:00Z'
            updated_at = '2026-08-18T03:00:00Z'
            repository = [ordered]@{ id = 1329517438 }
            head_repository = [ordered]@{ id = 1329517438 }
        })
        return
    }
    if ($command -eq 'api repos/walidatiyaai2025-gif/Monitor/actions/runs/200') {
        $created = if ($script:MockMode -eq 'bad-order') { '2026-08-18T02:59:00Z' } else { '2026-08-18T03:01:00Z' }
        ConvertTo-MockJson ([ordered]@{
            id = 200
            status = 'completed'
            conclusion = 'success'
            event = 'workflow_dispatch'
            head_branch = 'main'
            path = '.github/workflows/verify-durable-release.yml'
            head_sha = ('b' * 40)
            html_url = 'https://github.com/walidatiyaai2025-gif/Monitor/actions/runs/200'
            created_at = $created
            updated_at = '2026-08-18T03:03:00Z'
            repository = [ordered]@{ id = 1329517438 }
            head_repository = [ordered]@{ id = 1329517438 }
        })
        return
    }
    if ($command -eq 'api repos/walidatiyaai2025-gif/Monitor/git/ref/tags/v0.1.0-rc.61') {
        ConvertTo-MockJson ([ordered]@{
            ref = 'refs/tags/v0.1.0-rc.61'
            object = [ordered]@{ type = 'commit'; sha = $testedMerge }
        })
        return
    }
    if ($command -eq 'api repos/walidatiyaai2025-gif/Monitor/commits/v0.1.0-rc.61') {
        $sha = if ($script:MockMode -eq 'wrong-tag') { 'ffffffffffffffffffffffffffffffffffffffff' } else { $testedMerge }
        ConvertTo-MockJson ([ordered]@{ sha = $sha })
        return
    }
    if ($command -eq 'api repos/walidatiyaai2025-gif/Monitor/releases/tags/v0.1.0-rc.61') {
        $assets = @(
            [ordered]@{
                id = 501
                name = $zipName
                state = 'uploaded'
                size = 4000000
                digest = "sha256:$productSha"
                browser_download_url = "https://github.com/$repository/releases/download/$releaseTag/$zipName"
            },
            [ordered]@{
                id = 502
                name = $checksumName
                state = 'uploaded'
                size = 100
                digest = ('sha256:' + ('c' * 64))
                browser_download_url = "https://github.com/$repository/releases/download/$releaseTag/$checksumName"
            }
        )
        if ($script:MockMode -eq 'extra-asset') {
            $assets += [ordered]@{
                id = 503
                name = 'unexpected.txt'
                state = 'uploaded'
                size = 1
                digest = ('sha256:' + ('d' * 64))
                browser_download_url = "https://github.com/$repository/releases/download/$releaseTag/unexpected.txt"
            }
        }
        ConvertTo-MockJson ([ordered]@{
            id = 9001
            tag_name = $releaseTag
            name = 'Monitor 0.1.0-rc.61'
            draft = $false
            prerelease = $true
            assets = $assets
        })
        return
    }
    if ($command -eq "api repos/walidatiyaai2025-gif/Monitor/commits/$toolingCommit") {
        ConvertTo-MockJson ([ordered]@{ sha = $toolingCommit })
        return
    }
    if ($command -like "api repos/walidatiyaai2025-gif/Monitor/contents/*?ref=$toolingCommit") {
        ConvertTo-MockJson ([ordered]@{ type = 'file'; sha = ('e' * 40) })
        return
    }

    $global:LASTEXITCODE = 1
    "unexpected mock gh command: $command" >&2
}

function Assert-FailsClosed {
    param(
        [Parameter(Mandatory = $true)][string]$Mode,
        [Parameter(Mandatory = $true)][string]$Pattern
    )

    $script:MockMode = $Mode
    $failed = $false
    try {
        & $target -PromotionRunId 100 -VerificationRunId 200 | Out-Null
    }
    catch {
        $failed = $true
        if ($_.Exception.Message -notmatch $Pattern) {
            throw "Mode '$Mode' failed for the wrong reason: $($_.Exception.Message)"
        }
    }
    if (-not $failed) {
        throw "Mode '$Mode' unexpectedly passed the RC.61 cutover readiness gate."
    }
}

try {
    $script:MockMode = 'success'
    $result = & $target -PromotionRunId 100 -VerificationRunId 200
    if ($result.Status -cne 'READY_FOR_P0_5_PRE_CUTOVER_PREPARATION') { throw 'Happy-path readiness status drifted.' }
    if (-not $result.DurableReleasePrerequisiteSatisfied) { throw 'Happy-path durable prerequisite was not marked satisfied.' }
    if ($result.ExternalGatesPassed -ne 0) { throw 'Readiness gate manufactured external production PASS state.' }
    if ($result.ProductionMutationPerformed -or $result.MutatedGitHubState) { throw 'Readiness gate reported mutation on the read-only happy path.' }
    if ($result.PromotionRunId -ne 100 -or $result.VerificationRunId -ne 200) { throw 'Readiness result lost explicit workflow run identity.' }
    if ($result.OperatorToolingCommit -cne $toolingCommit) { throw 'Readiness result lost exact Acceptance Control Toolkit identity.' }

    Assert-FailsClosed -Mode 'bad-order' -Pattern 'created before the promotion run completed'
    Assert-FailsClosed -Mode 'wrong-tag' -Pattern 'does not resolve to the approved tested merge'
    Assert-FailsClosed -Mode 'extra-asset' -Pattern 'exactly two assets'

    foreach ($command in $script:ObservedCommands) {
        if ($command -match '(?i)workflow\s+run|release\s+create|--method\s+(POST|PATCH|PUT|DELETE)|git\s+(tag|push)') {
            throw "Readiness runtime observed a mutation-shaped gh command: $command"
        }
    }

    [pscustomobject]@{
        Status = 'PASS'
        HappyPath = $true
        NegativeCases = 3
        MutationCommandsObserved = 0
    }
}
finally {
    Remove-Item Function:\gh -ErrorAction SilentlyContinue
}
