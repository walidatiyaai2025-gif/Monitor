[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z._-]{0,79}$')]
    [string]$CandidateVersion,

    [Parameter(Mandatory = $true)]
    [string]$ArtifactFileName,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{64}$')]
    [string]$ArtifactSha256,

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
    [string]$StateRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
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

function Assert-BoundedIdentifier {
    param([string]$Name, [string]$Value, [int]$MaxLength = 120)
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.Length -gt $MaxLength -or $Value -match '[\r\n\x00-\x1F]') {
        throw "$Name must be a non-empty bounded single-line value."
    }
}

function Assert-WindowsAbsolutePath {
    param([string]$Name, [string]$Value)
    Assert-BoundedIdentifier -Name $Name -Value $Value -MaxLength 260
    if ($Value -notmatch '^(?:[A-Za-z]:\\|\\\\)') {
        throw "$Name must be an absolute Windows path."
    }
}

$expectedArtifactName = "Monitor-$CandidateVersion-win-x64.zip"
if ($ArtifactFileName -ne $expectedArtifactName -or [IO.Path]::GetFileName($ArtifactFileName) -ne $ArtifactFileName) {
    throw "ArtifactFileName must be exactly '$expectedArtifactName' and must not contain a path."
}

Assert-BoundedIdentifier -Name 'HostName' -Value $HostName -MaxLength 253
if ($HostName -match '[:/\\*]' -or $HostName -match '^(?i:localhost|127\.0\.0\.1|::1)$' -or $HostName -notmatch '^[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?$') {
    throw 'HostName must be an exact non-loopback DNS host name without scheme, port, wildcard, or path.'
}

Assert-BoundedIdentifier -Name 'SiteName' -Value $SiteName -MaxLength 120
Assert-BoundedIdentifier -Name 'AppPoolName' -Value $AppPoolName -MaxLength 120
Assert-BoundedIdentifier -Name 'AppPoolIdentity' -Value $AppPoolIdentity -MaxLength 180
if ($AppPoolIdentity -match '^(?i:LocalSystem|LocalService|NetworkService|Administrator|Administrators)$') {
    throw 'AppPoolIdentity must not be a built-in high-privilege or shared service identity.'
}

$normalizedThumbprint = ($CertificateThumbprint -replace '\s', '').ToUpperInvariant()
if ($normalizedThumbprint -notmatch '^[A-F0-9]{40,64}$') {
    throw 'CertificateThumbprint must contain 40 to 64 hexadecimal characters.'
}

Assert-BoundedIdentifier -Name 'OperationalBackupId' -Value $OperationalBackupId -MaxLength 160
Assert-WindowsAbsolutePath -Name 'PreviousPhysicalPath' -Value $PreviousPhysicalPath
Assert-WindowsAbsolutePath -Name 'StateRoot' -Value $StateRoot

$gates = [ordered]@{}
foreach ($gateName in $requiredGates) {
    $gates[$gateName] = [ordered]@{
        passed = $false
        verifiedAtUtc = $null
        evidenceRef = ''
        evidenceSha256 = ''
    }
}

$record = [ordered]@{
    schemaVersion = 1
    candidate = [ordered]@{
        version = $CandidateVersion
        sourceCommit = $SourceCommit.ToLowerInvariant()
        testedMergeCommit = $TestedMergeCommit.ToLowerInvariant()
        artifactFileName = $ArtifactFileName
        sha256 = $ArtifactSha256.ToLowerInvariant()
    }
    environment = [ordered]@{
        hostName = $HostName.ToLowerInvariant()
        siteName = $SiteName
        appPoolName = $AppPoolName
        appPoolIdentity = $AppPoolIdentity
        certificateThumbprint = $normalizedThumbprint
        deploymentMode = 'SingleNode'
        operationalBackupId = $OperationalBackupId
        previousPhysicalPath = $PreviousPhysicalPath
        stateRoot = $StateRoot
    }
    gates = $gates
    acceptedBy = ''
    acceptedAtUtc = $null
    note = 'External operator evidence starts fail-closed. The generator never marks a production gate PASS.'
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
Write-Host "Production acceptance evidence pack created at $OutputPath with $($requiredGates.Count) required gates, all fail-closed."
