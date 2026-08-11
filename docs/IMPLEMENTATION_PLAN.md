# Implementation Plan

This is the canonical execution plan. Update it in the same PR as material implementation changes.

## CURRENT P0 — Real SQL Production MVP

**Umbrella:** Issue #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Production acceptance guide:** `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`  
**Live production candidate/environment evidence:** Issue #116  
**Active gate:** Issue #116 / P0.5 First Production SingleNode  
**Project rule:** until P0.5 is accepted, production-slice blockers take priority over unrelated feature expansion.

The immediate product outcome is one trustworthy vertical slice:

`Login -> Add SQL Server -> Test -> Save -> Collect -> View Server Details -> Refresh -> Restart Monitor -> View trustworthy persisted target again`

Production-visible values must come from collected evidence. Missing, stale, permission-limited or uncollected dimensions must be explicit; default numeric values must never masquerade as measurements.

Dynamic candidate run numbers, artifact filenames and SHA-256 values are intentionally tracked on #116. This plan records stable dependencies, merged capabilities and external acceptance work so it does not become stale when an equivalent later RC is generated.

### P0 release chain

| Order | Release gate | Issue | Outcome | State |
|---|---|---|---|---|
| 1 | P0.1 | #112 | Real SQL registration: safe, testable and restart durable | COMPLETE — PR #119 / final CI `31476747212` |
| 2 | P0.2 | #113 | First real snapshot + truthful read-model mapping | COMPLETE — PR #121 / final CI `31478470867` |
| 3 | P0.3 | #114 | Server Details v0.1 trusted evidence surface | COMPLETE — PR #122 / final CI `31479311552` |
| 4 | P0.4 | #115 | Real SQL end-to-end acceptance under success/failure cases | COMPLETE — PR #124 merged `f4c08292734c293a6d0b865cc2a005b8c42b02a6`; normal `31481874425`; Real SQL `31481874501` |
| 5 | P0.5 | #116 | First IIS/HTTPS SingleNode production release | ACTIVE — repository/operator preparation merged; external IIS acceptance pending |

### Resolved production gates

- **P0.1 COMPLETE:** candidate Test Connection precedes durable registration commit; failed/cancelled Monitor-owned candidate credentials are compensated safely.
- **P0.2 COMPLETE:** absence is truthful; uncollected CPU/Memory/Agent data is not rendered as fake numeric zero.
- **P0.3 COMPLETE:** Server Details is evidence-first, synthetic Health Score is removed, and monitored GET remains cache-only.
- **P0.4 COMPLETE:** SQL Server 2022 proves the full Add/Test/Register/Collect/View/Refresh/Restart/View journey with a non-sysadmin least-privilege login plus controlled bad-password/network/timeout/TLS/server-permission/msdb-permission failure cases.
- P0.4 final same-head evidence: normal CI `31481874425` — 518/518; Real SQL `31481874501` — 8/8; both Green with 0 warnings/errors.

### P0.5 repository/operator preparation — merged

Stable repository milestones:

- PR #127 merged `9bdd96940454f2586c0e81ff0c25a524d7f1281c`: artifact/checksum/HTTPS acceptance tooling and `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`.
- PR #126 merged `d512ee156f07db566898a817f3c76dd3f46c1091`: Windows `win-x64` production-candidate workflow with Release build/test, secret-free SingleNode package validation, HTTPS health/authentication and restart evidence.
- PR #128 merged `564f7655a1001da98addd793a000a15d069a243a`: canonical P0 reconciliation after the candidate workflow merged.
- PR #129 merged `7cb47945b47aab6558f7132dcfa818b9f02d2b20`: read-only IIS preflight, plan-first/apply-gated deployment automation, stable `App_Data`, automatic physicalPath rollback, PowerShell parser gate, and self-contained `_operations` bundle.

The candidate pipeline now provides these stable invariants regardless of RC number:

- Windows `win-x64` framework-dependent Release publish.
- Full Windows suite before packaging.
- Production PowerShell syntax parser before candidate runtime/package acceptance.
- SingleNode package contract with Development credentials and persisted runtime `App_Data` excluded.
- HTTPS `/health/live`, `/health/ready`, `/health` and real Administrator authentication before and after candidate process restart.
- Persistent local Data Protection key-ring behavior across candidate process restart.
- Versioned ZIP + SHA-256 + provenance manifest + `_operations` deployment/acceptance bundle.
- Exact latest selected candidate details are recorded on #116, not duplicated in this plan.

The operator path is now:

`verified candidate -> read-only IIS preflight -> PLAN ONLY deployment review -> explicit -Apply -> HTTPS acceptance -> IIS recycle durability -> least-privilege SQL validation -> backup/rollback rehearsal -> final evidence`

This repository evidence is intentionally **not** production acceptance. It does not replace the real IIS host, trusted HTTPS certificate, intended application-pool identity, deployed least-privilege SQL behavior, real recycle durability, or rollback rehearsal.

### P0.5 execution order

| Task | Required result | State |
|---|---|---|
| P0-041 | Freeze production scope to SingleNode | COMPLETE — repository/CI |
| P0-042 | Secret-free Production baseline; runtime-only credentials | COMPLETE — repository/CI |
| P0-043 | Deploy to actual IIS with trusted HTTPS | **PENDING EXTERNAL**; preflight/deploy automation READY |
| P0-044 | Prove Data Protection/protected credentials through restart/recycle | CI process-restart VERIFIED; **IIS recycle pending external** |
| P0-045 | Prove registration/audit/history/incidents through real recycle | **PENDING EXTERNAL** |
| P0-046 | Run health smoke on deployed HTTPS endpoint | CI HTTPS VERIFIED; acceptance script READY; **IIS endpoint pending external** |
| P0-047 | Prove target remains read-only/least-privilege from deployed application identity | P0.4 prerequisite VERIFIED; **external deployment evidence pending** |
| P0-048 | Create/validate backup and rehearse rollback/recovery | code/unit/tooling VERIFIED; **production rehearsal pending external** |
| P0-049 | Versioned artifact/checksum/manifest/evidence | COMPLETE — repository/CI; **live selected artifact evidence on #116** |
| P0-050 | Final P0.5 acceptance and #111 closure | **PENDING EXTERNAL** |

### Immediate next actions

1. Select the verified candidate recorded on #116 and preserve its filename + SHA-256 as cutover evidence.
2. On the intended Windows/IIS host, configure/verify the approved application-pool identity and trusted machine certificate/HTTPS binding.
3. Run packaged `_operations/scripts/Test-IisProductionPrerequisites.ps1` and record PASS.
4. Prepare the approved secret-free Production configuration and validated pre-cutover operational backup.
5. Run packaged `_operations/scripts/Deploy-ProductionSingleNode.ps1` without `-Apply`; review the PLAN ONLY output.
6. Execute the reviewed plan with explicit `-Apply`; retain generated deployment and HTTPS acceptance evidence.
7. Authenticate on the real trusted HTTPS endpoint and register/test/refresh the approved least-privilege SQL target; confirm no DML/write privilege is required.
8. Recycle the IIS application pool and verify protected credential resolution, registration, audit/history/incidents and trustworthy cached/read paths recover.
9. Rehearse rollback/recovery using the recorded previous physicalPath and `docs/ROLLBACK_RUNBOOK.md`; repeat health/auth/read checks.
10. Record final host/environment/version/package SHA/recycle/rollback evidence on #116 and the approved production evidence record; only then close #116 and umbrella #111.

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
