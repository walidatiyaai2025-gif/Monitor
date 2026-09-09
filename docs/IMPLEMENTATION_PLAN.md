# Implementation Plan

This file is the **current repository implementation/convergence plan**. It is subordinate to live repository evidence. Execution authority is, in order:

1. exact live GitHub `main`;
2. `AGENTS.md`;
3. open PRs/issues/workflows and their current review/check state;
4. `docs/CURRENT_EXECUTION_PLAN.md`;
5. this file for the current implementation summary and dependency order.

The complete pre-convergence implementation history that previously occupied this file is preserved unchanged at `docs/history/IMPLEMENTATION_PLAN_PRE_CONVERGENCE_2026-09-09.md`. Historical branch/PR instructions in that archive are evidence only and must not be executed unless they are revalidated against current live state.

The authoritative executable owner/external handoff is `deploy/REMAINING_OWNER_EXTERNAL_GATES.md`. That handoff is fail-closed and deliberately does **not** count any OWNER_ONLY or EXTERNAL gate as PASS without real retained evidence.

## Current integrated state — 2026-09-09

**Fresh live base before this reconciliation:** `main@4d514daef24781724f24065accd95988b406d9e6`; exact-main push CI `34382598596` Green.

- M0 through M8 are verified.
- BATCH-100 through BATCH-800 are complete in repository scope; BATCH-800/#287 is closed completed.
- Website Monitoring #463/#466 is COMPLETE / MERGED through PR #467 -> PR #464. There is no remaining required Website Monitoring runtime implementation target.
- PR #473 `Release: bind tagged artifacts to deterministic notes` is COMPLETE / MERGED as `3a7e8daf9565d7cb75e8dc8111d6df7ae9e90c0d`; exact merged-main push CI run `34379131661` is Green.
- PR #472 was an **optional browser-verification follow-up**, not a missing #463 implementation. Recovery of its existing branch repaired historical CI/browser defects; exact head `fce84a2c6743061d6ff402d3e6ba1d9e66ac1db6` became current with `main`, had zero unresolved review threads, and passed normal CI `34384824308`, `website-monitoring-visual` `34384824489`, protected-P0 commits `34384824328`, and protected-P0 metadata `34384824478`. The owner then explicitly directed **close without merge**. Therefore the browser harness is not part of current `main`, #472 is not an active integration target, and the Green browser evidence is historical optional verification only.
- No open issue is currently a verified repository `CODE_GAP` whose own Definition of Done can be satisfied solely by Cloud Work. The four open issues remain owner/external gates #162, #353, #116 and #111.

## Integration and READY policy

A branch/PR is legitimate READY only when all of the following are true immediately before merge:

1. it represents existing committed product/repository scope rather than an unrelated nice-to-have;
2. it does not duplicate implementation already merged, owner-closed or actively owned by another legitimate branch;
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

Owner-closed work must not be reopened or re-created merely because its branch or Green CI remains available. A new authoritative requirement is required before reviving that scope.

## Current required dependency order

The remaining production dependency is strict:

`#162 -> #116 -> #111`

Expanded:

`#162 OWNER_ONLY durable RC.61 publication + independent verification -> #116 EXTERNAL_ENVIRONMENT real trusted-IIS 15/15 acceptance -> #111 closure-only umbrella completion`

Issue #353 is independent repository governance:

`#353 OWNER_ONLY / REPOSITORY_ADMIN provider-bound main branch protection apply + independent read-back`

No #116 production mutation may begin while #162 is open. #111 cannot close before #116. #353 does not satisfy any production acceptance gate.

## P0.5 repository preparation — COMPLETE

Repository-side P0.5 implementation is complete. The selected cutover candidate remains RC.61 unless #116 explicitly selects a later equivalently verified candidate.

### Selected RC.61 identity

- version: `0.1.0-rc.61`;
- product: `Monitor-0.1.0-rc.61-win-x64.zip`;
- companion checksum: `Monitor-0.1.0-rc.61-win-x64.zip.sha256`;
- product SHA-256: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- source production-candidate run: `31667721306`;
- Actions artifact ID: `9168574442`;
- outer artifact digest: `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`;
- source head: `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`;
- tested merge: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- release tag: `v0.1.0-rc.61`;
- Acceptance Control Toolkit exact source: `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`.

Repository CI/candidate evidence is not production acceptance.

### Durable promotion — #162 OWNER_ONLY / OPEN

Repository implementation for preserving the exact existing RC.61 candidate is complete. Actual durable publication remains an explicit owner/operator action. The exact executable commands, prerequisites, expected outputs, evidence paths, abort behavior and STOP conditions live in `deploy/REMAINING_OWNER_EXTERNAL_GATES.md`.

Preferred operator sequence remains exactly:

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

The exact promotion run must finish with:

`Status = PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED`

The helper must bind one exact promotion run. Ambiguity, timeout or failure is **do not redispatch**. The returned `IndependentVerificationCommand` must be executed separately; promotion cannot self-satisfy independent verification.

After the exact promotion run and separate verifier are both Green:

```powershell
.\scripts\Test-Rc61CutoverReadiness.ps1 `
  -PromotionRunId <PROMOTION_RUN_ID> `
  -VerificationRunId <VERIFICATION_RUN_ID>
```

Require `Status = READY_FOR_P0_5_PRE_CUTOVER_PREPARATION`, `DurableReleasePrerequisiteSatisfied = True`, `ExternalGatesPassed = 0`, `ProductionMutationPerformed = False`, and `MutatedGitHubState = False`. This is **0/15** production acceptance and performs **no production mutation**.

#162 remains OPEN until independent live evidence proves the approved tag, exactly the two approved durable assets and the approved product SHA-256 after both exact workflow runs succeed.

### First Production SingleNode — #116 EXTERNAL_ENVIRONMENT / OPEN

Only after #162 completes, the actual intended Windows/IIS environment must prove the real acceptance chain. `deploy/REMAINING_OWNER_EXTERNAL_GATES.md` is the executable authority and keeps the RC.61 product/deployment candidate separate from the later acceptance-control sidecar.

Required real proof includes:

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

### P0 umbrella — #111 closure-only / OPEN

#111 is not a fourth external acceptance gate. It has no independent production action and no separate PASS condition. It closes only after #116 is accepted under its real-environment Definition of Done and the finalized #116 evidence/session remains available for review. An incorrect or premature #111 closure must be reopened; no repository-only merge may auto-close or mark #111 complete.

## Canonical completed baseline evidence retained for regression

### BATCH-700 final portal/UI closeout

- final merge: `fd33e79c6d19d7f9852417b9c35a11f91f21714c`;
- exact final head: `0834db6b5d518fe5c52eec9b47c03e467929aa89`;
- repository scope: 50/50 COMPLETE;
- #116/#111 production acceptance remains independent of this repository/UI completion.

### BATCH-800 final repository closeout

**Umbrella:** Issue #287 — CLOSED / COMPLETED

- final task: `B800-100`;
- final merge: `a6832d99f629cdbd3a93887199fe608a3ae474ec`;
- exact final head: `4379dbc0e1b346cb51bebf8e7467823c58f2361c`;
- Linux CI: `32093252549`;
- Real SQL: `32093252670`;
- Windows production-candidate: `32093252563`;
- completed B100+B200+B300+B400+B500+B600+B700+B800 task accounting: **760**.

Diagnostic truth boundaries remain unchanged: bounded **TempDB**, **transaction-log** and **HA** evidence is available, while unsupported composite conclusions remain explicit; **query regression** remains a privacy-safe evidence contract with no live query-regression collection, SQL text or query-plan collection.

## Release integrity

### Package parity — COMPLETE

`release.yml` delegates package construction to the verified Windows production-candidate workflow. Tagged/manual release packaging therefore shares the same build/test/publish/secret-free/smoke/package-validation path rather than using a weaker independent publish path.

### Durable tagged release assets — COMPLETE

For normal pushed version tags, the release workflow can publish only the already-verified same-run ZIP and companion checksum after checksum validation. It has no rebuild/repackage/clobber path and does not imply external production acceptance.

### Deterministic tagged-release notes — PR #473 COMPLETE / MERGED

Future normal tagged-release publication uses deterministic notes bound to exact release/artifact identity. PR #473 did **not** create, publish, tag or mutate selected RC.61; it does not satisfy #162 and changes no #116/#111 state.

## Website Monitoring — #463/#466 COMPLETE / MERGED

The required Website Monitoring implementation is complete through PR #467 -> PR #464 and includes bounded target administration, fail-closed outbound authorization, DNS/TCP/TLS/HTTP evidence, durable target/history/scheduler/check state, incident reconciliation, bounded correlation, recipient groups, SMTP environment-secret resolution, durable notification outbox, authenticated management/cached-live UI, SharedState stores, CAS-backed ownership and distributed per-target coordination where required.

Monitoring and notifications remain default-disabled. MultiNode remains fail-closed unless all existing SharedState/coordination prerequisites are satisfied.

### Optional browser evidence truth

PR #472 introduced an optional Chromium/Playwright verification proposal. Its recovered exact head passed all selected verification gates and retained a browser artifact, but the owner explicitly closed the PR **without merge**. Consequently current `main` does not include the browser workflow/script/doc/test from #472, and no screenshot/browser run is claimed as merged product evidence. That closure does not make Website Monitoring incomplete.

## Repository governance — #353 OWNER_ONLY / OPEN

Repository-side branch-protection helper, safety tests and documentation are complete. Actual GitHub main protection remains an authenticated repository-admin action plus independent read-back. Required live policy remains provider-bound `build`, `protected-p0-pr-metadata`, `protected-p0-pr-commits`, strict/up-to-date checks, admin enforcement and conversation resolution enabled, force pushes and branch deletion disabled, with no unrelated PR-review/restriction expansion.

Do not close #353 from helper code, preview output, CI Green or merge evidence. Close only after actual application plus independent repository-admin read-back proves the exact live policy.

## Stable safety/truth boundaries

- Browser monitoring GETs remain cache/control-plane only and never initiate monitored SQL collection.
- Browser/UI code never connects directly to monitored SQL.
- Missing, stale, truncated, permission-limited or uncollected evidence remains explicit; it must not become synthetic zero/healthy/default truth.
- No autonomous remediation or AI-generated SQL execution.
- Credentials, full connection strings, current secret references, raw provider errors, arbitrary SQL text, client/table data and physical paths remain outside UI/audit/telemetry/exports/diagnostics/production evidence according to existing contracts.
- Mutations require the existing POST + antiforgery + named authorization boundaries.
- MultiNode stays fail-closed until its existing distributed prerequisites are genuinely satisfied.
- Repository CI cannot manufacture RC.61 publication, trusted-IIS acceptance, branch-protection enforcement or any other owner/external PASS.

## Current next legal actions

1. Keep owner-closed #472 closed unless new authoritative scope explicitly reopens it; do not duplicate its optional browser harness.
2. Repair any future exact-main regression or legitimate stale READY integration before new feature work.
3. Owner completes #162 through explicit promotion + separate verification + independent tag/exact-two-assets/hash/readiness evidence.
4. After #162 only, execute #116 on the actual trusted Windows/IIS/SQL environment and collect/review real 15/15 evidence.
5. Close #111 only after #116; #111 is closure-only and cannot manufacture another external PASS.
6. Repository admin applies and independently verifies #353 separately.

## Definition of Done for the active production plan

The active production plan is complete only when:

- #162 has passed its explicit owner-operated durable-publication and independent-verification closure rule;
- #116 has passed every real trusted-IIS/HTTPS/least-privilege/recycle/durability/backup/rollback 15/15 acceptance gate with reviewed evidence;
- #111 is closed only after #116;
- #353 is closed only after exact live repository-admin branch-protection apply/read-back;
- every repository integration used current-base, zero-thread and exact-head Green evidence and exact-main post-merge verification;
- no incomplete owner/external result is represented as repository completion.
