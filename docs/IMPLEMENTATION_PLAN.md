# Implementation Plan

This is the canonical execution plan. Update it in the same PR as material implementation changes.

## Verified foundation

| Milestone | Scope | State |
|---|---|---|
| M0 | Visual foundation, secure dev auth, premium shell, Command Center, core screens, design/motion, CI and user visual acceptance | VERIFIED |
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
| M7-017 | Shared-state document capability + dedicated Monitor SQL Server provider; storage capability only | PLANNED — ISSUE #52 |
| M7-018 | Migrate required shared repositories and add distributed scheduler ownership/cross-node single-flight | PLANNED |

## M8 — Zero-SQL Reads & Operator Refresh

| Task | Description | State |
|---|---|---|
| M8-001 | Read-only snapshot Peek contract | VERIFIED — CI RUN 31383991126 |
| M8-002 | Missing snapshot returns no cached result | VERIFIED — CI RUN 31383991126 |
| M8-003 | Fresh snapshot Peek classification | VERIFIED — CI RUN 31383991126 |
| M8-004 | Stale snapshot Peek classification | VERIFIED — CI RUN 31383991126 |
| M8-005 | Expired snapshot excluded from monitoring reads | VERIFIED — CI RUN 31383991126 |
| M8-006 | Server estate GET performs zero collection calls | VERIFIED — CI RUN 31383991126 |
| M8-007 | Health module GET performs zero collection calls | VERIFIED — CI RUN 31383991126 |
| M8-008 | Incident GET no longer evaluates/mutates incidents | VERIFIED — CI RUN 31383991126 |
| M8-009 | Successful manual refresh observes committed snapshot once | VERIFIED — CI RUN 31383991126 |
| M8-010 | Operator/Admin refresh command on Server Details | VERIFIED — CI RUN 31383991126 |
| M8-011 | Refresh mutation is POST-only and antiforgery protected | VERIFIED — CI RUN 31383991126 |
| M8-012 | Refresh result uses PRG and safe TempData copy | VERIFIED — CI RUN 31383991126 |
| M8-013 | Registered-unavailable source is not labeled stale | VERIFIED — CI RUN 31383991126 |
| M8-014 | Browser navigation remains cache-only | VERIFIED — CI RUN 31383991126 |
| M8-015 | Release build, Razor compilation and automated test gate | VERIFIED — CI RUN 31383991126 |

## Delivery loop

Plan -> Design -> Implement -> Show -> Connect Real Data -> Verify -> Commit -> Push -> Update Plan.
