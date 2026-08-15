[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ArtifactPath,
    [Parameter(Mandatory = $true)][string]$ChecksumPath,
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:[-.][0-9A-Za-z.-]+)?$')][string]$CandidateVersion,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-f0-9]{64}$')][string]$ExpectedProductSha256,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-f0-9]{40}$')][string]$SourceCommit,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-f0-9]{40}$')][string]$TestedMergeCommit
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedName = "Monitor-$CandidateVersion-win-x64.zip"
$expectedChecksumName = "$expectedName.sha256"
if ([IO.Path]::GetFileName($ArtifactPath) -cne $expectedName) { throw "Artifact file name must be exactly '$expectedName'." }
if ([IO.Path]::GetFileName($ChecksumPath) -cne $expectedChecksumName) { throw "Checksum file name must be exactly '$expectedChecksumName'." }
if (-not (Test-Path -LiteralPath $ArtifactPath -PathType Leaf)) { throw 'Artifact file was not found.' }
if (-not (Test-Path -LiteralPath $ChecksumPath -PathType Leaf)) { throw 'Checksum file was not found.' }

$resolvedArtifactPath = (Resolve-Path -LiteralPath $ArtifactPath).Path
$resolvedChecksumPath = (Resolve-Path -LiteralPath $ChecksumPath).Path
$artifactDirectory = [IO.Path]::GetDirectoryName($resolvedArtifactPath)
if ([IO.Path]::GetDirectoryName($resolvedChecksumPath) -cne $artifactDirectory) {
    throw 'Artifact ZIP and companion checksum must be in the same payload directory.'
}

$payloadEntries = @(Get-ChildItem -LiteralPath $artifactDirectory -Force)
if ($payloadEntries.Count -ne 2 -or @($payloadEntries | Where-Object { $_.PSIsContainer }).Count -ne 0) {
    throw 'Downloaded promotion payload must contain exactly the selected ZIP and companion checksum.'
}
[string[]]$payloadNames = @($payloadEntries | ForEach-Object { $_.Name } | Sort-Object)
[string[]]$expectedPayloadNames = @($expectedName, $expectedChecksumName) | Sort-Object
if ($payloadNames[0] -cne $expectedPayloadNames[0] -or $payloadNames[1] -cne $expectedPayloadNames[1]) {
    throw 'Downloaded promotion payload contains an unexpected file name.'
}

$checksumText = Get-Content -LiteralPath $resolvedChecksumPath -Raw
if ($checksumText.EndsWith("`r`n", [StringComparison]::Ordinal)) {
    $checksumLine = $checksumText.Substring(0, $checksumText.Length - 2)
}
elseif ($checksumText.EndsWith("`n", [StringComparison]::Ordinal)) {
    $checksumLine = $checksumText.Substring(0, $checksumText.Length - 1)
}
else {
    $checksumLine = $checksumText
}
if ($checksumLine.Contains("`r", [StringComparison]::Ordinal) -or $checksumLine.Contains("`n", [StringComparison]::Ordinal)) {
    throw 'Companion checksum must contain exactly one canonical checksum line.'
}
$expectedChecksumLine = "$ExpectedProductSha256  $expectedName"
if ($checksumLine -cne $expectedChecksumLine) {
    throw 'Companion checksum must be lowercase SHA-256, two spaces, and the exact ZIP filename.'
}

$actual = (Get-FileHash -LiteralPath $resolvedArtifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $ExpectedProductSha256) { throw 'Product SHA-256 does not match the expected selected hash.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($resolvedArtifactPath)
try {
    if ($archive.Entries.Count -gt 4096) {
        throw "Candidate ZIP contains too many entries ($($archive.Entries.Count)); maximum is 4096."
    }

    $seenEntries = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [long]$totalUncompressedBytes = 0
    foreach ($entry in $archive.Entries) {
        $entryName = [string]$entry.FullName
        if ([string]::IsNullOrWhiteSpace($entryName)) { throw 'Candidate ZIP contains an empty entry path.' }
        if ($entryName.StartsWith('/', [StringComparison]::Ordinal) -or
            $entryName.StartsWith('\', [StringComparison]::Ordinal) -or
            $entryName.Contains('\', [StringComparison]::Ordinal) -or
            $entryName -match '^[A-Za-z]:' -or
            $entryName.Contains(':', [StringComparison]::Ordinal)) {
            throw "Candidate ZIP contains an unsafe rooted or Windows-incompatible entry path: '$entryName'."
        }

        $normalizedEntryName = $entryName.TrimEnd('/')
        if ([string]::IsNullOrWhiteSpace($normalizedEntryName) -or $normalizedEntryName.Contains('//', [StringComparison]::Ordinal)) {
            throw "Candidate ZIP contains an unsafe empty path segment: '$entryName'."
        }
        $segments = $normalizedEntryName.Split('/')
        if (@($segments | Where-Object { $_ -eq '.' -or $_ -eq '..' }).Count -ne 0) {
            throw "Candidate ZIP contains a traversal path segment: '$entryName'."
        }

        foreach ($segment in $segments) {
            if ($segment.EndsWith('.', [StringComparison]::Ordinal) -or $segment.EndsWith(' ', [StringComparison]::Ordinal)) {
                throw "Candidate ZIP contains a path segment with a trailing dot or space: '$entryName'."
            }
            if ($segment -match '[<>"|?*\x00-\x1F]') {
                throw "Candidate ZIP contains a Windows-forbidden or control character in a path segment: '$entryName'."
            }
            $deviceStem = ($segment -split '\.', 2)[0]
            if ($deviceStem -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$') {
                throw "Candidate ZIP contains a Windows reserved device-name path segment: '$entryName'."
            }
        }

        $canonicalEntryName = $normalizedEntryName.Normalize([Text.NormalizationForm]::FormC)
        if ($canonicalEntryName.Length -gt 240) {
            throw "Candidate ZIP contains an overlong normalized entry path (>240 characters): '$entryName'."
        }
        if (-not $seenEntries.Add($canonicalEntryName)) {
            throw "Candidate ZIP contains a duplicate or case-colliding entry path (including Unicode normalization): '$entryName'."
        }

        $externalAttributes = [uint32](([int64]$entry.ExternalAttributes) -band 0xFFFFFFFFL)
        $unixFileType = ($externalAttributes -shr 16) -band 0xF000
        $dosAttributes = $externalAttributes -band 0xFFFF
        if ($unixFileType -eq 0xA000 -or ($dosAttributes -band [uint32]([IO.FileAttributes]::ReparsePoint)) -ne 0) {
            throw "Candidate ZIP contains a symlink or reparse-point entry: '$entryName'."
        }

        if ($entry.Length -gt 256MB) {
            throw "Candidate ZIP entry exceeds the 256 MiB uncompressed limit: '$entryName'."
        }
        $totalUncompressedBytes += [long]$entry.Length
        if ($totalUncompressedBytes -gt 1GB) {
            throw 'Candidate ZIP exceeds the 1 GiB total uncompressed-size limit.'
        }
        if ($entry.Length -ge 1MB -and
            ($entry.CompressedLength -le 0 -or ($entry.Length / [double]$entry.CompressedLength) -gt 200.0)) {
            throw "Candidate ZIP entry has a suspicious compression ratio above 200:1: '$entryName'."
        }
    }

    $entries = @($archive.Entries | Where-Object { $_.FullName -ceq '_operations/release-manifest.json' })
    if ($entries.Count -ne 1) { throw 'Candidate must contain exactly one _operations/release-manifest.json.' }
    $reader = [IO.StreamReader]::new($entries[0].Open())
    try { $manifestText = $reader.ReadToEnd() } finally { $reader.Dispose() }
}
finally { $archive.Dispose() }

$manifest = $manifestText | ConvertFrom-Json -Depth 20
if ([int]$manifest.schemaVersion -ne 2) { throw 'Candidate manifest schemaVersion must be 2.' }
if ([string]$manifest.product -ne 'Monitor') { throw 'Candidate manifest product mismatch.' }
if ([string]$manifest.version -ne $CandidateVersion) { throw 'Candidate manifest version mismatch.' }
if ([string]$manifest.sourceHeadSha -ne $SourceCommit) { throw 'Candidate manifest sourceHeadSha mismatch.' }
if ([string]$manifest.testedMergeSha -ne $TestedMergeCommit) { throw 'Candidate manifest testedMergeSha mismatch.' }
if ([string]$manifest.runtime -ne 'win-x64') { throw 'Candidate manifest runtime mismatch.' }
if ([string]$manifest.deploymentMode -ne 'SingleNode') { throw 'Candidate manifest deploymentMode mismatch.' }
if ([string]$manifest.configuration -ne 'Release') { throw 'Candidate manifest configuration mismatch.' }
if ([string]$manifest.candidateVerification.sourceOfTruth -ne '#116') { throw 'Candidate manifest source-of-truth mismatch.' }
if ([bool]$manifest.candidateVerification.embeddedWorkflowRunIds) { throw 'Candidate manifest must not embed candidate-specific workflow run IDs.' }
if ($null -ne $manifest.PSObject.Properties['realSqlAcceptance']) { throw 'Legacy realSqlAcceptance manifest field is forbidden.' }

[pscustomobject]@{
    CandidateVersion = $CandidateVersion
    ArtifactFileName = $expectedName
    ProductSha256 = $actual
    SourceCommit = $SourceCommit
    TestedMergeCommit = $TestedMergeCommit
    Valid = $true
}
