[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$helper = Join-Path $PSScriptRoot 'Set-MainBranchProtection.ps1'
$global:MonitorProtectionApplied = $false
$global:MonitorProtectionPutCount = 0
$global:MonitorProtectionPayload = $null
$global:MonitorCheckEvidenceMode = 'good'
$global:MonitorMainHeadSha = '28e5cc7377b5abd964c53d14722040741b246561'
$global:MonitorEvidenceHeadSha = 'fd3d3166911e7f1df258cc550f880fd07d4d179c'
$global:MonitorActionsAppId = 15368

function global:gh {
    $arguments = @($args | ForEach-Object { [string]$_ })
    $joined = $arguments -join ' '

    if ($joined -eq 'api repos/walidatiyaai2025-gif/Monitor') {
        $global:LASTEXITCODE = 0
        return '{"id":1329517438,"full_name":"walidatiyaai2025-gif/Monitor","default_branch":"main"}'
    }

    if ($joined -eq 'api repos/walidatiyaai2025-gif/Monitor/branches/main') {
        $global:LASTEXITCODE = 0
        return (@{
            name = 'main'
            protected = [bool]$global:MonitorProtectionApplied
            commit = @{ sha = $global:MonitorMainHeadSha }
        } | ConvertTo-Json -Compress)
    }

    if ($joined -eq 'api repos/walidatiyaai2025-gif/Monitor/branches/main/protection') {
        if (-not $global:MonitorProtectionApplied) {
            $global:LASTEXITCODE = 1
            return 'gh: Branch not protected (HTTP 404)'
        }

        $global:LASTEXITCODE = 0
        return (@{
            required_status_checks = @{
                strict = $true
                checks = @(
                    @{ context = 'build'; app_id = $global:MonitorActionsAppId },
                    @{ context = 'protected-p0-pr-metadata'; app_id = $global:MonitorActionsAppId },
                    @{ context = 'protected-p0-pr-commits'; app_id = $global:MonitorActionsAppId }
                )
            }
            enforce_admins = @{ enabled = $true }
            required_conversation_resolution = @{ enabled = $true }
            allow_force_pushes = @{ enabled = $false }
            allow_deletions = @{ enabled = $false }
        } | ConvertTo-Json -Depth 10 -Compress)
    }

    if ($joined -eq 'api -H Accept: application/vnd.github+json -H X-GitHub-Api-Version: 2022-11-28 repos/walidatiyaai2025-gif/Monitor/pulls?state=closed&base=main&sort=updated&direction=desc&per_page=20') {
        $global:LASTEXITCODE = 0
        $mergeSha = if ($global:MonitorCheckEvidenceMode -ceq 'main-head-mismatch') {
            'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
        }
        else {
            $global:MonitorMainHeadSha
        }

        return (@(
            @{
                number = 354
                merged_at = '2026-08-18T05:05:54Z'
                merge_commit_sha = $mergeSha
                head = @{
                    sha = $global:MonitorEvidenceHeadSha
                    repo = @{ id = 1329517438 }
                }
            }
        ) | ConvertTo-Json -Depth 10 -Compress)
    }

    if ($joined -eq "api -H Accept: application/vnd.github+json -H X-GitHub-Api-Version: 2022-11-28 repos/walidatiyaai2025-gif/Monitor/commits/$($global:MonitorEvidenceHeadSha)/check-runs?per_page=100") {
        $global:LASTEXITCODE = 0
        $runs = @(
            @{ id = 101; name = 'build'; status = 'completed'; conclusion = 'success'; app = @{ id = $global:MonitorActionsAppId } },
            @{ id = 102; name = 'protected-p0-pr-metadata'; status = 'completed'; conclusion = 'success'; app = @{ id = $global:MonitorActionsAppId } },
            @{ id = 103; name = 'protected-p0-pr-commits'; status = 'completed'; conclusion = 'success'; app = @{ id = $global:MonitorActionsAppId } }
        )

        if ($global:MonitorCheckEvidenceMode -ceq 'missing-check') {
            $runs = @($runs | Where-Object { $_.name -cne 'protected-p0-pr-commits' })
        }
        elseif ($global:MonitorCheckEvidenceMode -ceq 'failed-check') {
            $runs[1].conclusion = 'failure'
        }
        elseif ($global:MonitorCheckEvidenceMode -ceq 'ambiguous-provider') {
            $runs += @{ id = 104; name = 'protected-p0-pr-metadata'; status = 'completed'; conclusion = 'success'; app = @{ id = 99999 } }
        }

        return (@{
            total_count = $runs.Count
            check_runs = $runs
        } | ConvertTo-Json -Depth 10 -Compress)
    }

    if ($arguments -contains '--method' -and $arguments -contains 'PUT') {
        $global:MonitorProtectionPutCount++
        $inputIndex = [Array]::IndexOf($arguments, '--input')
        if ($inputIndex -lt 0 -or ($inputIndex + 1) -ge $arguments.Count) {
            throw 'Mock PUT did not receive an --input payload path.'
        }
        $payloadPath = $arguments[$inputIndex + 1]
        $global:MonitorProtectionPayload = Get-Content -LiteralPath $payloadPath -Raw | ConvertFrom-Json
        $global:MonitorProtectionApplied = $true
        $global:LASTEXITCODE = 0
        return '{"ok":true}'
    }

    $global:LASTEXITCODE = 1
    return "gh mock received unexpected arguments: $joined"
}

try {
    $preview = & $helper
    if ($preview.Status -cne 'READY_FOR_EXPLICIT_BRANCH_PROTECTION_ACKNOWLEDGEMENT') {
        throw "Unexpected preview status: $($preview.Status)"
    }
    if ($preview.MutationPerformed -ne $false) {
        throw 'Preview unexpectedly reported a mutation.'
    }
    if ($preview.ExternalProductionGatesPassed -ne 0) {
        throw 'Preview unexpectedly changed external production-gate truth.'
    }
    if ($global:MonitorProtectionPutCount -ne 0) {
        throw 'Preview unexpectedly attempted branch-protection mutation.'
    }

    $previewBindings = @($preview.RequiredCheckBindings | Sort-Object Context)
    if ($previewBindings.Count -ne 3) {
        throw "Preview did not expose exactly three provider-bound checks; observed $($previewBindings.Count)."
    }
    foreach ($binding in $previewBindings) {
        if ([int64]$binding.AppId -ne $global:MonitorActionsAppId) {
            throw "Preview binding for $($binding.Context) used unexpected app ID $($binding.AppId)."
        }
        if ([int]$binding.EvidencePullRequest -ne 354) {
            throw "Preview binding for $($binding.Context) used unexpected evidence PR $($binding.EvidencePullRequest)."
        }
    }

    $applied = & $helper -AcknowledgeProtection
    if ($applied.Status -cne 'BRANCH_PROTECTION_APPLIED_AND_VERIFIED') {
        throw "Unexpected acknowledged status: $($applied.Status)"
    }
    if ($applied.MutationPerformed -ne $true) {
        throw 'Acknowledged path did not report the verified mutation.'
    }
    if ($applied.ExternalProductionGatesPassed -ne 0) {
        throw 'Acknowledged repository-governance mutation changed external production-gate truth.'
    }
    if ($global:MonitorProtectionPutCount -ne 1) {
        throw "Expected exactly one protection PUT, observed $global:MonitorProtectionPutCount."
    }

    if ($null -ne $global:MonitorProtectionPayload.required_status_checks.contexts) {
        throw 'Protection payload unexpectedly used legacy unbound contexts.'
    }
    $payloadChecks = @($global:MonitorProtectionPayload.required_status_checks.checks | Sort-Object context)
    if ($payloadChecks.Count -ne 3) {
        throw "Protection payload required-check count mismatch: $($payloadChecks.Count)."
    }
    $expectedChecks = @('build', 'protected-p0-pr-metadata', 'protected-p0-pr-commits') | Sort-Object
    for ($i = 0; $i -lt $expectedChecks.Count; $i++) {
        if ([string]$payloadChecks[$i].context -cne [string]$expectedChecks[$i]) {
            throw "Protection payload check mismatch at index $i: $($payloadChecks[$i].context)."
        }
        if ([int64]$payloadChecks[$i].app_id -ne $global:MonitorActionsAppId) {
            throw "Protection payload provider mismatch for $($payloadChecks[$i].context)."
        }
    }
    if ($global:MonitorProtectionPayload.required_status_checks.strict -ne $true) { throw 'strict was not true.' }
    if ($global:MonitorProtectionPayload.enforce_admins -ne $true) { throw 'enforce_admins was not true.' }
    if ($global:MonitorProtectionPayload.required_conversation_resolution -ne $true) { throw 'required_conversation_resolution was not true.' }
    if ($global:MonitorProtectionPayload.allow_force_pushes -ne $false) { throw 'allow_force_pushes was not false.' }
    if ($global:MonitorProtectionPayload.allow_deletions -ne $false) { throw 'allow_deletions was not false.' }

    $already = & $helper
    if ($already.Status -cne 'ALREADY_PROTECTED_AS_REQUIRED') {
        throw "Unexpected already-protected status: $($already.Status)"
    }
    if ($global:MonitorProtectionPutCount -ne 1) {
        throw 'Already-protected path unexpectedly mutated protection again.'
    }

    $global:MonitorProtectionApplied = $false
    foreach ($mode in @('missing-check', 'failed-check', 'ambiguous-provider', 'main-head-mismatch')) {
        $global:MonitorCheckEvidenceMode = $mode
        $putsBefore = $global:MonitorProtectionPutCount
        $failedClosed = $false
        try {
            & $helper -AcknowledgeProtection | Out-Null
        }
        catch {
            $failedClosed = $true
        }
        if (-not $failedClosed) {
            throw "Evidence mode '$mode' did not fail closed."
        }
        if ($global:MonitorProtectionPutCount -ne $putsBefore) {
            throw "Evidence mode '$mode' attempted a branch-protection PUT before clean provider evidence."
        }
    }
    $global:MonitorCheckEvidenceMode = 'good'

    $wrongRepositoryFailed = $false
    try {
        & $helper -Repository 'someone/else' | Out-Null
    }
    catch {
        $wrongRepositoryFailed = $_.Exception.Message -match 'Repository must remain exactly'
    }
    if (-not $wrongRepositoryFailed) {
        throw 'Repository identity drift did not fail closed.'
    }

    [pscustomobject]@{
        Status = 'PASS'
        PreviewNoMutation = $true
        ProviderBindingsObserved = 3
        AcknowledgedSinglePut = ($global:MonitorProtectionPutCount -eq 1)
        ProviderBoundPayload = $true
        ReadBackVerified = $true
        AlreadyProtectedNoMutation = $true
        EvidenceFailClosedCases = 4
        RepositoryIdentityFailClosed = $true
        ExternalProductionGatesPassed = 0
    }
}
finally {
    Remove-Item Function:\global:gh -ErrorAction SilentlyContinue
    foreach ($name in @(
        'MonitorProtectionApplied',
        'MonitorProtectionPutCount',
        'MonitorProtectionPayload',
        'MonitorCheckEvidenceMode',
        'MonitorMainHeadSha',
        'MonitorEvidenceHeadSha',
        'MonitorActionsAppId'
    )) {
        Remove-Variable $name -Scope Global -ErrorAction SilentlyContinue
    }
    $global:LASTEXITCODE = 0
}
