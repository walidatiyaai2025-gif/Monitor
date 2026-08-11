# Implementation Plan

This is the canonical execution plan. Update it in the same PR as material implementation changes.

## CURRENT P0 — Real SQL Production MVP

**Umbrella:** Issue #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Production acceptance guide:** `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`  
**Active gate:** Issue #116 / P0.5 First Production SingleNode  
**Repository candidate:** PR #126 merged as `d512ee156f07db566898a817f3c76dd3f46c1091`  
**Project rule:** until P0.5 is accepted, production-slice blockers take priority over unrelated feature expansion.

The immediate product outcome is one trustworthy vertical slice:

`Login -> Add SQL Server -> Test -> Save -> Collect -> View Server Details -> Refresh -> Restart Monitor -> View trustworthy persisted target again`

Production-visible values must come from collected evidence. Missing, stale, permission-limited or uncollected dimensions must be explicit; default numeric values must never masquerade as measurements.

### P0 release chain

| Order | Release gate | Issue | Outcome | State |
|---|---|---|---|---|
| 1 | P0.1 | #112 | Real SQL registration: safe, testable and restart durable | COMPLETE — PR #119 / final CI `31476747212` |
| 2 | P0.2 | #113 | First real snapshot + truthful read-model mapping | COMPLETE — PR #121 / final CI `31478470867` |
| 3 | P0.3 | #114 | Server Details v0.1 trusted evidence surface | COMPLETE — PR #122 / final CI `31479311552` |
| 4 | P0.4 | #115 | Real SQL end-to-end acceptance under success/failure cases | COMPLETE — PR #124 merged `f4c08292734c293a6d0b865cc2a005b8c42b02a6`; normal `31481874425`; Real SQL `31481874501` |
| 5 | P0.5 | #116 | First IIS/HTTPS SingleNode production release | ACTIVE — repository preparation merged; external IIS acceptance pending |

### Resolved production gates

- **P0.1 COMPLETE:** candidate Test Connection precedes durable registration commit; failed/cancelled Monitor-owned candidate credentials are compensated safely.
- **P0.2 COMPLETE:** absence is truthful; uncollected CPU/Memory/Agent data is not rendered as fake numeric zero.
- **P0.3 COMPLETE:** Server Details is evidence-first, synthetic Health Score is removed, and monitored GET remains cache-only.
- **P0.4 COMPLETE:** SQL Server 2022 proves the full Add/Test/Register/Collect/View/Refresh/Restart/View journey with a non-sysadmin least-privilege login plus controlled bad-password/network/timeout/TLS/server-permission/msdb-permission failure cases.
- P0.4 final same-head evidence: normal CI `31481874425` — 518/518; Real SQL `31481874501` — 8/8; both Green with 0 warnings/errors.

### P0.5 repository preparation — merged

Two repository-side PRs now provide the production candidate and the operator acceptance tooling:

- PR #127 merged as `9bdd96940454f2586c0e81ff0c25a524d7f1281c`; CI `31485290730`, 522/522. It added `scripts/Accept-ProductionSingleNode.ps1` and `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`.
- PR #126 merged as `d512ee156f07db566898a817f3c76dd3f46c1091` after exact-head normal CI, Real SQL and Windows candidate gates all passed.

Final RC.20 evidence:

- tested source head `da69636f2ba3525426c43aaca17d17617244d9d4`;
- exact tested merge ref including PR #127 `f65ed48cea537b3ee7524f244a75a62fb442bd55`;
- normal CI `31485712373` Green;
- Real SQL `31485712392` Green, 8/8;
- Windows production-candidate `31485712370` Green on Windows Server 2025;
- Release build 0 warnings / 0 errors;
- 531/531 Windows candidate tests passed;
- `win-x64` framework-dependent publish;
- secret-free SingleNode package baseline;
- Production HTTPS startup with runtime-only masked Administrator material;
- `/health/live`, `/health/ready`, `/health` Green before and after restart;
- real antiforgery-protected Administrator login and authenticated `/servers/connections` access before and after restart;
- persistent local Data Protection key-ring directory observed after restart;
- runtime Production config/state removed before final packaging;
- candidate `Monitor-0.1.0-rc.20-win-x64.zip`;
- package SHA-256 `27743b8f3f162a43f8c1bb6b7b9d1977dc5d1c55e8a4664f0c84294b094150bc`;
- GitHub Actions artifact ID `9099041392`.

This repository evidence is intentionally **not** the production acceptance gate. It proves package/runtime behavior before deployment; it does not replace actual IIS, trusted HTTPS, application-pool identity, deployed least-privilege SQL, real recycle durability, or rollback rehearsal.

### P0.5 execution order

| Task | Required result | State |
|---|---|---|
| P0-041 | Freeze production scope to SingleNode | COMPLETE — repository/CI |
| P0-042 | Secret-free Production baseline; runtime-only credentials | COMPLETE — repository/CI |
| P0-043 | Deploy to actual IIS with trusted HTTPS | **PENDING EXTERNAL** |
| P0-044 | Prove Data Protection/protected credentials through restart/recycle | CI process-restart VERIFIED; **IIS recycle pending external** |
| P0-045 | Prove registration/audit/history/incidents through real recycle | **PENDING EXTERNAL** |
| P0-046 | Run health smoke on deployed HTTPS endpoint | CI HTTPS VERIFIED; acceptance script READY; **IIS endpoint pending external** |
| P0-047 | Prove target remains read-only/least-privilege from deployed application identity | P0.4 prerequisite VERIFIED; **external deployment evidence pending** |
| P0-048 | Create/validate backup and rehearse rollback/recovery | code/unit/tooling VERIFIED; **production rehearsal pending external** |
| P0-049 | Versioned artifact/checksum/manifest/evidence | COMPLETE — RC.20 recorded |
| P0-050 | Final P0.5 acceptance and #111 closure | **PENDING EXTERNAL** |

### Immediate next actions

1. Select RC.20 or a later final-head-equivalent artifact and preserve its filename + SHA-256 as cutover evidence.
2. Deploy it to the intended Windows/IIS SingleNode host under the approved application-pool identity.
3. Bind the real hostname to an approved trusted HTTPS certificate and configure approved environment/secret values.
4. Run `scripts/Accept-ProductionSingleNode.ps1` against the actual HTTPS endpoint and retain its evidence JSON.
5. Register/test/collect from the approved least-privilege SQL target and confirm no DML/write privilege is required.
6. Recycle the application pool and verify protected credential resolution, registration, audit/history/incidents and trustworthy cached/read paths recover.
7. Create and validate the operational backup; execute the approved rollback/recovery rehearsal and repeat health/auth/read checks.
8. Record host/environment/version/package SHA/recycle/rollback evidence in `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md` or an approved environment evidence record.
9. Only then complete the remaining P0.5 external tasks, close #116 and close umbrella #111.

## Verified foundation

The following foundations remain verified and are not reopened by P0.5:

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

Detailed task-level ledgers remain authoritative for completed batches:

- `docs/BATCH_100.md` — B100-001..100 complete.
- `docs/BATCH_200.md` — B200-001..100 complete.
- BATCH-300 — B300-001..100 complete; final reconciled CI `31465013971`.
- `docs/BATCH_400.md` — B400-001..110 complete across portal/typography plus production DBA diagnostics continuation.

The historical batch work remains available, but no additional feature breadth outranks P0.5 external production acceptance.

## Stable guardrails

- Browser GETs for monitoring surfaces remain cache/control-plane only and do not initiate monitored SQL collection.
- No browser connects directly to monitored SQL.
- No autonomous remediation or AI-generated SQL execution.
- Credentials/full connection strings/current secret references/raw provider errors/arbitrary SQL text remain outside UI, audit, telemetry, exports and diagnostics.
- Mutations require POST + antiforgery + named authorization.
- Suppression does not rewrite incident evidence.
- Maintenance changes scheduled collection behavior only; manual refresh remains explicit/audited.
- MultiNode remains fail-closed and deferred until after a stable SingleNode production release.

## Definition of done

The current plan is complete only when P0-001..050 are reconciled, P0.1..P0.5 are accepted in order, the final SingleNode release has actual IIS/HTTPS/recycle/least-privilege/rollback evidence, and final required CI/acceptance gates are Green.
