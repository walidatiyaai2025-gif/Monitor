# Implementation Plan

This is the canonical execution plan. Update it in the same PR as material implementation changes.

## CURRENT P0 — Real SQL Production MVP

**Umbrella:** Issue #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Project rule:** until P0.5 is accepted, production-slice blockers take priority over unrelated feature expansion.

The immediate product outcome is one trustworthy vertical slice:

`Login -> Add SQL Server -> Test -> Save -> Collect -> View Server Details -> Refresh -> Restart Monitor -> View trustworthy persisted target again`

Production-visible values must come from collected evidence. Missing, stale, permission-limited or uncollected dimensions must be explicit; default numeric values must never masquerade as measurements.

| Order | Release gate | Issue | Outcome | State |
|---|---|---|---|---|
| 1 | P0.1 | #112 | Real SQL registration: safe, testable and restart durable | COMPLETE — PR #119 / FINAL CI 31476747212 |
| 2 | P0.2 | #113 | First real snapshot + truthful read-model mapping | COMPLETE — PR #121 / FINAL CI 31478470867 |
| 3 | P0.3 | #114 | Server Details v0.1 becomes the trusted source of truth | COMPLETE — PR #122 / FINAL CI 31479311552 |
| 4 | P0.4 | #115 | Real SQL end-to-end acceptance under success/failure cases | REAL-SQL VERIFIED — PR #123 + #124 / FINAL HEAD GATE PENDING |
| 5 | P0.5 | #116 | First IIS/HTTPS SingleNode production release | READY / NEXT AFTER #124 MERGE |

### P0 production blockers and resolved gates

- P0.1 is complete: candidate Test Connection precedes durable registration commit, and newly-created Monitor-owned candidate credentials are compensated on failed/cancelled test or commit failure.
- P0.2 is complete: uncollected CPU/Memory/Agent evidence is explicit rather than numeric zero; SQL Agent projects actual total/enabled/failed-last-run facts; Server Details receives safe snapshot evidence from cache.
- P0.3 is complete: Server Details exposes availability/freshness/collected-at context plus the v0.1 evidence modules, and the synthetic numeric Health Score is removed.
- P0.4 is real-engine verified on SQL Server 2022. The full Add -> Test -> Register -> Collect -> View -> Refresh -> Restart -> View path passes with a non-sysadmin least-privilege login, and controlled bad-password/network/timeout/TLS/server-permission/msdb-permission cases fail safely.
- P0.4 implementation evidence: normal CI `31481298862` passed 518/518; Real SQL run `31481298848` passed 8/8. Durable evidence is recorded in `docs/REAL_SQL_ACCEPTANCE.md`.
- The final P0.4 PR head must rerun both normal CI and `real-sql-acceptance` after documentation synchronization before #115 closes.
- P0.5 is next after #124 merges. First production activation remains deliberately SingleNode; MultiNode production activation is deferred until after P0.5.

## Verified foundation

| Milestone | Scope | State |
|---|---|---|
| M0 | Visual foundation, secure dev auth, premium shell, Command Center, core screens, design/motion, CI and visual acceptance | VERIFIED |
| M1 | Registration/secret boundary, Test Connection, collector, snapshot/cache, real UI, throttled refresh, SignalR evaluation | VERIFIED — CI THROUGH 31371676834 |
| M2 | Memory/database/backup/Agent/storage/blocking/shared module UI/baseline performance | VERIFIED — CI THROUGH 31373849952 |
| M3 | Deterministic findings, incident lifecycle/query/details/operator workflow and recommendations | VERIFIED — CI THROUGH 31375034604 |
| M4 | Advisor context/provider boundary + guarded request/single-flight/cache/timeout/circuit/audit | VERIFIED — CI THROUGH 31376448363 |
| M5 | History, observer, collection cycle, trends, scheduler infrastructure, audit, RBAC, browser security, login limiting and transition audit | VERIFIED — CI THROUGH 31379998409 |
| M6 | Login -> Connections -> Register -> Test -> Collect -> Observe -> real multi-server estate/Dashboard/Health | VERIFIED — CI RUN 31378848889 |

## M7 — Production Persistence & Deployment Readiness

| Task | Description | State |
|---|---|---|
| M7-001 | Durable local server-registration metadata store with atomic writes and corruption fail-closed behavior | VERIFIED — CI RUN 31380699808 |
| M7-002 | Environment-injected external SQL secret provider behind the existing secret-store boundary | VERIFIED — CI RUN 31381465706 |
| M7-003 | Durable Monitor-owned audit/history/incident operational store | VERIFIED — CI RUN 31382770932 |
| M7-004 | Fail-closed SingleNode/MultiNode deployment topology guard + Administrator readiness surface | VERIFIED — CI RUN 31385935255 |
| M7-005 | Protected local SQL credential store options and paths | VERIFIED — CI RUN 31384727247 |
| M7-006 | Persistent ASP.NET Data Protection key ring | VERIFIED — CI RUN 31384727247 |
| M7-007 | Server-generated `local:v1` opaque references | VERIFIED — CI RUN 31384727247 |
| M7-008 | Versioned encrypted secret envelope | VERIFIED — CI RUN 31384727247 |
| M7-009 | Reference-scoped encryption purpose prevents ciphertext swapping | VERIFIED — CI RUN 31384727247 |
| M7-010 | Atomic candidate-file replacement | VERIFIED — CI RUN 31384727247 |
| M7-011 | Restart-safe credential resolution | VERIFIED — CI RUN 31384727247 |
| M7-012 | Lost/different key ring and tampered ciphertext fail closed | VERIFIED — CI RUN 31384727247 |
| M7-013 | Environment and legacy external-reference compatibility | VERIFIED — CI RUN 31384727247 |
| M7-014 | Credential length validation and write-only UI | VERIFIED — CI RUN 31384727247 |
| M7-015 | Plaintext canary exclusion from persisted secret file | VERIFIED — CI RUN 31384727247 |
| M7-016 | Owned-secret deletion without external-provider mutation | VERIFIED — CI RUN 31384727247 |
| M7-017 | Shared-state versioned document contract + dedicated Monitor SQL Server provider + schema/readiness/optimistic compare-exchange | VERIFIED — CI RUN 31386867949 |
| M7-018 | Migrate required shared repositories and add distributed scheduler ownership/cross-node single-flight | VERIFIED — B100 BATCH 1 / CI RUN 31389275376 |

## M8 — Zero-SQL Reads & Operator Refresh

| Task | Description | State |
|---|---|---|
| M8-001..M8-015 | Cache Peek, zero-SQL monitored GETs, read-only incidents, explicit protected refresh, PRG feedback and regression gate | VERIFIED — CI RUN 31383991126 |

## BATCH-100 — Production / Enterprise hardening

`docs/BATCH_100.md` is the task-level execution ledger. Each batch contains ten tasks and must pass merge-result CI before merge to `main`.

| Batch | Tasks | Scope | State |
|---|---|---|---|
| Batch 1 | B100-001..010 | Shared state & HA foundation | CI VERIFIED — 31389275376 |
| Batch 2 | B100-011..020 | HA secret & key management | CI VERIFIED — 31391446513 |
| Batch 3 | B100-021..030 | Backup, export & rollback-capable restore | CI VERIFIED — 31393040135 |
| Batch 4 | B100-031..040 | Production health, telemetry, correlation & redacted logging | CI VERIFIED — 31396619576 |
| Batch 5 | B100-041..050 | Performance & scale governance | CI VERIFIED — 31399632281 |
| Batch 6 | B100-051..060 | DBA UX & operations surfaces | CI VERIFIED — 31402491011 |
| Batch 7 | B100-061..070 | Web/application security hardening | CI VERIFIED — 31439153733 |
| Batch 8 | B100-071..080 | Reliability & concurrency verification | CI VERIFIED — 31439886994 |
| Batch 9 | B100-081..090 | Deployment & operations tooling/docs | CI VERIFIED — 31440573683 |
| Batch 10 | B100-091..100 | Enterprise operator features & RC acceptance | CI VERIFIED — 31442930470 |

Current progress: **100/100 tasks CI verified**. Batch 10 verification run `31442930470` passed Release build with warnings-as-errors and the complete RC suite (229/229 passed; 0 failed). The BATCH-100 production/enterprise program is complete.

## BATCH-300 — Daily target lifecycle

| Task | Description | State |
|---|---|---|
| B300-001 | Administrator pause-monitoring command | LOCAL VERIFIED |
| B300-002 | Administrator resume-monitoring command | LOCAL VERIFIED |
| B300-003 | Durable registration state preservation | LOCAL VERIFIED |
| B300-004 | Snapshot eviction on pause | LOCAL VERIFIED |
| B300-005 | In-flight generation guard after pause | LOCAL VERIFIED |
| B300-006 | Bounded lifecycle audit event | LOCAL VERIFIED |
| B300-007 | Idempotent repeated lifecycle commands | LOCAL VERIFIED |
| B300-008 | Connection Lab pause/resume operator UX | LOCAL VERIFIED |
| B300-009 | Antiforgery-protected POST/PRG workflow | LOCAL VERIFIED |
| B300-010 | Release build and 293-test regression gate | LOCAL VERIFIED |
| B300-011 | Write-only local credential replacement input | LOCAL VERIFIED |
| B300-012 | Server-generated replacement secret reference | LOCAL VERIFIED |
| B300-013 | Candidate Test Connection before registration mutation | LOCAL VERIFIED |
| B300-014 | Failed candidate compensation cleanup | LOCAL VERIFIED |
| B300-015 | Same registration identity and metadata after replacement | LOCAL VERIFIED |
| B300-016 | Old owned-secret cleanup after commit | LOCAL VERIFIED |
| B300-017 | External/shared old-secret preservation | LOCAL VERIFIED |
| B300-018 | Reconnect form with unique accessible labels | LOCAL VERIFIED |
| B300-019 | Password non-repopulation and safe PRG feedback | LOCAL VERIFIED |
| B300-020 | Release build and 295-test regression gate | LOCAL VERIFIED |

## Delivery loop

Plan -> Design -> Implement -> Show -> Connect Real Data -> Verify -> Commit -> Push -> Update Plan.

For P0.5, `Connect Real Data` means deploy the accepted SingleNode candidate to an actual IIS/HTTPS production-like host, run health/restart/rollback smoke, and keep monitored-SQL access read-only/least-privilege.

## BATCH-400 — Portal completion and typography

| Task | Description | State |
|---|---|---|
| B400-001 | Dedicated Performance Health page using cached module projections | LOCAL VERIFIED |
| B400-002 | Estate Recommendations index using the deterministic recommendation engine | LOCAL VERIFIED |
| B400-003 | Reports & Diagnostics landing page for bounded exports and support artifacts | LOCAL VERIFIED |
| B400-004 | Role-aware information architecture and navigation cleanup | LOCAL VERIFIED |
| B400-005 | Fleet, Help, Readiness, Audit, History and export discoverability | LOCAL VERIFIED |
| B400-006 | Self-hosted Google Inter Variable typography | LOCAL VERIFIED |
| B400-007 | Self-hosted Google Noto Sans Arabic fallback and Arabic typography contract | LOCAL VERIFIED |
| B400-008 | Explicit self-only font CSP and typography asset tests | LOCAL VERIFIED |
| B400-009 | Desktop and mobile sidebar overflow/responsive polish | LOCAL VERIFIED |
| B400-010 | Release build, 398-test regression gate and browser route acceptance | LOCAL VERIFIED |
