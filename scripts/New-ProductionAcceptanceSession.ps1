[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SessionRoot,

    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,

    [Parameter(Mandatory = $true)]
    [string]$ChecksumPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z._-]{0,79}$')]
    [string]$CandidateVersion,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{64}$')]
    [string]$ExpectedProductSha256,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string]$SourceCommit,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string]$TestedMergeCommit,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string]$OperatorToolingCommit,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{64}$')]
    [string]$ExpectedOperatorToolkitManifestSha256,

    [Parameter(Mandatory = $true)]
    [string]$HostName,

    [Parameter(Mandatory = $true)]
    [string]$SiteName,

    [Parameter(Mandatory = $true)]
    [string]$AppPoolName,

    [Parameter(Mandatory = $true)]
    [string]$AppPoolIdentity,

    [Parameter(Mandatory = $true)]
    [string]$CertificateThumbprint,

    [Parameter(Mandatory = $true)]
    [string]$OperationalBackupId,

    [Parameter(Mandatory = $true)]
    [string]$PreviousPhysicalPath,

    [Parameter(Mandatory = $true)]
    [string]$StateRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-ExactProperties {
    param([object]$Value, [string[]]$Allowed, [string]$Path)
    if ($null -eq $Value) { throw "$Path is required." }
    $names = @($Value.PSObject.Properties.Name)
    $missing = @($Allowed | Where-Object { $_ -cnotin $names })
    $unknown = @($names | Where-Object { $_ -cnotin $Allowed })
    if ($missing.Count -gt 0) { throw "$Path is missing required properties: $($missing -join ', ')." }
    if ($unknown.Count -gt 0) { throw "$Path contains unknown properties: $($unknown -join ', ')." }
}

function Assert-BoundedSafeText {
    param(
        [string]$Name,
        [string]$Value,
        [int]$MaxLength = 260
    )

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.Length -gt $MaxLength -or $Value -match '[\r\n\x00-\x1F]') {
        throw "$Name must be a non-empty bounded single-line value."
    }

    if ($Value -match '(?i)(?:password|pwd|user\s*id|initial\s+catalog|data\s+source|server)\s*=' -or
        $Value -match '(?i)(?:Microsoft\.Data\.SqlClient|System\.Data\.SqlClient|SqlException|Login failed for user)' -or
        $Value -match '(?i)\b(?:select|insert|update|delete|drop|alter|create|exec(?:ute)?)\s+' -or
        $Value -match '(?i)["'']?(?:password|pwd|secret|connection.?string|hashbase64|saltbase64|api.?key|token|private.?key)["'']?\s*[:=]') {
        throw "$Name contains prohibited credential, provider-error, connection-string, secret-like, or SQL-text material."
    }
}

function Assert-WindowsAbsolutePath {
    param([string]$Name, [string]$Value)

    Assert-BoundedSafeText -Name $Name -Value $Value -MaxLength 260
    if ($Value -notmatch '^(?:[A-Za-z]:\\|\\\\)') {
        throw "$Name must be an absolute Windows path."
    }
    if ($Value -ne $Value.Trim()) {
        throw "$Name must not contain leading or trailing whitespace."
    }
    if ($Value -match '(?:^|[\\/])\.\.?([\\/]|$)') {
        throw "$Name must not contain path traversal segments."
    }
}

function Assert-SafeSessionTarget {
    param([string]$Value)

    Assert-WindowsAbsolutePath -Name 'SessionRoot' -Value $Value
    $full = [IO.Path]::GetFullPath($Value).TrimEnd('\', '/')
    if ($full -match '^[A-Za-z]:$' -or $full -match '^\\\\[^\\]+\\[^\\]+$') {
        throw 'SessionRoot must not be a drive or UNC share root.'
    }
    if (Test-Path -LiteralPath $full) {
        throw 'SessionRoot must be fresh and must not already exist.'
    }

    $parent = Split-Path -Parent $full
    if ([string]::IsNullOrWhiteSpace($parent) -or -not (Test-Path -LiteralPath $parent -PathType Container)) {
        throw 'SessionRoot parent directory must already exist.'
    }

    return $full
}

function Read-ChecksumContract {
    param([string]$Path, [string]$ExpectedFileName)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Checksum file was not found: $Path"
    }

    $line = (Get-Content -LiteralPath $Path -Raw).Trim()
    $escaped = [Regex]::Escape($ExpectedFileName)
    if ($line -notmatch "^(?<hash>[a-fA-F0-9]{64})\s+\*?$escaped$") {
        throw "Checksum file must contain exactly '<64-hex-sha256>  $ExpectedFileName'."
    }

    return $Matches['hash'].ToLowerInvariant()
}

function Assert-ReadableZip {
    param([string]$Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = $null
    try {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
        if ($archive.Entries.Count -lt 1) {
            throw 'Candidate ZIP must contain at least one entry.'
        }
    }
    catch {
        throw "Candidate artifact is not a readable ZIP: $($_.Exception.Message)"
    }
    finally {
        if ($null -ne $archive) { $archive.Dispose() }
    }
}

$requiredOperatorToolingFiles = @(
    'New-ProductionAcceptanceSession.ps1',
    'New-ProductionAcceptanceEvidencePack.ps1',
    'Test-ProductionAcceptanceSessionBinding.ps1',
    'Set-ProductionAcceptanceGate.ps1',
    'Complete-ProductionAcceptance.ps1',
    'Test-ProductionAcceptanceEvidence.ps1'
)

$resolvedSessionRoot = Assert-SafeSessionTarget -Value $SessionRoot
$selectedProductHash = $ExpectedProductSha256.ToLowerInvariant()
$normalizedToolingCommit = $OperatorToolingCommit.ToLowerInvariant()
$expectedToolkitManifestHash = $ExpectedOperatorToolkitManifestSha256.ToLowerInvariant()

$toolkitManifestPath = Join-Path $PSScriptRoot 'toolkit-manifest.json'
$toolkitManifestLockPath = Join-Path $PSScriptRoot 'toolkit-manifest.sha256'
if (-not (Test-Path -LiteralPath $toolkitManifestPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $toolkitManifestLockPath -PathType Leaf)) {
    throw 'Acceptance Control Toolkit manifest and toolkit-manifest.sha256 must exist beside the initializer.'
}
$actualToolkitManifestHash = (Get-FileHash -LiteralPath $toolkitManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualToolkitManifestHash -ne $expectedToolkitManifestHash) {
    throw 'Acceptance Control Toolkit manifest SHA-256 does not match independently supplied ExpectedOperatorToolkitManifestSha256.'
}
$toolkitManifestLockLine = (Get-Content -LiteralPath $toolkitManifestLockPath -Raw).Trim()
if ($toolkitManifestLockLine -cne "$expectedToolkitManifestHash  toolkit-manifest.json") {
    throw 'toolkit-manifest.sha256 does not match independently supplied ExpectedOperatorToolkitManifestSha256.'
}
try {
    $toolkitManifest = Get-Content -LiteralPath $toolkitManifestPath -Raw | ConvertFrom-Json -Depth 20
}
catch {
    throw 'Acceptance Control Toolkit manifest is not valid JSON.'
}
Assert-ExactProperties -Value $toolkitManifest -Allowed @('schemaVersion', 'toolkitName', 'toolingCommit', 'fileCount', 'files', 'note') -Path '$toolkitManifest'
if ([int]$toolkitManifest.schemaVersion -ne 1 -or [string]$toolkitManifest.toolkitName -cne 'Monitor Acceptance Control Toolkit') {
    throw 'Acceptance Control Toolkit manifest schema/name is invalid.'
}
if (([string]$toolkitManifest.toolingCommit).ToLowerInvariant() -ne $normalizedToolingCommit) {
    throw 'Acceptance Control Toolkit manifest toolingCommit does not match OperatorToolingCommit.'
}
if ([int]$toolkitManifest.fileCount -ne $requiredOperatorToolingFiles.Count) {
    throw 'Acceptance Control Toolkit manifest fileCount must be exactly 6.'
}
$toolkitEntries = @($toolkitManifest.files)
if ($toolkitEntries.Count -ne $requiredOperatorToolingFiles.Count) {
    throw 'Acceptance Control Toolkit manifest must contain exactly six file entries.'
}

$operatorToolingFiles = [ordered]@{}
for ($i = 0; $i -lt $requiredOperatorToolingFiles.Count; $i++) {
    $toolName = $requiredOperatorToolingFiles[$i]
    $toolPath = Join-Path $PSScriptRoot $toolName
    if (-not (Test-Path -LiteralPath $toolPath -PathType Leaf)) {
        throw "Required acceptance-control sidecar file was not found beside the initializer: $toolName"
    }
    $toolHash = (Get-FileHash -LiteralPath $toolPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $entry = $toolkitEntries[$i]
    Assert-ExactProperties -Value $entry -Allowed @('fileName', 'sha256') -Path "`$toolkitManifest.files[$i]"
    if ([string]$entry.fileName -cne $toolName -or ([string]$entry.sha256).ToLowerInvariant() -ne $toolHash) {
        throw "Acceptance Control Toolkit manifest entry does not match the current sidecar file: $toolName"
    }
    $operatorToolingFiles[$toolName] = $toolHash
}

if (-not (Test-Path -LiteralPath $ArtifactPath -PathType Leaf)) {
    throw "Candidate artifact was not found: $ArtifactPath"
}
if (-not (Test-Path -LiteralPath $ChecksumPath -PathType Leaf)) {
    throw "Candidate checksum was not found: $ChecksumPath"
}

$expectedArtifactName = "Monitor-$CandidateVersion-win-x64.zip"
$artifactName = [IO.Path]::GetFileName($ArtifactPath)
if ($artifactName -ne $expectedArtifactName) {
    throw "Candidate artifact file name must be exactly '$expectedArtifactName'."
}

$expectedChecksumName = "$expectedArtifactName.sha256"
if ([IO.Path]::GetFileName($ChecksumPath) -ne $expectedChecksumName) {
    throw "Candidate checksum file name must be exactly '$expectedChecksumName'."
}

foreach ($pair in @(
    @('HostName', $HostName, 253),
    @('SiteName', $SiteName, 120),
    @('AppPoolName', $AppPoolName, 120),
    @('AppPoolIdentity', $AppPoolIdentity, 180),
    @('CertificateThumbprint', $CertificateThumbprint, 80),
    @('OperationalBackupId', $OperationalBackupId, 160),
    @('PreviousPhysicalPath', $PreviousPhysicalPath, 260),
    @('StateRoot', $StateRoot, 260)
)) {
    Assert-BoundedSafeText -Name ([string]$pair[0]) -Value ([string]$pair[1]) -MaxLength ([int]$pair[2])
}

$checksumHash = Read-ChecksumContract -Path $ChecksumPath -ExpectedFileName $expectedArtifactName
if ($checksumHash -ne $selectedProductHash) {
    throw 'Candidate checksum SHA-256 does not match the selected product SHA-256.'
}

$actualHash = (Get-FileHash -LiteralPath $ArtifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $checksumHash) {
    throw 'Candidate artifact SHA-256 does not match the selected checksum file.'
}
if ($actualHash -ne $selectedProductHash) {
    throw 'Candidate artifact SHA-256 does not match the selected product SHA-256.'
}
Assert-ReadableZip -Path $ArtifactPath

$generatorPath = Join-Path $PSScriptRoot 'New-ProductionAcceptanceEvidencePack.ps1'

$parentRoot = Split-Path -Parent $resolvedSessionRoot
$sessionLeaf = Split-Path -Leaf $resolvedSessionRoot
$tempRoot = Join-Path $parentRoot ('.' + $sessionLeaf + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')

try {
    New-Item -ItemType Directory -Path $tempRoot -ErrorAction Stop | Out-Null
    $candidateRoot = Join-Path $tempRoot 'candidate'
    $evidenceRoot = Join-Path $tempRoot 'evidence'
    $proofRoot = Join-Path $evidenceRoot 'proof'
    New-Item -ItemType Directory -Path $candidateRoot, $evidenceRoot, $proofRoot -ErrorAction Stop | Out-Null

    $copiedArtifact = Join-Path $candidateRoot $expectedArtifactName
    $copiedChecksum = Join-Path $candidateRoot $expectedChecksumName
    Copy-Item -LiteralPath $ArtifactPath -Destination $copiedArtifact -ErrorAction Stop
    Copy-Item -LiteralPath $ChecksumPath -Destination $copiedChecksum -ErrorAction Stop

    $copiedHash = (Get-FileHash -LiteralPath $copiedArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($copiedHash -ne $actualHash) {
        throw 'Candidate artifact changed while creating the acceptance session.'
    }
    if ($copiedHash -ne $selectedProductHash) {
        throw 'Copied candidate artifact SHA-256 does not match the selected product SHA-256.'
    }
    if ((Read-ChecksumContract -Path $copiedChecksum -ExpectedFileName $expectedArtifactName) -ne $selectedProductHash) {
        throw 'Copied checksum no longer matches the selected product SHA-256.'
    }

    $packPath = Join-Path $evidenceRoot 'p0-5-evidence-pack.json'
    & $generatorPath `
        -CandidateVersion $CandidateVersion `
        -ArtifactFileName $expectedArtifactName `
        -ArtifactSha256 $selectedProductHash `
        -SourceCommit $SourceCommit `
        -TestedMergeCommit $TestedMergeCommit `
        -HostName $HostName `
        -SiteName $SiteName `
        -AppPoolName $AppPoolName `
        -AppPoolIdentity $AppPoolIdentity `
        -CertificateThumbprint $CertificateThumbprint `
        -OperationalBackupId $OperationalBackupId `
        -PreviousPhysicalPath $PreviousPhysicalPath `
        -StateRoot $StateRoot `
        -OutputPath $packPath

    $pack = Get-Content -LiteralPath $packPath -Raw | ConvertFrom-Json -Depth 20
    $gateProperties = @($pack.gates.PSObject.Properties)
    if ($gateProperties.Count -ne 15) {
        throw 'Session initializer requires the exact 15-gate production evidence contract.'
    }
    foreach ($gate in $gateProperties) {
        if ([bool]$gate.Value.passed -or $null -ne $gate.Value.verifiedAtUtc -or
            -not [string]::IsNullOrWhiteSpace([string]$gate.Value.evidenceRef) -or
            -not [string]::IsNullOrWhiteSpace([string]$gate.Value.evidenceSha256)) {
            throw "New session gate '$($gate.Name)' was not fail-closed."
        }
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$pack.acceptedBy) -or $null -ne $pack.acceptedAtUtc) {
        throw 'New production acceptance session must not contain final acceptance metadata.'
    }
    if ($pack.candidate.artifactFileName -ne $expectedArtifactName -or $pack.candidate.sha256 -ne $selectedProductHash) {
        throw 'Generated evidence pack is not bound to the selected candidate product SHA-256.'
    }

    $createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $normalizedThumbprint = ($CertificateThumbprint -replace '\s', '').ToUpperInvariant()
    $manifest = [ordered]@{
        schemaVersion = 1
        createdAtUtc = $createdAtUtc
        status = 'PreparedFailClosed'
        deploymentMode = 'SingleNode'
        candidateVersion = $CandidateVersion
        artifactFileName = $expectedArtifactName
        artifactSha256 = $selectedProductHash
        selectedProductSha256 = $selectedProductHash
        sourceCommit = $SourceCommit.ToLowerInvariant()
        testedMergeCommit = $TestedMergeCommit.ToLowerInvariant()
        operatorToolingCommit = $normalizedToolingCommit
        operatorToolkitManifestSha256 = $expectedToolkitManifestHash
        operatorToolingFiles = $operatorToolingFiles
        hostName = $HostName.ToLowerInvariant()
        siteName = $SiteName
        appPoolName = $AppPoolName
        appPoolIdentity = $AppPoolIdentity
        certificateThumbprint = $normalizedThumbprint
        operationalBackupId = $OperationalBackupId
        previousPhysicalPath = $PreviousPhysicalPath
        stateRoot = $StateRoot
        candidateArtifactRelativePath = "candidate/$expectedArtifactName"
        candidateChecksumRelativePath = "candidate/$expectedChecksumName"
        evidencePackRelativePath = 'evidence/p0-5-evidence-pack.json'
        proofRootRelativePath = 'evidence/proof'
        externalGateCount = 15
        externalGatesPassed = 0
        note = 'Prepared workspace only. No external production gate is PASS and no final acceptance is granted.'
    }

    $manifestPath = Join-Path $tempRoot 'session-manifest.json'
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
    $manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$manifestHash  session-manifest.json" | Set-Content -LiteralPath (Join-Path $tempRoot 'session-manifest.sha256') -Encoding ascii

    $nextSteps = @(
        'Monitor P0.5 production acceptance session — NEXT STEPS',
        '1. Preserve the returned ManifestSha256 outside the mutable session and verify session-manifest.sha256 before any production operation.',
        '2. Retain the exact reviewed OperatorToolingCommit and ExpectedOperatorToolkitManifestSha256; do not modify the six acceptance-control sidecar files or toolkit manifest/lock.',
        '3. Run Test-IisProductionPrerequisites.ps1 on the intended host and retain bounded non-secret proof.',
        '4. Run Deploy-ProductionSingleNode.ps1 in PLAN ONLY mode and review the plan.',
        '5. Use explicit -Apply only after the reviewed plan and operational backup are approved.',
        '6. Collect real non-secret evidence beneath evidence/proof for each external gate.',
        '7. Record each real gate with Set-ProductionAcceptanceGate.ps1, the preserved manifest SHA and explicit -AcknowledgePass.',
        '8. After real 15/15, run Complete-ProductionAcceptance.ps1 with the preserved manifest SHA and explicit final acknowledgement.',
        '9. Review the real session-bound closure summary before #116 or #111 can be closed.',
        'Session creation itself proves 0/15 external gates and grants no production acceptance.'
    )
    $nextSteps | Set-Content -LiteralPath (Join-Path $tempRoot 'OPERATOR-NEXT-STEPS.txt') -Encoding utf8NoBOM

    Move-Item -LiteralPath $tempRoot -Destination $resolvedSessionRoot -ErrorAction Stop

    [pscustomobject]@{
        SessionRoot = $resolvedSessionRoot
        ManifestPath = Join-Path $resolvedSessionRoot 'session-manifest.json'
        ManifestSha256 = $manifestHash
        EvidencePath = Join-Path $resolvedSessionRoot 'evidence\p0-5-evidence-pack.json'
        CandidateArtifact = Join-Path $resolvedSessionRoot "candidate\$expectedArtifactName"
        SelectedProductSha256 = $selectedProductHash
        OperatorToolingCommit = $normalizedToolingCommit
        OperatorToolkitManifestSha256 = $expectedToolkitManifestHash
        OperatorToolingFileCount = $requiredOperatorToolingFiles.Count
        ExternalGateCount = 15
        ExternalGatesPassed = 0
        ProductionAccepted = $false
    }
}
catch {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    throw
}
