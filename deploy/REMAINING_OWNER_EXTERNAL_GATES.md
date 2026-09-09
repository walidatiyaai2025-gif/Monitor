# Remaining owner-only and external gates — executable fail-closed handoff

Generated against exact live `main`:

- repository: `walidatiyaai2025-gif/Monitor`
- repository ID: `1329517438`
- exact main SHA: `8158b898db94679e9a620c44a5db77a3e5b237ba`
- generated state date: `2026-09-09`

This handoff covers every currently remaining direct owner-only/external gate found by the live open-issue audit:

1. **#162 — OWNER_ONLY:** durable publication and independent verification of selected RC.61.
2. **#353 — OWNER_ONLY / REPOSITORY_ADMIN:** apply and independently read back exact `main` branch protection.
3. **#116 — EXTERNAL_ENVIRONMENT:** real trusted-certificate Windows/IIS SingleNode 15/15 acceptance, strictly after #162.

Issue #111 is the parent P0 closure and has no independent execution action: it stays OPEN until #116 is actually accepted.

No section below marks an owner-only or external gate PASS. Commands, repository CI, previews, plans, synthetic tests and this document are preparation only.

---

## Global immutable identities

```text
Repository                walidatiyaai2025-gif/Monitor
Repository ID             1329517438
Handoff main              8158b898db94679e9a620c44a5db77a3e5b237ba
RC version                0.1.0-rc.61
RC tag                    v0.1.0-rc.61
RC artifact               Monitor-0.1.0-rc.61-win-x64.zip
RC checksum               Monitor-0.1.0-rc.61-win-x64.zip.sha256
RC product SHA-256        d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
Actions source run         31667721306
Actions artifact ID       9168574442
Actions artifact name     Monitor-0.1.0-rc.61-win-x64
Outer artifact digest     sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382
Source commit             e28158da67b36dfc5dbf8f4c38b5c43d99c7c728
Tested merge commit       158148d8bfd05f724014541bc7a0b1eab5dae1b5
Acceptance toolkit commit b422eaaee53d931a62a43b3c36a53b68cd4f3e27
```

Live state at generation time is deliberately **NOT PASS**:

- source artifact `9168574442` exists, `expired=false`, and expires `2026-09-12T04:41:34Z`;
- tag `v0.1.0-rc.61` is absent;
- GitHub Release `v0.1.0-rc.61` is absent;
- `main` reports `protected=false`;
- #162, #116 and #353 remain OPEN.

If any immutable identity above is intentionally superseded, stop and replace this handoff in a reviewed repository change before executing an owner/external mutation.

---

# Gate #162 — durable RC.61 publication and independent verification

## Authority and separation

#162 preserves selected bytes as durable GitHub Release assets. It is repository/recoverability work only. It must not deploy IIS, create production acceptance evidence, or mark any of #116's 15 external gates PASS.

## Required operator environment

- trusted operator workstation;
- authenticated GitHub CLI `gh` with permission to dispatch the approved workflows and create the approved release/tag through those workflows;
- clean checkout of this repository at exact handoff main SHA `8158b898db94679e9a620c44a5db77a3e5b237ba`;
- PowerShell 7;
- network access to GitHub API/Actions;
- source Actions artifact `9168574442` still unexpired and matching the locked digest/provenance.

## Exact checkout/live-main guard

Run from the repository root before the preview:

```powershell
$ErrorActionPreference = 'Stop'
$repo = 'walidatiyaai2025-gif/Monitor'
$handoffMain = '8158b898db94679e9a620c44a5db77a3e5b237ba'

if ((git rev-parse HEAD).Trim() -cne $handoffMain) {
    throw "STOP: operator checkout is not exact handoff main $handoffMain."
}
if (-not [string]::IsNullOrWhiteSpace((git status --porcelain=v1 --untracked-files=no))) {
    throw 'STOP: tracked checkout is dirty.'
}
gh auth status
$remoteMain = (gh api "repos/$repo/branches/main" --jq '.commit.sha').Trim()
if ($remoteMain -cne $handoffMain) {
    throw "STOP: remote main moved to $remoteMain. Refresh/review the handoff before owner mutation."
}
```

## Step 0 — exact read-only preview

```powershell
.\scripts\Invoke-Rc61DurablePromotion.ps1
```

Required output values:

```text
Status                           READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT
Version                          0.1.0-rc.61
ReleaseTag                       v0.1.0-rc.61
ProductSha256                    d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
WorkflowDispatchPerformed        False
IndependentVerificationDispatched False
ProductionMutationPerformed      False
MutatedGitHubState               False
```

Optional lower-level diagnosis, still read-only:

```powershell
.\scripts\Test-Rc61DurablePromotionPreflight.ps1 | Format-List
```

For the first publication attempt require:

```text
Status             READY_FOR_EXPLICIT_MANUAL_PROMOTION
MutatedGitHubState False
TagExists           False
ReleaseExists       False
```

## Step 1 — exact acknowledged owner mutation

Only after Step 0 is exact:

```powershell
$promotion = .\scripts\Invoke-Rc61DurablePromotion.ps1 -AcknowledgePromotion
$promotion | Format-List
```

Required successful state:

```text
Status              PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED
PromotionRunStatus  completed
PromotionRunConclusion success
```

Retain these dynamic outputs without guessing or redispatching:

```powershell
$promotionRunId = [long]$promotion.PromotionRunId
$promotionRunUrl = [string]$promotion.PromotionRunUrl
$independentVerificationCommand = [string]$promotion.IndependentVerificationCommand

if ($promotionRunId -le 0 -or [string]::IsNullOrWhiteSpace($promotionRunUrl)) {
    throw 'STOP: exact promotion run identity was not returned.'
}
```

## Step 2 — separate independent verification action

The verifier is a separate owner action. First require that the helper-returned command is exactly the approved workflow family, then execute that returned command:

```powershell
if ($independentVerificationCommand -notmatch '^gh workflow run verify-durable-release\.yml ') {
    throw 'STOP: helper returned an unexpected independent verification command.'
}
Invoke-Expression $independentVerificationCommand
```

The authoritative verifier inputs are exactly:

```text
release_version          0.1.0-rc.61
release_tag              v0.1.0-rc.61
expected_commit          158148d8bfd05f724014541bc7a0b1eab5dae1b5
expected_product_sha256  d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
```

For audit only, the equivalent dispatch shape is:

```powershell
gh workflow run verify-durable-release.yml `
  --repo 'walidatiyaai2025-gif/Monitor' `
  --ref main `
  -f 'release_version=0.1.0-rc.61' `
  -f 'release_tag=v0.1.0-rc.61' `
  -f 'expected_commit=158148d8bfd05f724014541bc7a0b1eab5dae1b5' `
  -f 'expected_product_sha256=d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5'
```

Do not use the audit form to bypass any helper/preflight stop condition. Capture exactly one Green verification run ID; ambiguity means STOP, not redispatch.

## Step 3 — bind the two exact Green runs

```powershell
$verificationRunId = Read-Host 'Exact successful verify-durable-release run ID'
if ($verificationRunId -notmatch '^[1-9][0-9]*$') { throw 'STOP: invalid verification run ID.' }

$readiness = .\scripts\Test-Rc61CutoverReadiness.ps1 `
  -PromotionRunId $promotionRunId `
  -VerificationRunId ([long]$verificationRunId)
$readiness | Format-List
```

Required PASS-like readiness output — note that this is still **0 production gates**:

```text
Status                              READY_FOR_P0_5_PRE_CUTOVER_PREPARATION
DurableReleasePrerequisiteSatisfied True
ExternalGatesPassed                 0
ProductionMutationPerformed         False
MutatedGitHubState                  False
```

#162 is not complete until both exact workflow runs are Green **and** the live tag/release/assets/hash are independently verified.

## Exact durable asset acceptance

After the separate verifier is Green, require all of the following:

- tag `v0.1.0-rc.61` resolves to `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- release contains exactly:
  - `Monitor-0.1.0-rc.61-win-x64.zip`
  - `Monitor-0.1.0-rc.61-win-x64.zip.sha256`
- downloaded ZIP SHA-256 is exactly `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- companion checksum line is canonical and points to the exact ZIP name.

Independent local verification:

```powershell
$verifyRoot = 'C:\ProgramData\Monitor\OwnerEvidence\162\durable-assets'
if (Test-Path -LiteralPath $verifyRoot) { throw 'STOP: durable asset verification root already exists; preserve the prior evidence.' }
New-Item -ItemType Directory -Path $verifyRoot -Force | Out-Null

gh release download 'v0.1.0-rc.61' --repo $repo --pattern 'Monitor-0.1.0-rc.61-win-x64.zip' --dir $verifyRoot
gh release download 'v0.1.0-rc.61' --repo $repo --pattern 'Monitor-0.1.0-rc.61-win-x64.zip.sha256' --dir $verifyRoot

$zip = Join-Path $verifyRoot 'Monitor-0.1.0-rc.61-win-x64.zip'
$shaFile = "$zip.sha256"
$expectedSha = 'd0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5'
$actualSha = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualSha -cne $expectedSha) { throw "STOP: durable ZIP SHA drift: $actualSha" }
$expectedLine = "$expectedSha  Monitor-0.1.0-rc.61-win-x64.zip"
if ((Get-Content -LiteralPath $shaFile -Raw).Trim() -cne $expectedLine) {
    throw 'STOP: durable companion checksum is not canonical.'
}
```

## Evidence paths for #162

Retain non-secret evidence under:

```text
C:\ProgramData\Monitor\OwnerEvidence\162\promotion-run.json
C:\ProgramData\Monitor\OwnerEvidence\162\verification-run.json
C:\ProgramData\Monitor\OwnerEvidence\162\tag.json
C:\ProgramData\Monitor\OwnerEvidence\162\release.json
C:\ProgramData\Monitor\OwnerEvidence\162\durable-assets\Monitor-0.1.0-rc.61-win-x64.zip
C:\ProgramData\Monitor\OwnerEvidence\162\durable-assets\Monitor-0.1.0-rc.61-win-x64.zip.sha256
```

Example exact capture after both run IDs are known:

```powershell
$evidence162 = 'C:\ProgramData\Monitor\OwnerEvidence\162'
New-Item -ItemType Directory -Path $evidence162 -Force | Out-Null
gh api "repos/$repo/actions/runs/$promotionRunId" | Set-Content "$evidence162\promotion-run.json" -Encoding utf8NoBOM
gh api "repos/$repo/actions/runs/$verificationRunId" | Set-Content "$evidence162\verification-run.json" -Encoding utf8NoBOM
gh api "repos/$repo/git/ref/tags/v0.1.0-rc.61" | Set-Content "$evidence162\tag.json" -Encoding utf8NoBOM
gh api "repos/$repo/releases/tags/v0.1.0-rc.61" | Set-Content "$evidence162\release.json" -Encoding utf8NoBOM
```

Record the two run IDs, URLs, exact tag target, exact-two assets and local SHA result on Issue #162 before closing it.

## Rollback / abort for #162

**Post-publication automatic rollback command: NONE by design.** Durable release/tag state is immutable retention evidence; deleting/recreating it would destroy the chain of custody.

If a workflow is still running and no durable mutation is known to have occurred, an operator may abort the exact run:

```powershell
gh run cancel $promotionRunId --repo 'walidatiyaai2025-gif/Monitor'
```

This is an abort, not a rollback. If the run was dispatched and discovery/status is ambiguous, or any tag/release/assets may already exist, **do not cancel/delete/recreate/redispatch blindly**. Stop and independently inspect the exact run and durable state.

## Explicit #162 STOP conditions

STOP and keep #162 OPEN on any of these:

- checkout or remote `main` differs from `8158b898db94679e9a620c44a5db77a3e5b237ba`;
- source artifact expired, missing, different ID/name/digest/repository/source SHA;
- preview/preflight status differs from the exact required values;
- tag or release already exists before a first publication attempt;
- GitHub auth/network/API result is ambiguous;
- promotion run discovery is ambiguous or exact promotion run is not Green;
- verifier was not a separate operator action or is not Green;
- tag target, exact-two assets, ZIP hash or canonical checksum differ;
- readiness does not return exact `READY_FOR_P0_5_PRE_CUTOVER_PREPARATION` with `ExternalGatesPassed=0`.

---

# Gate #353 — repository-admin `main` protection

## Authority and separation

#353 is repository governance only. It does not publish RC.61 and does not satisfy #162, #116 or #111.

## Required operator environment

- trusted repository-admin workstation;
- authenticated `gh` identity with administration permission on this repository;
- PowerShell 7;
- clean exact checkout `8158b898db94679e9a620c44a5db77a3e5b237ba`;
- remote `main` must still equal that exact SHA for this handoff;
- live starting state for this handoff must still be unprotected (`protected=false`).

The required provider-bound checks are exactly:

```text
build                       app_id 15368
protected-p0-pr-metadata    app_id 15368
protected-p0-pr-commits     app_id 15368
```

Current provider evidence comes from merged PR #464 exact head `cc39086ae4864fc149c299468fddf842d9e4aa7a`.

## Exact preview

Use the same checkout/live-main guard from #162, then:

```powershell
$protectionPreview = .\scripts\Set-MainBranchProtection.ps1
$protectionPreview | Format-List
```

For the current known unprotected baseline require:

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

Inspect `RequiredCheckBindings`; it must contain exactly the three contexts above, each with `AppId=15368`, `EvidencePullRequest=464` and `EvidenceHeadSha=cc39086ae4864fc149c299468fddf842d9e4aa7a` for this pinned handoff.

## Exact owner mutation

```powershell
$protectionResult = .\scripts\Set-MainBranchProtection.ps1 -AcknowledgeProtection
$protectionResult | Format-List
```

Required output:

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

## Independent read-back and evidence paths

```powershell
$evidence353 = 'C:\ProgramData\Monitor\OwnerEvidence\353'
if (Test-Path -LiteralPath $evidence353) { throw 'STOP: #353 evidence root already exists; preserve prior evidence.' }
New-Item -ItemType Directory -Path $evidence353 -Force | Out-Null

gh api 'repos/walidatiyaai2025-gif/Monitor/branches/main' |
  Set-Content "$evidence353\branch.json" -Encoding utf8NoBOM

gh api 'repos/walidatiyaai2025-gif/Monitor/branches/main/protection' |
  Set-Content "$evidence353\protection.json" -Encoding utf8NoBOM
```

Independent PASS evidence must prove:

- branch `protected=true`;
- `required_status_checks.strict=true`;
- exact `checks` set = only the three approved contexts;
- every context has `app_id=15368`;
- `enforce_admins.enabled=true`;
- `required_conversation_resolution.enabled=true`;
- `allow_force_pushes.enabled=false`;
- `allow_deletions.enabled=false`.

Record timestamp, exact `main` SHA, three context/app bindings and both read-back files on #353 before closing it.

## Exact rollback command for #353

The live baseline used by this handoff is **no branch protection**. Therefore rollback to the captured pre-state is permitted only if the preview proved `CurrentProtected=False` and remote main was exactly the pinned handoff SHA before mutation:

```powershell
gh api --method DELETE 'repos/walidatiyaai2025-gif/Monitor/branches/main/protection'
```

Then independently require:

```powershell
gh api 'repos/walidatiyaai2025-gif/Monitor/branches/main' --jq '.protected'
```

to return `false`, and the protection endpoint to return an actual 404. If the starting state was not exactly the known unprotected baseline, this canned rollback is prohibited: capture/review the real pre-state instead.

## Explicit #353 STOP conditions

STOP and keep #353 OPEN on any of these:

- remote/main checkout SHA drift;
- starting branch unexpectedly already protected or has a policy not represented by this handoff;
- helper cannot bind current main to exactly one merged same-repository PR;
- any required check is missing/failed/ambiguous or provider app ID is not exactly `15368`;
- preview differs from exact required policy;
- mutation returns but independent read-back differs;
- repository-admin API access is denied/ambiguous.

---

# Gate #116 — real trusted-IIS SingleNode 15/15 acceptance

## Hard dependency and immutable product/tool identities

**Do not perform any production mutation while #162 is OPEN.** #162 durable publication and separate verification must be complete first.

Product identity used by the production host:

```text
Version            0.1.0-rc.61
Tag                v0.1.0-rc.61
ZIP                Monitor-0.1.0-rc.61-win-x64.zip
ZIP SHA-256         d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
Source commit       e28158da67b36dfc5dbf8f4c38b5c43d99c7c728
Tested merge        158148d8bfd05f724014541bc7a0b1eab5dae1b5
```

Acceptance-control sidecar identity is separate and immutable:

```text
OperatorToolingCommit b422eaaee53d931a62a43b3c36a53b68cd4f3e27
```

Never rebuild/repackage RC.61 to add newer acceptance scripts. Candidate-bundled `_operations` owns candidate IIS/deploy/HTTPS operations; the separately exported six-script sidecar owns session/gate/finalization evidence.

## Required real environment/prerequisites

- intended Windows Server production host;
- elevated PowerShell 7;
- IIS `WebAdministration` scripting tools;
- .NET 8 ASP.NET Core Hosting Bundle and ANCM v2;
- existing IIS site and app pool, default names `Monitor` / `Monitor` unless the approved environment explicitly differs;
- app pool `No Managed Code` and identity `ApplicationPoolIdentity` or approved dedicated `SpecificUser`; never LocalSystem/LocalService/NetworkService;
- trusted machine certificate in `Cert:\LocalMachine\My` with private key, not expiring within 24 hours;
- exact HTTPS DNS binding/hostname to that certificate;
- secret-free `appsettings.Production.json`, `Deployment:Mode=SingleNode`, exact AllowedHosts entry, no embedded password/hash/connection string;
- production admin verifier and monitored-SQL credentials supplied only through approved secret/environment mechanisms;
- stable state root outside releases: `C:\ProgramData\Monitor\App_Data`;
- validated pre-cutover operational backup ID and rollback point;
- previous IIS physical path recorded before cutover;
- approved non-sysadmin least-privilege monitored SQL login;
- operator identity authorized to make and review the real 15 PASS attestations.

## Step A — prove #162 readiness before any production preparation/mutation

From the exact repository handoff checkout, using the exact Green IDs recorded on #162:

```powershell
$promotionRunId = Read-Host 'Exact #162 promotion run ID'
$verificationRunId = Read-Host 'Exact #162 independent verification run ID'
if ($promotionRunId -notmatch '^[1-9][0-9]*$' -or $verificationRunId -notmatch '^[1-9][0-9]*$') {
    throw 'STOP: valid #162 run IDs are required.'
}

$rc61Ready = .\scripts\Test-Rc61CutoverReadiness.ps1 `
  -PromotionRunId ([long]$promotionRunId) `
  -VerificationRunId ([long]$verificationRunId)

if ($rc61Ready.Status -cne 'READY_FOR_P0_5_PRE_CUTOVER_PREPARATION' -or
    -not [bool]$rc61Ready.DurableReleasePrerequisiteSatisfied -or
    [int]$rc61Ready.ExternalGatesPassed -ne 0 -or
    [bool]$rc61Ready.ProductionMutationPerformed -or
    [bool]$rc61Ready.MutatedGitHubState) {
    throw 'STOP: #162 durable-release prerequisite is not exactly satisfied.'
}
```

This is still 0/15 and does not count as a #116 PASS.

## Step B — download and independently re-hash the durable release

```powershell
$repo = 'walidatiyaai2025-gif/Monitor'
$cutoverRoot = 'C:\ProgramData\Monitor\Cutover\rc61'
if (Test-Path -LiteralPath $cutoverRoot) { throw 'STOP: cutover root already exists; preserve prior attempt and create a reviewed new attempt instead.' }
New-Item -ItemType Directory -Path $cutoverRoot -Force | Out-Null

gh release download 'v0.1.0-rc.61' --repo $repo --pattern 'Monitor-0.1.0-rc.61-win-x64.zip' --dir $cutoverRoot
gh release download 'v0.1.0-rc.61' --repo $repo --pattern 'Monitor-0.1.0-rc.61-win-x64.zip.sha256' --dir $cutoverRoot

$artifact = Join-Path $cutoverRoot 'Monitor-0.1.0-rc.61-win-x64.zip'
$checksum = "$artifact.sha256"
$expectedProductSha256 = 'd0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5'
$actualProductSha256 = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualProductSha256 -cne $expectedProductSha256) { throw 'STOP: durable candidate hash mismatch.' }
if ((Get-Content -LiteralPath $checksum -Raw).Trim() -cne "$expectedProductSha256  Monitor-0.1.0-rc.61-win-x64.zip") {
    throw 'STOP: durable candidate checksum file is not canonical.'
}
```

Extract only to access the immutable candidate-bundled operations scripts; deployment itself still consumes the verified ZIP:

```powershell
$candidateExtract = Join-Path $cutoverRoot 'candidate-extracted'
Expand-Archive -LiteralPath $artifact -DestinationPath $candidateExtract
$candidateOps = Join-Path $candidateExtract '_operations\scripts'
foreach ($required in @('Test-IisProductionPrerequisites.ps1','Deploy-ProductionSingleNode.ps1','Accept-ProductionSingleNode.ps1')) {
    if (-not (Test-Path -LiteralPath (Join-Path $candidateOps $required) -PathType Leaf)) {
        throw "STOP: immutable RC.61 package is missing candidate operation $required."
    }
}
```

## Step C — collect approved real environment inputs without embedding secrets

```powershell
$hostName = Read-Host 'Exact production DNS host name (no scheme/port)'
$certificateThumbprint = Read-Host 'Approved LocalMachine/My certificate thumbprint'
$operationalBackupId = Read-Host 'Validated pre-cutover operational backup ID'
$productionConfigPath = Read-Host 'Absolute path to approved secret-free appsettings.Production.json'
$acceptedBy = Read-Host 'Approved final operator identity'

$siteName = 'Monitor'
$appPoolName = 'Monitor'
$stateRoot = 'C:\ProgramData\Monitor\App_Data'
$releaseRoot = 'C:\Program Files\Monitor\releases'
$releaseVersion = '0.1.0-rc.61'
$baseUri = [Uri]("https://$hostName/")

if ([string]::IsNullOrWhiteSpace($operationalBackupId)) { throw 'STOP: validated operational backup ID is required.' }
if (-not (Test-Path -LiteralPath $productionConfigPath -PathType Leaf)) { throw 'STOP: production configuration file not found.' }
```

The backup ID must come from a real pre-cutover backup that was actually validated. Do not invent an ID merely to satisfy the parameter.

## Step D — candidate-bundled real IIS preflight; capture previous path/identity

```powershell
$preflightEvidence = 'C:\ProgramData\Monitor\PreSessionEvidence\rc61-iis-preflight.txt'
New-Item -ItemType Directory -Path (Split-Path -Parent $preflightEvidence) -Force | Out-Null

$preflight = & (Join-Path $candidateOps 'Test-IisProductionPrerequisites.ps1') `
  -HostName $hostName `
  -CertificateThumbprint $certificateThumbprint `
  -SiteName $siteName `
  -AppPoolName $appPoolName `
  -HttpsPort 443 `
  -PassThru

$preflight | Format-List | Out-String | Set-Content -LiteralPath $preflightEvidence -Encoding utf8NoBOM
if (-not [bool]$preflight.Ready) { throw 'STOP: IIS production preflight did not return Ready=True.' }
$previousPhysicalPath = [string]$preflight.SitePhysicalPath

Import-Module WebAdministration -ErrorAction Stop
$pool = Get-Item -LiteralPath "IIS:\AppPools\$appPoolName"
$appPoolIdentity = if ([string]$pool.processModel.identityType -ceq 'ApplicationPoolIdentity') {
    "IIS AppPool\$appPoolName"
} else {
    [string]$pool.processModel.userName
}
if ([string]::IsNullOrWhiteSpace($appPoolIdentity)) { throw 'STOP: app-pool identity is ambiguous.' }
```

Expected preflight result includes `Ready=True`; the non-`PassThru` form prints exactly:

```text
Monitor IIS production prerequisites passed. No configuration was changed.
```

## Step E — export and independently verify the exact acceptance-control sidecar

Use a fresh exact source checkout; do not export from `main`:

```powershell
$operatorToolingCommit = 'b422eaaee53d931a62a43b3c36a53b68cd4f3e27'
$toolingSource = "C:\ProgramData\Monitor\ToolingSource\$operatorToolingCommit"
$acceptanceTools = "C:\ProgramData\Monitor\AcceptanceTooling\$operatorToolingCommit"

if (Test-Path -LiteralPath $toolingSource -or Test-Path -LiteralPath $acceptanceTools) {
    throw 'STOP: tooling source/output path already exists; preserve prior provenance and use a reviewed new attempt.'
}
New-Item -ItemType Directory -Path (Split-Path -Parent $toolingSource) -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $acceptanceTools) -Force | Out-Null

git clone 'https://github.com/walidatiyaai2025-gif/Monitor.git' $toolingSource
git -C $toolingSource checkout --detach $operatorToolingCommit
if ((git -C $toolingSource rev-parse HEAD).Trim() -cne $operatorToolingCommit) { throw 'STOP: toolkit checkout commit mismatch.' }
if (-not [string]::IsNullOrWhiteSpace((git -C $toolingSource status --porcelain=v1 --untracked-files=no))) { throw 'STOP: toolkit checkout is dirty.' }

Push-Location $toolingSource
try {
    $toolkit = .\scripts\Export-ProductionAcceptanceToolkit.ps1 `
      -ExpectedToolingCommit $operatorToolingCommit `
      -OutputDirectory $acceptanceTools
} finally {
    Pop-Location
}
$operatorToolkitManifestSha256 = [string]$toolkit.ToolkitManifestSha256

$toolkitVerification = & "$acceptanceTools\Test-ProductionAcceptanceToolkit.ps1" `
  -ToolkitRoot $acceptanceTools `
  -ExpectedToolingCommit $operatorToolingCommit `
  -ExpectedToolkitManifestSha256 $operatorToolkitManifestSha256

if (-not [bool]$toolkitVerification.Verified -or [int]$toolkitVerification.FileCount -ne 6) {
    throw 'STOP: Acceptance Control Toolkit verification failed.'
}
```

Required verified state:

```text
ToolingCommit  b422eaaee53d931a62a43b3c36a53b68cd4f3e27
FileCount      6
Verified       True
```

Preserve `$operatorToolkitManifestSha256` outside the mutable acceptance session.

## Step F — create exactly one fresh fail-closed 0/15 session

```powershell
$sessionRoot = 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-61'
if (Test-Path -LiteralPath $sessionRoot) { throw 'STOP: acceptance session root already exists; never reuse it.' }
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
    throw 'STOP: new session is not exact fail-closed 0/15.'
}
```

Required state:

```text
ExternalGateCount  15
ExternalGatesPassed 0
ProductionAccepted False
```

The SHA-locked session manifest itself must have `status=PreparedFailClosed`. Preserve `$sessionManifestSha256` independently outside session files.

Copy the real preflight evidence into the immutable session proof root only after session binding is established:

```powershell
Copy-Item -LiteralPath $preflightEvidence -Destination (Join-Path $proofRoot '02-iis-preflight.txt')
```

## Step G — exact PLAN ONLY deployment

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

Required terminal text includes:

```text
PLAN ONLY. No IIS, filesystem, ACL, application-pool, binding, certificate, configuration or state changes were made.
Re-run with -Apply only after reviewing this plan and the pre-cutover operational backup.
```

A human operator must review the plan and backup before `deploymentPlanReviewed` may be attested PASS.

## Step H — explicit Apply; automatic immediate path rollback remains active

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
  -Apply 2>&1 |
  Tee-Object -FilePath $applyEvidence
```

Required successful output includes:

```text
Monitor SingleNode candidate '0.1.0-rc.61' is active on https://<approved-host>/.
Previous IIS physicalPath retained for rollback: <captured-previous-path>
Repository/cutover health acceptance passed. P0.5 still requires the documented IIS recycle, registration/credential durability, deployed least-privilege, and rollback rehearsal gates.
```

The deployment script automatically restores the previous IIS physical path if its immediate candidate health acceptance fails. If automatic rollback itself fails, keep the site drained and use the explicit rollback below.

## Step I — execute the real environment journey and create non-secret proof

The following operations are real external actions; none may be inferred from repository CI:

1. authenticate as an approved Administrator through the actual trusted HTTPS endpoint;
2. register/test/refresh the approved monitored SQL target;
3. prove the deployed monitored-SQL login is non-sysadmin and requires no target DML/write privilege;
4. capture final cache/evidence read from the deployed application;
5. recycle the IIS app pool and repeat health + authentication;
6. prove registration survives recycle;
7. prove protected credential resolves after recycle and bounded Test/Refresh succeeds;
8. prove audit/history/incident operational state survives recycle;
9. prove the pre-cutover operational backup ID is real and validated.

Store bounded non-secret evidence only at these exact session paths:

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

Do not place passwords, connection strings, tokens, raw provider errors, SQL text, Data Protection keys or secret references in those files. The recorder/validator intentionally reject secret-like content.

## Step J — exact rollback rehearsal command

This is the explicit application-only rollback for the captured pre-state. It does not delete stable `App_Data` or Data Protection keys.

```powershell
$rollbackEvidence = Join-Path $proofRoot '13-rollback-rehearsal.txt'
Import-Module WebAdministration -ErrorAction Stop

@(
  "RollbackStartedUtc=$([DateTimeOffset]::UtcNow.ToString('O'))",
  "SiteName=$siteName",
  "AppPoolName=$appPoolName",
  "From=$releaseRoot\$releaseVersion",
  "To=$previousPhysicalPath"
) | Set-Content -LiteralPath $rollbackEvidence -Encoding utf8NoBOM

Stop-WebAppPool -Name $appPoolName
Set-ItemProperty -LiteralPath "IIS:\Sites\$siteName" -Name physicalPath -Value $previousPhysicalPath
Start-WebAppPool -Name $appPoolName
```

Post-rollback health evidence:

```powershell
$postRollbackEvidence = Join-Path $proofRoot '14-post-rollback-health.txt'
$live = Invoke-RestMethod -Uri ([Uri]::new($baseUri,'/health/live')) -Headers @{Accept='application/json'}
$ready = Invoke-RestMethod -Uri ([Uri]::new($baseUri,'/health/ready')) -Headers @{Accept='application/json'}
$health = Invoke-RestMethod -Uri ([Uri]::new($baseUri,'/health')) -Headers @{Accept='application/json'}
if ($live.status -cne 'Live' -or $ready.status -cne 'Ready' -or $health.status -cne 'Ready') {
    throw 'STOP: post-rollback health is not exact Live/Ready/Ready.'
}
@(
  "VerifiedAtUtc=$([DateTimeOffset]::UtcNow.ToString('O'))",
  "Live=$($live.status)",
  "Ready=$($ready.status)",
  "Health=$($health.status)"
) | Set-Content -LiteralPath $postRollbackEvidence -Encoding utf8NoBOM
```

After the rollback rehearsal is proven, explicitly return to the already verified RC.61 release directory before final acceptance; do not redeploy/re-extract or overwrite it:

```powershell
$rc61ReleasePath = Join-Path $releaseRoot $releaseVersion
if (-not (Test-Path -LiteralPath $rc61ReleasePath -PathType Container)) { throw 'STOP: RC.61 immutable release directory is missing.' }
Stop-WebAppPool -Name $appPoolName
Set-ItemProperty -LiteralPath "IIS:\Sites\$siteName" -Name physicalPath -Value $rc61ReleasePath
Start-WebAppPool -Name $appPoolName
```

Then repeat trusted health/authentication/read checks and create the real `proof/15-final-read-evidence.txt`. If the rollback requires operational-state restoration, follow the approved backup restore flow only after its dry-run validation; never delete/replace protected secrets or key rings as a shortcut.

## Step K — record exactly the 15 real PASS gates

The 15 required gate names and evidence files are fixed:

```powershell
$gateEvidence = [ordered]@{
  artifactChecksumVerified              = 'proof/01-artifact-checksum.txt'
  iisPreflightPassed                    = 'proof/02-iis-preflight.txt'
  deploymentPlanReviewed                = 'proof/03-deployment-plan.json'
  cutoverApplied                        = 'proof/04-cutover-apply.txt'
  trustedHttpsHealthPassed              = 'proof/05-trusted-https-health.json'
  administratorAuthenticationPassed     = 'proof/06-administrator-authentication.txt'
  leastPrivilegeSqlVerified             = 'proof/07-least-privilege-sql.txt'
  iisRecyclePassed                      = 'proof/08-iis-recycle.txt'
  registrationDurabilityVerified        = 'proof/09-registration-durability.txt'
  protectedCredentialDurabilityVerified = 'proof/10-protected-credential-durability.txt'
  operationalStateDurabilityVerified    = 'proof/11-operational-state-durability.txt'
  operationalBackupValidated            = 'proof/12-operational-backup-validated.txt'
  rollbackRehearsed                     = 'proof/13-rollback-rehearsal.txt'
  postRollbackHealthPassed              = 'proof/14-post-rollback-health.txt'
  finalReadEvidencePassed               = 'proof/15-final-read-evidence.txt'
}

foreach ($gate in $gateEvidence.GetEnumerator()) {
    $absoluteEvidence = Join-Path (Split-Path -Parent $evidencePath) $gate.Value
    if (-not (Test-Path -LiteralPath $absoluteEvidence -PathType Leaf)) {
        throw "STOP: real evidence is missing for $($gate.Key): $($gate.Value)"
    }

    & "$acceptanceTools\Set-ProductionAcceptanceGate.ps1" `
      -EvidencePath $evidencePath `
      -ExpectedSessionManifestSha256 $sessionManifestSha256 `
      -GateName $gate.Key `
      -EvidenceFile $gate.Value `
      -AcknowledgePass
}
```

Each successful recorder invocation must report `Passed=True` and a SHA-256-bound `EvidenceRef`/`EvidenceSha256`. File presence alone is not evidence; `-AcknowledgePass` is an explicit real-operator attestation after reviewing the actual operation.

## Step L — explicit finalizer and independent 15/15 validation

Only after all 15 real gates were explicitly recorded:

```powershell
$finalSummary = & "$acceptanceTools\Complete-ProductionAcceptance.ps1" `
  -EvidencePath $evidencePath `
  -ExpectedSessionManifestSha256 $sessionManifestSha256 `
  -AcceptedBy $acceptedBy `
  -ClosureSummaryFile 'p0-5-closure-summary.json' `
  -AcknowledgeFinalAcceptance
$finalSummary | Format-List
```

Then independently validate the same authoritative session:

```powershell
$independentSummaryPath = Join-Path (Split-Path -Parent $evidencePath) 'p0-5-independent-review.json'
$independentSummary = & "$acceptanceTools\Test-ProductionAcceptanceEvidence.ps1" `
  -EvidencePath $evidencePath `
  -EvidenceRoot (Split-Path -Parent $evidencePath) `
  -ClosureSummaryPath $independentSummaryPath `
  -ExpectedSessionManifestSha256 $sessionManifestSha256
$independentSummary | Format-List
```

Required final terminal output:

```text
Production acceptance evidence PASS: 15/15 external gates verified with matching evidence hashes.
```

Required summary values include:

```text
result             PASS
candidateVersion   0.1.0-rc.61
artifactFileName   Monitor-0.1.0-rc.61-win-x64.zip
artifactSha256     d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
sourceCommit       e28158da67b36dfc5dbf8f4c38b5c43d99c7c728
testedMergeCommit  158148d8bfd05f724014541bc7a0b1eab5dae1b5
deploymentMode     SingleNode
requiredGateCount  15
sessionManifestSha256 <must equal independently preserved session hash>
operatorToolingCommit b422eaaee53d931a62a43b3c36a53b68cd4f3e27
```

Only after this real reviewed evidence may #116 be closed. #111 may close only after #116 is accepted.

## Authoritative #116 evidence paths

```text
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\session-manifest.json
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\session-manifest.sha256
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\candidate\Monitor-0.1.0-rc.61-win-x64.zip
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\candidate\Monitor-0.1.0-rc.61-win-x64.zip.sha256
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\evidence\p0-5-evidence-pack.json
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\evidence\proof\01..15 files listed above
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\evidence\p0-5-closure-summary.json
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\evidence\p0-5-independent-review.json
C:\ProgramData\Monitor\App_Data\deployment-current.json
```

Preserve outside the mutable session: selected product SHA-256, exact toolkit commit, toolkit-manifest SHA-256, and returned session-manifest SHA-256.

## Explicit #116 STOP conditions

STOP; do not mark the affected gate PASS; keep #116 OPEN when any of these occurs:

- #162 is not closed with exact promotion + separate verifier + durable tag/assets/hash evidence;
- durable ZIP/checksum/tag/tested-merge identity differs;
- toolkit checkout is not clean exact `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`;
- toolkit verification is not `Verified=True`, file count is not exactly 6, or manifest hash is not independently preserved;
- session root already exists, session binding fails, or session is not exact fail-closed 0/15;
- operational backup is absent/unvalidated or rollback point is unknown;
- IIS preflight is not `Ready=True`;
- deployment plan has not been human-reviewed;
- Apply fails or immediate acceptance fails;
- trusted certificate/health/authentication fails;
- monitored SQL requires write/DML/sysadmin privilege;
- recycle breaks health, registration, protected credential or operational state;
- rollback or return-to-candidate health/read checks fail;
- any gate proof is missing, secret-bearing, stale, from another session or has a hash mismatch;
- recorder/finalizer/validator reports any failure;
- final independent output is anything other than real `PASS` with 15/15 matching evidence hashes.

Repository CI, synthetic Windows acceptance, successful package deployment alone, and documentation are never substitutes for these real environment results.
