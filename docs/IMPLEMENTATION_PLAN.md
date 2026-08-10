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
| M1-003 | Lightweight collector: name/version/edition/instance/uptime/database counts | VERIFIED — CI RUN 31369800023 |
| M1-004 | `ServerHealthSnapshot` domain contract + cache | VERIFIED — CI RUN 31370422613 |
| M1-005 | Replace one demo server with real snapshot data | VERIFIED — CI RUN 31371256976 |
| M1-006 | Backend-controlled/throttled refresh | VERIFIED — CI RUN 31371676834 |
| M1-007 | SignalR snapshot delivery evaluation | VERIFIED — DEFERRED BY ADR-013 |

## M2 — Health Modules

| Task | Description | State |
|---|---|---|
| M2-001 | Memory snapshot contract + collector projection | VERIFIED — CI RUN 31372045546 |
| M2-002 | Memory health UI from cached snapshot | VERIFIED — CI RUN 31372312362 |
| M2-003 | Database health detail contract + projection | VERIFIED — CI RUN 31372957383 |
| M2-004 | Backup health summary | VERIFIED — CI RUN 31372957383 |
| M2-005 | SQL Agent jobs summary | VERIFIED — CI RUN 31372957383 |
| M2-006 | Storage allocation summary | VERIFIED — CI RUN 31372957383 |
| M2-007 | Blocking summary | VERIFIED — CI RUN 31372957383 |
| M2-008 | Shared cached health-module read projection | VERIFIED — CI RUN 31373849952 |
| M2-009 | Real database and backup health UI | VERIFIED — CI RUN 31373849952 |
| M2-010 | Real SQL Agent jobs UI | VERIFIED — CI RUN 31373849952 |
| M2-011 | Real storage allocation UI | VERIFIED — CI RUN 31373849952 |
| M2-012 | Real blocking UI | VERIFIED — CI RUN 31373849952 |
| M2-013 | Bounded baseline performance snapshot | VERIFIED — CI RUN 31373849952 |

## M3 — Incidents and Recommendations

| Task | Description | State |
|---|---|---|
| M3-001 | Immutable allowlisted health finding contract | VERIFIED — CI RUN 31373849952 |
| M3-002 | Deterministic health rule evaluator | VERIFIED — CI RUN 31373849952 |
| M3-003 | Incident dedupe and lifecycle repository | VERIFIED — CI RUN 31373849952 |
| M3-004 | Cached incident read and UI integration | VERIFIED — CI RUN 31373849952 |
| M3-005 | Idempotent incident observations | VERIFIED — CI RUN 31375034604 |
| M3-006 | Bounded incident query contract | VERIFIED — CI RUN 31375034604 |
| M3-007 | Incident summary counts | VERIFIED — CI RUN 31375034604 |
| M3-008 | Severity/status/rule filters | VERIFIED — CI RUN 31375034604 |
| M3-009 | Incident detail read model and page | VERIFIED — CI RUN 31375034604 |
| M3-010 | Acknowledge transition | VERIFIED — CI RUN 31375034604 |
| M3-011 | Resolve transition | VERIFIED — CI RUN 31375034604 |
| M3-012 | Reopen transition | VERIFIED — CI RUN 31375034604 |
| M3-013 | Antiforgery-protected operator commands | VERIFIED — CI RUN 31375034604 |
| M3-014 | Deterministic recommendation contract | VERIFIED — CI RUN 31375034604 |
| M3-015 | Allowlisted recommendation catalog | VERIFIED — CI RUN 31375034604 |
| M3-016 | Recommendation detail UI | VERIFIED — CI RUN 31375034604 |

## M4 — AI Advisor Boundary

| Task | Description | State |
|---|---|---|
| M4-001 | Normalized advisor context contract | VERIFIED — CI RUN 31375034604 |
| M4-002 | Redacted context builder | VERIFIED — CI RUN 31375034604 |
| M4-003 | Backend-only advisor provider abstraction | VERIFIED — CI RUN 31375034604 |
| M4-004 | Disabled-by-default provider and result | VERIFIED — CI RUN 31375034604 |
| M4-005 | Advisor orchestration on incident details | VERIFIED — CI RUN 31375034604 |
| M4-006 | Human-reviewed advisory UI with no execution path | VERIFIED — CI RUN 31375034604 |

## M5 — History and Operational Hardening

| Task | Description | State |
|---|---|---|
| M5-001 | Allowlisted snapshot history contract | VERIFIED — CI RUN 31375034604 |
| M5-002 | 24-hour / 288-point retention | VERIFIED — CI RUN 31375034604 |
| M5-003 | Timestamp dedupe and per-server isolation | VERIFIED — CI RUN 31375034604 |
| M5-004 | Post-collection snapshot observer | VERIFIED — CI RUN 31375034604 |
| M5-005 | Disabled-by-default validated schedule policy | VERIFIED — CI RUN 31375034604 |
| M5-006 | Deterministic collection cycle | VERIFIED — CI RUN 31375034604 |
| M5-007 | Fixed-window read-only trends page | VERIFIED — CI RUN 31375034604 |
| M5-008 | Operator audit trail for protected incident transitions + read-only Admin view | IMPLEMENTED — CI PENDING |

## Delivery loop

Plan -> Design -> Implement -> Show -> Connect Real Data -> Verify -> Commit -> Push -> Update Plan.
