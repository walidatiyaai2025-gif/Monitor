# Implementation Plan

This file is the **current repository implementation/convergence plan**. It is subordinate to live repository evidence. Execution authority is, in order:

1. exact live GitHub `main`;
2. `AGENTS.md`;
3. open PRs/issues/workflows and their current review/check state;
4. `docs/CURRENT_EXECUTION_PLAN.md`;
5. this file for the current implementation summary and dependency order.

The complete pre-convergence implementation history is preserved unchanged at `docs/history/IMPLEMENTATION_PLAN_PRE_CONVERGENCE_2026-09-09.md`. Historical branch/PR instructions there are evidence only and must not be executed unless revalidated against current live state.

The authoritative executable owner/external handoff is `deploy/REMAINING_OWNER_EXTERNAL_GATES.md`. It remains fail-closed and never counts an OWNER_ONLY or EXTERNAL gate as PASS without real retained evidence.

## Current live implementation state — 2026-09-09

Fresh base before the current integration line: `main@4d514daef24781724f24065accd95988b406d9e6`; exact-main push CI `34382598596` Green.

Completed baseline:

- M0 through M8 verified;
- BATCH-100 through BATCH-800 complete in repository scope; #287 closed completed;
- P0.1 through P0.4 complete with real repository/Real-SQL evidence;
- Website Monitoring #463/#466 complete through PR #467 -> PR #464;
- P0.5 repository packaging/deployment/evidence/session/finalization/recovery/release tooling complete;
- PR #473 deterministic tagged-release-note identity complete/merged as `3a7e8daf9565d7cb75e8dc8111d6df7ae9e90c0d`, exact merged-main CI `34379131661` Green.

## Canonical retained closeout evidence

The compact live plan preserves the canonical evidence required by repository regression gates and must not replace it with abbreviated state:

- BATCH-700 remains **50/50 COMPLETE**. Final PR #240 squash-merged as `fd33e79c6d19d7f9852417b9c35a11f91f21714c` from exact final head `0834db6b5d518fe5c52eec9b47c03e467929aa89`.
- BATCH-800 remains **100/100 COMPLETE** only at **B800-100**.
- **Umbrella:** Issue #287 — CLOSED / COMPLETED.
- B800 final PR #335 squash-merged as `a6832d99f629cdbd3a93887199fe608a3ae474ec`; exact final head `4379dbc0e1b346cb51bebf8e7467823c58f2361c` passed CI `32093252549`, Real SQL `32093252670`, and Windows production-candidate `32093252563`.
- Completed task accounting remains **760** for the canonical BATCH-800 closeout; later closeout/documentation PRs do not double-count that total.
- The diagnostic truth boundary remains explicit for **TempDB**, **transaction-log**, **HA**, and **query regression** evidence: unsupported or not-evaluated domains cannot be represented as collected healthy evidence.

Current lawful cloud-actionable line: **issue #476 / PR #477 — Dashboard configurable live database status**.

Owner-closed optional line: **PR #472 — CLOSED UNMERGED BY OWNER DIRECTION**. Its recovered exact head passed its selected optional browser checks, but it is not part of `main`, not an active integration target, and must not be duplicated or revived without new authoritative scope.

## #476 / PR #477 implementation contract

PR #477 must add the Dashboard live database-status feature without creating a second monitored-SQL collection path.

Required behavior:

- inject a `Live Database Status` panel on `/dashboard` immediately after the existing truthful SQL-estate evidence surface;
- derive per-server state, online/total database counts and freshness from already-rendered cached Dashboard evidence;
- allow only 1/2/5/10/15/30-minute auto-refresh cadence, default 5 minutes, persisted client-side;
- background refresh may perform authenticated `GET /dashboard` only;
- browser code must never call monitored SQL, collectors or `/refresh-snapshot`;
- redirects, non-HTML responses and missing expected Dashboard evidence fail closed and retain the last displayed state;
- overlapping refreshes are blocked and hidden tabs do not perform background network refresh;
- responsive animation/status feedback is allowed only as client presentation and must honor `prefers-reduced-motion`;
- existing fresh/stale/unavailable truth semantics and all P0/owner/external boundaries remain unchanged;
- regression coverage must lock cached/read-only behavior, no-direct-SQL/no-refresh mutation and reduced-motion/accessibility contracts.

`AGENTS.md` requires a material feature to update `docs/STATUS.md` and `docs/FEATURE_CATALOG.md` in the same PR. Those tracking updates belong to this single existing PR rather than a parallel documentation PR.

## Integration and READY policy

A branch/PR is legitimate READY only when all of the following are true immediately before merge:

1. it represents existing committed product/repository scope;
2. it does not duplicate implementation already merged, owner-closed or actively owned elsewhere;
3. its lawful base is identified and it is current with that base (`behind_by=0` or equivalent exact evidence);
4. unresolved review threads are zero;
5. every selected exact-head gate is completed Green; earlier-head evidence is insufficient after any commit;
6. owner-only/external acceptance is not simulated, inferred or converted to PASS;
7. its diff contains only intended implementation/reconciliation scope and preserves unrelated legitimate work.

For PR #477 the selected gate set is normal CI, Windows production-candidate, protected-P0 commit guard and protected-P0 metadata guard unless fresh repository workflow policy selects an additional gate. Real SQL is not implied by this browser/cache-only change unless live workflow selection actually requires it.

After merge:

- fetch exact `main`;
- require exact-main CI Green;
- reconcile issue/PR state to what actually merged;
- repair any integration-caused regression immediately;
- never infer owner/external completion from repository CI.

## Selected RC.61 identity

```text
Version                    0.1.0-rc.61
Tag                        v0.1.0-rc.61
ZIP                        Monitor-0.1.0-rc.61-win-x64.zip
Checksum                   Monitor-0.1.0-rc.61-win-x64.zip.sha256
Product SHA-256            d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
Source run                 31667721306
Actions artifact ID        9168574442
Outer artifact digest      sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382
Source commit              e28158da67b36dfc5dbf8f4c38b5c43d99c7c728
Tested merge               158148d8bfd05f724014541bc7a0b1eab5dae1b5
Acceptance toolkit source  b422eaaee53d931a62a43b3c36a53b68cd4f3e27
```

No live `v0.1.0-rc.61` tag or GitHub Release exists. Repository CI/candidate evidence is not durable publication or production acceptance.

## Remaining direct gates

### #162 — OWNER_ONLY / OPEN / NOT PASS

Durable RC.61 publication remains an explicit owner action. The canonical operator-helper reconciliation is merge `3cd711b608e4ceaf8872eb22a25541bbbfe2729a` and the fail-closed order remains:

1. Preview with `Invoke-Rc61DurablePromotion.ps1` and require `READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT`, **0/15** external gates, and **no production mutation**.
2. After review execute `Invoke-Rc61DurablePromotion.ps1 -AcknowledgePromotion`. If the exact promotion result is ambiguous, timed out, or failed: **do not redispatch**.
3. Require `PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED`, then separately execute the returned `IndependentVerificationCommand`; promotion must not self-satisfy independent verification.
4. Run `Test-Rc61CutoverReadiness.ps1` with the two exact run IDs and require `ExternalGatesPassed = 0` plus **no production mutation** before any #116 work.
5. Independently verify the tag, exact two release assets, and product SHA-256.

### #116 — EXTERNAL_ENVIRONMENT / OPEN / NOT PASS

Production mutation is blocked while #162 remains open. After #162 really completes, use exact RC.61 bytes plus independently verified Acceptance Control Toolkit source `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`, create one fresh 0/15 session and execute real trusted HTTPS/IIS/least-privilege/recycle/durability/backup/rollback acceptance. Final acceptance requires independently validated 15/15 evidence.

### #111 — umbrella closure only / OPEN / NOT PASS

No independent production action. Close only after #116 has genuine accepted external evidence.

### #353 — OWNER_ONLY / REPOSITORY_ADMIN / OPEN / NOT PASS

Repository helper/tests/docs are complete. Live `main.protected=false`, rulesets are empty, and the administration-gated protection endpoint is inaccessible to the connected integration. Exact provider-bound required checks, strict up-to-date checks, admin enforcement and conversation resolution must be applied by repository admin, with force pushes and deletion disabled, then independently read back.

## Dependency and safety boundary

```text
#162 -> #116 -> #111
```

#353 is independent repository governance.

Stable rules:

- browser/navigation GETs are cache/control-plane only and never initiate monitored-SQL collection;
- browser/UI code never connects directly to monitored SQL;
- missing/stale/truncated/uncollected evidence remains explicit;
- no autonomous remediation or AI-generated SQL execution;
- credentials, full connection strings, secret material, arbitrary SQL text, client/table data and physical paths remain excluded according to existing contracts;
- mutations retain POST + antiforgery + named authorization boundaries;
- MultiNode remains fail-closed until its distributed prerequisites are genuinely satisfied;
- repository CI, optional browser evidence, release tooling or documentation cannot manufacture OWNER_ONLY or EXTERNAL PASS.

## Next legal actions

1. Complete and integrate #476/#477 only after fresh exact-head READY evidence.
2. Verify exact merged `main` and repair any integration regression before new work.
3. Keep #472 closed unless new authoritative scope explicitly requires revival.
4. Owner completes #162.
5. After #162 only, execute real #116 acceptance.
6. Close #111 only after #116.
7. Repository admin applies and independently verifies #353.

`VERIFIED_FINAL_COMPLETE` remains forbidden until every required owner/external closure rule is genuinely satisfied.
