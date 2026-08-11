[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [Uri]$BaseUri,

    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,

    [Parameter(Mandatory = $true)]
    [string]$ChecksumPath,

    [Parameter(Mandatory = $true)]
    [string]$EvidencePath,

    [ValidateRange(1, 120)]
    [int]$TimeoutSeconds = 15,

    [switch]$SkipArtifactValidation
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-HttpsBaseUri {
    param([Uri]$Uri)
    if (-not $Uri.IsAbsoluteUri -or $Uri.Scheme -ne 'https') {
        throw 'Production SingleNode acceptance requires an absolute HTTPS BaseUri.'
    }
}

function Assert-ReleaseArtifact {
    param([string]$ZipPath, [string]$ShaPath)

    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
        throw "Release artifact was not found: $ZipPath"
    }
    if (-not (Test-Path -LiteralPath $ShaPath -PathType Leaf)) {
        throw "Checksum file was not found: $ShaPath"
    }

    $expected = ((Get-Content -LiteralPath $ShaPath -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
    if ($expected -notmatch '^[a-f0-9]{64}$') {
        throw 'Checksum file does not contain a valid SHA-256 value.'
    }

    $actual = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "Release artifact SHA-256 mismatch. Expected $expected but calculated $actual."
    }

    return $actual
}

function Invoke-HealthProbe {
    param([string]$Path, [string]$ExpectedStatus)

    $target = [Uri]::new($BaseUri, $Path)
    try {
        $response = Invoke-RestMethod -Uri $target -Method Get -TimeoutSec $TimeoutSeconds -Headers @{ Accept = 'application/json' }
    }
    catch {
        throw "Production probe failed for $Path."
    }

    if ($null -eq $response -or [string]::IsNullOrWhiteSpace([string]$response.status)) {
        throw "Production probe $Path returned no bounded status field."
    }
    if ([string]$response.status -ne $ExpectedStatus) {
        throw "Production probe $Path returned '$($response.status)' instead of '$ExpectedStatus'."
    }

    [pscustomobject]@{ Path = $Path; Status = [string]$response.status; Passed = $true }
}

Assert-HttpsBaseUri -Uri $BaseUri

$checksum = if ($SkipArtifactValidation) { 'SKIPPED' } else { Assert-ReleaseArtifact -ZipPath $ArtifactPath -ShaPath $ChecksumPath }

$probes = @(
    Invoke-HealthProbe -Path '/health/live' -ExpectedStatus 'Live'
    Invoke-HealthProbe -Path '/health/ready' -ExpectedStatus 'Ready'
    Invoke-HealthProbe -Path '/health' -ExpectedStatus 'Ready'
)

$evidenceDirectory = Split-Path -Parent $EvidencePath
if (-not [string]::IsNullOrWhiteSpace($evidenceDirectory)) {
    New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
}

$record = [ordered]@{
    schemaVersion = 1
    acceptedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    deploymentMode = 'SingleNode'
    baseUri = $BaseUri.AbsoluteUri
    artifact = if ($SkipArtifactValidation) { 'SKIPPED' } else { [IO.Path]::GetFileName($ArtifactPath) }
    sha256 = $checksum
    health = @($probes | ForEach-Object { [ordered]@{ path = $_.Path; status = $_.Status; passed = $_.Passed } })
    operatorChecks = [ordered]@{
        iisHttpsBindingVerified = $true
        processRecycleRestartVerified = $false
        durableRegistrationVerifiedAfterRestart = $false
        protectedCredentialVerifiedAfterRestart = $false
        monitoredSqlLeastPrivilegeVerified = $false
        operationalBackupCreated = $false
        rollbackDryRunVerified = $false
    }
    note = 'Health probes passed. The false operatorChecks are deliberate blockers that must be changed only after the actual environment checks are performed.'
}

$record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $EvidencePath -Encoding utf8NoBOM
$probes | Format-Table -AutoSize
Write-Host "Production SingleNode health acceptance passed. Evidence written to $EvidencePath."
Write-Host 'P0.5 is NOT complete until every operatorChecks field is independently verified true in the actual IIS/HTTPS environment.'
