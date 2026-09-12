# Remaining OWNER_ONLY / EXTERNAL gates — executable fail-closed handoff

This is the canonical handoff for the remaining non-cloud gates. It deliberately does **not** mark any OWNER_ONLY or EXTERNAL_ENVIRONMENT gate PASS.

## Authority and selected identity

Repository: `walidatiyaai2025-gif/Monitor` (ID `1329517438`), default branch `main`.

RC.61 is historical only. Actions artifact `9168574442` expired at `2026-09-12T04:41:36Z`; never reconstruct or substitute bytes under `v0.1.0-rc.61`.

Selected replacement candidate:

```text
Version                    0.1.0-rc.854
Tag                        v0.1.0-rc.854
ZIP                        Monitor-0.1.0-rc.854-win-x64.zip
Checksum                   Monitor-0.1.0-rc.854-win-x64.zip.sha256
Product SHA-256            b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7
Source Actions run         34710820438
Source artifact ID         10303396821
Source artifact name       Monitor-0.1.0-rc.854-win-x64
Outer artifact digest      sha256:e1f0b7facc756758a13653c3ad2bfa5a4af9107b02e14f4682aaaabb286f01e3
Artifact expires           2026-10-12T18:22:15Z
Source PR                  #479
Source head                ef7209cbf099da65330887508ab4380a8b4196d2
Tested PR merge            e1d0daedf8b2209934a1bcd01bff5d46229df20a
Integrated main merge      0cc2087aa9da887046986d413ab46df2bcbab735
```

The live artifact API and independently downloaded product matched run/head/repository identity, exact outer digest, product ZIP SHA-256, companion checksum and embedded `_operations/release-manifest.json`.

Dependency order:

```text
#162 -> #116 -> #111
```

#353 is independent repository governance.

---

## Common fresh-main guard

Before any owner mutation, use a trusted authenticated checkout and require exact remote `main` plus a clean tracked tree:

```powershell
$ErrorActionPreference = 'Stop'
$repo = 'walidatiyaai2025-gif/Monitor'
gh auth status
$meta = gh api "repos/$repo" | ConvertFrom-Json
if ([long]$meta.id -ne 1329517438 -or [string]$meta.default_branch -cne 'main') { throw 'STOP: repository identity/default branch mismatch.' }
$executionMain = (gh api "repos/$repo/branches/main" --jq '.commit.sha').Trim()
if ((git rev-parse HEAD).Trim() -cne $executionMain) { throw "STOP: checkout is not exact remote main $executionMain." }
if (-not [string]::IsNullOrWhiteSpace((git status --porcelain=v1 --untracked-files=no))) { throw 'STOP: tracked checkout is dirty.' }
"ExecutionMain=$executionMain"
```

---

# #162 — OWNER_ONLY durable publication

State: **OPEN / NOT PASS** until publication and independent verification are real.

The connected automation can inspect Actions evidence but cannot dispatch workflows. The repository-side operator path is complete.

### Step 0 — read-only preview

```powershell
pwsh ./scripts/Invoke-SelectedDurablePromotion.ps1
```

Require the exact rc.854 tuple above, `ExternalGatesPassed = 0`, `ProductionMutationPerformed = False`, and:

```text
READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT
```

The helper live-checks repository identity, successful production-candidate source run, exact artifact ID/name/run/head/repository IDs, outer artifact digest, non-expired status/expiry, nested product hash/checksum and embedded release manifest. Any drift is **STOP**.

### Step 1 — explicit acknowledged promotion

Only after reviewing Step 0:

```powershell
pwsh ./scripts/Invoke-SelectedDurablePromotion.ps1 -AcknowledgePromotion
```

The helper dispatches `.github/workflows/promote-existing-candidate.yml` from `main` with the workflow's exact live contract:

```text
candidate_version               0.1.0-rc.854
source_run_id                   34710820438
source_artifact_id              10303396821
expected_outer_artifact_digest  sha256:e1f0b7facc756758a13653c3ad2bfa5a4af9107b02e14f4682aaaabb286f01e3
expected_product_sha256         b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7
source_commit                   ef7209cbf099da65330887508ab4380a8b4196d2
tested_merge_commit             e1d0daedf8b2209934a1bcd01bff5d46229df20a
release_tag                     v0.1.0-rc.854
acknowledge_promotion           true
```

It binds exactly one post-dispatch run, watches it to completion and reads back immutable tag/release identity. Ambiguity, failure, expiry or unexpected pre-existing durable state is **STOP / DO NOT REDISPATCH**.

Capture the exact successful promotion run ID and the returned `IndependentVerificationCommand`.

### Step 2 — separate independent durable-release verifier

This must be a separate action after promotion succeeds. Execute the exact returned command, equivalent to:

```powershell
gh workflow run verify-durable-release.yml --repo walidatiyaai2025-gif/Monitor --ref main `
  -f release_version=0.1.0-rc.854 `
  -f release_tag=v0.1.0-rc.854 `
  -f expected_commit=e1d0daedf8b2209934a1bcd01bff5d46229df20a `
  -f expected_product_sha256=b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7
```

Capture exactly one successful `verify-durable-release` run ID. Never let promotion self-satisfy independent verification.

### Step 3 — independent release evidence

Download the durable assets and verify exact bytes:

```powershell
$evidence162 = 'C:\ProgramData\Monitor\OwnerEvidence\162-rc854'
if (Test-Path -LiteralPath $evidence162) { throw 'STOP: preserve the previous evidence attempt.' }
New-Item -ItemType Directory -Path $evidence162 -Force | Out-Null

gh release download v0.1.0-rc.854 --repo $repo --pattern 'Monitor-0.1.0-rc.854-win-x64.zip' --dir $evidence162
gh release download v0.1.0-rc.854 --repo $repo --pattern 'Monitor-0.1.0-rc.854-win-x64.zip.sha256' --dir $evidence162

$zip = Join-Path $evidence162 'Monitor-0.1.0-rc.854-win-x64.zip'
$expected = 'b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7'
$actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -cne $expected) { throw "STOP: durable product hash mismatch: $actual" }

gh api "repos/$repo/git/ref/tags/v0.1.0-rc.854" | Set-Content "$evidence162\tag.json" -Encoding utf8NoBOM
gh api "repos/$repo/releases/tags/v0.1.0-rc.854" | Set-Content "$evidence162\release.json" -Encoding utf8NoBOM
```

Close #162 only after retained evidence proves the promotion run Green, the separate verifier run Green, tag target exactly `e1d0daed...`, exactly the selected ZIP/checksum release assets, and exact product hash `b0370b3e...f0f27b7`.

---

# #116 — EXTERNAL_ENVIRONMENT production acceptance

State: **OPEN / NOT PASS**. Do not mutate production until #162 is genuinely closed with durable-release evidence.

After #162 passes, use exact durable rc.854 bytes and the embedded acceptance-control toolkit to create one fresh 0/15 production acceptance session. Execute real trusted HTTPS/IIS, real least-privilege SQL, service/app-pool recycle, durability, backup/restore validation and rollback evidence according to the embedded `_operations/docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md` and scripts.

Required rules:

- exact selected product SHA-256 remains `b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7`;
- no credentials/secrets are committed to GitHub;
- each gate uses real external evidence, not CI substitution;
- one acceptance session reaches genuine 15/15;
- independent validation passes before #116 closes.

---

# #111 — umbrella / real least-privilege closure

State: **OPEN / NOT PASS**.

There is no additional cloud implementation action. Close only after #116 has genuine accepted evidence and the required real production least-privilege operator/estate evidence is recorded. Record identity/date/decision only; never commit the password/secret.

---

# #353 — OWNER_ONLY / REPOSITORY_ADMIN branch protection

State: **OPEN / NOT PASS**. The connected integration has no GitHub Administration permission, so it cannot apply or independently read back branch protection.

Issue #353 intentionally requires exactly three provider-bound checks:

```text
build
protected-p0-pr-metadata
protected-p0-pr-commits
```

Real SQL and Windows production-candidate are separate acceptance gates and are intentionally not required branch-protection contexts.

### Step 0 — preview

```powershell
pwsh ./scripts/Set-MainBranchProtection.ps1
```

Require `READY_FOR_EXPLICIT_BRANCH_PROTECTION_ACKNOWLEDGEMENT`, no mutation, and exactly the three successful provider-bound checks discovered from the current merged same-repository PR head.

### Step 1 — explicit apply

```powershell
pwsh ./scripts/Set-MainBranchProtection.ps1 -AcknowledgeProtection
```

Require `BRANCH_PROTECTION_APPLIED_AND_VERIFIED`, strict required checks, admin enforcement and conversation resolution enabled, force pushes/deletion disabled, and `ExternalProductionGatesPassed = 0`.

### Step 2 — independent admin read-back

```powershell
gh api "repos/$repo/branches/main"
gh api "repos/$repo/branches/main/protection"
```

Close #353 only when independent admin evidence proves `protected=true`, strict required checks, exact provider-bound three-check set above, admin enforcement and conversation resolution enabled, and force pushes/deletion disabled.

---

## Final completion rule

`VERIFIED_FINAL_COMPLETE` is forbidden while #162, #116, #111 or #353 lacks genuine evidence. Repository CI, candidate selection, preview output or documentation cannot manufacture those PASS states.
