[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EvidencePath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{64}$')]
    [string]$ExpectedSessionManifestSha256,

    [Parameter(Mandatory = $true)]
    [string]$AcceptedBy,

    [string]$ClosureSummaryFile = 'p0-5-closure-summary.json',

    [switch]$AcknowledgeFinalAcceptance
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-SafeOperatorIdentity {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value) -or
        $Value.Length -gt 120 -or
        $Value -match '[\r\n\x00-\x1F]' -or
        $Value -match '(?i)REPLACE_') {
        throw 'AcceptedBy must be a non-placeholder bounded single-line operator identity.'
    }

    if ($Value -match '(?i)(?:password|pwd|user\s*id|initial\s+catalog|data\s+source|server)\s*=' -or
        $Value -match '(?i)(?:Microsoft\.Data\.SqlClient|System\.Data\.SqlClient|SqlException|Login failed for user)' -or
        $Value -match '(?i)\b(?:select|insert|update|delete|drop|alter|create|exec(?:ute)?)\s+' -or
        $Value -match '(?i)["'']?(?:password|pwd|secret|connection.?string|hashbase64|saltbase64|api.?key|token|private.?key)["'']?\s*[:=]') {
        throw 'AcceptedBy contains prohibited credential, provider-error, connection-string, secret-like, or SQL-text material.'
    }
}

function Resolve-SafeRelativeOutputPath {
    param(
        [string]$Root,
        [string]$RelativePath,
        [string]$EvidencePackPath
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        [IO.Path]::IsPathRooted($RelativePath) -or
        $RelativePath -match '(^|[\\/])\.\.([\\/]|$)' -or
        $RelativePath -match '[?#]' -or
        $RelativePath.Length -gt 260) {
        throw 'ClosureSummaryFile must be a bounded relative local path beneath the evidence-pack root without traversal, query, or fragment.'
    }

    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $rootPrefix = $rootFull + [IO.Path]::DirectorySeparatorChar
    $targetFull = [IO.Path]::GetFullPath((Join-Path $rootFull $RelativePath))

    if (-not $targetFull.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'ClosureSummaryFile escapes the evidence-pack root.'
    }
    if ($targetFull.Equals([IO.Path]::GetFullPath($EvidencePackPath), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'ClosureSummaryFile must not overwrite the evidence pack.'
    }

    return $targetFull
}

function Write-AtomicText {
    param([string]$Path, [string]$Text)

    $directory = Split-Path -Parent $Path
    $temp = Join-Path $directory ('.' + [IO.Path]::GetFileName($Path) + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllText($temp, $Text, [Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temp -Destination $Path -Force
    }
    finally {
        if (Test-Path -LiteralPath $temp) {
            Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
        }
    }
}

if (-not $AcknowledgeFinalAcceptance) {
    throw 'Final production acceptance requires explicit -AcknowledgeFinalAcceptance after all real external operations are complete.'
}
if (-not (Test-Path -LiteralPath $EvidencePath -PathType Leaf)) {
    throw "Evidence pack was not found: $EvidencePath"
}

Assert-SafeOperatorIdentity -Value $AcceptedBy

$resolvedPackPath = (Resolve-Path -LiteralPath $EvidencePath).Path
$bindingVerifierPath = Join-Path $PSScriptRoot 'Test-ProductionAcceptanceSessionBinding.ps1'
if (-not (Test-Path -LiteralPath $bindingVerifierPath -PathType Leaf)) {
    throw 'Test-ProductionAcceptanceSessionBinding.ps1 must be present beside the finalizer.'
}
& $bindingVerifierPath `
    -EvidencePath $resolvedPackPath `
    -ExpectedSessionManifestSha256 $ExpectedSessionManifestSha256 | Out-Null

$evidenceRoot = Split-Path -Parent $resolvedPackPath
$closureSummaryPath = Resolve-SafeRelativeOutputPath -Root $evidenceRoot -RelativePath $ClosureSummaryFile -EvidencePackPath $resolvedPackPath
if (Test-Path -LiteralPath $closureSummaryPath) {
    throw 'Closure summary already exists. Preserve the existing acceptance record or create a new evidence pack for a new acceptance attempt.'
}

$validatorPath = Join-Path $PSScriptRoot 'Test-ProductionAcceptanceEvidence.ps1'
if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
    throw 'Test-ProductionAcceptanceEvidence.ps1 must be present beside the finalizer.'
}

$originalRaw = Get-Content -LiteralPath $resolvedPackPath -Raw -ErrorAction Stop
$originalSha256 = (Get-FileHash -LiteralPath $resolvedPackPath -Algorithm SHA256).Hash.ToLowerInvariant()
try {
    $record = $originalRaw | ConvertFrom-Json -Depth 20
}
catch {
    throw 'Evidence pack is not valid JSON.'
}

$topLevelNames = @($record.PSObject.Properties.Name)
foreach ($requiredName in @('schemaVersion', 'candidate', 'environment', 'gates', 'acceptedBy', 'acceptedAtUtc', 'note')) {
    if ($requiredName -cnotin $topLevelNames) {
        throw "Evidence pack is missing required property '$requiredName'."
    }
}
if (-not [string]::IsNullOrWhiteSpace([string]$record.acceptedBy) -or $null -ne $record.acceptedAtUtc) {
    throw 'Evidence pack already contains final operator acceptance metadata and is immutable. Create a new pack for corrections.'
}

$acceptedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
$record.acceptedBy = $AcceptedBy
$record.acceptedAtUtc = $acceptedAtUtc
$prospectiveJson = $record | ConvertTo-Json -Depth 20

$nonce = [Guid]::NewGuid().ToString('N')
$prospectivePath = Join-Path $evidenceRoot ".p0-5-prospective-$nonce.json"
$prospectiveSummaryPath = Join-Path $evidenceRoot ".p0-5-prospective-summary-$nonce.json"
$authoritativeCommitted = $false

try {
    [IO.File]::WriteAllText($prospectivePath, $prospectiveJson, [Text.UTF8Encoding]::new($false))

    # Validate the exact prospective accepted record against all 15 real gates and SHA-bound evidence
    # before mutating the authoritative evidence pack. The prospective copy is not itself the canonical
    # session path, so the authoritative locked-session binding is checked immediately before commit.
    & $validatorPath `
        -EvidencePath $prospectivePath `
        -EvidenceRoot $evidenceRoot `
        -ClosureSummaryPath $prospectiveSummaryPath | Out-Null

    $currentSha256 = (Get-FileHash -LiteralPath $resolvedPackPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($currentSha256 -ne $originalSha256) {
        throw 'Evidence pack changed during finalization. No final acceptance metadata was committed; restart finalization from the current pack.'
    }

    & $bindingVerifierPath `
        -EvidencePath $resolvedPackPath `
        -ExpectedSessionManifestSha256 $ExpectedSessionManifestSha256 | Out-Null

    # Atomic authoritative commit: the only semantic changes are acceptedBy and acceptedAtUtc.
    Move-Item -LiteralPath $prospectivePath -Destination $resolvedPackPath -Force
    $authoritativeCommitted = $true

    try {
        $summary = & $validatorPath `
            -EvidencePath $resolvedPackPath `
            -EvidenceRoot $evidenceRoot `
            -ClosureSummaryPath $closureSummaryPath `
            -ExpectedSessionManifestSha256 $ExpectedSessionManifestSha256
    }
    catch {
        # Evidence or its locked session binding may have changed after prospective validation.
        # Fail closed by restoring the original unaccepted pack and removing any partial closure summary.
        Write-AtomicText -Path $resolvedPackPath -Text $originalRaw
        $authoritativeCommitted = $false
        Remove-Item -LiteralPath $closureSummaryPath -Force -ErrorAction SilentlyContinue
        throw
    }

    Write-Host 'Final operator acceptance metadata recorded after locked-session binding plus prospective and authoritative 15/15 validation. Review the closure summary before separately closing #116.'
    $summary
}
finally {
    Remove-Item -LiteralPath $prospectivePath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $prospectiveSummaryPath -Force -ErrorAction SilentlyContinue

    if (-not $authoritativeCommitted -and (Test-Path -LiteralPath $closureSummaryPath)) {
        Remove-Item -LiteralPath $closureSummaryPath -Force -ErrorAction SilentlyContinue
    }
}
