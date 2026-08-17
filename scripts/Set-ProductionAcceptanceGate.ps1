[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EvidencePath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{64}$')]
    [string]$ExpectedSessionManifestSha256,

    [Parameter(Mandatory = $true)]
    [ValidateSet(
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
    )]
    [string]$GateName,

    [Parameter(Mandatory = $true)]
    [string]$EvidenceFile,

    [switch]$AcknowledgePass,
    [switch]$ReplaceExistingPass
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

function Assert-SafeText {
    param([string]$Value, [string]$Path, [int]$MaxLength = 65536)
    if ($null -eq $Value) { return }
    if ($Value.Length -gt $MaxLength -or $Value -match '[\x00-\x08\x0B\x0C\x0E-\x1F]') {
        throw "$Path exceeds the bounded safe-text contract."
    }
    if ($Value -match '(?i)(?:password|pwd|user\s*id|initial\s+catalog|data\s+source|server)\s*=' -or
        $Value -match '(?i)(?:Microsoft\.Data\.SqlClient|System\.Data\.SqlClient|SqlException|Login failed for user)' -or
        $Value -match '(?i)\b(?:select|insert|update|delete|drop|alter|create|exec(?:ute)?)\s+' -or
        $Value -match '(?i)["'']?(?:password|pwd|secret|connection.?string|hashbase64|saltbase64|api.?key|token|private.?key)["'']?\s*[:=]') {
        throw "$Path contains prohibited credential, provider-error, connection-string, secret-like, or SQL-text material."
    }
}

function Assert-NoSecretMaterial {
    param([object]$Node, [string]$Path = '$')

    if ($null -eq $Node) { return }
    if ($Node -is [string]) {
        Assert-SafeText -Value ([string]$Node) -Path $Path -MaxLength 4096
        return
    }
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

function Write-AtomicJson {
    param([string]$Path, [object]$Value)
    $directory = Split-Path -Parent $Path
    $temp = Join-Path $directory ('.' + [IO.Path]::GetFileName($Path) + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $Value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $temp -Encoding utf8NoBOM
        Move-Item -LiteralPath $temp -Destination $Path -Force
    }
    finally {
        if (Test-Path -LiteralPath $temp) {
            Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
        }
    }
}

if (-not $AcknowledgePass) {
    throw 'Recording a production gate PASS requires explicit -AcknowledgePass. The recorder never infers PASS from file presence.'
}
if (-not (Test-Path -LiteralPath $EvidencePath -PathType Leaf)) {
    throw "Evidence pack was not found: $EvidencePath"
}

$resolvedPackPath = (Resolve-Path -LiteralPath $EvidencePath).Path
$bindingVerifierPath = Join-Path $PSScriptRoot 'Test-ProductionAcceptanceSessionBinding.ps1'
if (-not (Test-Path -LiteralPath $bindingVerifierPath -PathType Leaf)) {
    throw 'Test-ProductionAcceptanceSessionBinding.ps1 must be present beside the gate recorder.'
}
$sessionBinding = & $bindingVerifierPath `
    -EvidencePath $resolvedPackPath `
    -ExpectedSessionManifestSha256 $ExpectedSessionManifestSha256

$evidenceRoot = Split-Path -Parent $resolvedPackPath
$rootFull = [IO.Path]::GetFullPath($evidenceRoot).TrimEnd('\', '/')
$rootPrefix = $rootFull + [IO.Path]::DirectorySeparatorChar

if ([IO.Path]::IsPathRooted($EvidenceFile) -or $EvidenceFile -match '(^|[\\/])\.\.([\\/]|$)' -or $EvidenceFile -match '[?#]' -or [string]::IsNullOrWhiteSpace($EvidenceFile)) {
    throw 'EvidenceFile must be a relative local path inside the evidence-pack root without traversal, query, or fragment.'
}

$targetFull = [IO.Path]::GetFullPath((Join-Path $rootFull $EvidenceFile))
if (-not $targetFull.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'EvidenceFile escapes the evidence-pack root.'
}
if (-not (Test-Path -LiteralPath $targetFull -PathType Leaf)) {
    throw "Evidence file was not found: $EvidenceFile"
}

try {
    $record = Get-Content -LiteralPath $resolvedPackPath -Raw | ConvertFrom-Json -Depth 20
}
catch {
    throw 'Evidence pack is not valid JSON.'
}

Assert-ExactProperties -Value $record -Allowed @('schemaVersion', 'candidate', 'environment', 'gates', 'acceptedBy', 'acceptedAtUtc', 'note') -Path '$'
Assert-NoSecretMaterial -Node $record
if ([int]$record.schemaVersion -ne 1) { throw 'schemaVersion must be exactly 1.' }
Assert-ExactProperties -Value $record.gates -Allowed $requiredGates -Path '$.gates'

if (-not [string]::IsNullOrWhiteSpace([string]$record.acceptedBy) -or $null -ne $record.acceptedAtUtc) {
    throw 'Evidence pack already contains final operator acceptance metadata and is immutable. Create a new pack for corrections.'
}

$gateProperty = $record.gates.PSObject.Properties[$GateName]
if ($null -eq $gateProperty) { throw "Unknown production gate '$GateName'." }
$gate = $gateProperty.Value
Assert-ExactProperties -Value $gate -Allowed @('passed', 'verifiedAtUtc', 'evidenceRef', 'evidenceSha256') -Path "$.gates.$GateName"
if ($gate.passed -isnot [bool]) { throw "$.gates.$GateName.passed must be a boolean." }

$wasPassed = [bool]$gate.passed
if ($wasPassed -and -not $ReplaceExistingPass) {
    throw "Production gate '$GateName' is already PASS. Use -ReplaceExistingPass only after intentionally superseding its evidence."
}
if (-not $wasPassed) {
    if ($null -ne $gate.verifiedAtUtc -or -not [string]::IsNullOrWhiteSpace([string]$gate.evidenceRef) -or -not [string]::IsNullOrWhiteSpace([string]$gate.evidenceSha256)) {
        throw "Fail-closed production gate '$GateName' contains contradictory evidence metadata. Repair or recreate the pack before recording PASS."
    }
}

$evidenceText = Get-Content -LiteralPath $targetFull -Raw -ErrorAction Stop
Assert-SafeText -Value $evidenceText -Path "evidence:$GateName"
$evidenceHash = (Get-FileHash -LiteralPath $targetFull -Algorithm SHA256).Hash.ToLowerInvariant()
$verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
$relativeEvidence = $EvidenceFile.Replace('\\', '/')

$gate.passed = $true
$gate.verifiedAtUtc = $verifiedAtUtc
$gate.evidenceRef = $relativeEvidence
$gate.evidenceSha256 = $evidenceHash

Write-AtomicJson -Path $resolvedPackPath -Value $record

$result = [pscustomobject]@{
    GateName = $GateName
    Passed = $true
    VerifiedAtUtc = $verifiedAtUtc
    EvidenceRef = $relativeEvidence
    EvidenceSha256 = $evidenceHash
    ReplacedExistingPass = $wasPassed
    EvidencePack = $resolvedPackPath
    SessionManifestSha256 = $sessionBinding.SessionManifestSha256
    SelectedProductSha256 = $sessionBinding.SelectedProductSha256
}

Write-Host "Recorded explicit operator PASS for '$GateName' with SHA-256-bound evidence inside the locked acceptance session. Final P0.5 closure still requires all 15 gates plus Test-ProductionAcceptanceEvidence.ps1."
$result
