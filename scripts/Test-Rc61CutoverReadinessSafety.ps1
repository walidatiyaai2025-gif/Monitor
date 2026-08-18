[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$target = Join-Path $PSScriptRoot 'Test-Rc61CutoverReadiness.ps1'
if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    throw "RC.61 cutover readiness target not found: $target"
}

$global:Rc61CutoverReadinessMock = [ordered]@{
    Mode = 'success'
    ObservedCommands = [System.Collections.Generic.List[string]]::new()
    TestedMerge = '158148d8bfd05f724014541bc7a0b1eab5dae1b5'
    ProductSha = 'd0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5'
    ToolingCommit = 'b422eaaee53d931a62a43b3c36a53b68cd4f3e27'
    ZipName = 'Monitor-0.1.0-rc.61-win-x64.zip'
    ChecksumName = 'Monitor-0.1.0-rc.61-win-x64.zip.sha256'
    ReleaseTag = 'v0.1.0-rc.61'
    Repository = 'walidatiyaai2025-gif/Monitor'
}

function global:gh {
    $state = $global:Rc61CutoverReadinessMock
    $command = ($args | ForEach-Object { [string]$_ }) -join ' '
    $state.ObservedCommands.Add($command)
    $global:LASTEXITCODE = 0

    function Write-MockJson {
        param([Parameter(Mandatory = $true)]$Value)
        $Value | ConvertTo-Json -Depth 12 -Compress
    }

    if ($command -eq 'auth status') {
        'mock authenticated'
        return
    }
    if ($command -like 'repo view walidatiyaai2025-gif/Monitor*') {
        Write-MockJson ([ordered]@{
            nameWithOwner = $state.Repository
            defaultBranchRef = [ordered]@{ name = 'main' }
        })
        return
    }
    if ($command -eq 'api repos/walidatiyaai2025-gif/Monitor/actions/runs/100') {
        Write-MockJson ([ordered]@{
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
        $created = if ($state.Mode -eq 'bad-order') { '2026-08-18T02:59:00Z' } else { '2026-08-18T03:01:00Z' }
        Write-MockJson ([ordered]@{
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
        Write-MockJson ([ordered]@{
            ref = 'refs/tags/v0.1.0-rc.61'
            object = [ordered]@{ type = 'commit'; sha = $state.TestedMerge }
        })
        return
    }
    if ($command -eq 'api repos/walidatiyaai2025-gif/Monitor/commits/v0.1.0-rc.61') {
        $sha = if ($state.Mode -eq 'wrong-tag') { 'ffffffffffffffffffffffffffffffffffffffff' } else { $state.TestedMerge }
        Write-MockJson ([ordered]@{ sha = $sha })
        return
    }
    if ($command -eq 'api repos/walidatiyaai2025-gif/Monitor/releases/tags/v0.1.0-rc.61') {
        $assets = @(
            [ordered]@{
                id = 501
                name = $state.ZipName
                state = 'uploaded'
                size = 4000000
                digest = "sha256:$($state.ProductSha)"
                browser_download_url = "https://github.com/$($state.Repository)/releases/download/$($state.ReleaseTag)/$($state.ZipName)"
            },
            [ordered]@{
                id = 502
                name = $state.ChecksumName
                state = 'uploaded'
                size = 100
                digest = ('sha256:' + ('c' * 64))
                browser_download_url = "https://github.com/$($state.Repository)/releases/download/$($state.ReleaseTag)/$($state.ChecksumName)"
            }
        )
        if ($state.Mode -eq 'extra-asset') {
            $assets += [ordered]@{
                id = 503
                name = 'unexpected.txt'
                state = 'uploaded'
                size = 1
                digest = ('sha256:' + ('d' * 64))
                browser_download_url = "https://github.com/$($state.Repository)/releases/download/$($state.ReleaseTag)/unexpected.txt"
            }
        }
        Write-MockJson ([ordered]@{
            id = 9001
            tag_name = $state.ReleaseTag
            name = 'Monitor 0.1.0-rc.61'
            draft = $false
            prerelease = $true
            assets = $assets
        })
        return
    }
    if ($command -eq "api repos/walidatiyaai2025-gif/Monitor/commits/$($state.ToolingCommit)") {
        Write-MockJson ([ordered]@{ sha = $state.ToolingCommit })
        return
    }
    if ($command -like "api repos/walidatiyaai2025-gif/Monitor/contents/*?ref=$($state.ToolingCommit)") {
        Write-MockJson ([ordered]@{ type = 'file'; sha = ('e' * 40) })
        return
    }

    $global:LASTEXITCODE = 1
    [Console]::Error.WriteLine("unexpected mock gh command: $command")
}

function Assert-FailsClosed {
    param(
        [Parameter(Mandatory = $true)][string]$Mode,
        [Parameter(Mandatory = $true)][string]$Pattern
    )

    $global:Rc61CutoverReadinessMock.Mode = $Mode
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
    $global:Rc61CutoverReadinessMock.Mode = 'success'
    $result = & $target -PromotionRunId 100 -VerificationRunId 200
    if ($result.Status -cne 'READY_FOR_P0_5_PRE_CUTOVER_PREPARATION') { throw 'Happy-path readiness status drifted.' }
    if (-not $result.DurableReleasePrerequisiteSatisfied) { throw 'Happy-path durable prerequisite was not marked satisfied.' }
    if ($result.ExternalGatesPassed -ne 0) { throw 'Readiness gate manufactured external production PASS state.' }
    if ($result.ProductionMutationPerformed -or $result.MutatedGitHubState) { throw 'Readiness gate reported mutation on the read-only happy path.' }
    if ($result.PromotionRunId -ne 100 -or $result.VerificationRunId -ne 200) { throw 'Readiness result lost explicit workflow run identity.' }
    if ($result.OperatorToolingCommit -cne $global:Rc61CutoverReadinessMock.ToolingCommit) { throw 'Readiness result lost exact Acceptance Control Toolkit identity.' }

    Assert-FailsClosed -Mode 'bad-order' -Pattern 'created before the promotion run completed'
    Assert-FailsClosed -Mode 'wrong-tag' -Pattern 'does not resolve to the approved tested merge'
    Assert-FailsClosed -Mode 'extra-asset' -Pattern 'exactly two assets'

    foreach ($command in $global:Rc61CutoverReadinessMock.ObservedCommands) {
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
    Remove-Variable Rc61CutoverReadinessMock -Scope Global -ErrorAction SilentlyContinue
}
