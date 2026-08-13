# Implementation Plan

This is the canonical execution plan. Update it in the same PR as material implementation changes.

## CURRENT P0 — Real SQL Production MVP

**Umbrella:** Issue #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Production acceptance guide:** `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`  
**Active release gate:** Issue #116 / P0.5 First Production SingleNode  
**Repository cutover/evidence/session/finalization/release/durable-tag tooling:** COMPLETE through Issue #159 / PR #160.  
**Live selected candidate/evidence ledger:** Issue #116 — RC.61  
**Project rule:** until P0.5 is accepted on the real environment, production-slice blockers outrank unrelated feature expansion.

The production outcome remains one trustworthy vertical slice:

`Login -> Add SQL Server -> Test -> Save -> Collect -> View Server Details -> Refresh -> Restart/Recycle -> View trustworthy persisted target again`

Production-visible values must come from collected evidence. Missing, stale, permission-limited or uncollected dimensions must be explicit; default numeric values must never masquerade as measurements.

### P0 release chain

| Order | Release gate | Issue | Outcome | State |
|---|---|---|---|---|
| 1 | P0.1 | #112 | Real SQL registration: safe, testable and restart durable | COMPLETE — PR #119 / final CI `31476747212` |
| 2 | P0.2 | #113 | First real snapshot + truthful read-model mapping | COMPLETE — PR #121 / final CI `31478470867` |
| 3 | P0.3 | #114 | Server Details v0.1 trusted evidence surface | COMPLETE — PR #122 / final CI `31479311552` |
| 4 | P0.4 | #115 | Real SQL end-to-end acceptance under success/failure cases | COMPLETE — PR #124; normal `31481874425`; Real SQL `31481874501` |
| 5 | P0.5 | #116 | First trusted-HTTPS IIS SingleNode production release | **ACTIVE — repository workflow complete through durable tag publication; real environment acceptance pending** |

### Resolved production gates

- **P0.1 COMPLETE:** candidate Test Connection precedes durable registration commit; failed/cancelled Monitor-owned candidate credentials are compensated safely.
- **P0.2 COMPLETE:** absent evidence stays absent; uncollected CPU/Memory/Agent dimensions are not rendered as fake numeric zero.
- **P0.3 COMPLETE:** Server Details is evidence-first, synthetic Health Score is removed, and monitored GET remains cache-only.
- **P0.4 COMPLETE:** SQL Server 2022 proves Add/Test/Register/Collect/View/Refresh/Restart/View with a non-sysadmin least-privilege login and controlled auth/network/timeout/TLS/server/msdb permission failures. Final normal CI `31481874425` — 518/518; Real SQL `31481874501` — 8/8.

## P0.5 repository preparation — COMPLETE / EXTERNAL ACCEPTANCE ACTIVE

The repository contains the complete operator cutover, evidence and release workflow while intentionally leaving production acceptance external:

- PR #127 — HTTPS-only acceptance harness and production acceptance guide; merged `9bdd96940454f2586c0e81ff0c25a524d7f1281c`.
- PR #126 — Windows production-candidate pipeline; merged `d512ee156f07db566898a817f3c76dd3f46c1091`.
- PR #129 — safe IIS preflight + plan-first/apply-gated deployment + stable external `App_Data` + automatic physicalPath rollback; merged `7cb47945b47aab6558f7132dcfa818b9f02d2b20`.
- BATCH-500 / BATCH-600 — production safety and live operator-readiness orchestration; complete without changing the external-acceptance boundary.
- PR #142 / Issue #141 — exact 15-gate fail-closed evidence pack and closure validator; complete.
- PR #145 / Issue #144 — explicit one-gate-at-a-time recorder `Set-ProductionAcceptanceGate.ps1`; complete.
- PR #148 / Issue #147 — explicit fail-closed final operator acceptance finalizer `Complete-ProductionAcceptance.ps1`; complete and merged `e15a9654fbe744e426c95d5965a5faba60868e14`.
- PR #151 / Issue #150 — immutable candidate-bound acceptance-session initializer `New-ProductionAcceptanceSession.ps1`; complete and merged `9a76abe61422502c4889b04ce8b6a59f18ac04f4`.
- PR #155 / Issue #154 — tagged/manual release-package parity; **COMPLETE**, squash-merged `8d8ae2c5f35e8a1d774c5a9480f582e432e5dc03`. `production-candidate.yml` is reusable through `workflow_call`; `release.yml` delegates to that exact Windows workflow; explicit candidate versions are syntax-bounded; manifest schema 2 records fixed P0.4 run IDs under `prerequisiteEvidence.p04` and leaves candidate-specific CI authoritative on #116.
- PR #160 / Issue #159 — durable tagged GitHub Release assets; **COMPLETE**, squash-merged `a14110181932bcd6e14b99e5b6984974a5b477f8`. Real pushed version tags publish only the already-verified same-run ZIP + `.sha256` after checksum re-verification; package construction remains solely in `production-candidate.yml`; publication is tag-only, has job-scoped `contents: write`, never rebuilds/repackages, never clobbers an existing release, and accepts a rerun only when existing release assets exactly match the verified product/checksum. Final exact-head normal CI `31677055397` was Green 809/809, Real SQL `31677055241` Green 8/8, and Windows production-candidate `31677055305` Green 809/809.

### Selected repository candidate evidence — RC.61

Issue #116 is the live source of truth. Current selected candidate:

- package `Monitor-0.1.0-rc.61-win-x64.zip`;
- product SHA-256 `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- Actions artifact `9168574442`;
- outer artifact digest `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`;
- source head `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`;
- tested merge ref `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- merged main commit `8d8ae2c5f35e8a1d774c5a9480f582e432e5dc03`;
- normal CI `31667721350` Green, Release 0 warnings/errors, 770/770;
- Real SQL `31667721353` Green, 8/8;
- Windows production-candidate `31667721306` Green, Release 0 warnings/errors, 770/770;
- candidate-version validation, immutable session initializer, recorder, finalizer and exact synthetic 15/15 closure validation Green;
- HTTPS health/authentication before and after process restart Green;
- package is secret-free SingleNode with persisted runtime state excluded.

Independent artifact inspection on 2026-08-13 recomputed the product SHA-256, matched the companion checksum, confirmed 95 package files and all 19 expected `_operations` entries, and verified manifest schema 2 with `prerequisiteEvidence.p04`, `candidateVerification.sourceOfTruth=#116`, `embeddedWorkflowRunIds=false`, and no legacy `realSqlAcceptance` field.

RC.61 supersedes RC.53 unless a later equivalently verified candidate is explicitly selected on #116. The RC.75 artifact produced while verifying PR #160 is implementation evidence only and is not automatically promoted.

Repository candidate evidence is **not** production acceptance. It does not replace actual IIS, a trusted machine certificate, intended app-pool identity, real recycle durability, deployed least-privilege SQL behavior, operational backup, rollback rehearsal, or human review of the real evidence.

### Finalizer contract — COMPLETE

`Complete-ProductionAcceptance.ps1` closes the last manual JSON mutation in the external evidence workflow:

1. requires explicit `-AcknowledgeFinalAcceptance` and a bounded non-secret operator identity;
2. never changes a gate from FAIL to PASS;
3. restricts closure summary output to a relative path under the evidence-pack root;
4. creates and validates a prospective finalized copy against all exact 15 SHA-bound gates before authoritative mutation;
5. re-hashes the authoritative pack to detect concurrent mutation;
6. atomically commits only `acceptedBy` / `acceptedAtUtc`;
7. revalidates the authoritative finalized pack and writes the closure summary;
8. restores the original unaccepted pack if final authoritative validation unexpectedly fails;
9. refuses existing acceptance metadata, existing closure summary, unsafe paths and re-finalization;
10. has no IIS deployment/recycle, SQL execution, GitHub API call or issue-closing authority.

### Immutable acceptance session contract — COMPLETE

`New-ProductionAcceptanceSession.ps1` makes the last pre-cutover setup deterministic without manufacturing production evidence:

1. requires a fresh absolute Windows session root and rejects drive/share roots, leading/trailing whitespace, relative roots, explicit `.` / `..` traversal segments and reuse;
2. verifies exact candidate filename/version, matching checksum contract, actual artifact SHA-256 and readable non-empty ZIP before creating the session;
3. validates non-secret production metadata through the existing evidence-pack contract;
4. atomically creates a candidate-bound workspace and copies the exact artifact/checksum into `candidate/`;
5. invokes the canonical 15-gate generator and verifies all 15 gates remain false with no final acceptance metadata;
6. writes bounded non-secret `session-manifest.json`, `session-manifest.sha256` and deterministic `OPERATOR-NEXT-STEPS.txt`;
7. creates `evidence/proof/` as the bounded authoritative proof root;
8. returns `ExternalGateCount=15`, `ExternalGatesPassed=0`, `ProductionAccepted=false`;
9. never deploys/recycles IIS, executes SQL, records a gate PASS, finalizes acceptance, calls GitHub or closes #116/#111;
10. is parsed, executed and packaged by the Windows production-candidate gate with positive and negative runtime cases.

### Release-package parity contract — COMPLETE

The release artifact no longer has a weaker construction path than the selected production candidate:

1. `production-candidate.yml` is the single reusable Windows package workflow for PR candidates and release callers;
2. an explicit reusable `candidate_version` is syntax-bounded before it can reach artifact paths/version metadata;
3. `release.yml` resolves/validates the tag/manual version and delegates packaging to the reusable production-candidate workflow rather than running independent publish/zip/upload steps;
4. tagged/manual releases inherit the same Release build warnings-as-errors, full tests, production PowerShell parser, immutable-session runtime, recorder/finalizer runtime, RID-specific win-x64 publish, secret-free baseline validation, HTTPS/auth smoke before/after restart, runtime-state removal, `_operations` staging, clean-package validation and SHA-256 artifact upload;
5. release manifest schema 2 records fixed P0.4 run IDs as `prerequisiteEvidence.p04`, while candidate-specific run evidence remains authoritative on #116;
6. regression tests fail if independent release packaging or the ambiguous `realSqlAcceptance` manifest field returns;
7. this is repository release-integrity evidence only and cannot satisfy a real IIS gate.

### Durable tagged release asset contract — #159 / PR #160 COMPLETE

A real version tag remains recoverable after GitHub Actions artifact retention expires without introducing a second build path:

1. only a pushed version tag may enter the durable publication job; `workflow_dispatch` and PR candidate runs remain Actions-artifact-only;
2. publication depends on the successful reusable Windows production-candidate job and downloads that exact same-run artifact by deterministic name;
3. the downloaded ZIP must match its strict companion SHA-256 record before any GitHub Release mutation;
4. `contents: write` is scoped only to the tag-publication job; default workflow and candidate permissions remain `contents: read`;
5. a new release is created only for the exact existing pushed tag with `--verify-tag`, and only the verified ZIP plus `.sha256` are attached;
6. non-plain semantic versions are marked prerelease;
7. reruns do not upload or clobber assets: if a release exists, both assets are downloaded and must exactly match the expected product hash/checksum/filename; missing or mismatched assets fail closed;
8. regression tests prohibit independent `dotnet publish`, packaging, `upload-artifact` in `release.yml`, `gh release upload`, and `--clobber`;
9. #159 completion proves release durability only; it does not satisfy any external IIS gate or change the selected cutover candidate automatically.

### P0.5 execution order

| Task | Required result | State |
|---|---|---|
| P0-041 | Freeze production scope to SingleNode | COMPLETE — repository/CI |
| P0-042 | Secret-free Production baseline; runtime-only credentials | COMPLETE — repository/CI |
| P0-043 | Deploy to actual IIS with trusted HTTPS | **PENDING EXTERNAL** |
| P0-044 | Prove Data Protection/protected credentials through restart/recycle | CI process-restart VERIFIED; **IIS recycle pending external** |
| P0-045 | Prove registration/audit/history/incidents through real recycle | **PENDING EXTERNAL** |
| P0-046 | Run health smoke on deployed HTTPS endpoint | CI HTTPS VERIFIED; acceptance tooling READY; **IIS endpoint pending external** |
| P0-047 | Prove target remains read-only/least-privilege from deployed application identity | P0.4 prerequisite VERIFIED; **external deployment evidence pending** |
| P0-048 | Create/validate backup and rehearse rollback/recovery | code/unit/tooling VERIFIED; **production rehearsal pending external** |
| P0-049 | Versioned artifact/checksum + deterministic session/evidence/finalization/release workflow | **COMPLETE — repository/CI; RC.61 selected; durable tagged GitHub Release publication verified through #159/#160** |
| P0-050 | Final real-environment 15/15 acceptance and #111 closure | **PENDING EXTERNAL** |

### Immediate next actions

1. Preserve RC.61 and product SHA-256 from #116 unless #116 explicitly selects another equivalently verified candidate.
2. On the intended Windows/IIS host, create/validate the pre-cutover operational backup.
3. Start the real cutover by creating one fresh immutable candidate-bound acceptance session; verify `session-manifest.sha256`, `PreparedFailClosed` and 0/15 before any production mutation.
4. Run packaged IIS preflight, review PLAN ONLY deploy output, then cut over with explicit `-Apply`.
5. Prove trusted HTTPS health/authentication and the approved least-privilege monitored SQL path.
6. Recycle IIS and prove registration, protected credential and operational-state durability.
7. Rehearse rollback/recovery and repeat health/auth/read checks.
8. Record each real gate with `Set-ProductionAcceptanceGate.ps1` and SHA-bound non-secret evidence from the same session.
9. After real 15/15, run `Complete-ProductionAcceptance.ps1` with the approved operator identity and explicit final acknowledgement; retain the closure summary.
10. Human-review the real closure evidence. Only then may #116 close; #111 closes only after #116.

## Verified foundation

| Milestone | Scope | State |
|---|---|---|
| M0 | Visual foundation, secure development auth, shell, Command Center and CI/visual acceptance | VERIFIED |
| M1 | Registration/secret boundary, Test Connection, collector, snapshot/cache, real UI, throttled refresh | VERIFIED |
| M2 | Memory/database/backup/Agent/storage/blocking/performance health modules | VERIFIED |
| M3 | Deterministic findings, incidents, recommendations and operator workflow | VERIFIED |
| M4 | AI Advisor advisory-only boundary, guarded request/cache/timeout/circuit/audit | VERIFIED |
| M5 | History, collection cycle, trends, scheduler, audit, RBAC, browser security | VERIFIED |
| M6 | Real multi-server onboarding and estate UI | VERIFIED |
| M7 | Durable registration/operational state/protected credentials/shared-state readiness/deployment safety | VERIFIED |
| M8 | Zero-SQL monitored GETs and explicit protected refresh | VERIFIED |

## Historical hardening batches

- `docs/BATCH_100.md` — B100-001..100 COMPLETE.
- `docs/BATCH_200.md` — B200-001..100 COMPLETE; current-main reconciliation **COMPLETE** through Issue #99 / PR #156, squash-merged `221e44a9f13ed02e994311addff94b0e7996e444`. Final exact-head normal CI `31669072593`, Real SQL `31669072572`, and Windows production-candidate `31669072625` are Green.
- BATCH-300 — B300-001..100 COMPLETE; final reconciled CI `31465013971`.
- `docs/BATCH_400.md` — B400-001..110 COMPLETE.
- BATCH-500 — B500-001..100 COMPLETE.
- BATCH-600 — B600-001..100 COMPLETE.

The BATCH-200 reconciliation selectively restored retention governance, enterprise security hardening and bounded scale primitives plus mapped B200-051..090 regression coverage and an additional audit-pagination regression on RC.61-era current main. Legacy issues #87/#91/#93 are closed completed, while stale PRs #88/#92/#94/#104 are closed unmerged as superseded. This was baseline correction rather than feature expansion or new task accounting; it preserves `IServerTargetLifecycleService`, BATCH-300 and all P0 production/release boundaries and does not change #116 or selected RC.61.

Historical feature breadth remains available, but it does not outrank the remaining P0.5 production acceptance gate.

## Stable guardrails

- Browser monitoring GETs remain cache/control-plane only and never initiate monitored SQL collection.
- No browser connects directly to monitored SQL.
- No autonomous remediation or AI-generated SQL execution.
- Credentials/full connection strings/current secret references/raw provider errors/arbitrary SQL text remain outside UI, audit, telemetry, exports, diagnostics and production evidence.
- Mutations require POST + antiforgery + named authorization.
- Suppression does not rewrite incident evidence.
- Maintenance changes scheduled collection behavior only; manual refresh remains explicit/audited.
- MultiNode remains fail-closed and deferred until after stable SingleNode production acceptance.
- Repository CI/synthetic evidence/session/finalizer/release-package validation cannot close #116.

## Definition of done

The plan is complete only when P0-001..050 are reconciled, P0.1..P0.5 are accepted in order, the selected SingleNode release has actual trusted-HTTPS IIS/recycle/least-privilege/backup/rollback evidence, the real 15/15 evidence pack is explicitly finalized and validates, and the final required CI/acceptance gates are Green.