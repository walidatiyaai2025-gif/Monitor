[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ToolkitRoot,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string]$ExpectedToolingCommit,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{64}$')]
    [string]$ExpectedToolkitManifestSha256
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$requiredFiles = @(
    'New-ProductionAcceptanceSession.ps1',
    'New-ProductionAcceptanceEvidencePack.ps1',
    'Test-ProductionAcceptanceSessionBinding.ps1',
    'Set-ProductionAcceptanceGate.ps1',
    'Complete-ProductionAcceptance.ps1',
    'Test-ProductionAcceptanceEvidence.ps1'
)
$allowedRootFiles = @($requiredFiles + @('toolkit-manifest.json', 'toolkit-manifest.sha256'))

function Assert-ExactProperties {
    param([object]$Value, [string[]]$Allowed, [string]$Path)
    if ($null -eq $Value) { throw "$Path is required." }
    $names = @($Value.PSObject.Properties.Name)
    $missing = @($Allowed | Where-Object { $_ -cnotin $names })
    $unknown = @($names | Where-Object { $_ -cnotin $Allowed })
    if ($missing.Count -gt 0) { throw "$Path is missing required properties: $($missing -join ', ')." }
    if ($unknown.Count -gt 0) { throw "$Path contains unknown properties: $($unknown -join ', ')." }
}

if ([string]::IsNullOrWhiteSpace($ToolkitRoot) -or -not [IO.Path]::IsPathRooted($ToolkitRoot)) {
    throw 'ToolkitRoot must be an absolute path.'
}
if ($ToolkitRoot -match '(?:^|[\\/])\.\.?([\\/]|$)') {
    throw 'ToolkitRoot must not contain path traversal segments.'
}
$resolvedRoot = [IO.Path]::GetFullPath($ToolkitRoot).TrimEnd('\', '/')
if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
    throw "ToolkitRoot was not found: $resolvedRoot"
}

$actualNames = @(Get-ChildItem -LiteralPath $resolvedRoot -Force | ForEach-Object { $_.Name } | Sort-Object)
$expectedNames = @($allowedRootFiles | Sort-Object)
$rootNameDifferences = @(Compare-Object -ReferenceObject $expectedNames -DifferenceObject $actualNames)
if ($actualNames.Count -ne $expectedNames.Count -or $rootNameDifferences.Count -ne 0) {
    throw 'Acceptance Control Toolkit root must contain exactly the six approved scripts plus toolkit-manifest.json and toolkit-manifest.sha256; missing or extra entries fail closed.'
}
foreach ($name in $actualNames) {
    if ([IO.Path]::GetFileName($name) -cne $name -or $name -match '[\\/]') {
        throw 'Toolkit root entry names must be plain file names without path separators.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $resolvedRoot $name) -PathType Leaf)) {
        throw "Toolkit root entry must be a regular file: $name"
    }
}

$manifestPath = Join-Path $resolvedRoot 'toolkit-manifest.json'
$lockPath = Join-Path $resolvedRoot 'toolkit-manifest.sha256'
$expectedManifestHash = $ExpectedToolkitManifestSha256.ToLowerInvariant()
$actualManifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualManifestHash -ne $expectedManifestHash) {
    throw 'Toolkit manifest SHA-256 does not match independently supplied ExpectedToolkitManifestSha256.'
}
$lockLine = (Get-Content -LiteralPath $lockPath -Raw).Trim()
if ($lockLine -cne "$expectedManifestHash  toolkit-manifest.json") {
    throw 'toolkit-manifest.sha256 does not match the independently supplied toolkit manifest SHA-256.'
}

try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 20
}
catch {
    throw 'Toolkit manifest is not valid JSON.'
}
Assert-ExactProperties -Value $manifest -Allowed @('schemaVersion', 'toolkitName', 'toolingCommit', 'fileCount', 'files', 'note') -Path '$manifest'
if ([int]$manifest.schemaVersion -ne 1) { throw 'Toolkit manifest schemaVersion must be exactly 1.' }
if ([string]$manifest.toolkitName -cne 'Monitor Acceptance Control Toolkit') { throw 'Toolkit manifest toolkitName is invalid.' }
$expectedCommit = $ExpectedToolingCommit.ToLowerInvariant()
if (([string]$manifest.toolingCommit).ToLowerInvariant() -ne $expectedCommit) {
    throw 'Toolkit manifest toolingCommit does not match independently supplied ExpectedToolingCommit.'
}
if ([int]$manifest.fileCount -ne $requiredFiles.Count) { throw 'Toolkit manifest fileCount must be exactly 6.' }

$entries = @($manifest.files)
if ($entries.Count -ne $requiredFiles.Count) { throw 'Toolkit manifest must contain exactly six file entries.' }
for ($i = 0; $i -lt $requiredFiles.Count; $i++) {
    $entry = $entries[$i]
    Assert-ExactProperties -Value $entry -Allowed @('fileName', 'sha256') -Path "`$manifest.files[$i]"
    $expectedName = $requiredFiles[$i]
    if ([string]$entry.fileName -cne $expectedName -or [IO.Path]::GetFileName([string]$entry.fileName) -cne [string]$entry.fileName) {
        throw "Toolkit manifest file entry $i must be exactly '$expectedName'."
    }
    $expectedHash = ([string]$entry.sha256).ToLowerInvariant()
    if ($expectedHash -notmatch '^[a-f0-9]{64}$') { throw "Toolkit manifest SHA-256 is invalid for '$expectedName'." }
    $actualHash = (Get-FileHash -LiteralPath (Join-Path $resolvedRoot $expectedName) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "Acceptance Control Toolkit file SHA-256 mismatch: $expectedName"
    }
}

[pscustomobject]@{
    ToolkitRoot = $resolvedRoot
    ToolingCommit = $expectedCommit
    ToolkitManifestSha256 = $expectedManifestHash
    FileCount = $requiredFiles.Count
    Verified = $true
}
