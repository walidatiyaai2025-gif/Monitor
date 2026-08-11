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
    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string]$SourceCommit,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string]$TestedMergeCommit,

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
    if ($line -notmatch "^(?<hash>[a-fA-F0-9]{64})\\s+\\*?$escaped$") {
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

$resolvedSessionRoot = Assert-SafeSessionTarget -Value $SessionRoot

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
$actualHash = (Get-FileHash -LiteralPath $ArtifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $checksumHash) {
    throw 'Candidate artifact SHA-256 does not match the selected checksum file.'
}
Assert-ReadableZip -Path $ArtifactPath

$generatorPath = Join-Path $PSScriptRoot 'New-ProductionAcceptanceEvidencePack.ps1'
if (-not (Test-Path -LiteralPath $generatorPath -PathType Leaf)) {
    throw 'New-ProductionAcceptanceEvidencePack.ps1 was not found beside the session initializer.'
}

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
    if ((Read-ChecksumContract -Path $copiedChecksum -ExpectedFileName $expectedArtifactName) -ne $copiedHash) {
        throw 'Copied checksum no longer matches the candidate artifact.'
    }

    $packPath = Join-Path $evidenceRoot 'p0-5-evidence-pack.json'
    & $generatorPath `
        -CandidateVersion $CandidateVersion `
        -ArtifactFileName $expectedArtifactName `
        -ArtifactSha256 $copiedHash `
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
    if ($pack.candidate.artifactFileName -ne $expectedArtifactName -or $pack.candidate.sha256 -ne $copiedHash) {
        throw 'Generated evidence pack is not bound to the copied candidate artifact.'
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
        artifactSha256 = $copiedHash
        sourceCommit = $SourceCommit.ToLowerInvariant()
        testedMergeCommit = $TestedMergeCommit.ToLowerInvariant()
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
        '1. Verify session-manifest.sha256 before any production operation.',
        '2. Run Test-IisProductionPrerequisites.ps1 on the intended host and retain bounded non-secret proof.',
        '3. Run Deploy-ProductionSingleNode.ps1 in PLAN ONLY mode and review the plan.',
        '4. Use explicit -Apply only after the reviewed plan and operational backup are approved.',
        '5. Collect real non-secret evidence beneath evidence/proof for each external gate.',
        '6. Record each real gate with Set-ProductionAcceptanceGate.ps1 and explicit -AcknowledgePass.',
        '7. After real 15/15, run Complete-ProductionAcceptance.ps1 with explicit final acknowledgement.',
        '8. Review the real closure summary before #116 or #111 can be closed.',
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
