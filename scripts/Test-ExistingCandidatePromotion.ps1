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
if ([IO.Path]::GetFileName($ArtifactPath) -ne $expectedName) { throw "Artifact file name must be exactly '$expectedName'." }
if ([IO.Path]::GetFileName($ChecksumPath) -ne "$expectedName.sha256") { throw "Checksum file name must be exactly '$expectedName.sha256'." }
if (-not (Test-Path -LiteralPath $ArtifactPath -PathType Leaf)) { throw 'Artifact file was not found.' }
if (-not (Test-Path -LiteralPath $ChecksumPath -PathType Leaf)) { throw 'Checksum file was not found.' }

$line = (Get-Content -LiteralPath $ChecksumPath -Raw).Trim()
$escaped = [Regex]::Escape($expectedName)
if ($line -notmatch "^(?<hash>[a-f0-9]{64})\s+\*?$escaped$") { throw 'Companion checksum format or file name is invalid.' }
$actual = (Get-FileHash -LiteralPath $ArtifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $Matches['hash'] -or $actual -ne $ExpectedProductSha256) { throw 'Product SHA-256 does not match both the companion checksum and expected selected hash.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ArtifactPath).Path)
try {
    $entries = @($archive.Entries | Where-Object { $_.FullName -eq '_operations/release-manifest.json' })
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
