# RC.61 Durable Promotion Inputs

Implementation status: **COMPLETE** via PR #163 / merge `43d8a193205495f155bb8866532a4e99ed93b655`, with subsequent durable-release hardening through PR #219, the read-only operator preflight through #266 / PR #267, deterministic post-verification readiness through PR #337, and the explicit promotion operator helper through #338 / PR #339.  
Execution status: **PENDING MANUAL DISPATCH** under Issue #162.  
Current release lookup: `v0.1.0-rc.61` is not yet present.

This file is the short operator handoff for the selected existing-candidate retention operation. It must stay aligned with the current hardened preflight, explicit promotion helper, promotion workflow and independent-verification workflow.

## Selected candidate identity — do not substitute

- version: `0.1.0-rc.61`
- source workflow run: `31667721306`
- source Actions artifact: `9168574442`
- outer Actions artifact digest: `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`
- product SHA-256: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`
- source head: `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`
- tested merge: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- release tag: `v0.1.0-rc.61`

## Step 0 — read-only fail-closed preview

The preferred operator entry point is the explicit helper introduced by PR #339:

```powershell
.\scripts\Invoke-Rc61DurablePromotion.ps1
```

The helper runs `Test-Rc61DurablePromotionPreflight.ps1` before it can dispatch anything. Without `-AcknowledgePromotion` it is read-only and must return:

- `Status=READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT`
- `WorkflowDispatchPerformed=False`
- `IndependentVerificationDispatched=False`
- `ProductionMutationPerformed=False`
- `MutatedGitHubState=False`

For lower-level diagnosis, the underlying preflight may also be run directly:

```powershell
.\scripts\Test-Rc61DurablePromotionPreflight.ps1 | Format-List
```

For a first publication attempt, that direct preflight must still report all of:

- `Status=READY_FOR_EXPLICIT_MANUAL_PROMOTION`
- `MutatedGitHubState=False`
- `TagExists=False`
- `ReleaseExists=False`

The preflight is pinned to the exact repository and RC.61 identity above. It authenticates `gh`, requires the Monitor default branch to remain `main`, verifies the selected successful source run and exact artifact provenance/name/size/expiry/outer digest, and probes the durable tag/release without mutating GitHub state. Only an actual 404 is treated as resource absence; authentication, network, API or other ambiguous probe failures stop the operation.

If the helper/preflight reports existing durable state, artifact expiry, provenance/digest drift, authentication/API ambiguity, or any other failure, **stop and investigate; do not dispatch or redispatch**.

A successful preview/preflight does not satisfy #162. It authorizes only the explicit acknowledged promotion step below.

## Step 1 — explicit manual durable promotion

After reviewing the preview, use the preferred helper path:

```powershell
.\scripts\Invoke-Rc61DurablePromotion.ps1 -AcknowledgePromotion
```

The helper dispatches **only** `.github/workflows/promote-existing-candidate.yml` from `main`, using the exact locked RC.61 inputs. It captures and monitors the exact promotion run. If run discovery is ambiguous, the run times out, or the captured run fails, the helper fails closed with a **do not redispatch** instruction; inspect the exact run instead.

A successful promotion helper returns:

```text
PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED
```

and includes `PromotionRunId`, `PromotionRunUrl`, `IndependentVerificationCommand`, and `PostVerificationReadinessCommand`.

### Raw workflow input reference — audit/troubleshooting only

These values remain the authoritative workflow contract and are retained for independent review. The preferred execution path is the helper above; do not retype these values merely to bypass it.

- `candidate_version=0.1.0-rc.61`
- `source_run_id=31667721306`
- `source_artifact_id=9168574442`
- `expected_outer_artifact_digest=sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`
- `expected_product_sha256=d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`
- `source_commit=e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`
- `tested_merge_commit=158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- `release_tag=v0.1.0-rc.61`
- `acknowledge_promotion=true`

The promotion workflow requires the exact completed successful production-candidate run, exact artifact ID/name/source repository/head SHA, non-expired artifact state, positive size and exact outer digest. It downloads only the selected artifact and validates the companion checksum, product hash, embedded release manifest and hardened ZIP-safety rules. It creates or verifies the durable tag/release without rebuilding, publishing, compressing or repackaging RC.61.

A Green promotion run is necessary but is **not sufficient** to close #162.

## Step 2 — separate independent read-only verification

Only after the exact promotion run is Green, execute the exact `IndependentVerificationCommand` returned by `Invoke-Rc61DurablePromotion.ps1`.

That command separately dispatches `.github/workflows/verify-durable-release.yml` **from `main`** with exactly:

- `release_version=0.1.0-rc.61`
- `release_tag=v0.1.0-rc.61`
- `expected_commit=158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- `expected_product_sha256=d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`

The promotion helper deliberately does **not** dispatch this verifier automatically. Separate dispatch is part of #162's closure contract.

This second workflow is read-only. It independently verifies tag provenance, release metadata, exact-two asset identity, REST asset metadata/digests/URLs, exact-ID downloaded bytes and the canonical checksum while detecting tag/release mutation across verification snapshots.

Retain the Green promotion run ID/URL and the separate Green verification run ID/URL as #162 closure evidence.

## Step 3 — deterministic post-verification handoff

After both workflow runs are Green, run:

```powershell
.\scripts\Test-Rc61CutoverReadiness.ps1 `
  -PromotionRunId <PROMOTION_RUN_ID> `
  -VerificationRunId <VERIFICATION_RUN_ID>
```

Require:

- `Status=READY_FOR_P0_5_PRE_CUTOVER_PREPARATION`
- `DurableReleasePrerequisiteSatisfied=True`
- `ExternalGatesPassed=0`
- `ProductionMutationPerformed=False`
- `MutatedGitHubState=False`

This readiness result is a deterministic handoff/verification aid only. It does not itself close #162 or authorize production mutation before the issue's full closure evidence is recorded and reviewed.

## #162 closure requirements

Keep #162 OPEN until all are independently true:

- manual `promote-existing-candidate` run from `main` is Green with the exact inputs above;
- separate `verify-durable-release` run from `main` is Green with the exact inputs above;
- tag `v0.1.0-rc.61` resolves to tested merge `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- release contains exactly `Monitor-0.1.0-rc.61-win-x64.zip` and `Monitor-0.1.0-rc.61-win-x64.zip.sha256`;
- durable ZIP SHA-256 is exactly `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- companion checksum line matches that exact hash and ZIP filename;
- no alternate candidate, rebuild or repackaging was introduced.

For the expanded contract and safety ceilings, see `docs/P05_EXISTING_CANDIDATE_PROMOTION.md`. For the focused helper behavior and no-redispatch rules, see `deploy/RC61_PROMOTION_OPERATOR.md`.

## External production boundary

This operation is retention/recoverability only. It does not deploy IIS, configure the trusted production certificate or app-pool identity, exercise the real monitored SQL target, prove IIS recycle durability, rehearse production rollback, or satisfy the real 15/15 evidence gate.

#116 remains the production acceptance authority, and #111 remains open until #116 is accepted.
