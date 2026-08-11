[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EvidencePath,

    [string]$EvidenceRoot,

    [string]$ClosureSummaryPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$requiredGates = @(
    'artifactChecksumVerified',
    'iisPreflightPassed',
    'deploymentPlanReviewed',
    'cutoverApplied',
    'trustedHttpsHealthPassed',
    'administratorAuthenticationPassed',
    'leastPrivilegeSqlVerified',
    'iisRecyclePassed',
    'registrationDurabilityVerified',
    'protectedCredentialDurabilityVerified',
    'operationalStateDurabilityVerified',
    'operationalBackupValidated',
    'rollbackRehearsed',
    'postRollbackHealthPassed',
    'finalReadEvidencePassed'
)

function Assert-ExactProperties {
    param([object]$Value, [string[]]$Allowed, [string]$Path)
    if ($null -eq $Value) { throw "$Path is required." }
    $names = @($Value.PSObject.Properties.Name)
    $missing = @($Allowed | Where-Object { $_ -cnotin $names })
    $unknown = @($names | Where-Object { $_ -cnotin $Allowed })
    if ($missing.Count -gt 0) { throw "$Path is missing required properties: $($missing -join ', ')." }
    if ($unknown.Count -gt 0) { throw "$Path contains unknown properties: $($unknown -join ', ')." }
}

function Assert-SafeString {
    param([string]$Value, [string]$Path, [int]$MaxLength = 1024)
    if ($null -eq $Value) { return }
    if ($Value.Length -gt $MaxLength -or $Value -match '[\x00-\x08\x0B\x0C\x0E-\x1F]') {
        throw "$Path exceeds the bounded safe-text contract."
    }
    if ($Value -match '(?i)(?:password|pwd|user\s*id|initial\s+catalog|data\s+source|server)\s*=' -or
        $Value -match '(?i)(?:Microsoft\.Data\.SqlClient|System\.Data\.SqlClient|SqlException|Login failed for user)' -or
        $Value -match '(?i)\b(?:select|insert|update|delete|drop|alter|create|exec(?:ute)?)\s+') {
        throw "$Path contains prohibited credential, provider-error, connection-string, or SQL-text material."
    }
}

function Assert-NoSecretMaterial {
    param([object]$Node, [string]$Path = '$')

    if ($null -eq $Node) { return }

    if ($Node -is [string]) {
        Assert-SafeString -Value ([string]$Node) -Path $Path
        return
    }

    # ConvertFrom-Json represents JSON booleans/numbers as CLR value types.
    # They are terminal JSON values; traversing their adapted PSObject properties can recurse into
    # framework metadata rather than the JSON document itself.
    if ($Node -is [ValueType]) { return }

    if ($Node -is [System.Collections.IDictionary]) {
        foreach ($key in $Node.Keys) {
            $keyText = [string]$key
            if ($keyText -match '(?i)(password|pwd|secret|connection.?string|hashbase64|saltbase64|api.?key|token|private.?key)') {
                throw "$Path contains prohibited secret-like key '$keyText'."
            }
            Assert-NoSecretMaterial -Node $Node[$key] -Path "$Path.$keyText"
        }
        return
    }

    if ($Node -is [System.Collections.IEnumerable] -and $Node -isnot [System.Management.Automation.PSCustomObject]) {
        $index = 0
        foreach ($item in $Node) {
            Assert-NoSecretMaterial -Node $item -Path "$Path[$index]"
            $index++
        }
        return
    }

    foreach ($property in $Node.PSObject.Properties) {
        if ($property.Name -match '(?i)(password|pwd|secret|connection.?string|hashbase64|saltbase64|api.?key|token|private.?key)') {
            throw "$Path contains prohibited secret-like key '$($property.Name)'."
        }
        Assert-NoSecretMaterial -Node $property.Value -Path "$Path.$($property.Name)"
    }
}

function Assert-BoundedIdentifier {
    param([string]$Name, [string]$Value, [int]$MaxLength = 160)
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.Length -gt $MaxLength -or $Value -match '[\r\n\x00-\x1F]' -or $Value -match '(?i)REPLACE_') {
        throw "$Name must be a non-placeholder bounded single-line value."
    }
    Assert-SafeString -Value $Value -Path $Name -MaxLength $MaxLength
}

function Assert-WindowsAbsolutePath {
    param([string]$Name, [string]$Value)
    Assert-BoundedIdentifier -Name $Name -Value $Value -MaxLength 260
    if ($Value -notmatch '^(?:[A-Za-z]:\\|\\\\)') {
        throw "$Name must be an absolute Windows path."
    }
}

function Parse-UtcTimestamp {
    param([string]$Value, [string]$Path)
    Assert-BoundedIdentifier -Name $Path -Value $Value -MaxLength 80
    try {
        $parsed = [DateTimeOffset]::Parse($Value, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
    }
    catch {
        throw "$Path must be a valid ISO-8601 timestamp."
    }
    if ($parsed -gt [DateTimeOffset]::UtcNow.AddMinutes(5)) {
        throw "$Path cannot be materially in the future."
    }
    return $parsed.ToUniversalTime()
}

if (-not (Test-Path -LiteralPath $EvidencePath -PathType Leaf)) {
    throw "Evidence pack was not found: $EvidencePath"
}

$resolvedEvidencePath = (Resolve-Path -LiteralPath $EvidencePath).Path
if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) {
    $EvidenceRoot = Split-Path -Parent $resolvedEvidencePath
}
if (-not (Test-Path -LiteralPath $EvidenceRoot -PathType Container)) {
    throw "EvidenceRoot was not found: $EvidenceRoot"
}
$rootFull = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $EvidenceRoot).Path).TrimEnd('\', '/')
$rootPrefix = $rootFull + [IO.Path]::DirectorySeparatorChar

try {
    $record = Get-Content -LiteralPath $resolvedEvidencePath -Raw | ConvertFrom-Json -Depth 20
}
catch {
    throw 'Evidence pack is not valid JSON.'
}

Assert-ExactProperties -Value $record -Allowed @('schemaVersion', 'candidate', 'environment', 'gates', 'acceptedBy', 'acceptedAtUtc', 'note') -Path '$'
Assert-NoSecretMaterial -Node $record

if ([int]$record.schemaVersion -ne 1) { throw 'schemaVersion must be exactly 1.' }

Assert-ExactProperties -Value $record.candidate -Allowed @('version', 'sourceCommit', 'testedMergeCommit', 'artifactFileName', 'sha256') -Path '$.candidate'
Assert-BoundedIdentifier -Name '$.candidate.version' -Value ([string]$record.candidate.version) -MaxLength 80
if ([string]$record.candidate.version -notmatch '^[0-9A-Za-z][0-9A-Za-z._-]{0,79}$') { throw '$.candidate.version is invalid.' }
if ([string]$record.candidate.sourceCommit -notmatch '^[a-fA-F0-9]{40}$') { throw '$.candidate.sourceCommit must be a full 40-hex commit SHA.' }
if ([string]$record.candidate.testedMergeCommit -notmatch '^[a-fA-F0-9]{40}$') { throw '$.candidate.testedMergeCommit must be a full 40-hex merge SHA.' }
if ([string]$record.candidate.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw '$.candidate.sha256 must be a 64-hex SHA-256.' }
$expectedArtifact = "Monitor-$($record.candidate.version)-win-x64.zip"
if ([string]$record.candidate.artifactFileName -ne $expectedArtifact -or [IO.Path]::GetFileName([string]$record.candidate.artifactFileName) -ne [string]$record.candidate.artifactFileName) {
    throw "$.candidate.artifactFileName must be exactly '$expectedArtifact'."
}

Assert-ExactProperties -Value $record.environment -Allowed @('hostName', 'siteName', 'appPoolName', 'appPoolIdentity', 'certificateThumbprint', 'deploymentMode', 'operationalBackupId', 'previousPhysicalPath', 'stateRoot') -Path '$.environment'
Assert-BoundedIdentifier -Name '$.environment.hostName' -Value ([string]$record.environment.hostName) -MaxLength 253
if ([string]$record.environment.hostName -match '[:/\\*]' -or [string]$record.environment.hostName -match '^(?i:localhost|127\.0\.0\.1|::1)$' -or [string]$record.environment.hostName -notmatch '^[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?$') {
    throw '$.environment.hostName must be an exact non-loopback DNS host name.'
}
foreach ($pair in @(
    @('$.environment.siteName', [string]$record.environment.siteName, 120),
    @('$.environment.appPoolName', [string]$record.environment.appPoolName, 120),
    @('$.environment.appPoolIdentity', [string]$record.environment.appPoolIdentity, 180),
    @('$.environment.operationalBackupId', [string]$record.environment.operationalBackupId, 160)
)) {
    Assert-BoundedIdentifier -Name $pair[0] -Value $pair[1] -MaxLength ([int]$pair[2])
}
if ([string]$record.environment.appPoolIdentity -match '^(?i:LocalSystem|LocalService|NetworkService|Administrator|Administrators)$') {
    throw '$.environment.appPoolIdentity is not an approved low-privilege identity.'
}
$thumbprint = ([string]$record.environment.certificateThumbprint -replace '\s', '')
if ($thumbprint -notmatch '^[A-Fa-f0-9]{40,64}$') { throw '$.environment.certificateThumbprint is invalid.' }
if ([string]$record.environment.deploymentMode -cne 'SingleNode') { throw '$.environment.deploymentMode must be exactly SingleNode.' }
Assert-WindowsAbsolutePath -Name '$.environment.previousPhysicalPath' -Value ([string]$record.environment.previousPhysicalPath)
Assert-WindowsAbsolutePath -Name '$.environment.stateRoot' -Value ([string]$record.environment.stateRoot)

Assert-ExactProperties -Value $record.gates -Allowed $requiredGates -Path '$.gates'
$latestGateTime = [DateTimeOffset]::MinValue
foreach ($gateName in $requiredGates) {
    $gate = $record.gates.PSObject.Properties[$gateName].Value
    Assert-ExactProperties -Value $gate -Allowed @('passed', 'verifiedAtUtc', 'evidenceRef', 'evidenceSha256') -Path "$.gates.$gateName"
    if ($gate.passed -isnot [bool] -or -not [bool]$gate.passed) {
        throw "Required production gate '$gateName' is not PASS."
    }

    $verifiedAt = Parse-UtcTimestamp -Value ([string]$gate.verifiedAtUtc) -Path "$.gates.$gateName.verifiedAtUtc"
    if ($verifiedAt -gt $latestGateTime) { $latestGateTime = $verifiedAt }

    $evidenceRef = [string]$gate.evidenceRef
    Assert-BoundedIdentifier -Name "$.gates.$gateName.evidenceRef" -Value $evidenceRef -MaxLength 260
    if ([IO.Path]::IsPathRooted($evidenceRef) -or $evidenceRef -match '(^|[\\/])\.\.([\\/]|$)' -or $evidenceRef -match '[?#]') {
        throw "Evidence reference for '$gateName' must be a relative local path without traversal, query, or fragment."
    }
    if ([string]$gate.evidenceSha256 -notmatch '^[a-fA-F0-9]{64}$') {
        throw "Evidence SHA-256 for '$gateName' is invalid."
    }

    $targetFull = [IO.Path]::GetFullPath((Join-Path $rootFull $evidenceRef))
    if (-not $targetFull.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Evidence reference for '$gateName' escapes EvidenceRoot."
    }
    if (-not (Test-Path -LiteralPath $targetFull -PathType Leaf)) {
        throw "Evidence file for '$gateName' was not found."
    }
    $actualEvidenceHash = (Get-FileHash -LiteralPath $targetFull -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualEvidenceHash -ne ([string]$gate.evidenceSha256).ToLowerInvariant()) {
        throw "Evidence SHA-256 mismatch for '$gateName'."
    }

    $evidenceText = Get-Content -LiteralPath $targetFull -Raw -ErrorAction Stop
    Assert-SafeString -Value $evidenceText -Path "evidence:$gateName" -MaxLength 65536
}

Assert-BoundedIdentifier -Name '$.acceptedBy' -Value ([string]$record.acceptedBy) -MaxLength 120
$acceptedAt = Parse-UtcTimestamp -Value ([string]$record.acceptedAtUtc) -Path '$.acceptedAtUtc'
if ($acceptedAt -lt $latestGateTime) {
    throw '$.acceptedAtUtc cannot be earlier than the latest verified production gate.'
}

$packSha256 = (Get-FileHash -LiteralPath $resolvedEvidencePath -Algorithm SHA256).Hash.ToLowerInvariant()
$summary = [ordered]@{
    schemaVersion = 1
    result = 'PASS'
    validatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    evidencePackSha256 = $packSha256
    candidateVersion = [string]$record.candidate.version
    artifactFileName = [string]$record.candidate.artifactFileName
    artifactSha256 = ([string]$record.candidate.sha256).ToLowerInvariant()
    sourceCommit = ([string]$record.candidate.sourceCommit).ToLowerInvariant()
    testedMergeCommit = ([string]$record.candidate.testedMergeCommit).ToLowerInvariant()
    hostName = ([string]$record.environment.hostName).ToLowerInvariant()
    deploymentMode = 'SingleNode'
    requiredGateCount = $requiredGates.Count
    acceptedBy = [string]$record.acceptedBy
    acceptedAtUtc = $acceptedAt.ToString('O')
}

if (-not [string]::IsNullOrWhiteSpace($ClosureSummaryPath)) {
    $summaryDirectory = Split-Path -Parent $ClosureSummaryPath
    if (-not [string]::IsNullOrWhiteSpace($summaryDirectory)) {
        New-Item -ItemType Directory -Path $summaryDirectory -Force | Out-Null
    }
    $summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ClosureSummaryPath -Encoding utf8NoBOM
}

Write-Host "Production acceptance evidence PASS: $($requiredGates.Count)/$($requiredGates.Count) external gates verified with matching evidence hashes."
$summary
