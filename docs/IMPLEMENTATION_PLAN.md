# Implementation Plan

This file is the **current repository implementation/convergence plan**. It is subordinate to live repository evidence. Execution authority is, in order:

1. exact live GitHub `main`;
2. `AGENTS.md`;
3. open PRs/issues/workflows and their current review/check state;
4. `docs/CURRENT_EXECUTION_PLAN.md`;
5. this file for the current implementation summary and dependency order.

The complete pre-convergence implementation history that previously occupied this file is preserved unchanged at `docs/history/IMPLEMENTATION_PLAN_PRE_CONVERGENCE_2026-09-09.md`. Historical branch/PR instructions in that archive are evidence only and must not be executed unless they are revalidated against current live state.

## Current integrated state — 2026-09-09

**Verified integration base before this documentation reconciliation:** `main@3a7e8daf9565d7cb75e8dc8111d6df7ae9e90c0d`.

- M0 through M8 are verified.
- BATCH-100 through BATCH-800 are complete in repository scope; BATCH-800/#287 is closed completed.
- Website Monitoring #463/#466 is COMPLETE / MERGED through PR #467 -> PR #464. There is no remaining required Website Monitoring runtime implementation target.
- PR #473 `Release: bind tagged artifacts to deterministic notes` is COMPLETE / MERGED as `3a7e8daf9565d7cb75e8dc8111d6df7ae9e90c0d` after a current-base reconciliation, zero unresolved review threads, exact-head Linux CI, Windows production-candidate and both protected-P0 guards Green. Exact merged-main push CI run `34379131661` is Green.
- Draft PR #472 is an **optional browser-verification follow-up**, not a missing #463 implementation. It remains DRAFT / NOT READY: its branch is stale against current main and dedicated `website-monitoring-visual` run `34371076685` failed at authenticated browser acceptance. It must not be merged or used to reopen #463 unless its purpose remains lawful, it is reconciled to current main, its dedicated browser gate is repaired and Green, all selected exact-head gates are Green, and review state is clear.
- No open issue is currently a verified repository `CODE_GAP` whose own Definition of Done can be satisfied solely by Cloud Work.

## Integration and READY policy

A branch/PR is legitimate READY only when all of the following are true immediately before merge:

1. it represents existing committed product/repository scope rather than an unrelated nice-to-have;
2. it does not duplicate implementation already merged or actively owned by another legitimate branch;
3. its lawful base is identified and the branch is current with that base (`behind_by=0` or equivalent exact evidence);
4. unresolved review threads are zero where GitHub exposes thread data;
5. every selected exact-head gate is completed Green; earlier-head or pre-rebase evidence is not sufficient;
6. owner-only/external acceptance is not being simulated, inferred or converted to PASS;
7. its diff contains only the intended implementation/reconciliation scope and preserves unrelated legitimate work.

After every merge:

- fetch exact `main`;
- read/run the strongest exact-main verification available;
- repair any integration-caused regression immediately on a current-main branch;
- reconcile issue/PR/document state to what actually merged;
- never claim external/owner-only completion from repository CI.

## Current required dependency order

The remaining production dependency is strict:

`#162 OWNER_ONLY durable RC.61 publication + independent verification -> #116 EXTERNAL_ENVIRONMENT real trusted-IIS 15/15 acceptance -> #111 EXTERNAL_ENVIRONMENT umbrella closure`

Issue #353 is independent repository governance:

`#353 OWNER_ONLY / REPOSITORY_ADMIN provider-bound main branch protection apply + independent read-back`

No #116 production mutation may begin while #162 is open. #111 cannot close before #116. #353 does not satisfy any production acceptance gate.

## P0.5 repository preparation — COMPLETE

Repository-side P0.5 implementation is complete. The selected cutover candidate remains RC.61 unless #116 explicitly selects a later equivalently verified candidate.

### Selected RC.61 identity

- version: `0.1.0-rc.61`;
- product: `Monitor-0.1.0-rc.61-win-x64.zip`;
- product SHA-256: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- source production-candidate run: `31667721306`;
- Actions artifact ID: `9168574442`;
- outer artifact digest: `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`;
- source head: `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`;
- tested merge: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- release tag: `v0.1.0-rc.61`.

Repository CI/candidate evidence is not production acceptance.

### Durable promotion — #162 OWNER_ONLY / OPEN

Repository implementation for preserving the exact existing RC.61 candidate is complete. Actual durable publication remains an explicit owner/operator action.

Preferred operator sequence:

`Invoke-Rc61DurablePromotion.ps1 preview -> explicit -AcknowledgePromotion -> exact captured promotion run -> separately execute returned IndependentVerificationCommand -> Test-Rc61CutoverReadiness.ps1 with the two exact run IDs`

Preview:

```powershell
.\scripts\Invoke-Rc61DurablePromotion.ps1
```

Require a clean fail-closed preview including:

- `Status = READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT`;
- `WorkflowDispatchPerformed = False`;
- `IndependentVerificationDispatched = False`;
- `ProductionMutationPerformed = False`;
- `MutatedGitHubState = False`.

Only after review:

```powershell
.\scripts\Invoke-Rc61DurablePromotion.ps1 -AcknowledgePromotion
```

The helper must bind one exact promotion run. Ambiguity, timeout or failure is **do not redispatch**. The independent verifier remains a separate operator action.

After the exact promotion run and separate verifier are both Green:

```powershell
.\scripts\Test-Rc61CutoverReadiness.ps1 `
  -PromotionRunId <PROMOTION_RUN_ID> `
  -VerificationRunId <VERIFICATION_RUN_ID>
```

Require `Status = READY_FOR_P0_5_PRE_CUTOVER_PREPARATION`, `DurableReleasePrerequisiteSatisfied = True`, `ExternalGatesPassed = 0`, `ProductionMutationPerformed = False`, and `MutatedGitHubState = False`.

#162 remains OPEN until independent live evidence proves the approved tag, exactly the two approved durable assets and the approved product SHA-256 after both exact workflow runs succeed.

### First Production SingleNode — #116 EXTERNAL_ENVIRONMENT / OPEN

Only after #162 completes, the actual intended Windows/IIS environment must prove the real acceptance chain:

- verified durable RC.61 bytes;
- independently verified Acceptance Control Toolkit from exact source `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`;
- fresh immutable session with externally preserved session-manifest SHA-256;
- trusted machine certificate and HTTPS binding;
- intended application-pool identity;
- candidate-bundled IIS prerequisite preflight;
- reviewed PLAN ONLY deployment followed by explicit `-Apply`;
- trusted HTTPS health/authentication;
- approved least-privilege monitored SQL registration/Test/Refresh path;
- IIS recycle durability for registration, protected credential and operational state;
- backup/rollback/recovery rehearsal;
- exact real 15/15 SHA-bound evidence;
- explicit operator finalization;
- independent evidence validation and human review.

Repository workflows, Windows runner validation, synthetic 15/15 packs, release packaging, tag tooling or documentation do not satisfy #116.

### P0 umbrella — #111 EXTERNAL_ENVIRONMENT / OPEN

#111 closes only after #116 is accepted under its own real-environment Definition of Done. No repository-only merge may auto-close or mark #111 complete.

## Release integrity

### Package parity — COMPLETE

`release.yml` delegates package construction to the verified Windows production-candidate workflow. Tagged/manual release packaging therefore shares the same build/test/publish/secret-free/smoke/package-validation path rather than using a weaker independent publish path.

### Durable tagged release assets — COMPLETE

For normal pushed version tags, the release workflow can publish only the already-verified same-run ZIP and companion checksum after checksum validation. It has no rebuild/repackage/clobber path and does not imply external production acceptance.

### Deterministic tagged-release notes — PR #473 COMPLETE / MERGED

PR #473 adds a deterministic release-note identity contract for **future normal tagged-release publication**:

1. release notes are generated only after the downloaded product checksum has been verified;
2. notes bind exact version, tag, release source commit, ZIP filename, product SHA-256, companion checksum and verified production-candidate provenance;
3. new release creation uses the deterministic notes file rather than mutable/generated prose;
4. if a release already exists, both the exact notes and exact approved assets must match; drift fails closed;
5. regression coverage prohibits weakening the deterministic notes/asset contract.

PR #473 did **not** create, publish, tag or mutate selected RC.61. It therefore does not satisfy #162 and changes no #116/#111 state.

## Website Monitoring — #463/#466 COMPLETE / MERGED

### Merged runtime contract

The required Website Monitoring implementation is complete through PR #467 -> PR #464 and includes:

- bounded target administration;
- HTTP/HTTPS-only URL validation and credential-in-URL rejection;
- fail-closed SSRF/private-destination authorization with re-resolution/re-authorization;
- DNS/TCP/TLS/HTTP/status/redirect/content/latency/certificate evidence;
- durable SingleNode target/history/schedule/check state;
- existing-incident confirmation/recovery/reopen reconciliation;
- bounded correlation without causal overclaim;
- recipient groups;
- SMTP environment-secret resolution;
- durable outbox/retry/dead-letter behavior;
- `/websites` management and cached live UI;
- attributable audit, antiforgery and named role boundaries;
- SharedState stores and CAS-backed ownership for HA paths;
- one coordinated execution service with local single-flight and distributed per-target leases.

Monitoring and notifications remain default-disabled. MultiNode remains fail-closed unless all existing SharedState/coordination prerequisites are satisfied.

### Visual/browser evidence truth

The merged product claims source/route responsive/accessibility acceptance, including reduced-motion and explicit 390px behavior. It does **not** claim a merged browser screenshot run.

Draft PR #472 is optional verification work only. Current known state:

- draft/open;
- branch stale against integrated main;
- no runtime implementation is required by the PR;
- earlier normal CI and protected-P0 guards were Green;
- dedicated `website-monitoring-visual` run `34371076685` failed at authenticated browser acceptance.

Therefore #472 is not READY and is not a prerequisite for closed #463 or the P0 production chain. It may be repaired/reconsidered only as existing verification scope; it must not redefine completed runtime scope without an authoritative committed requirement.

## Repository governance — #353 OWNER_ONLY / OPEN

Repository-side branch-protection helper, safety tests and documentation are complete. Actual GitHub main protection remains an authenticated repository-admin action plus independent read-back.

Required live policy remains:

- required provider-bound checks: `build`, `protected-p0-pr-metadata`, `protected-p0-pr-commits`;
- one reviewed provider identity per required check;
- strict/up-to-date checks enabled;
- admin enforcement enabled;
- conversation resolution required;
- force pushes disabled;
- branch deletion disabled;
- no unrelated PR-review/restriction policy expansion.

Do not close #353 from helper code, preview output, CI Green or merge evidence. Close only after actual application plus independent repository-admin read-back proves the exact live policy.

## Completed implementation baselines

The following are complete repository state and must not be reopened from historical branch instructions:

- M0–M8 verified foundation;
- BATCH-100..BATCH-800 complete repository scope;
- BATCH-700 visible portal/UI completion;
- BATCH-800 full functional operator wiring, Issue #287 CLOSED/COMPLETED;
- real SQL gates P0.1–P0.4 complete;
- selected-product-hash acceptance binding;
- locked-session evidence-chain binding;
- immutable Acceptance Control Toolkit provenance;
- IIS bootstrap/fresh-host/PowerShell 7 prerequisite hardening;
- clean IIS no-demo production/staging behavior;
- GitHub Actions supply-chain/native Node 24 hardening;
- durable release/promotion preflight and operator-helper implementation;
- programming truthfulness/security/control closures through PR #369;
- atomic SharedState execution guard #423/#424;
- incident-note durability/replay/cross-process closures #445–#450;
- core SingleNode file cross-process closure #451/#452;
- operator metadata cross-process closure #459/#460;
- Website Monitoring #463/#466 through #467/#464;
- future normal tagged-release deterministic-notes hardening #473.

Exact historical implementation/evidence details remain preserved in `docs/history/IMPLEMENTATION_PLAN_PRE_CONVERGENCE_2026-09-09.md` and the associated closed issues/PRs.

## Stable safety/truth boundaries

- Browser monitoring GETs remain cache/control-plane only and never initiate monitored SQL collection.
- Browser/UI code never connects directly to monitored SQL.
- Missing, stale, truncated, permission-limited or uncollected evidence remains explicit; it must not become synthetic zero/healthy/default truth.
- No autonomous remediation or AI-generated SQL execution.
- Credentials, full connection strings, current secret references, raw provider errors, arbitrary SQL text, client/table data and physical paths remain outside UI/audit/telemetry/exports/diagnostics/production evidence according to existing contracts.
- Mutations require the existing POST + antiforgery + named authorization boundaries.
- MultiNode stays fail-closed until its existing distributed prerequisites are genuinely satisfied.
- Repository CI cannot manufacture RC.61 publication, trusted-IIS acceptance, branch-protection enforcement or any other owner/external PASS.
- No new feature is added merely because it would be useful; work must trace to existing committed product/repository scope.

## Current next legal actions

1. Merge only branches that satisfy the READY policy above.
2. Keep #472 DRAFT / NOT READY until its optional verification line is current, lawful and Green; do not treat it as required implementation.
3. Owner completes #162 through explicit promotion + separate verification + independent tag/exact-two-assets/hash/readiness evidence.
4. After #162 only, execute #116 on the actual trusted Windows/IIS/SQL environment and collect/review real 15/15 evidence.
5. Close #111 only after #116.
6. Repository admin applies and independently verifies #353 separately.

## Definition of Done for the active production plan

The active production plan is complete only when:

- #162 has passed its explicit owner-operated durable-publication and independent-verification closure rule;
- #116 has passed every real trusted-IIS/HTTPS/least-privilege/recycle/durability/backup/rollback 15/15 acceptance gate with reviewed evidence;
- #111 is closed only after #116;
- #353 is closed only after exact live repository-admin branch-protection apply/read-back;
- every repository integration used current-base, zero-thread and exact-head Green evidence and exact-main post-merge verification;
- no incomplete owner/external result is represented as repository completion.
