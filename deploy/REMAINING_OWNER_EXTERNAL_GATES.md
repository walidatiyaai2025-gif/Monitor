# Remaining owner-only and external gates — executable fail-closed handoff

Live authority when this handoff was generated:

```text
Repository        walidatiyaai2025-gif/Monitor
Repository ID     1329517438
Exact main        8158b898db94679e9a620c44a5db77a3e5b237ba
Date              2026-09-09
```

Remaining direct execution gates are only:

1. **#162 — OWNER_ONLY:** preserve selected RC.61 as a durable GitHub Release and run a separate independent verifier.
2. **#353 — OWNER_ONLY / REPOSITORY_ADMIN:** apply and independently read back the exact required `main` protection policy.
3. **#116 — EXTERNAL_ENVIRONMENT:** real trusted-certificate Windows/IIS SingleNode acceptance with all 15 real gates.

Issue #111 is the parent closure only. It has no separate execution action and remains OPEN until #116 is accepted.

Nothing in this document, repository CI, a preview, a plan, or synthetic acceptance marks any owner-only/external gate PASS.

## Locked product and tooling identity

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

Fresh live state on 2026-09-09 is deliberately **NOT PASS**: artifact `9168574442` exists with `expired=false` and expiry `2026-09-12T04:41:34Z`; tag and release `v0.1.0-rc.61` are absent; `main` is unprotected; #162/#116/#353 are OPEN.

If the exact `main`, selected RC, product hash, source/tested-merge identity, or toolkit identity is intentionally superseded, STOP and replace/review this handoff before executing a mutation.

---

# #162 — OWNER_ONLY durable RC.61 release

This gate is retention/recoverability only. It is intentionally separate from #116 and cannot mark any of #116's 15 gates PASS.

## Prerequisites

- trusted operator workstation;
- PowerShell 7;
- `git` and authenticated `gh` with workflow/release permissions;
- network access to GitHub;
- clean checkout at exact `8158b898db94679e9a620c44a5db77a3e5b237ba`;
- source artifact `9168574442` still present/unexpired with the locked digest/provenance.

## Exact live-main guard

```powershell
$ErrorActionPreference = 'Stop'
$repo = 'walidatiyaai2025-gif/Monitor'
$handoffMain = '8158b898db94679e9a620c44a5db77a3e5b237ba'

if ((git rev-parse HEAD).Trim() -cne $handoffMain) { throw 'STOP: checkout SHA drift.' }
if (-not [string]::IsNullOrWhiteSpace((git status --porcelain=v1 --untracked-files=no))) { throw 'STOP: tracked checkout is dirty.' }
gh auth status
$remoteMain = (gh api "repos/$repo/branches/main" --jq '.commit.sha').Trim()
if ($remoteMain -cne $handoffMain) { throw "STOP: remote main moved to $remoteMain; refresh the handoff." }
```

## Preview — no mutation

```powershell
$preview = .\scripts\Invoke-Rc61DurablePromotion.ps1
$preview | Format-List
```

Require exactly:

```text
Status                            READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT
Version                           0.1.0-rc.61
ReleaseTag                        v0.1.0-rc.61
ProductSha256                     d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
WorkflowDispatchPerformed         False
IndependentVerificationDispatched False
ProductionMutationPerformed       False
MutatedGitHubState                False
```

Lower-level diagnosis remains read-only:

```powershell
.\scripts\Test-Rc61DurablePromotionPreflight.ps1 | Format-List
```

For a first publication attempt require `Status=READY_FOR_EXPLICIT_MANUAL_PROMOTION`, `MutatedGitHubState=False`, `TagExists=False`, `ReleaseExists=False`.

## Exact acknowledged promotion

```powershell
$promotion = .\scripts\Invoke-Rc61DurablePromotion.ps1 -AcknowledgePromotion
$promotion | Format-List
```

Require:

```text
Status                 PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED
PromotionRunStatus     completed
PromotionRunConclusion success
```

Capture only returned identity:

```powershell
$promotionRunId = [long]$promotion.PromotionRunId
$promotionRunUrl = [string]$promotion.PromotionRunUrl
$verifyCommand = [string]$promotion.IndependentVerificationCommand
if ($promotionRunId -le 0 -or [string]::IsNullOrWhiteSpace($promotionRunUrl)) { throw 'STOP: promotion run identity missing.' }
if ($verifyCommand -notmatch '^gh workflow run verify-durable-release\.yml ') { throw 'STOP: verifier command drift.' }
```

Ambiguous discovery, timeout, or failed exact run means **STOP / DO NOT REDISPATCH**.

## Separate independent verification action

The verifier must be a separate operator action. Execute the exact command returned above:

```powershell
Invoke-Expression $verifyCommand
```

Its authoritative locked inputs are:

```text
release_version          0.1.0-rc.61
release_tag              v0.1.0-rc.61
expected_commit          158148d8bfd05f724014541bc7a0b1eab5dae1b5
expected_product_sha256  d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
```

Capture exactly one successful verification run ID. Any ambiguity means STOP, not redispatch.

## Bind exact Green run IDs

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

This is still **0/15 external production gates**.

## Durable asset verification and evidence

After both exact workflow runs are Green:

```powershell
$evidence162 = 'C:\ProgramData\Monitor\OwnerEvidence\162'
$assetRoot = Join-Path $evidence162 'durable-assets'
if (Test-Path -LiteralPath $evidence162) { throw 'STOP: #162 evidence root already exists; preserve the prior attempt.' }
New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null

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

Before closing #162 independently prove: tag resolves to tested merge `158148...`; release contains exactly the ZIP + `.sha256`; durable ZIP hash equals the locked product SHA; checksum line is canonical; both exact run IDs/URLs are Green and recorded.

### #162 rollback/abort

There is **no post-publication rollback command by design**. Deleting/recreating a durable tag/release would destroy chain of custody. If the exact promotion workflow is still running and no durable mutation is known to have occurred, abort only that exact run:

```powershell
gh run cancel $promotionRunId --repo 'walidatiyaai2025-gif/Monitor'
```

If dispatch/run discovery or durable state is ambiguous, do not cancel/delete/recreate/redispatch blindly; STOP and inspect the exact run/tag/release state.

### #162 STOP conditions

STOP and keep #162 OPEN for any main/checkout drift, artifact expiry/missing/digest/provenance drift, auth/API ambiguity, unexpected existing tag/release on a first attempt, non-exact preview, ambiguous/failed promotion, non-separate/failed verifier, tag/assets/hash/checksum mismatch, or readiness output differing from the exact state above.

---

# #353 — OWNER_ONLY repository-admin branch protection

This gate is repository governance only. It does not satisfy #162/#116/#111.

## Prerequisites

- trusted repository-admin workstation;
- PowerShell 7 and authenticated `gh` with administration permission;
- clean exact checkout and remote `main` at `8158b898db94679e9a620c44a5db77a3e5b237ba`;
- live starting state still `protected=false`.

Required provider-bound checks:

```text
build                       app_id 15368
protected-p0-pr-metadata    app_id 15368
protected-p0-pr-commits     app_id 15368
```

Pinned evidence for this handoff is merged PR #464 head `cc39086ae4864fc149c299468fddf842d9e4aa7a`.

## Preview

Run the same exact-main guard from #162, then:

```powershell
$preview = .\scripts\Set-MainBranchProtection.ps1
$preview | Format-List
```

For this known unprotected baseline require:

```text
Status                         READY_FOR_EXPLICIT_BRANCH_PROTECTION_ACKNOWLEDGEMENT
CurrentProtected               False
StrictRequiredChecks           True
EnforceAdmins                  True
ConversationResolutionRequired True
ForcePushesAllowed             False
DeletionsAllowed               False
MutationPerformed              False
ExternalProductionGatesPassed  0
```

`RequiredCheckBindings` must contain only the three contexts above with `AppId=15368`, `EvidencePullRequest=464`, `EvidenceHeadSha=cc39086ae4864fc149c299468fddf842d9e4aa7a` for this pinned main.

## Apply

```powershell
$result = .\scripts\Set-MainBranchProtection.ps1 -AcknowledgeProtection
$result | Format-List
```

Require:

```text
Status                         BRANCH_PROTECTION_APPLIED_AND_VERIFIED
MutationPerformed              True
StrictRequiredChecks           True
EnforceAdmins                  True
ConversationResolutionRequired True
ForcePushesAllowed             False
DeletionsAllowed               False
ExternalProductionGatesPassed  0
```

## Independent read-back evidence

```powershell
$evidence353 = 'C:\ProgramData\Monitor\OwnerEvidence\353'
if (Test-Path -LiteralPath $evidence353) { throw 'STOP: #353 evidence root already exists.' }
New-Item -ItemType Directory -Path $evidence353 -Force | Out-Null

gh api 'repos/walidatiyaai2025-gif/Monitor/branches/main' | Set-Content "$evidence353\branch.json" -Encoding utf8NoBOM
gh api 'repos/walidatiyaai2025-gif/Monitor/branches/main/protection' | Set-Content "$evidence353\protection.json" -Encoding utf8NoBOM
```

Only close #353 after independent read-back proves `protected=true`, `required_status_checks.strict=true`, exactly the three provider-bound checks above, `enforce_admins.enabled=true`, `required_conversation_resolution.enabled=true`, `allow_force_pushes.enabled=false`, and `allow_deletions.enabled=false`.

## Exact rollback to captured baseline

This rollback is valid **only** if the preview proved the starting policy was the exact known unprotected baseline. Otherwise preserve/reconstruct the real pre-state rather than using this command.

```powershell
gh api --method DELETE 'repos/walidatiyaai2025-gif/Monitor/branches/main/protection'
```

Then independently require branch `.protected=false` and an actual 404 from the protection endpoint.

### #353 STOP conditions

STOP and keep #353 OPEN if main moved, starting protection unexpectedly differs, the helper cannot bind current main to exactly one merged same-repo PR, any required check/provider is missing/failed/ambiguous, app ID differs from `15368`, repository-admin access is unavailable, or independent read-back differs from the required policy.

---

# #116 — EXTERNAL trusted-IIS real 15/15 acceptance

#116 is a real production-environment gate. **Do not perform production mutation while #162 is OPEN.** Durable release #162 stays separate from these 15 gates and never counts as one of them.

## Required environment/prerequisites

- #162 actually complete with exact promotion + separate verifier + durable tag/assets/hash evidence;
- intended Windows Server production host, elevated PowerShell 7;
- IIS WebAdministration, .NET 8 ASP.NET Core Hosting Bundle and ANCM v2;
- IIS site/app pool (normally `Monitor`/`Monitor`) with No Managed Code and ApplicationPoolIdentity or approved dedicated SpecificUser; never LocalSystem/LocalService/NetworkService;
- trusted machine certificate in `Cert:\LocalMachine\My`, private key available, >24h validity, exact HTTPS host binding;
- secret-free approved `appsettings.Production.json`, `Deployment:Mode=SingleNode`, exact AllowedHosts, no embedded passwords/hashes/connection strings;
- stable state root `C:\ProgramData\Monitor\App_Data` outside release root;
- validated real pre-cutover operational backup ID and captured rollback physical path;
- approved non-sysadmin least-privilege monitored SQL login;
- exact RC.61 durable ZIP/checksum;
- separately exported/verified Acceptance Control Toolkit from exact `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`.

## Re-prove #162 readiness — still 0/15

```powershell
$promotionRunId = Read-Host 'Exact successful #162 promotion run ID'
$verificationRunId = Read-Host 'Exact successful #162 verifier run ID'
if ($promotionRunId -notmatch '^[1-9][0-9]*$' -or $verificationRunId -notmatch '^[1-9][0-9]*$') { throw 'STOP: valid #162 run IDs required.' }
$rc61Ready = .\scripts\Test-Rc61CutoverReadiness.ps1 -PromotionRunId ([long]$promotionRunId) -VerificationRunId ([long]$verificationRunId)
if ($rc61Ready.Status -cne 'READY_FOR_P0_5_PRE_CUTOVER_PREPARATION' -or -not [bool]$rc61Ready.DurableReleasePrerequisiteSatisfied -or [int]$rc61Ready.ExternalGatesPassed -ne 0) { throw 'STOP: #162 prerequisite not exact.' }
```

## Obtain and hash durable bytes

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

## Capture real host inputs and run candidate preflight

```powershell
$hostName = Read-Host 'Exact production DNS hostname, no scheme/port'
$certificateThumbprint = Read-Host 'Approved LocalMachine/My certificate thumbprint'
$operationalBackupId = Read-Host 'Validated pre-cutover operational backup ID'
$productionConfigPath = Read-Host 'Absolute approved secret-free appsettings.Production.json path'
$acceptedBy = Read-Host 'Approved final operator identity'
$siteName = 'Monitor'; $appPoolName = 'Monitor'; $stateRoot = 'C:\ProgramData\Monitor\App_Data'; $releaseRoot = 'C:\Program Files\Monitor\releases'; $releaseVersion = '0.1.0-rc.61'; $baseUri = [Uri]("https://$hostName/")
if ([string]::IsNullOrWhiteSpace($operationalBackupId)) { throw 'STOP: real validated backup ID required.' }
if (-not (Test-Path -LiteralPath $productionConfigPath -PathType Leaf)) { throw 'STOP: production config missing.' }

$preflight = & (Join-Path $candidateOps 'Test-IisProductionPrerequisites.ps1') -HostName $hostName -CertificateThumbprint $certificateThumbprint -SiteName $siteName -AppPoolName $appPoolName -HttpsPort 443 -PassThru
if (-not [bool]$preflight.Ready) { throw 'STOP: IIS preflight not Ready.' }
$previousPhysicalPath = [string]$preflight.SitePhysicalPath

Import-Module WebAdministration -ErrorAction Stop
$pool = Get-Item -LiteralPath "IIS:\AppPools\$appPoolName"
$appPoolIdentity = if ([string]$pool.processModel.identityType -ceq 'ApplicationPoolIdentity') { "IIS AppPool\$appPoolName" } else { [string]$pool.processModel.userName }
if ([string]::IsNullOrWhiteSpace($appPoolIdentity)) { throw 'STOP: app-pool identity ambiguous.' }
```

Expected preflight result is `Ready=True`; non-PassThru output ends with `Monitor IIS production prerequisites passed. No configuration was changed.`

## Export and verify the separate six-script sidecar

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
try { $toolkit = .\scripts\Export-ProductionAcceptanceToolkit.ps1 -ExpectedToolingCommit $operatorToolingCommit -OutputDirectory $acceptanceTools } finally { Pop-Location }
$operatorToolkitManifestSha256 = [string]$toolkit.ToolkitManifestSha256
$toolkitCheck = & "$acceptanceTools\Test-ProductionAcceptanceToolkit.ps1" -ToolkitRoot $acceptanceTools -ExpectedToolingCommit $operatorToolingCommit -ExpectedToolkitManifestSha256 $operatorToolkitManifestSha256
if (-not [bool]$toolkitCheck.Verified -or [int]$toolkitCheck.FileCount -ne 6) { throw 'STOP: toolkit verification failed.' }
```

Require `ToolingCommit=b422...`, `FileCount=6`, `Verified=True`. Preserve the toolkit-manifest SHA-256 outside the mutable session.

## Create exactly one fresh 0/15 session

```powershell
$sessionRoot = 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-61'
if (Test-Path -LiteralPath $sessionRoot) { throw 'STOP: acceptance session already exists; never reuse it.' }
New-Item -ItemType Directory -Path (Split-Path -Parent $sessionRoot) -Force | Out-Null

$session = & "$acceptanceTools\New-ProductionAcceptanceSession.ps1" `
  -SessionRoot $sessionRoot -ArtifactPath $artifact -ChecksumPath $checksum -CandidateVersion '0.1.0-rc.61' `
  -ExpectedProductSha256 $expectedProductSha256 -SourceCommit 'e28158da67b36dfc5dbf8f4c38b5c43d99c7c728' `
  -TestedMergeCommit '158148d8bfd05f724014541bc7a0b1eab5dae1b5' -OperatorToolingCommit $operatorToolingCommit `
  -ExpectedOperatorToolkitManifestSha256 $operatorToolkitManifestSha256 -HostName $hostName -SiteName $siteName `
  -AppPoolName $appPoolName -AppPoolIdentity $appPoolIdentity -CertificateThumbprint $certificateThumbprint `
  -OperationalBackupId $operationalBackupId -PreviousPhysicalPath $previousPhysicalPath -StateRoot $stateRoot

$sessionManifestSha256 = [string]$session.ManifestSha256
$evidencePath = [string]$session.EvidencePath
$proofRoot = Join-Path (Split-Path -Parent $evidencePath) 'proof'
if ([int]$session.ExternalGateCount -ne 15 -or [int]$session.ExternalGatesPassed -ne 0 -or [bool]$session.ProductionAccepted) { throw 'STOP: session not exact fail-closed 0/15.' }
```

Require session manifest `status=PreparedFailClosed`, `ExternalGateCount=15`, `ExternalGatesPassed=0`, `ProductionAccepted=False`. Preserve `$sessionManifestSha256` independently.

Create the real artifact-hash proof from the independently re-hashed bytes:

```powershell
@(
  'Artifact=Monitor-0.1.0-rc.61-win-x64.zip',
  "ExpectedSha256=$expectedProductSha256",
  "ActualSha256=$actualProductSha256",
  'ChecksumCanonical=True'
) | Set-Content -LiteralPath (Join-Path $proofRoot '01-artifact-checksum.txt') -Encoding utf8NoBOM
```

Capture preflight proof only from the actual real preflight result:

```powershell
$preflight | Format-List | Out-String | Set-Content -LiteralPath (Join-Path $proofRoot '02-iis-preflight.txt') -Encoding utf8NoBOM
```

## PLAN ONLY then explicit Apply

```powershell
$planEvidence = Join-Path $proofRoot '03-deployment-plan.json'
& (Join-Path $candidateOps 'Deploy-ProductionSingleNode.ps1') `
  -ArtifactPath $artifact -ChecksumPath $checksum -ReleaseVersion $releaseVersion -ProductionConfigPath $productionConfigPath `
  -OperationalBackupId $operationalBackupId -HostName $hostName -CertificateThumbprint $certificateThumbprint `
  -SiteName $siteName -AppPoolName $appPoolName -ReleaseRoot $releaseRoot -StateRoot $stateRoot -HttpsPort 443 |
  Tee-Object -FilePath $planEvidence
```

Require exact terminal statements: `PLAN ONLY. No IIS, filesystem, ACL, application-pool, binding, certificate, configuration or state changes were made.` and `Re-run with -Apply only after reviewing this plan and the pre-cutover operational backup.` A human must actually review the plan before that gate can be PASS.

Apply only after that review:

```powershell
$applyEvidence = Join-Path $proofRoot '04-cutover-apply.txt'
$healthEvidence = Join-Path $proofRoot '05-trusted-https-health.json'
& (Join-Path $candidateOps 'Deploy-ProductionSingleNode.ps1') `
  -ArtifactPath $artifact -ChecksumPath $checksum -ReleaseVersion $releaseVersion -ProductionConfigPath $productionConfigPath `
  -OperationalBackupId $operationalBackupId -HostName $hostName -CertificateThumbprint $certificateThumbprint `
  -SiteName $siteName -AppPoolName $appPoolName -ReleaseRoot $releaseRoot -StateRoot $stateRoot -HttpsPort 443 `
  -EvidencePath $healthEvidence -Apply 2>&1 | Tee-Object -FilePath $applyEvidence
```

Success includes `Monitor SingleNode candidate '0.1.0-rc.61' is active on https://<host>/` and the retained previous physical path. Immediate acceptance failure automatically attempts to restore the previous physical path.

## Required real evidence files

The remaining real external journey must actually be performed and recorded, without passwords/connection strings/tokens/raw provider errors/SQL text/secrets:

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

The operator must really: authenticate over trusted HTTPS; register/Test/Refresh the approved target; prove non-sysadmin/no target DML requirement; recycle the app pool and repeat health/auth; prove registration, protected credential and audit/history/incident state durability; validate the real backup; rehearse rollback; return to RC.61; and perform final health/auth/read verification.

## Explicit rollback rehearsal

Application-only rollback to the captured previous physical path; stable `App_Data` and Data Protection keys are not deleted:

```powershell
Import-Module WebAdministration -ErrorAction Stop
$rollbackEvidence = Join-Path $proofRoot '13-rollback-rehearsal.txt'
@("RollbackStartedUtc=$([DateTimeOffset]::UtcNow.ToString('O'))","SiteName=$siteName","AppPoolName=$appPoolName","To=$previousPhysicalPath") | Set-Content $rollbackEvidence -Encoding utf8NoBOM
Stop-WebAppPool -Name $appPoolName
Set-ItemProperty -LiteralPath "IIS:\Sites\$siteName" -Name physicalPath -Value $previousPhysicalPath
Start-WebAppPool -Name $appPoolName
```

Then require `/health/live=Live`, `/health/ready=Ready`, `/health=Ready`, authentication and durable reads. Save bounded proof in `proof/14-post-rollback-health.txt`.

After a successful rollback rehearsal, return to the already-created immutable RC.61 release directory; do not repackage/re-extract it:

```powershell
$rc61ReleasePath = Join-Path $releaseRoot $releaseVersion
if (-not (Test-Path -LiteralPath $rc61ReleasePath -PathType Container)) { throw 'STOP: RC.61 release directory missing.' }
Stop-WebAppPool -Name $appPoolName
Set-ItemProperty -LiteralPath "IIS:\Sites\$siteName" -Name physicalPath -Value $rc61ReleasePath
Start-WebAppPool -Name $appPoolName
```

If operational-state restore is needed, follow the validated backup dry-run/restore procedure in `docs/ROLLBACK_RUNBOOK.md`; never delete protected secrets/key rings or overwrite a newer state schema as a shortcut.

## Record exactly 15 real PASS gates

```powershell
$gateEvidence = [ordered]@{
  artifactChecksumVerified='proof/01-artifact-checksum.txt'; iisPreflightPassed='proof/02-iis-preflight.txt'; deploymentPlanReviewed='proof/03-deployment-plan.json';
  cutoverApplied='proof/04-cutover-apply.txt'; trustedHttpsHealthPassed='proof/05-trusted-https-health.json'; administratorAuthenticationPassed='proof/06-administrator-authentication.txt';
  leastPrivilegeSqlVerified='proof/07-least-privilege-sql.txt'; iisRecyclePassed='proof/08-iis-recycle.txt'; registrationDurabilityVerified='proof/09-registration-durability.txt';
  protectedCredentialDurabilityVerified='proof/10-protected-credential-durability.txt'; operationalStateDurabilityVerified='proof/11-operational-state-durability.txt';
  operationalBackupValidated='proof/12-operational-backup-validated.txt'; rollbackRehearsed='proof/13-rollback-rehearsal.txt'; postRollbackHealthPassed='proof/14-post-rollback-health.txt';
  finalReadEvidencePassed='proof/15-final-read-evidence.txt'
}
foreach ($gate in $gateEvidence.GetEnumerator()) {
  $absolute = Join-Path (Split-Path -Parent $evidencePath) $gate.Value
  if (-not (Test-Path -LiteralPath $absolute -PathType Leaf)) { throw "STOP: missing real evidence for $($gate.Key)." }
  & "$acceptanceTools\Set-ProductionAcceptanceGate.ps1" -EvidencePath $evidencePath -ExpectedSessionManifestSha256 $sessionManifestSha256 -GateName $gate.Key -EvidenceFile $gate.Value -AcknowledgePass
}
```

`-AcknowledgePass` is an explicit real-operator attestation after reviewing the actual operation; file presence is not sufficient.

## Finalizer and independent validator

Only after all 15 real gates:

```powershell
$final = & "$acceptanceTools\Complete-ProductionAcceptance.ps1" -EvidencePath $evidencePath -ExpectedSessionManifestSha256 $sessionManifestSha256 -AcceptedBy $acceptedBy -ClosureSummaryFile 'p0-5-closure-summary.json' -AcknowledgeFinalAcceptance
$final | Format-List

$reviewPath = Join-Path (Split-Path -Parent $evidencePath) 'p0-5-independent-review.json'
$review = & "$acceptanceTools\Test-ProductionAcceptanceEvidence.ps1" -EvidencePath $evidencePath -EvidenceRoot (Split-Path -Parent $evidencePath) -ClosureSummaryPath $reviewPath -ExpectedSessionManifestSha256 $sessionManifestSha256
$review | Format-List
```

Required terminal PASS output:

```text
Production acceptance evidence PASS: 15/15 external gates verified with matching evidence hashes.
```

Required summary identity includes `result=PASS`, version `0.1.0-rc.61`, exact ZIP/hash/source/tested merge above, `deploymentMode=SingleNode`, `requiredGateCount=15`, the independently preserved session-manifest SHA, and `operatorToolingCommit=b422eaaee53d931a62a43b3c36a53b68cd4f3e27`.

Only then may #116 close; #111 closes only after #116.

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

STOP, do not mark the affected gate PASS, and keep #116 OPEN if #162 is incomplete; durable identity differs; toolkit is not exact/clean/Verified=True/6 files; session is reused or not 0/15; backup/rollback point is absent; IIS preflight fails; plan is not human-reviewed; Apply/health/auth fails; monitored SQL requires sysadmin/write/DML; recycle breaks health/registration/credential/state; rollback or return-to-candidate checks fail; evidence is missing/secret-bearing/stale/from another session/hash-mismatched; or recorder/finalizer/independent validator reports anything other than the exact real 15/15 PASS.
