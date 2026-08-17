[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string]$ExpectedToolingCommit,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
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

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$expectedCommit = $ExpectedToolingCommit.ToLowerInvariant()

function Invoke-GitText {
    param([string[]]$Arguments)
    $output = & git -C $repoRoot @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed: git $($Arguments -join ' ')`n$($output -join [Environment]::NewLine)"
    }
    return (($output | ForEach-Object { [string]$_ }) -join "`n").Trim()
}

function Assert-FreshAbsoluteOutput {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { throw 'OutputDirectory is required.' }
    if (-not [IO.Path]::IsPathRooted($Value)) { throw 'OutputDirectory must be absolute.' }
    if ($Value -match '[\r\n\x00-\x1F]') { throw 'OutputDirectory contains invalid control characters.' }
    if ($Value -match '(?:^|[\\/])\.\.?([\\/]|$)') { throw 'OutputDirectory must not contain path traversal segments.' }

    $full = [IO.Path]::GetFullPath($Value).TrimEnd('\', '/')
    if ($full -match '^[A-Za-z]:$' -or $full -match '^\\\\[^\\]+\\[^\\]+$' -or $full -eq [IO.Path]::GetPathRoot($full).TrimEnd('\', '/')) {
        throw 'OutputDirectory must not be a filesystem root.'
    }
    if (Test-Path -LiteralPath $full) { throw 'OutputDirectory must be fresh and must not already exist.' }
    $parent = Split-Path -Parent $full
    if ([string]::IsNullOrWhiteSpace($parent) -or -not (Test-Path -LiteralPath $parent -PathType Container)) {
        throw 'OutputDirectory parent must already exist.'
    }

    $repoFull = [IO.Path]::GetFullPath($repoRoot).TrimEnd('\', '/')
    $repoPrefix = $repoFull + [IO.Path]::DirectorySeparatorChar
    if ($full.Equals($repoFull, [StringComparison]::OrdinalIgnoreCase) -or
        $full.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'OutputDirectory must be outside the source Git checkout.'
    }

    return $full
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is required to prove Acceptance Control Toolkit provenance.'
}

$actualCommit = (Invoke-GitText -Arguments @('rev-parse', '--verify', 'HEAD')).ToLowerInvariant()
if ($actualCommit -notmatch '^[a-f0-9]{40}$') {
    throw 'Git HEAD did not resolve to an exact 40-hex commit.'
}
if ($actualCommit -ne $expectedCommit) {
    throw "Git HEAD '$actualCommit' does not equal independently supplied ExpectedToolingCommit '$expectedCommit'."
}

$dirtyTracked = Invoke-GitText -Arguments @('status', '--porcelain=v1', '--untracked-files=no')
if (-not [string]::IsNullOrWhiteSpace($dirtyTracked)) {
    throw 'Tracked Git checkout state must be clean before Acceptance Control Toolkit export.'
}

foreach ($fileName in $requiredFiles) {
    $relative = "scripts/$fileName"
    Invoke-GitText -Arguments @('ls-files', '--error-unmatch', '--', $relative) | Out-Null
    $path = Join-Path $PSScriptRoot $fileName
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required tracked acceptance-control file is missing: $relative"
    }
}

$resolvedOutput = Assert-FreshAbsoluteOutput -Value $OutputDirectory
$temp = Join-Path (Split-Path -Parent $resolvedOutput) ('.' + (Split-Path -Leaf $resolvedOutput) + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')

try {
    New-Item -ItemType Directory -Path $temp -ErrorAction Stop | Out-Null
    $fileEntries = @()

    foreach ($fileName in $requiredFiles) {
        $source = Join-Path $PSScriptRoot $fileName
        $target = Join-Path $temp $fileName
        Copy-Item -LiteralPath $source -Destination $target -ErrorAction Stop
        $hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
        $fileEntries += [pscustomobject][ordered]@{
            fileName = $fileName
            sha256 = $hash
        }
    }

    $manifest = [ordered]@{
        schemaVersion = 1
        toolkitName = 'Monitor Acceptance Control Toolkit'
        toolingCommit = $expectedCommit
        fileCount = $requiredFiles.Count
        files = $fileEntries
        note = 'Exactly six acceptance-control scripts exported from a clean exact Git commit. No candidate bytes or secrets are included.'
    }

    $manifestPath = Join-Path $temp 'toolkit-manifest.json'
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
    $manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$manifestHash  toolkit-manifest.json" | Set-Content -LiteralPath (Join-Path $temp 'toolkit-manifest.sha256') -Encoding ascii

    Move-Item -LiteralPath $temp -Destination $resolvedOutput -ErrorAction Stop

    [pscustomobject]@{
        ToolkitRoot = $resolvedOutput
        ToolingCommit = $expectedCommit
        ToolkitManifestPath = Join-Path $resolvedOutput 'toolkit-manifest.json'
        ToolkitManifestSha256 = $manifestHash
        FileCount = $requiredFiles.Count
    }
}
catch {
    if (Test-Path -LiteralPath $temp) {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
    throw
}
