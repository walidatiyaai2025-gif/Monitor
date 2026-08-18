[CmdletBinding()]
param(
    [switch]$AcknowledgeProtection,
    [string]$Repository = 'walidatiyaai2025-gif/Monitor',
    [string]$Branch = 'main'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedRepository = 'walidatiyaai2025-gif/Monitor'
$expectedRepositoryId = 1329517438
$expectedBranch = 'main'
$expectedChecks = @(
    'build',
    'protected-p0-pr-metadata',
    'protected-p0-pr-commits'
)

if ($Repository -cne $expectedRepository) {
    throw "Repository must remain exactly '$expectedRepository'."
}
if ($Branch -cne $expectedBranch) {
    throw "Branch must remain exactly '$expectedBranch'."
}

function Invoke-GhJson {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,
        [switch]$AllowNotFound
    )

    $output = & gh @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $text = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine

    if ($exitCode -ne 0) {
        if ($AllowNotFound -and $text -match '(?i)(HTTP 404|Not Found)') {
            return $null
        }
        throw "gh api failed (exit $exitCode): $text"
    }

    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    return $text | ConvertFrom-Json -Depth 50
}

function Get-ProtectionSnapshot {
    $repositoryInfo = Invoke-GhJson -Arguments @('api', "repos/$Repository")
    if ([int64]$repositoryInfo.id -ne $expectedRepositoryId) {
        throw "Repository ID mismatch. Expected $expectedRepositoryId, observed $($repositoryInfo.id)."
    }
    if ([string]$repositoryInfo.full_name -cne $expectedRepository) {
        throw "Repository full_name mismatch: $($repositoryInfo.full_name)."
    }
    if ([string]$repositoryInfo.default_branch -cne $expectedBranch) {
        throw "Default branch mismatch: $($repositoryInfo.default_branch)."
    }

    $branchInfo = Invoke-GhJson -Arguments @('api', "repos/$Repository/branches/$Branch")
    if ([string]::IsNullOrWhiteSpace([string]$branchInfo.commit.sha)) {
        throw 'Default branch head SHA is missing or ambiguous.'
    }

    $protection = Invoke-GhJson -Arguments @('api', "repos/$Repository/branches/$Branch/protection") -AllowNotFound

    $bindings = @()
    if ($null -ne $protection -and $null -ne $protection.required_status_checks) {
        if ($null -ne $protection.required_status_checks.checks) {
            $bindings = @($protection.required_status_checks.checks | ForEach-Object {
                [pscustomobject]@{
                    Context = [string]$_.context
                    AppId = if ($null -eq $_.app_id) { $null } else { [int64]$_.app_id }
                }
            })
        }
        elseif ($null -ne $protection.required_status_checks.contexts) {
            $bindings = @($protection.required_status_checks.contexts | ForEach-Object {
                [pscustomobject]@{
                    Context = [string]$_
                    AppId = $null
                }
            })
        }
    }

    [pscustomobject]@{
        RepositoryInfo = $repositoryInfo
        BranchInfo = $branchInfo
        Protection = $protection
        Protected = [bool]$branchInfo.protected
        RequiredCheckBindings = @($bindings | Sort-Object Context -Unique)
    }
}

function Get-ObservedRequiredCheckBindings {
    param([Parameter(Mandatory)]$BranchInfo)

    $branchHeadSha = [string]$BranchInfo.commit.sha
    $pulls = Invoke-GhJson -Arguments @(
        'api',
        '-H', 'Accept: application/vnd.github+json',
        '-H', 'X-GitHub-Api-Version: 2022-11-28',
        "repos/$Repository/pulls?state=closed&base=$Branch&sort=updated&direction=desc&per_page=20"
    )

    $evidencePulls = @($pulls | Where-Object {
        $null -ne $_.merged_at -and
        [string]$_.merge_commit_sha -ceq $branchHeadSha -and
        $null -ne $_.head -and
        $null -ne $_.head.repo -and
        [int64]$_.head.repo.id -eq $expectedRepositoryId
    })

    if ($evidencePulls.Count -ne 1) {
        throw "Current '$Branch' head $branchHeadSha must resolve to exactly one recently merged same-repository PR before protection can be changed; observed $($evidencePulls.Count)."
    }

    $evidencePull = $evidencePulls[0]
    $evidenceHeadSha = [string]$evidencePull.head.sha
    if ([string]::IsNullOrWhiteSpace($evidenceHeadSha)) {
        throw 'Evidence PR head SHA is missing.'
    }

    $checkRuns = Invoke-GhJson -Arguments @(
        'api',
        '-H', 'Accept: application/vnd.github+json',
        '-H', 'X-GitHub-Api-Version: 2022-11-28',
        "repos/$Repository/commits/$evidenceHeadSha/check-runs?per_page=100"
    )

    if ($null -eq $checkRuns -or $null -eq $checkRuns.check_runs) {
        throw "Check-run evidence is missing for PR #$($evidencePull.number) head $evidenceHeadSha."
    }

    $bindings = @()
    foreach ($checkName in $expectedChecks) {
        $matching = @($checkRuns.check_runs | Where-Object { [string]$_.name -ceq $checkName })
        if ($matching.Count -lt 1) {
            throw "Required check '$checkName' was not observed on PR #$($evidencePull.number) head $evidenceHeadSha."
        }

        $successful = @($matching | Where-Object {
            [string]$_.status -ceq 'completed' -and [string]$_.conclusion -ceq 'success'
        })
        if ($successful.Count -lt 1) {
            throw "Required check '$checkName' has no completed/successful run on PR #$($evidencePull.number) head $evidenceHeadSha."
        }

        $appIds = @($successful | ForEach-Object {
            if ($null -eq $_.app -or $null -eq $_.app.id) {
                throw "Required check '$checkName' has no GitHub App provider identity."
            }
            [int64]$_.app.id
        } | Sort-Object -Unique)

        if ($appIds.Count -ne 1 -or $appIds[0] -le 0) {
            throw "Required check '$checkName' provider identity is ambiguous."
        }

        $selected = $successful | Where-Object { [int64]$_.app.id -eq $appIds[0] } | Select-Object -First 1
        $bindings += [pscustomobject]@{
            Context = $checkName
            AppId = [int64]$appIds[0]
            EvidencePullRequest = [int]$evidencePull.number
            EvidenceHeadSha = $evidenceHeadSha
            EvidenceCheckRunId = [int64]$selected.id
        }
    }

    $providerIds = @($bindings | ForEach-Object { [int64]$_.AppId } | Sort-Object -Unique)
    if ($providerIds.Count -ne 1) {
        throw "The required checks are not emitted by one unambiguous GitHub App provider; observed app IDs: $($providerIds -join ', ')."
    }

    return @($bindings | Sort-Object Context)
}

function Test-ProtectionExact {
    param(
        [Parameter(Mandatory)]$Snapshot,
        [Parameter(Mandatory)][object[]]$ExpectedBindings
    )

    if (-not $Snapshot.Protected -or $null -eq $Snapshot.Protection) { return $false }
    if ($Snapshot.Protection.required_status_checks.strict -ne $true) { return $false }
    if ($Snapshot.Protection.enforce_admins.enabled -ne $true) { return $false }
    if ($Snapshot.Protection.required_conversation_resolution.enabled -ne $true) { return $false }
    if ($Snapshot.Protection.allow_force_pushes.enabled -ne $false) { return $false }
    if ($Snapshot.Protection.allow_deletions.enabled -ne $false) { return $false }

    $observed = @($Snapshot.RequiredCheckBindings | Sort-Object Context)
    $expected = @($ExpectedBindings | Sort-Object Context)
    if ($observed.Count -ne $expected.Count) { return $false }

    for ($i = 0; $i -lt $expected.Count; $i++) {
        if ([string]$observed[$i].Context -cne [string]$expected[$i].Context) { return $false }
        if ($null -eq $observed[$i].AppId -or [int64]$observed[$i].AppId -ne [int64]$expected[$i].AppId) { return $false }
    }

    return $true
}

$before = Get-ProtectionSnapshot
$observedBindings = @(Get-ObservedRequiredCheckBindings -BranchInfo $before.BranchInfo)
$alreadyExact = Test-ProtectionExact -Snapshot $before -ExpectedBindings $observedBindings

if ($alreadyExact) {
    [pscustomobject]@{
        Status = 'ALREADY_PROTECTED_AS_REQUIRED'
        Repository = $Repository
        RepositoryId = $expectedRepositoryId
        Branch = $Branch
        RequiredCheckBindings = $observedBindings
        StrictRequiredChecks = $true
        EnforceAdmins = $true
        ConversationResolutionRequired = $true
        ForcePushesAllowed = $false
        DeletionsAllowed = $false
        MutationPerformed = $false
        ExternalProductionGatesPassed = 0
    }
    return
}

if (-not $AcknowledgeProtection) {
    [pscustomobject]@{
        Status = 'READY_FOR_EXPLICIT_BRANCH_PROTECTION_ACKNOWLEDGEMENT'
        Repository = $Repository
        RepositoryId = $expectedRepositoryId
        Branch = $Branch
        CurrentProtected = $before.Protected
        CurrentRequiredCheckBindings = $before.RequiredCheckBindings
        RequiredCheckBindings = $observedBindings
        StrictRequiredChecks = $true
        EnforceAdmins = $true
        ConversationResolutionRequired = $true
        ForcePushesAllowed = $false
        DeletionsAllowed = $false
        MutationPerformed = $false
        ExternalProductionGatesPassed = 0
        ApplyCommand = '.\scripts\Set-MainBranchProtection.ps1 -AcknowledgeProtection'
    }
    return
}

$payloadChecks = @($observedBindings | ForEach-Object {
    [ordered]@{
        context = [string]$_.Context
        app_id = [int64]$_.AppId
    }
})

$payload = [ordered]@{
    required_status_checks = [ordered]@{
        strict = $true
        checks = $payloadChecks
    }
    enforce_admins = $true
    required_pull_request_reviews = $null
    restrictions = $null
    required_conversation_resolution = $true
    allow_force_pushes = $false
    allow_deletions = $false
}

$tempPath = Join-Path ([System.IO.Path]::GetTempPath()) ("monitor-branch-protection-{0}.json" -f ([guid]::NewGuid().ToString('N')))
try {
    $payload | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $tempPath -Encoding utf8NoBOM
    Invoke-GhJson -Arguments @(
        'api',
        '--method', 'PUT',
        '-H', 'Accept: application/vnd.github+json',
        '-H', 'X-GitHub-Api-Version: 2022-11-28',
        "repos/$Repository/branches/$Branch/protection",
        '--input', $tempPath
    ) | Out-Null
}
finally {
    Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
}

$after = Get-ProtectionSnapshot
if (-not (Test-ProtectionExact -Snapshot $after -ExpectedBindings $observedBindings)) {
    throw 'Branch protection mutation returned but read-back verification did not match the exact required provider-bound policy.'
}

[pscustomobject]@{
    Status = 'BRANCH_PROTECTION_APPLIED_AND_VERIFIED'
    Repository = $Repository
    RepositoryId = $expectedRepositoryId
    Branch = $Branch
    RequiredCheckBindings = $observedBindings
    StrictRequiredChecks = $true
    EnforceAdmins = $true
    ConversationResolutionRequired = $true
    ForcePushesAllowed = $false
    DeletionsAllowed = $false
    MutationPerformed = $true
    ExternalProductionGatesPassed = 0
}
