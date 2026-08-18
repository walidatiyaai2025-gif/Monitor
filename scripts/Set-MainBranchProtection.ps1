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
    $protection = Invoke-GhJson -Arguments @('api', "repos/$Repository/branches/$Branch/protection") -AllowNotFound

    $contexts = @()
    if ($null -ne $protection -and $null -ne $protection.required_status_checks) {
        if ($null -ne $protection.required_status_checks.contexts) {
            $contexts = @($protection.required_status_checks.contexts | ForEach-Object { [string]$_ })
        }
        elseif ($null -ne $protection.required_status_checks.checks) {
            $contexts = @($protection.required_status_checks.checks | ForEach-Object { [string]$_.context })
        }
    }

    [pscustomobject]@{
        RepositoryInfo = $repositoryInfo
        BranchInfo = $branchInfo
        Protection = $protection
        Protected = [bool]$branchInfo.protected
        RequiredChecks = @($contexts | Sort-Object -Unique)
    }
}

function Test-ProtectionExact {
    param([Parameter(Mandatory)]$Snapshot)

    if (-not $Snapshot.Protected -or $null -eq $Snapshot.Protection) { return $false }
    if ($Snapshot.Protection.required_status_checks.strict -ne $true) { return $false }
    if ($Snapshot.Protection.enforce_admins.enabled -ne $true) { return $false }
    if ($Snapshot.Protection.required_conversation_resolution.enabled -ne $true) { return $false }
    if ($Snapshot.Protection.allow_force_pushes.enabled -ne $false) { return $false }
    if ($Snapshot.Protection.allow_deletions.enabled -ne $false) { return $false }

    $observed = @($Snapshot.RequiredChecks | Sort-Object)
    $expected = @($expectedChecks | Sort-Object)
    if ($observed.Count -ne $expected.Count) { return $false }
    for ($i = 0; $i -lt $expected.Count; $i++) {
        if ($observed[$i] -cne $expected[$i]) { return $false }
    }

    return $true
}

$before = Get-ProtectionSnapshot
$alreadyExact = Test-ProtectionExact -Snapshot $before

if ($alreadyExact) {
    [pscustomobject]@{
        Status = 'ALREADY_PROTECTED_AS_REQUIRED'
        Repository = $Repository
        RepositoryId = $expectedRepositoryId
        Branch = $Branch
        RequiredChecks = $expectedChecks
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
        CurrentRequiredChecks = $before.RequiredChecks
        RequiredChecks = $expectedChecks
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

$payload = [ordered]@{
    required_status_checks = [ordered]@{
        strict = $true
        contexts = $expectedChecks
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
if (-not (Test-ProtectionExact -Snapshot $after)) {
    throw 'Branch protection mutation returned but read-back verification did not match the exact required policy.'
}

[pscustomobject]@{
    Status = 'BRANCH_PROTECTION_APPLIED_AND_VERIFIED'
    Repository = $Repository
    RepositoryId = $expectedRepositoryId
    Branch = $Branch
    RequiredChecks = $after.RequiredChecks
    StrictRequiredChecks = $true
    EnforceAdmins = $true
    ConversationResolutionRequired = $true
    ForcePushesAllowed = $false
    DeletionsAllowed = $false
    MutationPerformed = $true
    ExternalProductionGatesPassed = 0
}
