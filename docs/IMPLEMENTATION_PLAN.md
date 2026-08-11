# Implementation Plan

This is the canonical execution plan. Update it in the same PR as material implementation changes.

## CURRENT P0 — Real SQL Production MVP

**Umbrella:** Issue #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Production acceptance guide:** `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`  
**Active release gate:** Issue #116 / P0.5 First Production SingleNode  
**Repository cutover/evidence/finalization tooling:** COMPLETE through Issue #147 / PR #148  
**Live selected candidate/evidence ledger:** Issue #116  
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
| 5 | P0.5 | #116 | First trusted-HTTPS IIS SingleNode production release | **ACTIVE — repository tooling complete; real environment acceptance pending** |

### Resolved production gates

- **P0.1 COMPLETE:** candidate Test Connection precedes durable registration commit; failed/cancelled Monitor-owned candidate credentials are compensated safely.
- **P0.2 COMPLETE:** absent evidence stays absent; uncollected CPU/Memory/Agent dimensions are not rendered as fake numeric zero.
- **P0.3 COMPLETE:** Server Details is evidence-first, synthetic Health Score is removed, and monitored GET remains cache-only.
- **P0.4 COMPLETE:** SQL Server 2022 proves Add/Test/Register/Collect/View/Refresh/Restart/View with a non-sysadmin least-privilege login and controlled auth/network/timeout/TLS/server/msdb permission failures. Final normal CI `31481874425` — 518/518; Real SQL `31481874501` — 8/8.

## P0.5 repository preparation — COMPLETE

The repository contains the complete operator cutover and evidence workflow while intentionally leaving production acceptance external:

- PR #127 — HTTPS-only acceptance harness and production acceptance guide; merged `9bdd96940454f2586c0e81ff0c25a524d7f1281c`.
- PR #126 — Windows production-candidate pipeline; merged `d512ee156f07db566898a817f3c76dd3f46c1091`.
- PR #129 — safe IIS preflight + plan-first/apply-gated deployment + stable external `App_Data` + automatic physicalPath rollback; merged `7cb47945b47aab6558f7132dcfa818b9f02d2b20`.
- BATCH-500 / BATCH-600 — production safety and live operator-readiness orchestration; complete without changing the external-acceptance boundary.
- PR #142 / Issue #141 — exact 15-gate fail-closed evidence pack and closure validator; complete.
- PR #145 / Issue #144 — explicit one-gate-at-a-time recorder `Set-ProductionAcceptanceGate.ps1`; complete.
- PR #148 / Issue #147 — explicit fail-closed final operator acceptance finalizer `Complete-ProductionAcceptance.ps1`; complete and merged `e15a9654fbe744e426c95d5965a5faba60868e14`.

### Final repository candidate evidence — RC.43

Issue #116 is the live source of truth. Current selected candidate:

- package `Monitor-0.1.0-rc.43-win-x64.zip`;
- product SHA-256 `95d6d545cfa53fb514814fb22c82cfafc2c14cf28c1e07c15177852b677234aa`;
- Actions artifact `9119560465`;
- source head `d05bea3ea1372a6566eb9c237bb06e84de681014`;
- tested merge ref `0445ac9c8bbeafb075a506a06231dd87c4b1b27b`;
- normal CI `31537914600` Green;
- Real SQL `31537914667` Green, 8/8;
- Windows production-candidate `31537914596` Green, Release 0 warnings/errors, 761/761;
- recorder + finalizer runtime and exact synthetic 15/15 closure validation Green;
- HTTPS health/authentication before and after process restart Green;
- package is secret-free SingleNode with persisted runtime state excluded.

RC.43 supersedes RC.41 unless a later equivalently verified candidate is explicitly selected on #116.

Repository candidate evidence is **not** production acceptance. It does not replace actual IIS, a trusted machine certificate, intended app-pool identity, real recycle durability, deployed least-privilege SQL behavior, operational backup, rollback rehearsal, or human review of the real evidence.

### Finalizer contract — COMPLETE

`Complete-ProductionAcceptance.ps1` closes the last manual JSON mutation in the external evidence workflow:

1. requires explicit `-AcknowledgeFinalAcceptance` and a bounded non-secret operator identity;
2. never changes a gate from FAIL to PASS;
3. restricts closure summary output to a relative in-root path;
4. creates and validates a prospective finalized copy against all exact 15 SHA-bound gates before authoritative mutation;
5. re-hashes the authoritative pack to detect concurrent mutation;
6. atomically commits only `acceptedBy` / `acceptedAtUtc`;
7. revalidates the authoritative finalized pack and writes the closure summary;
8. restores the original unaccepted pack if final authoritative validation unexpectedly fails;
9. refuses existing acceptance metadata, existing closure summary, unsafe paths and re-finalization;
10. has no IIS deployment/recycle, SQL execution, GitHub API call or issue-closing authority.

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
| P0-049 | Versioned artifact/checksum + deterministic evidence/finalization workflow | **COMPLETE — repository/CI; RC.43 verified** |
| P0-050 | Final real-environment 15/15 acceptance and #111 closure | **PENDING EXTERNAL** |

### Immediate next actions — external cutover only

1. Preserve RC.43 filename/hash/source/tested merge evidence from #116 and create/validate the pre-cutover operational backup.
2. On the intended Windows/IIS host, run packaged IIS preflight and retain bounded non-secret evidence.
3. Review `Deploy-ProductionSingleNode.ps1` in PLAN ONLY mode, then perform the approved cutover with explicit `-Apply`.
4. Prove trusted HTTPS health/authentication and the approved least-privilege monitored SQL path.
5. Recycle IIS and prove registration, protected credential and operational-state durability.
6. Rehearse rollback/recovery and repeat health/auth/read checks.
7. Record each real gate with `Set-ProductionAcceptanceGate.ps1` and SHA-bound non-secret evidence.
8. After real 15/15, run `Complete-ProductionAcceptance.ps1` with the approved operator identity and explicit final acknowledgement; retain the closure summary.
9. Human-review the real closure evidence. Only then may #116 close; #111 closes only after #116.

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
- `docs/BATCH_200.md` — B200-001..100 COMPLETE.
- BATCH-300 — B300-001..100 COMPLETE; final reconciled CI `31465013971`.
- `docs/BATCH_400.md` — B400-001..110 COMPLETE.
- BATCH-500 — B500-001..100 COMPLETE.
- BATCH-600 — B600-001..100 COMPLETE.

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
- Repository CI/synthetic evidence/finalizer validation cannot close #116.

## Definition of done

The plan is complete only when P0-001..050 are reconciled, P0.1..P0.5 are accepted in order, the selected SingleNode release has actual trusted-HTTPS IIS/recycle/least-privilege/backup/rollback evidence, the real 15/15 evidence pack is explicitly finalized and validates, and the final required CI/acceptance gates are Green.