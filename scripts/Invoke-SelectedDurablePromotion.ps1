[CmdletBinding()]
param(
    [string]$Repository = 'walidatiyaai2025-gif/Monitor',
    [switch]$AcknowledgePromotion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Selected replacement candidate. RC.61 is historical only: its Actions artifact expired on 2026-09-12.
$SourceRunId = '34710820438'
$SourceArtifactId = '10303396821'
$SourceArtifactName = 'Monitor-0.1.0-rc.854-win-x64'
$SourceHeadSha = 'ef7209cbf099da65330887508ab4380a8b4196d2'
$TestedMergeSha = 'e1d0daedf8b2209934a1bcd01bff5d46229df20a'
$SourcePrNumber = '479'
$Version = '0.1.0-rc.854'
$TagName = 'v0.1.0-rc.854'
$ProductZip = 'Monitor-0.1.0-rc.854-win-x64.zip'
$ChecksumFile = "$ProductZip.sha256"
$ProductSha256 = 'b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7'
$ArtifactExpiresAtUtc = [DateTimeOffset]::Parse('2026-10-12T18:18:29Z')
$PromotionWorkflow = 'promote-existing-candidate.yml'

function Invoke-GhJson {
    param([Parameter(Mandatory)][string[]]$Arguments)
    $output = & gh @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "gh failed: $($output -join [Environment]::NewLine)" }
    return ($output -join [Environment]::NewLine) | ConvertFrom-Json
}

function Assert-Equal {
    param([string]$Name, $Actual, $Expected)
    if ([string]$Actual -cne [string]$Expected) {
        throw "$Name mismatch. Expected '$Expected'; observed '$Actual'."
    }
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI (gh) is required.' }
& gh auth status | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI is not authenticated.' }

$run = Invoke-GhJson @('api', "repos/$Repository/actions/runs/$SourceRunId")
Assert-Equal 'source workflow conclusion' $run.conclusion 'success'
Assert-Equal 'source workflow head SHA' $run.head_sha $SourceHeadSha

$artifact = Invoke-GhJson @('api', "repos/$Repository/actions/artifacts/$SourceArtifactId")
Assert-Equal 'artifact name' $artifact.name $SourceArtifactName
if ([bool]$artifact.expired) { throw "Selected artifact $SourceArtifactId is expired. Select and verify a newer production-candidate; never fall back to RC.61." }
$liveExpiry = [DateTimeOffset]::Parse([string]$artifact.expires_at)
if ($liveExpiry -ne $ArtifactExpiresAtUtc) { throw "Artifact expiry changed from the locked selection. Expected $ArtifactExpiresAtUtc; observed $liveExpiry." }
if ([DateTimeOffset]::UtcNow -ge $ArtifactExpiresAtUtc) { throw "Selected artifact expired at $ArtifactExpiresAtUtc." }

# Promotion must be immutable. Existing tag or release requires independent reconciliation, never overwrite.
$existingTag = & gh api "repos/$Repository/git/ref/tags/$TagName" 2>$null
if ($LASTEXITCODE -eq 0) { throw "Tag $TagName already exists. Do not overwrite or redispatch." }
$existingRelease = & gh api "repos/$Repository/releases/tags/$TagName" 2>$null
if ($LASTEXITCODE -eq 0) { throw "Release $TagName already exists. Do not overwrite or redispatch." }

$work = Join-Path ([IO.Path]::GetTempPath()) ("monitor-selected-promotion-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    & gh run download $SourceRunId --repo $Repository --name $SourceArtifactName --dir $work
    if ($LASTEXITCODE -ne 0) { throw 'Unable to download the selected Actions artifact.' }

    $zipPath = Join-Path $work $ProductZip
    $checksumPath = Join-Path $work $ChecksumFile
    if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) { throw "Product ZIP missing: $ProductZip" }
    if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) { throw "Checksum file missing: $ChecksumFile" }

    $actualSha = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-Equal 'product SHA-256' $actualSha $ProductSha256
    $checksumText = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
    if ($checksumText -notmatch "^$ProductSha256\s+$([regex]::Escape($ProductZip))$") { throw 'Embedded checksum file does not lock the selected product ZIP.' }

    $expanded = Join-Path $work 'expanded-product'
    Expand-Archive -LiteralPath $zipPath -DestinationPath $expanded -Force
    $manifestPath = Join-Path $expanded '_operations/release-manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'release-manifest.json is missing from the product ZIP.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    Assert-Equal 'manifest version' $manifest.version $Version
    Assert-Equal 'manifest source head' $manifest.sourceHeadSha $SourceHeadSha
    Assert-Equal 'manifest tested merge' $manifest.testedMergeSha $TestedMergeSha
    Assert-Equal 'manifest runtime' $manifest.runtime 'win-x64'
    Assert-Equal 'manifest deployment mode' $manifest.deploymentMode 'SingleNode'

    [pscustomobject]@{
        State = 'SELECTED_CANDIDATE_VERIFIED'
        SourceRunId = $SourceRunId
        ArtifactId = $SourceArtifactId
        ArtifactName = $SourceArtifactName
        ArtifactExpiresAtUtc = $ArtifactExpiresAtUtc.UtcDateTime.ToString('o')
        SourceHeadSha = $SourceHeadSha
        TestedMergeSha = $TestedMergeSha
        SourcePrNumber = $SourcePrNumber
        Version = $Version
        Tag = $TagName
        ProductZip = $ProductZip
        ProductSha256 = $ProductSha256
        ExternalGatesPassed = 0
        ProductionMutationPerformed = $false
    } | Format-List | Out-Host

    if (-not $AcknowledgePromotion) {
        Write-Host 'READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT'
        Write-Host 'No tag, release, deployment, database, IIS, or production mutation was performed.'
        Write-Host 'Review the tuple above, then rerun with -AcknowledgePromotion.'
        exit 0
    }

    $dispatchStarted = [DateTimeOffset]::UtcNow
    & gh workflow run $PromotionWorkflow --repo $Repository --ref main `
        -f "source_run_id=$SourceRunId" `
        -f "source_artifact_id=$SourceArtifactId" `
        -f "tested_merge_commit=$TestedMergeSha" `
        -f "product_sha256=$ProductSha256" `
        -f "tag_name=$TagName" `
        -f 'prerelease=true' `
        -f 'acknowledge_promotion=PROMOTE'
    if ($LASTEXITCODE -ne 0) { throw 'Promotion workflow dispatch failed. Do not retry until the failure is understood.' }

    Start-Sleep -Seconds 4
    $runs = Invoke-GhJson @('run','list','--repo',$Repository,'--workflow',$PromotionWorkflow,'--event','workflow_dispatch','--limit','10','--json','databaseId,createdAt,status,conclusion')
    $matches = @($runs | Where-Object { [DateTimeOffset]::Parse([string]$_.createdAt) -ge $dispatchStarted.AddSeconds(-2) })
    if ($matches.Count -ne 1) { throw "Expected exactly one promotion run after dispatch; observed $($matches.Count). Do not redispatch." }
    $promotionRunId = [string]$matches[0].databaseId

    & gh run watch $promotionRunId --repo $Repository --exit-status
    if ($LASTEXITCODE -ne 0) { throw "Promotion run $promotionRunId failed. Do not redispatch automatically." }

    $tag = Invoke-GhJson @('api', "repos/$Repository/git/ref/tags/$TagName")
    $release = Invoke-GhJson @('api', "repos/$Repository/releases/tags/$TagName")
    Assert-Equal 'published tag target' $tag.object.sha $TestedMergeSha
    if (-not [bool]$release.prerelease) { throw 'Published release is not marked prerelease.' }
    $assetNames = @($release.assets | ForEach-Object { [string]$_.name })
    foreach ($required in @($ProductZip, $ChecksumFile)) {
        if ($assetNames -notcontains $required) { throw "Published release is missing required asset $required." }
    }

    Write-Host "PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED run=$promotionRunId tag=$TagName sha=$ProductSha256"
    Write-Host 'Do not begin production acceptance until a separate durable-release verifier passes and its exact run ID is recorded.'
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
