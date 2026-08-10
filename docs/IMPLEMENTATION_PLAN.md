# Implementation Plan

This is the canonical execution plan. Update it in the same PR as material implementation changes.

## M0 — Visual Foundation

| Task | Description | State |
|---|---|---|
| M0-001 | Bootstrap repository, solution and project tracking docs | VERIFIED |
| M0-002 | Secure development Admin cookie authentication | VERIFIED — CI |
| M0-003 | Global premium app shell and navigation | VERIFIED — CI |
| M0-004 | SQL Command Center with centralized live area | VERIFIED — CI |
| M0-004A | Command Center estate topology / visual telemetry polish | VERIFIED — CI RUN 31365813089 |
| M0-005 | Servers + Server Details | VERIFIED — CI |
| M0-005A | Operational Server Estate + DBA Server Details polish | VERIFIED — CI RUN 31366381962 |
| M0-006 | Database Health + Memory Health | VERIFIED — CI |
| M0-007 | Alerts / Incidents + Settings | VERIFIED — CI |
| M0-008 | Design system + controlled motion | VERIFIED — CI |
| M0-009 | GitHub Actions restore/build verification | VERIFIED — RUN 31364393808 |
| M0-010 | Visual browser review and M0 acceptance | VERIFIED — USER ACCEPTED |

## M1 — First Real SQL Vertical Slice

| Task | Description | State |
|---|---|---|
| M1-001 | Server registration model and secure connection secret boundary | VERIFIED — CI RUN 31368239695 |
| M1-002 | Test Connection workflow | VERIFIED — CI RUN 31368995784 |
| M1-003 | Lightweight collector: identity, uptime and database availability | VERIFIED — CI RUN 31369800023 |
| M1-004 | `ServerHealthSnapshot` domain contract + cache | VERIFIED — CI RUN 31370422613 |
| M1-005 | Replace demo server with real snapshot data | VERIFIED — CI RUN 31371256976 |
| M1-006 | Backend-controlled/throttled refresh | VERIFIED — CI RUN 31371676834 |
| M1-007 | SignalR snapshot delivery evaluation | VERIFIED — DEFERRED BY ADR-013 |

## M2 — Health Modules

| Task | Description | State |
|---|---|---|
| M2-001..M2-013 | Memory, database, backup, Agent, storage, blocking, shared cached module UI and baseline performance | VERIFIED — CI THROUGH 31373849952 |

## M3 — Incidents and Recommendations

| Task | Description | State |
|---|---|---|
| M3-001..M3-016 | Deterministic findings, incident lifecycle/query/details/operator workflow and deterministic recommendations | VERIFIED — CI THROUGH 31375034604 |

## M4 — AI Advisor Boundary

| Task | Description | State |
|---|---|---|
| M4-001..M4-006 | Normalized advisor context/provider boundary and advisory UI | VERIFIED — CI RUN 31375034604 |
| M4-007..M4-013 | Explicit guarded request, single-flight/cache/timeout/circuit/audit | VERIFIED — CI RUN 31376448363 |

## M5 — History and Operational Hardening

| Task | Description | State |
|---|---|---|
| M5-001..M5-007 | History, observer, deterministic collection cycle and trend reads | VERIFIED — CI RUN 31375034604 |
| M5-008..M5-025 | Scheduler infrastructure, audit, RBAC, browser security and login limiting | VERIFIED — CI RUN 31376448363 |
| M5-026 | Incident transition audit enrichment | VERIFIED — CI RUN 31379998409 |

## M6 — Real Server User Journey

| Task | Description | State |
|---|---|---|
| M6-001..M6-050 | Login -> Connections -> Register -> Test -> Collect -> Observe -> real multi-server estate/Dashboard/Health | VERIFIED — CI RUN 31378848889 |

## M7 — Production Persistence & Deployment Readiness

| Task | Description | State |
|---|---|---|
| M7-001 | Durable local server-registration metadata store with atomic writes and corruption fail-closed behavior | VERIFIED — CI RUN 31380699808 |
| M7-002 | Environment-injected external SQL secret provider behind the existing secret-store boundary | VERIFIED — CI RUN 31381465706 |
| M7-003 | Durable Monitor-owned audit/history/incident operational store | VERIFIED — CI RUN 31382770932 |
| M7-004 | Fail-closed deployment topology / HA readiness guard and Administrator readiness view | VERIFIED — CI RUN 31383750309 |
| M7-005 | Shared-state provider capability contract and first real shared implementation selection | PLANNED |
| M7-006 | Distributed scheduler ownership / cross-node single-flight coordination | PLANNED |

## Delivery loop

Plan -> Design -> Implement -> Show -> Connect Real Data -> Verify -> Commit -> Push -> Update Plan.
