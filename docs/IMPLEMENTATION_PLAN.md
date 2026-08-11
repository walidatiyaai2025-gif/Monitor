# Implementation Plan

This is the canonical execution plan. Update it in the same PR as material implementation changes.

## CURRENT P0 — Real SQL Production MVP

**Umbrella:** Issue #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Production acceptance guide:** `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`  
**Active release gate:** Issue #116 / P0.5 First Production SingleNode  
**Active repository subtask:** Issue #147 / fail-closed final operator acceptance finalizer  
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
| 5 | P0.5 | #116 | First trusted-HTTPS IIS SingleNode production release | **ACTIVE — repository tooling verified; real environment acceptance pending** |

### Resolved production gates

- **P0.1 COMPLETE:** candidate Test Connection precedes durable registration commit; failed/cancelled Monitor-owned candidate credentials are compensated safely.
- **P0.2 COMPLETE:** absent evidence stays absent; uncollected CPU/Memory/Agent dimensions are not rendered as fake numeric zero.
- **P0.3 COMPLETE:** Server Details is evidence-first, synthetic Health Score is removed, and monitored GET remains cache-only.
- **P0.4 COMPLETE:** SQL Server 2022 proves Add/Test/Register/Collect/View/Refresh/Restart/View with a non-sysadmin least-privilege login and controlled auth/network/timeout/TLS/server/msdb permission failures. Final normal CI `31481874425` — 518/518; Real SQL `31481874501` — 8/8.

## P0.5 repository preparation

The repository now contains the full cutover/evidence toolchain while intentionally leaving production acceptance external:

- PR #127 — HTTPS-only acceptance harness and production acceptance guide; merged `9bdd96940454f2586c0e81ff0c25a524d7f1281c`.
- PR #126 — Windows production-candidate pipeline; merged `d512ee156f07db566898a817f3c76dd3f46c1091`.
- PR #129 — safe IIS preflight + plan-first/apply-gated deployment + stable external `App_Data` + automatic physicalPath rollback; merged `7cb47945b47aab6558f7132dcfa818b9f02d2b20`.
- BATCH-500 / BATCH-600 — production safety and live operator-readiness orchestration; complete without changing the external-acceptance boundary.
- PR #142 / Issue #141 — exact 15-gate fail-closed evidence pack and closure validator; complete.
- PR #145 / Issue #144 — explicit one-gate-at-a-time recorder `Set-ProductionAcceptanceGate.ps1`; complete.
- PR #146 — reconciled recorder completion and selected RC.41 evidence; merged `0dce09eb51ec95fb405480b17ed77a43d4eb5cb4`.

The selected repository-verified candidate is tracked live on #116. At the start of #147 it is RC.41: `Monitor-0.1.0-rc.41-win-x64.zip`, product SHA-256 `0017e29ad2d88f5adbb2a7da2bca51fa5fb62f4f88c2c3984795c4eee6f6c1c2`, artifact `9118116181`, with normal CI `31534154666`, Real SQL `31534154685` 8/8 and Windows candidate `31534154674` 753/753 Green.

Repository candidate evidence is **not** production acceptance. It does not replace actual IIS, a trusted machine certificate, intended app-pool identity, real recycle durability, deployed least-privilege SQL behavior, operational backup, rollback rehearsal, or human review of the real evidence.

## ACTIVE repository subtask — #147 final acceptance finalizer

The last repository-side workflow gap is manual editing of final `acceptedBy` / `acceptedAtUtc` metadata after all 15 gates are recorded. #147 removes that manual mutation without weakening the closure boundary.

Required implementation:

1. Add `scripts/Complete-ProductionAcceptance.ps1`.
2. Require explicit `-AcknowledgeFinalAcceptance` and a bounded non-secret operator identity.
3. Refuse finalization unless all exact 15 gates already validate with SHA-bound evidence.
4. Validate a prospective accepted copy before touching the authoritative pack.
5. Re-hash the authoritative pack to detect concurrent operator/process mutation.
6. Atomically commit only `acceptedBy` / `acceptedAtUtc`.
7. Revalidate the authoritative finalized pack and write the closure summary.
8. Restore the original unaccepted pack if authoritative final validation unexpectedly fails.
9. Reject rooted/traversal summary output, existing summaries and re-finalization.
10. Parse, execute and package the finalizer in Windows production-candidate CI with positive and negative cases.
11. Keep #116 and #111 OPEN; the finalizer has no deployment, SQL or GitHub issue-closing authority.

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
| P0-049 | Versioned artifact/checksum + deterministic evidence workflow | RC.41/generator/recorder/validator VERIFIED; **#147 finalizer ACTIVE** |
| P0-050 | Final real-environment 15/15 acceptance and #111 closure | **PENDING EXTERNAL** |

### Immediate next actions

1. Finish #147 implementation, full Release tests, Real SQL and Windows production-candidate gates; merge only when all are Green.
2. Keep #116 as the live selected candidate/evidence source; do not promote a later RC without equivalent gates.
3. On the intended Windows/IIS host, preserve the selected artifact/hash and create/validate the pre-cutover backup.
4. Run packaged IIS preflight, review PLAN ONLY deploy output, then cut over with explicit `-Apply`.
5. Prove trusted HTTPS health/authentication and the approved least-privilege monitored SQL path.
6. Recycle IIS and prove registration, protected credential and operational-state durability.
7. Rehearse rollback/recovery and repeat health/auth/read checks.
8. Record each real gate with `Set-ProductionAcceptanceGate.ps1` and SHA-bound non-secret evidence.
9. After real 15/15, use `Complete-ProductionAcceptance.ps1` for explicit operator finalization and retain the closure summary.
10. Only after human review of the real closure evidence may #116 close; #111 closes only after #116.

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
- Repository CI/synthetic evidence cannot close #116.

## Definition of done

The plan is complete only when P0-001..050 are reconciled, P0.1..P0.5 are accepted in order, the selected SingleNode release has actual trusted-HTTPS IIS/recycle/least-privilege/backup/rollback evidence, the real 15/15 evidence pack is explicitly finalized and validates, and the final required CI/acceptance gates are Green.