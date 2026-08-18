[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$helper = Join-Path $PSScriptRoot 'Set-MainBranchProtection.ps1'
$global:MonitorProtectionApplied = $false
$global:MonitorProtectionPutCount = 0
$global:MonitorProtectionPayload = $null

function global:gh {
    $arguments = @($args | ForEach-Object { [string]$_ })
    $joined = $arguments -join ' '

    if ($joined -eq 'api repos/walidatiyaai2025-gif/Monitor') {
        $global:LASTEXITCODE = 0
        return '{"id":1329517438,"full_name":"walidatiyaai2025-gif/Monitor","default_branch":"main"}'
    }

    if ($joined -eq 'api repos/walidatiyaai2025-gif/Monitor/branches/main') {
        $global:LASTEXITCODE = 0
        if ($global:MonitorProtectionApplied) {
            return '{"name":"main","protected":true}'
        }
        return '{"name":"main","protected":false}'
    }

    if ($joined -eq 'api repos/walidatiyaai2025-gif/Monitor/branches/main/protection') {
        if (-not $global:MonitorProtectionApplied) {
            $global:LASTEXITCODE = 1
            return 'gh: Branch not protected (HTTP 404)'
        }

        $global:LASTEXITCODE = 0
        return '{"required_status_checks":{"strict":true,"contexts":["build","protected-p0-pr-metadata","protected-p0-pr-commits"]},"enforce_admins":{"enabled":true},"required_conversation_resolution":{"enabled":true},"allow_force_pushes":{"enabled":false},"allow_deletions":{"enabled":false}}'
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

    $expectedChecks = @('build', 'protected-p0-pr-metadata', 'protected-p0-pr-commits') | Sort-Object
    $payloadChecks = @($global:MonitorProtectionPayload.required_status_checks.contexts | ForEach-Object { [string]$_ }) | Sort-Object
    if (($payloadChecks -join '|') -cne ($expectedChecks -join '|')) {
        throw "Protection payload checks mismatch: $($payloadChecks -join ', ')"
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
        AcknowledgedSinglePut = ($global:MonitorProtectionPutCount -eq 1)
        ReadBackVerified = $true
        AlreadyProtectedNoMutation = $true
        RepositoryIdentityFailClosed = $true
        ExternalProductionGatesPassed = 0
    }
}
finally {
    Remove-Item Function:\global:gh -ErrorAction SilentlyContinue
    Remove-Variable MonitorProtectionApplied -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable MonitorProtectionPutCount -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable MonitorProtectionPayload -Scope Global -ErrorAction SilentlyContinue
    $global:LASTEXITCODE = 0
}
