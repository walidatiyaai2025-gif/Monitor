# Remaining OWNER_ONLY / EXTERNAL gates — executable fail-closed handoff

This document is the execution handoff for every remaining non-cloud gate. It deliberately does **not** mark any OWNER_ONLY or EXTERNAL gate PASS.

## Authority and immutable identities

Repository:

```text
Repository        walidatiyaai2025-gif/Monitor
Repository ID     1329517438
Default branch    main
```

The repository `main` SHA is **not hard-coded** here because a documentation-only reconciliation merge must not make the handoff unusable. Every execution captures fresh remote `main`, requires the operator checkout to equal that exact SHA, and preserves the captured value in evidence.

The selected product/tooling identities are immutable:

```text
RC version                 0.1.0-rc.61
RC tag                     v0.1.0-rc.61
ZIP                        Monitor-0.1.0-rc.61-win-x64.zip
Checksum                   Monitor-0.1.0-rc.61-win-x64.zip.sha256
Product SHA-256            d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
Source Actions run         31667721306
Source artifact ID         9168574442
Source artifact name       Monitor-0.1.0-rc.61-win-x64
Outer artifact digest      sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382
Source commit              e28158da67b36dfc5dbf8f4c38b5c43d99c7c728
Tested merge               158148d8bfd05f724014541bc7a0b1eab5dae1b5
Acceptance toolkit commit  b422eaaee53d931a62a43b3c36a53b68cd4f3e27
```

Live recheck on 2026-09-09:

```text
Source artifact 9168574442   present; expired=false
Artifact expiry               2026-09-12T04:41:34Z
Artifact digest               sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382
GitHub releases               none
v0.1.0-rc.61 tag              absent
main protection               false
repository rulesets           none
```

Remaining direct gates:

1. **#162 — OWNER_ONLY:** durable RC.61 promotion plus a separate independent verifier.
2. **#353 — OWNER_ONLY / REPOSITORY_ADMIN:** apply and independently read back required `main` protection.
3. **#116 — EXTERNAL_ENVIRONMENT:** real trusted-certificate Windows/IIS SingleNode acceptance with all 15 real gates.

Issue **#111** is only the parent closure. It has no separate execution action and remains OPEN until #116 has real accepted evidence.

Dependency order is strict:

```text
#162 -> #116 -> #111
```

#353 is independent repository governance and cannot satisfy #162/#116/#111.

---

# Common fresh-main guard

Run this from a trusted authenticated checkout immediately before any OWNER_ONLY mutation. Re-run it if the operator pauses long enough for repository state to change.

```powershell
$ErrorActionPreference = 'Stop'
$repo = 'walidatiyaai2025-gif/Monitor'

gh auth status
$repoMeta = gh api "repos/$repo" | ConvertFrom-Json
if ([long]$repoMeta.id -ne 1329517438) { throw 'STOP: repository identity mismatch.' }
if ([string]$repoMeta.default_branch -cne 'main') { throw 'STOP: default branch drift.' }

$executionMain = (gh api "repos/$repo/branches/main" --jq '.commit.sha').Trim()
if ($executionMain -notmatch '^[0-9a-f]{40}$') { throw 'STOP: invalid remote main SHA.' }
if ((git rev-parse HEAD).Trim() -cne $executionMain) { throw "STOP: checkout is not exact remote main $executionMain." }
if (-not [string]::IsNullOrWhiteSpace((git status --porcelain=v1 --untracked-files=no))) { throw 'STOP: tracked checkout is dirty.' }

"ExecutionMain=$executionMain"
```

**STOP** on repository/default-branch ambiguity, authentication/API ambiguity, checkout drift or dirty tracked state. Do not bypass the guard by substituting an older SHA.

---

# #162 — OWNER_ONLY durable RC.61 release

This gate is release retention/recoverability only. It is intentionally separate from #116 and always reports **0/15 external production gates**.

## Prerequisites

- trusted operator workstation;
- PowerShell 7;
- `git` and authenticated `gh` with Actions/release permissions;
- network access to GitHub;
- fresh-main guard above passes;
- source artifact `9168574442` is still present/unexpired and its name/digest/source provenance match the immutable identities above.

## Step 0 — fail-closed preview, no mutation

```powershell
$preview = .\scripts\Invoke-Rc61DurablePromotion.ps1
$preview | Format-List
```

Require exactly:

```text
Status                             READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT
Version                            0.1.0-rc.61
ReleaseTag                         v0.1.0-rc.61
ProductSha256                      d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
WorkflowDispatchPerformed          False
IndependentVerificationDispatched  False
ProductionMutationPerformed        False
MutatedGitHubState                 False
```

Lower-level diagnosis is read-only:

```powershell
.\scripts\Test-Rc61DurablePromotionPreflight.ps1 | Format-List
```

For a first publication attempt require:

```text
Status             READY_FOR_EXPLICIT_MANUAL_PROMOTION
MutatedGitHubState False
TagExists          False
ReleaseExists      False
```

If tag/release state already exists, the source artifact is expired/missing, provenance/digest differs, or GitHub probing is ambiguous, **STOP**. Do not dispatch.

## Step 1 — explicit acknowledged promotion

```powershell
$promotion = .\scripts\Invoke-Rc61DurablePromotion.ps1 -AcknowledgePromotion
$promotion | Format-List
```

Require:

```text
Status                  PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED
PromotionRunStatus      completed
PromotionRunConclusion  success
```

Capture only the returned exact identity:

```powershell
$promotionRunId = [long]$promotion.PromotionRunId
$promotionRunUrl = [string]$promotion.PromotionRunUrl
$verifyCommand = [string]$promotion.IndependentVerificationCommand
if ($promotionRunId -le 0) { throw 'STOP: promotion run ID missing.' }
if ([string]::IsNullOrWhiteSpace($promotionRunUrl)) { throw 'STOP: promotion run URL missing.' }
if ($verifyCommand -notmatch '^gh workflow run verify-durable-release\.yml ') { throw 'STOP: independent verifier command drift.' }
```

Ambiguous run discovery, timeout or failure means **STOP / DO NOT REDISPATCH**.

## Step 2 — separate independent verifier

This must be a separate operator action after the exact promotion run is Green. Execute the exact command returned by the helper:

```powershell
Invoke-Expression $verifyCommand
```

Its immutable inputs must be:

```text
release_version          0.1.0-rc.61
release_tag              v0.1.0-rc.61
expected_commit          158148d8bfd05f724014541bc7a0b1eab5dae1b5
expected_product_sha256  d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
```

Capture exactly one successful `verify-durable-release` run ID. Ambiguity means STOP, not redispatch.

## Step 3 — bind the two exact Green runs

```powershell
$verificationRunId = Read-Host 'Exact successful verify-durable-release run ID'
if ($verificationRunId -notmatch '^[1-9][0-9]*$') { throw 'STOP: invalid verification run ID.' }

$ready = .\scripts\Test-Rc61CutoverReadiness.ps1 `
  -PromotionRunId $promotionRunId `
  -VerificationRunId ([long]$verificationRunId)
$ready | Format-List
```

Require exactly:

```text
Status                              READY_FOR_P0_5_PRE_CUTOVER_PREPARATION
DurableReleasePrerequisiteSatisfied True
ExternalGatesPassed                 0
ProductionMutationPerformed         False
MutatedGitHubState                  False
```

This does **not** pass any #116 gate.

## Step 4 — independent durable tag/assets/hash evidence

```powershell
$evidence162 = 'C:\ProgramData\Monitor\OwnerEvidence\162'
$assetRoot = Join-Path $evidence162 'durable-assets'
if (Test-Path -LiteralPath $evidence162) { throw 'STOP: #162 evidence root already exists; preserve the previous attempt.' }
New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null

$executionMain | Set-Content (Join-Path $evidence162 'execution-main.txt') -Encoding utf8NoBOM

gh release download 'v0.1.0-rc.61' --repo $repo --pattern 'Monitor-0.1.0-rc.61-win-x64.zip' --dir $assetRoot
gh release download 'v0.1.0-rc.61' --repo $repo --pattern 'Monitor-0.1.0-rc.61-win-x64.zip.sha256' --dir $assetRoot

$zip = Join-Path $assetRoot 'Monitor-0.1.0-rc.61-win-x64.zip'
$shaFile = "$zip.sha256"
$expectedSha = 'd0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5'
$actualSha = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualSha -cne $expectedSha) { throw "STOP: durable ZIP SHA mismatch: $actualSha" }
if ((Get-Content -LiteralPath $shaFile -Raw).Trim() -cne "$expectedSha  Monitor-0.1.0-rc.61-win-x64.zip") { throw 'STOP: companion checksum is not canonical.' }

gh api "repos/$repo/actions/runs/$promotionRunId" | Set-Content "$evidence162\promotion-run.json" -Encoding utf8NoBOM
gh api "repos/$repo/actions/runs/$verificationRunId" | Set-Content "$evidence162\verification-run.json" -Encoding utf8NoBOM
gh api "repos/$repo/git/ref/tags/v0.1.0-rc.61" | Set-Content "$evidence162\tag.json" -Encoding utf8NoBOM
gh api "repos/$repo/releases/tags/v0.1.0-rc.61" | Set-Content "$evidence162\release.json" -Encoding utf8NoBOM
```

Before #162 can close, independently prove all of these from retained evidence:

- exact promotion run Green;
- separate verifier run Green;
- tag resolves to tested merge `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- release contains exactly the ZIP and `.sha256` assets;
- downloaded durable ZIP SHA-256 is exactly `d0a71...c3d5`;
- companion checksum line is canonical;
- readiness helper binds those exact two run IDs and still reports `ExternalGatesPassed=0`.

### #162 abort / rollback

There is deliberately no destructive post-publication rollback command. Deleting/recreating a durable tag or release destroys chain of custody.

If the exact promotion workflow is still running and no durable mutation is known to have occurred, cancel only that exact run:

```powershell
gh run cancel $promotionRunId --repo 'walidatiyaai2025-gif/Monitor'
```

If state is ambiguous, **do not cancel/delete/recreate/redispatch blindly**. Inspect the exact run/tag/release state and keep #162 OPEN.

### #162 STOP conditions

STOP and keep #162 OPEN on any repository/checkout drift, source artifact expiry/missing/digest/provenance mismatch, auth/API ambiguity, unexpected existing durable state on a first attempt, preview mismatch, ambiguous/failed promotion, verifier not performed separately, ambiguous/failed verifier, tag/assets/hash/checksum mismatch, or readiness output differing from the exact expected state.

---

# #353 — OWNER_ONLY / REPOSITORY_ADMIN branch protection

This is repository governance only and cannot satisfy #162/#116/#111.

## Prerequisites

- trusted repository-admin workstation;
- PowerShell 7 and authenticated `gh` with repository administration permission;
- common fresh-main guard passes;
- live starting state has been captured before mutation.

Required provider-bound checks:

```text
build                     GitHub Actions app_id 15368
protected-p0-pr-metadata  GitHub Actions app_id 15368
protected-p0-pr-commits   GitHub Actions app_id 15368
```

The helper must dynamically bind those checks to the unique same-repository merged PR that produced the current `main`. Do not hard-code an older PR head after a legitimate later merge.

## Step 0 — preview, no mutation

Capture baseline first:

```powershell
$evidence353 = 'C:\ProgramData\Monitor\OwnerEvidence\353'
if (Test-Path -LiteralPath $evidence353) { throw 'STOP: #353 evidence root already exists.' }
New-Item -ItemType Directory -Path $evidence353 -Force | Out-Null
$executionMain | Set-Content "$evidence353\execution-main.txt" -Encoding utf8NoBOM

gh api "repos/$repo/branches/main" | Set-Content "$evidence353\before-branch.json" -Encoding utf8NoBOM
try { gh api "repos/$repo/branches/main/protection" | Set-Content "$evidence353\before-protection.json" -Encoding utf8NoBOM } catch { 'UNPROTECTED_OR_UNREADABLE' | Set-Content "$evidence353\before-protection.txt" -Encoding utf8NoBOM }

$preview = .\scripts\Set-MainBranchProtection.ps1
$preview | Format-List
```

When the known unprotected baseline still applies, require:

```text
Status                          READY_FOR_EXPLICIT_BRANCH_PROTECTION_ACKNOWLEDGEMENT
CurrentProtected                False
StrictRequiredChecks            True
EnforceAdmins                   True
ConversationResolutionRequired  True
ForcePushesAllowed              False
DeletionsAllowed                False
MutationPerformed               False
ExternalProductionGatesPassed   0
```

`RequiredCheckBindings` must contain exactly the three contexts above, each with `AppId=15368`, and must identify one exact current merged PR/head/check-run evidence set.

STOP if current main cannot be tied unambiguously to one current merged same-repository PR, required checks are missing/failed/ambiguous, or the provider app ID is not `15368`.

## Step 1 — explicit apply

```powershell
$result = .\scripts\Set-MainBranchProtection.ps1 -AcknowledgeProtection
$result | Format-List
```

Require:

```text
Status                          BRANCH_PROTECTION_APPLIED_AND_VERIFIED
MutationPerformed               True
StrictRequiredChecks            True
EnforceAdmins                   True
ConversationResolutionRequired  True
ForcePushesAllowed              False
DeletionsAllowed                False
ExternalProductionGatesPassed   0
```

## Step 2 — independent repository-admin read-back

```powershell
gh api "repos/$repo/branches/main" | Set-Content "$evidence353\after-branch.json" -Encoding utf8NoBOM
gh api "repos/$repo/branches/main/protection" | Set-Content "$evidence353\after-protection.json" -Encoding utf8NoBOM
```

Only mark #353 PASS after independent read-back proves:

```text
branch protected                           true
required_status_checks.strict              true
required checks                            exactly build + protected-p0-pr-metadata + protected-p0-pr-commits
provider app_id for each                   15368
enforce_admins.enabled                     true
required_conversation_resolution.enabled   true
allow_force_pushes.enabled                 false
allow_deletions.enabled                    false
```

Record timestamp, `$executionMain`, exact evidence PR/head and the three provider-bound check-run IDs.

## #353 rollback

Rollback to unprotected is valid **only if the captured pre-state proves that exact unprotected baseline**. If an unexpected pre-existing policy existed, restore that real policy instead of using this command.

```powershell
gh api --method DELETE "repos/$repo/branches/main/protection"
```

Then independently require branch `.protected=false` and a real 404 from the protection endpoint.

### #353 STOP conditions

STOP and keep #353 OPEN if current main changes during the operation, pre-state differs unexpectedly, current merged-PR evidence is ambiguous, any required check/provider is missing/failed/ambiguous, app ID differs from `15368`, repository-admin access is unavailable, apply output differs, or independent read-back differs from the required policy.

---

# #116 — EXTERNAL trusted-IIS real 15/15 acceptance

#116 is a real production-environment gate. **Do not perform production mutation while #162 is OPEN.** #162 retention is separate and never counts as one of these 15 gates.

## Required real environment

- #162 genuinely complete with exact promotion run, separate verifier run, tag/assets/hash evidence and readiness;
- intended Windows Server production host;
- elevated PowerShell 7;
- IIS WebAdministration;
- .NET 8 ASP.NET Core Hosting Bundle / ANCM v2;
- intended IIS site/app pool, normally `Monitor` / `Monitor`;
- app pool `No Managed Code` and `ApplicationPoolIdentity` or approved dedicated `SpecificUser`; never LocalSystem/LocalService/NetworkService;
- trusted machine certificate under `Cert:\LocalMachine\My`, private key available, correct host, >24h validity;
- approved secret-free `appsettings.Production.json`, `Deployment:Mode=SingleNode`, exact AllowedHosts and no embedded password/hash/connection string;
- stable state root `C:\ProgramData\Monitor\App_Data` outside release root;
- validated real pre-cutover operational backup ID and physical rollback path;
- approved non-sysadmin least-privilege monitored SQL login;
- exact durable RC.61 ZIP/checksum;
- separately exported and verified Acceptance Control Toolkit from exact commit `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`.

## Step 0 — re-prove #162 readiness, still 0/15

```powershell
$promotionRunId = Read-Host 'Exact successful #162 promotion run ID'
$verificationRunId = Read-Host 'Exact successful #162 verifier run ID'
if ($promotionRunId -notmatch '^[1-9][0-9]*$' -or $verificationRunId -notmatch '^[1-9][0-9]*$') { throw 'STOP: valid #162 run IDs required.' }

$rc61Ready = .\scripts\Test-Rc61CutoverReadiness.ps1 `
  -PromotionRunId ([long]$promotionRunId) `
  -VerificationRunId ([long]$verificationRunId)

if ($rc61Ready.Status -cne 'READY_FOR_P0_5_PRE_CUTOVER_PREPARATION' -or `
    -not [bool]$rc61Ready.DurableReleasePrerequisiteSatisfied -or `
    [int]$rc61Ready.ExternalGatesPassed -ne 0) {
  throw 'STOP: #162 prerequisite not exact.'
}
```

## Step 1 — obtain and hash durable RC.61 bytes

```powershell
$repo = 'walidatiyaai2025-gif/Monitor'
$cutoverRoot = 'C:\ProgramData\Monitor\Cutover\rc61'
if (Test-Path -LiteralPath $cutoverRoot) { throw 'STOP: cutover root already exists.' }
New-Item -ItemType Directory -Path $cutoverRoot -Force | Out-Null

gh release download 'v0.1.0-rc.61' --repo $repo --pattern 'Monitor-0.1.0-rc.61-win-x64.zip' --dir $cutoverRoot
gh release download 'v0.1.0-rc.61' --repo $repo --pattern 'Monitor-0.1.0-rc.61-win-x64.zip.sha256' --dir $cutoverRoot

$artifact = Join-Path $cutoverRoot 'Monitor-0.1.0-rc.61-win-x64.zip'
$checksum = "$artifact.sha256"
$expectedProductSha256 = 'd0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5'
$actualProductSha256 = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualProductSha256 -cne $expectedProductSha256) { throw 'STOP: candidate hash mismatch.' }
if ((Get-Content -LiteralPath $checksum -Raw).Trim() -cne "$expectedProductSha256  Monitor-0.1.0-rc.61-win-x64.zip") { throw 'STOP: checksum file mismatch.' }

$candidateExtract = Join-Path $cutoverRoot 'candidate-extracted'
Expand-Archive -LiteralPath $artifact -DestinationPath $candidateExtract
$candidateOps = Join-Path $candidateExtract '_operations\scripts'
foreach ($name in @('Test-IisProductionPrerequisites.ps1','Deploy-ProductionSingleNode.ps1','Accept-ProductionSingleNode.ps1')) {
  if (-not (Test-Path -LiteralPath (Join-Path $candidateOps $name) -PathType Leaf)) { throw "STOP: RC.61 missing $name." }
}
```

## Step 2 — capture real host inputs and preflight

```powershell
$hostName = Read-Host 'Exact production DNS hostname, no scheme/port'
$certificateThumbprint = Read-Host 'Approved LocalMachine/My certificate thumbprint'
$operationalBackupId = Read-Host 'Validated pre-cutover operational backup ID'
$productionConfigPath = Read-Host 'Absolute approved secret-free appsettings.Production.json path'
$acceptedBy = Read-Host 'Approved final operator identity'

$siteName = 'Monitor'
$appPoolName = 'Monitor'
$stateRoot = 'C:\ProgramData\Monitor\App_Data'
$releaseRoot = 'C:\Program Files\Monitor\releases'
$releaseVersion = '0.1.0-rc.61'
$baseUri = [Uri]("https://$hostName/")

if ([string]::IsNullOrWhiteSpace($operationalBackupId)) { throw 'STOP: real validated backup ID required.' }
if (-not (Test-Path -LiteralPath $productionConfigPath -PathType Leaf)) { throw 'STOP: production config missing.' }

$preflight = & (Join-Path $candidateOps 'Test-IisProductionPrerequisites.ps1') `
  -HostName $hostName `
  -CertificateThumbprint $certificateThumbprint `
  -SiteName $siteName `
  -AppPoolName $appPoolName `
  -HttpsPort 443 `
  -PassThru

if (-not [bool]$preflight.Ready) { throw 'STOP: IIS preflight not Ready.' }
$previousPhysicalPath = [string]$preflight.SitePhysicalPath

Import-Module WebAdministration -ErrorAction Stop
$pool = Get-Item -LiteralPath "IIS:\AppPools\$appPoolName"
$appPoolIdentity = if ([string]$pool.processModel.identityType -ceq 'ApplicationPoolIdentity') { "IIS AppPool\$appPoolName" } else { [string]$pool.processModel.userName }
if ([string]::IsNullOrWhiteSpace($appPoolIdentity)) { throw 'STOP: app-pool identity ambiguous.' }
```

Expected preflight is `Ready=True`. Non-PassThru success ends with:

```text
Monitor IIS production prerequisites passed. No configuration was changed.
```

## Step 3 — export and independently verify exact acceptance sidecar

```powershell
$operatorToolingCommit = 'b422eaaee53d931a62a43b3c36a53b68cd4f3e27'
$toolingSource = "C:\ProgramData\Monitor\ToolingSource\$operatorToolingCommit"
$acceptanceTools = "C:\ProgramData\Monitor\AcceptanceTooling\$operatorToolingCommit"

if ((Test-Path -LiteralPath $toolingSource) -or (Test-Path -LiteralPath $acceptanceTools)) { throw 'STOP: toolkit source/output already exists; preserve prior provenance.' }
New-Item -ItemType Directory -Path (Split-Path -Parent $toolingSource) -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $acceptanceTools) -Force | Out-Null

git clone 'https://github.com/walidatiyaai2025-gif/Monitor.git' $toolingSource
git -C $toolingSource checkout --detach $operatorToolingCommit
if ((git -C $toolingSource rev-parse HEAD).Trim() -cne $operatorToolingCommit) { throw 'STOP: toolkit commit mismatch.' }
if (-not [string]::IsNullOrWhiteSpace((git -C $toolingSource status --porcelain=v1 --untracked-files=no))) { throw 'STOP: toolkit checkout dirty.' }

Push-Location $toolingSource
try {
  $toolkit = .\scripts\Export-ProductionAcceptanceToolkit.ps1 `
    -ExpectedToolingCommit $operatorToolingCommit `
    -OutputDirectory $acceptanceTools
} finally {
  Pop-Location
}

$operatorToolkitManifestSha256 = [string]$toolkit.ToolkitManifestSha256
$toolkitCheck = & "$acceptanceTools\Test-ProductionAcceptanceToolkit.ps1" `
  -ToolkitRoot $acceptanceTools `
  -ExpectedToolingCommit $operatorToolingCommit `
  -ExpectedToolkitManifestSha256 $operatorToolkitManifestSha256

if (-not [bool]$toolkitCheck.Verified -or [int]$toolkitCheck.FileCount -ne 6) { throw 'STOP: toolkit verification failed.' }
```

Require:

```text
ToolingCommit b422eaaee53d931a62a43b3c36a53b68cd4f3e27
FileCount     6
Verified      True
```

Preserve the toolkit-manifest SHA-256 independently outside mutable session state.

## Step 4 — create exactly one fresh fail-closed 0/15 session

```powershell
$sessionRoot = 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-61'
if (Test-Path -LiteralPath $sessionRoot) { throw 'STOP: acceptance session already exists; never reuse it.' }
New-Item -ItemType Directory -Path (Split-Path -Parent $sessionRoot) -Force | Out-Null

$session = & "$acceptanceTools\New-ProductionAcceptanceSession.ps1" `
  -SessionRoot $sessionRoot `
  -ArtifactPath $artifact `
  -ChecksumPath $checksum `
  -CandidateVersion '0.1.0-rc.61' `
  -ExpectedProductSha256 $expectedProductSha256 `
  -SourceCommit 'e28158da67b36dfc5dbf8f4c38b5c43d99c7c728' `
  -TestedMergeCommit '158148d8bfd05f724014541bc7a0b1eab5dae1b5' `
  -OperatorToolingCommit $operatorToolingCommit `
  -ExpectedOperatorToolkitManifestSha256 $operatorToolkitManifestSha256 `
  -HostName $hostName `
  -SiteName $siteName `
  -AppPoolName $appPoolName `
  -AppPoolIdentity $appPoolIdentity `
  -CertificateThumbprint $certificateThumbprint `
  -OperationalBackupId $operationalBackupId `
  -PreviousPhysicalPath $previousPhysicalPath `
  -StateRoot $stateRoot

$sessionManifestSha256 = [string]$session.ManifestSha256
$evidencePath = [string]$session.EvidencePath
$proofRoot = Join-Path (Split-Path -Parent $evidencePath) 'proof'

if ([int]$session.ExternalGateCount -ne 15 -or [int]$session.ExternalGatesPassed -ne 0 -or [bool]$session.ProductionAccepted) {
  throw 'STOP: session not exact fail-closed 0/15.'
}
```

Require session manifest state:

```text
status              PreparedFailClosed
ExternalGateCount   15
ExternalGatesPassed 0
ProductionAccepted  False
```

Create real product-hash and preflight proof:

```powershell
@(
  'Artifact=Monitor-0.1.0-rc.61-win-x64.zip',
  "ExpectedSha256=$expectedProductSha256",
  "ActualSha256=$actualProductSha256",
  'ChecksumCanonical=True'
) | Set-Content -LiteralPath (Join-Path $proofRoot '01-artifact-checksum.txt') -Encoding utf8NoBOM

$preflight | Format-List | Out-String | Set-Content -LiteralPath (Join-Path $proofRoot '02-iis-preflight.txt') -Encoding utf8NoBOM
```

## Step 5 — PLAN ONLY, human review, then explicit Apply

Plan:

```powershell
$planEvidence = Join-Path $proofRoot '03-deployment-plan.json'
& (Join-Path $candidateOps 'Deploy-ProductionSingleNode.ps1') `
  -ArtifactPath $artifact `
  -ChecksumPath $checksum `
  -ReleaseVersion $releaseVersion `
  -ProductionConfigPath $productionConfigPath `
  -OperationalBackupId $operationalBackupId `
  -HostName $hostName `
  -CertificateThumbprint $certificateThumbprint `
  -SiteName $siteName `
  -AppPoolName $appPoolName `
  -ReleaseRoot $releaseRoot `
  -StateRoot $stateRoot `
  -HttpsPort 443 |
  Tee-Object -FilePath $planEvidence
```

Require terminal statements:

```text
PLAN ONLY. No IIS, filesystem, ACL, application-pool, binding, certificate, configuration or state changes were made.
Re-run with -Apply only after reviewing this plan and the pre-cutover operational backup.
```

A real operator must review the plan before that gate can pass.

Apply:

```powershell
$applyEvidence = Join-Path $proofRoot '04-cutover-apply.txt'
$healthEvidence = Join-Path $proofRoot '05-trusted-https-health.json'

& (Join-Path $candidateOps 'Deploy-ProductionSingleNode.ps1') `
  -ArtifactPath $artifact `
  -ChecksumPath $checksum `
  -ReleaseVersion $releaseVersion `
  -ProductionConfigPath $productionConfigPath `
  -OperationalBackupId $operationalBackupId `
  -HostName $hostName `
  -CertificateThumbprint $certificateThumbprint `
  -SiteName $siteName `
  -AppPoolName $appPoolName `
  -ReleaseRoot $releaseRoot `
  -StateRoot $stateRoot `
  -HttpsPort 443 `
  -EvidencePath $healthEvidence `
  -Apply 2>&1 | Tee-Object -FilePath $applyEvidence
```

Success includes:

```text
Monitor SingleNode candidate '0.1.0-rc.61' is active on https://<host>/
```

Immediate acceptance failure must restore the previous physical path automatically; if it does not, STOP and execute the validated rollback path below.

## Step 6 — required real evidence

The real journey must produce these non-secret evidence files from the same immutable session:

```text
proof/01-artifact-checksum.txt
proof/02-iis-preflight.txt
proof/03-deployment-plan.json
proof/04-cutover-apply.txt
proof/05-trusted-https-health.json
proof/06-administrator-authentication.txt
proof/07-least-privilege-sql.txt
proof/08-iis-recycle.txt
proof/09-registration-durability.txt
proof/10-protected-credential-durability.txt
proof/11-operational-state-durability.txt
proof/12-operational-backup-validated.txt
proof/13-rollback-rehearsal.txt
proof/14-post-rollback-health.txt
proof/15-final-read-evidence.txt
```

The operator must actually:

- authenticate over trusted HTTPS;
- register/Test/Refresh the approved SQL target;
- prove the monitored login is non-sysadmin and does not require target DML/write privilege;
- recycle the IIS app pool and repeat health/auth;
- prove registration durability;
- prove protected credential durability/resolution after recycle;
- prove audit/history/incident operational-state durability;
- validate the real pre-cutover backup;
- rehearse application rollback;
- prove health/auth/read after rollback;
- return to the immutable RC.61 release directory;
- perform final health/auth/read verification.

Do not store passwords, tokens, connection strings, raw provider errors, SQL text or other secret-bearing material in evidence.

## Step 7 — explicit rollback rehearsal

Application-only rollback to the captured previous physical path; do not delete stable `App_Data` or Data Protection keys:

```powershell
Import-Module WebAdministration -ErrorAction Stop
$rollbackEvidence = Join-Path $proofRoot '13-rollback-rehearsal.txt'

@(
  "RollbackStartedUtc=$([DateTimeOffset]::UtcNow.ToString('O'))",
  "SiteName=$siteName",
  "AppPoolName=$appPoolName",
  "To=$previousPhysicalPath"
) | Set-Content $rollbackEvidence -Encoding utf8NoBOM

Stop-WebAppPool -Name $appPoolName
Set-ItemProperty -LiteralPath "IIS:\Sites\$siteName" -Name physicalPath -Value $previousPhysicalPath
Start-WebAppPool -Name $appPoolName
```

Require `/health/live=Live`, `/health/ready=Ready`, `/health=Ready`, authentication and durable reads. Save bounded proof to `proof/14-post-rollback-health.txt`.

Return to the already-created immutable RC.61 release directory; never repackage/re-extract it during the rehearsal:

```powershell
$rc61ReleasePath = Join-Path $releaseRoot $releaseVersion
if (-not (Test-Path -LiteralPath $rc61ReleasePath -PathType Container)) { throw 'STOP: RC.61 release directory missing.' }

Stop-WebAppPool -Name $appPoolName
Set-ItemProperty -LiteralPath "IIS:\Sites\$siteName" -Name physicalPath -Value $rc61ReleasePath
Start-WebAppPool -Name $appPoolName
```

If operational-state restore is required, use the validated backup dry-run/restore procedure in `docs/ROLLBACK_RUNBOOK.md`; never delete protected secrets/key rings or overwrite a newer schema as a shortcut.

## Step 8 — record exactly 15 real PASS gates

```powershell
$gateEvidence = [ordered]@{
  artifactChecksumVerified='proof/01-artifact-checksum.txt'
  iisPreflightPassed='proof/02-iis-preflight.txt'
  deploymentPlanReviewed='proof/03-deployment-plan.json'
  cutoverApplied='proof/04-cutover-apply.txt'
  trustedHttpsHealthPassed='proof/05-trusted-https-health.json'
  administratorAuthenticationPassed='proof/06-administrator-authentication.txt'
  leastPrivilegeSqlVerified='proof/07-least-privilege-sql.txt'
  iisRecyclePassed='proof/08-iis-recycle.txt'
  registrationDurabilityVerified='proof/09-registration-durability.txt'
  protectedCredentialDurabilityVerified='proof/10-protected-credential-durability.txt'
  operationalStateDurabilityVerified='proof/11-operational-state-durability.txt'
  operationalBackupValidated='proof/12-operational-backup-validated.txt'
  rollbackRehearsed='proof/13-rollback-rehearsal.txt'
  postRollbackHealthPassed='proof/14-post-rollback-health.txt'
  finalReadEvidencePassed='proof/15-final-read-evidence.txt'
}

foreach ($gate in $gateEvidence.GetEnumerator()) {
  $absolute = Join-Path (Split-Path -Parent $evidencePath) $gate.Value
  if (-not (Test-Path -LiteralPath $absolute -PathType Leaf)) { throw "STOP: missing real evidence for $($gate.Key)." }

  & "$acceptanceTools\Set-ProductionAcceptanceGate.ps1" `
    -EvidencePath $evidencePath `
    -ExpectedSessionManifestSha256 $sessionManifestSha256 `
    -GateName $gate.Key `
    -EvidenceFile $gate.Value `
    -AcknowledgePass
}
```

`-AcknowledgePass` is an explicit operator attestation after review of the actual operation. File presence alone is insufficient.

## Step 9 — finalizer and independent validator

Only after all 15 real gates:

```powershell
$final = & "$acceptanceTools\Complete-ProductionAcceptance.ps1" `
  -EvidencePath $evidencePath `
  -ExpectedSessionManifestSha256 $sessionManifestSha256 `
  -AcceptedBy $acceptedBy `
  -ClosureSummaryFile 'p0-5-closure-summary.json' `
  -AcknowledgeFinalAcceptance
$final | Format-List

$reviewPath = Join-Path (Split-Path -Parent $evidencePath) 'p0-5-independent-review.json'
$review = & "$acceptanceTools\Test-ProductionAcceptanceEvidence.ps1" `
  -EvidencePath $evidencePath `
  -EvidenceRoot (Split-Path -Parent $evidencePath) `
  -ClosureSummaryPath $reviewPath `
  -ExpectedSessionManifestSha256 $sessionManifestSha256
$review | Format-List
```

The only acceptable terminal PASS is:

```text
Production acceptance evidence PASS: 15/15 external gates verified with matching evidence hashes.
```

Required final identity includes:

```text
result                 PASS
version                0.1.0-rc.61
product SHA-256        d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
source commit          e28158da67b36dfc5dbf8f4c38b5c43d99c7c728
tested merge           158148d8bfd05f724014541bc7a0b1eab5dae1b5
deployment mode        SingleNode
required gate count    15
operator tooling       b422eaaee53d931a62a43b3c36a53b68cd4f3e27
session manifest SHA   exact independently preserved session value
```

Only then may #116 close. #111 closes only after #116.

## Authoritative #116 evidence paths

```text
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\session-manifest.json
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\session-manifest.sha256
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\candidate\Monitor-0.1.0-rc.61-win-x64.zip
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\candidate\Monitor-0.1.0-rc.61-win-x64.zip.sha256
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\evidence\p0-5-evidence-pack.json
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\evidence\proof\01..15
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\evidence\p0-5-closure-summary.json
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\evidence\p0-5-independent-review.json
C:\ProgramData\Monitor\App_Data\deployment-current.json
```

Preserve outside mutable session files: selected product SHA-256, exact toolkit commit, toolkit-manifest SHA-256 and returned session-manifest SHA-256.

### #116 STOP conditions

STOP, do not mark the affected gate PASS, and keep #116 OPEN if any of these occur:

- #162 incomplete or its durable identity differs;
- toolkit is not exact, clean, six-file and independently `Verified=True`;
- session is reused or is not exact `PreparedFailClosed` 0/15;
- backup/rollback point absent or unvalidated;
- IIS preflight fails;
- deployment plan is not actually human-reviewed;
- Apply, trusted health or authentication fails;
- monitored SQL requires sysadmin or target write/DML privilege;
- IIS recycle breaks health, registration, protected credential or operational state;
- rollback rehearsal or return-to-RC.61 validation fails;
- evidence is missing, secret-bearing, stale, from another session or hash-mismatched;
- recorder/finalizer/independent validator reports anything other than the exact real 15/15 PASS.

---

# Completion semantics

Do **not** claim `VERIFIED_FINAL_COMPLETE` while #162, #353, #116 or dependent #111 lacks its required real evidence.

Repository CI, synthetic 15/15 tests, a preview, a Green helper runtime, candidate packaging, documentation, or a merged PR is repository evidence only. It cannot manufacture OWNER_ONLY or EXTERNAL acceptance.
