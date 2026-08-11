# Project Status

## CURRENT P0 — Real SQL Production MVP

**Updated:** 2026-08-11 13:18 +03:00  
**Branch:** `agent/p0-4-full-journey`  
**Umbrella:** #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**P0.1:** COMPLETE — PR #119 / final CI `31476747212` / 501/501  
**P0.2:** COMPLETE — PR #121 / final CI `31478470867` / 505/505  
**P0.3:** COMPLETE — PR #122 MERGED `245bb0770d7ec6e7a334f7763d3560cef80324fe` / final CI `31479311552` / 507/507  
**P0.4 foundation:** PR #123 MERGED `83540afe15f5d52ee528ff7de46430682444594d`; first Green real-engine run `31480624953` / 4/4 RealSql  
**P0.4 full-journey PR:** #124  
**P0.4 implementation gates:** normal CI `31481298862` — 518/518 Green; Real SQL `31481298848` — 8/8 Green  
**Next gate after final #124 same-head verification/merge:** #116 / P0.5 First Production SingleNode  
**Production target:** actual IIS/HTTPS SingleNode release acceptance.

### P0.4 sprint result — REAL SQL VERIFIED / FINAL HEAD GATE PENDING

- The acceptance harness boots a real Microsoft SQL Server 2022 Developer Linux engine and waits independently for SQL Server and SQL Server Agent readiness.
- All SQL passwords are generated at workflow runtime, immediately masked and destroyed with the disposable container; no acceptance credential is committed.
- The primary monitor login is explicitly verified not to be `sysadmin`.
- The exact `scripts/sql/monitored_sql_least_privilege.sql` is applied before production tester/collector execution.
- Real SQL discovery corrected the deployment script so `sqlcmd -v MonitorLogin=...` is not overridden by an internal `:setvar`.
- Real SQL discovery proved `sys.master_files` metadata visibility needed the read-only metadata permission now included in the baseline.
- Cross-platform closed-port behavior exposed a structured socket-classification gap; the tester now classifies structured SocketException evidence instead of matching provider text or treating every unknown provider error as network failure.
- SQL Server Agent startup was separated from engine readiness to remove fixture timing races.
- The full application path now passes against SQL Server 2022: `ConnectionLabController` registration → protected local credential → durable file registration → Test Connection → first collection/cache → `MonitorReadService`/`OperationsController.ServerDetails` → explicit `SnapshotRefreshService` refresh → service/persistence/key-ring reconstruction → Test/Collect/View again after simulated process restart.
- The full-journey test proves registration identity and the opaque secret reference survive restart, while username/password canaries are absent from both registration metadata and the encrypted secret file.
- Controlled real failure cases are Green: bad password, strict self-signed TLS rejection, closed-port network unavailable, accepted-but-silent TCP timeout, generic insufficient monitoring permissions and deliberately missing msdb permissions.
- The complete collector role succeeds while incomplete permission profiles fail closed with bounded safe messages.
- Implementation normal CI `31481298862`: Release build **0 warnings / 0 errors**, **518/518 passed**, 0 failed, 0 skipped.
- Implementation Real SQL run `31481298848`: Release build **0 warnings / 0 errors**, **8/8 RealSql passed**.
- Durable acceptance evidence is recorded in `docs/REAL_SQL_ACCEPTANCE.md`.
- The workflow now triggers for the canonical P0 acceptance docs as well, so the final documentation-synchronized head must pass both normal CI and the real SQL workflow before merge and #115 closure.

### P0.3 sprint result — COMPLETE

- PR #122 squash-merged to `main` as `245bb0770d7ec6e7a334f7763d3560cef80324fe`.
- Issue #114 closed — completed.
- Final code+docs CI `31479311552`: Release build **0 warnings / 0 errors**, **507/507 passed**, 0 failed, 0 skipped.
- Server Details is evidence-first: synthetic numeric Health Score removed; availability/freshness/collected-at/age are explicit; instance/uptime, database states, memory, backups, SQL Agent, storage, blocking and runtime evidence are visible or explicitly Not collected.
- CPU remains outside the v0.1 bounded snapshot contract and is not inferred from proxy data.
- Normal Server Details GET remains cache-only.

### P0.2 sprint result — COMPLETE

- P0.2 PR #121 squash-merged to `main` as `a294c6530d60f17e7c60e3a1ac070ce562af7b18`.
- Issue #113 closed — completed.
- Final code+docs CI `31478470867`: Release build **0 warnings / 0 errors**, **505/505 passed**, 0 failed, 0 skipped.
- `ServerCard.CpuPercent` and `MemoryPercent` are nullable evidence: absence is represented as absence rather than numeric zero.
- Real SQL Agent uses actual `TotalJobs`, `EnabledJobs`, and `FailedLastRun` facts; no real synthetic healthy-job count is published.
- Real cards carry instance name, uptime and collected-at evidence, while Server Details receives the safe cached envelope for memory/database/backups/Agent/storage/blocking/runtime.
- Dashboard, Servers, Server Details and Memory Health render or aggregate only observed evidence.

### P0.1 sprint result — COMPLETE

- Initial SQL registration tests the candidate before durable registration commit; failed safe connection tests no longer leave a normal enabled target as a side effect.
- A newly-created Monitor-owned credential is compensated on failed/cancelled initial Test Connection and on durable-registration commit failure.
- External secret references are not mutated by failed initial registration.
- SQL passwords remain write-only and are cleared from failed/cancelled controller flows.
- Integrated Security continues without creating a credential reference.
- Final code+docs CI `31476747212`: Release build **0 warnings / 0 errors**, **501/501 passed**, 0 failed, 0 skipped.
- PR #119 squash-merged to `main` as `57ab5cae6b5bdd3a04adb5069008aae80a1f84e0`.
- Issue #112 closed — completed.

### Management decision

- The immediate delivery objective is now the first actual SingleNode production release, not additional feature breadth.
- Until P0.5 is accepted, production deployment blockers remain higher priority than unrelated feature expansion.
- Production-visible SQL evidence rules from P0.2/P0.3 remain unchanged: observed data or explicit absence, never placeholder numeric zero.
- P0.1, P0.2 and P0.3 are merged and complete.
- P0.4 has real SQL Server 2022 evidence for the complete application journey and controlled failure matrix. Only final same-head normal/real-SQL verification and merge remain before #115 can close.
- **P0.5 / #116 becomes ACTIVE / NEXT immediately after #124 merges.** P0.5 owns real IIS/HTTPS SingleNode deployment, process recycle/restart durability, health smoke, read-only monitored-target validation, backup/rollback and versioned candidate artifact acceptance.
- MultiNode activation remains deferred until after the first stable SingleNode production release.

### P0 release chain

| Order | Release | Issue | State |
|---|---|---|---|
| 1 | P0.1 Real SQL Registration | #112 | COMPLETE — PR #119 MERGED / FINAL CI GREEN |
| 2 | P0.2 First Real Snapshot + truthful mapping | #113 | COMPLETE — PR #121 MERGED / FINAL CI GREEN |
| 3 | P0.3 Server Details v0.1 source of truth | #114 | COMPLETE — PR #122 MERGED / FINAL CI GREEN |
| 4 | P0.4 Real SQL end-to-end acceptance | #115 | REAL-SQL VERIFIED — PR #124 FINAL SAME-HEAD GATES PENDING |
| 5 | P0.5 First Production SingleNode | #116 | READY / NEXT AFTER #124 MERGE |

**Overall:** 🟢 verified foundation · 🟢 P0.1 COMPLETE · 🟢 P0.2 COMPLETE · 🟢 P0.3 COMPLETE · 🟢 P0.4 real-engine verified · 🟡 P0.5 next · 🔴 production deployment acceptance not yet granted

## BATCH-400 — Production DBA diagnostics continuation

- Issue #108 delivered **100 additional code tasks B400-011..110**, preserving the portal/typography work already merged by PR #107 as B400-001..010.
- Added deterministic wait-stat intelligence, query-regression scoring, TempDB pressure, transaction-log health, I/O latency, SQL Agent reliability, HA readiness, maintenance decision safety and fleet signal correlation.
- Added the Read-policy-protected `/intelligence/v2/contract` endpoint and a fail-closed 100-task continuation release contract.
- Clean implementation CI on top of PR #107: `31467831498` — Release build **0 warnings / 0 errors**, **498/498 tests passed**.
- Final PR CI on merge ref: `31468048589` — Release build **0 warnings / 0 errors**, **498/498 tests passed**.
- PR #109: **squash-merged** to `main` as `9345c4ca8b67e617a9aa9580bbb481819e5babb7`.
- Issue #108: **closed — completed**.
- B400-011..110: **100/100 COMPLETE** with 100 mapped acceptance tests.

## BATCH-400 — Portal completion and Google typography

- Added dedicated Performance Health, Recommendations, and Reports & Diagnostics pages.
- Reorganized navigation around Operations, Health, Intelligence, Administration, and contextual Help.
- Connected previously orphaned Fleet, Help, Readiness, Audit, History, and enterprise export capabilities.
- Made management links role-aware and removed the dead standalone AI Advisor link.
- Adopted self-hosted Google Fonts: Inter Variable for the Latin UI and Noto Sans Arabic Variable for Arabic glyphs.
- Kept the strict CSP by serving font assets locally with `font-src 'self'`.
- Added bounded desktop/mobile sidebar scrolling while preserving the existing command-center visual identity.
- Local verification: Release build **0 warnings / 0 errors**, **398/398 tests passed**, desktop and 390px browser acceptance passed with no console warnings/errors.
- State: **MERGED — PR #107**.

## Verified historical baseline

**Prior canonical update:** 2026-08-11 10:15 +03:00  
**Prior target:** BATCH-400 — Production DBA Diagnostics & Decision Safety COMPLETE  
**Prior issues:** #108 CLOSED · **PR:** #109 MERGED  
**Foundation:** 🟢 M0–M8 VERIFIED · 🟢 BATCH-100 100/100 COMPLETE · 🟢 BATCH-200 100/100 COMPLETE · 🟢 BATCH-300 100/100 COMPLETE · 🟢 BATCH-400 B400-001..110 COMPLETE

## BATCH-300 final verification

- Implementation CI: `31464569180` — Release build **0 warnings / 0 errors**, **390/390 tests passed**.
- Reconciled final CI after preserving concurrent `main` work: `31465013971` — Release build **0 warnings / 0 errors**, **395/395 tests passed**.
- B300-specific acceptance coverage: **100 mapped tests**, one for every B300-001..100 task.
- PR #102: **squash-merged** to `main` as `385c2ee7a4d592c1e32e6e00a5c533c8790963b6`.
- Issue #97: **closed — completed**.
- BATCH-300: **100/100 COMPLETE**.

## Concurrent team additions — preserved outside B300-001..100 ledger

### Daily target lifecycle

- Administrators can pause and resume each registered target from Connection Lab.
- Pausing persists `IsEnabled=false`, evicts the cached snapshot and prevents an older in-flight collection from republishing evidence.
- Resuming preserves registration ID, endpoint, credential reference, creation time, history and incidents.
- Repeated commands are idempotent; committed transitions emit bounded audit metadata.

### Protected credential reconnect

- Administrators can provide a new write-only SQL username/password for the existing target.
- The encrypted candidate is tested before registration metadata changes.
- Failed/cancelled candidates are compensated; the previous reference remains active.
- A successful replacement preserves registration ID, endpoint, timestamps, history and incidents, then removes the old Monitor-owned secret when safe.

## BATCH-300 delivered

- Defensive SQL estate identity/version/edition/uptime primitives with opaque stable identifiers.
- Bounded capacity growth and threshold forecasting helpers.
- Recovery-model-aware full/log backup compliance scoring and reasons.
- Deterministic database state, availability and failover-readiness intelligence.
- Runtime memory/blocking/scheduler/I/O pressure scoring and hotspot detection.
- Age/suppression/maintenance-aware fleet risk aggregation.
- Safe deterministic alert routing, escalation, cooldown and deduplication policy helpers.
- Operator input safety, secret-shape detection, formula neutralization, fingerprints and diagnostics allowlists.
- Versioned bounded UTF-8 export contracts with SHA-256 checksums and deterministic ordering.
- Fail-closed release invariants plus Read-policy-protected `/intelligence/contract` endpoint.

## BATCH-200 final verification

- GitHub Actions final release-candidate run: `31446970475`.
- Release build: **Green** with `--warnaserror`.
- Tests: **290/290 passed; 0 failed**.
- B200-001..100: **CI VERIFIED**.
- BATCH-200: **100/100 COMPLETE**.

## Stable guardrails

- Navigation, reporting, diagnostics, fleet, help, readiness and intelligence GETs do not initiate monitored-SQL collection.
- No browser connects directly to monitored SQL.
- No autonomous remediation or AI SQL execution.
- Credentials/full connection strings/raw provider errors/SQL text remain outside UI, audit, exports and diagnostics.
- Suppression does not rewrite incident evidence.
- Maintenance affects scheduled collection only; manual refresh is explicit and audited.
- Mutations remain POST + antiforgery + named authorization policy.
- MultiNode remains fail-closed behind shared-state/security/credential readiness.
- Concurrent team lifecycle/reconnect and portal/typography work remain preserved.
