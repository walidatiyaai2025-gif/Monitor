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
| M3-005 | Idempotent incident observations | IMPLEMENTED — LOCAL VERIFIED |
| M3-006 | Bounded incident query contract | IMPLEMENTED — LOCAL VERIFIED |
| M3-007 | Incident summary counts | IMPLEMENTED — LOCAL VERIFIED |
| M3-008 | Severity/status/rule filters | IMPLEMENTED — LOCAL VERIFIED |
| M3-009 | Incident detail read model and page | IMPLEMENTED — LOCAL VERIFIED |
| M3-010 | Acknowledge transition | IMPLEMENTED — LOCAL VERIFIED |
| M3-011 | Resolve transition | IMPLEMENTED — LOCAL VERIFIED |
| M3-012 | Reopen transition | IMPLEMENTED — LOCAL VERIFIED |
| M3-013 | Antiforgery-protected operator commands | IMPLEMENTED — LOCAL VERIFIED |
| M3-014 | Deterministic recommendation contract | IMPLEMENTED — LOCAL VERIFIED |
| M3-015 | Allowlisted recommendation catalog | IMPLEMENTED — LOCAL VERIFIED |
| M3-016 | Recommendation detail UI | IMPLEMENTED — LOCAL VERIFIED |

## M4 — AI Advisor Boundary

| Task | Description | State |
|---|---|---|
| M4-001 | Normalized advisor context contract | IMPLEMENTED — LOCAL VERIFIED |
| M4-002 | Redacted context builder | IMPLEMENTED — LOCAL VERIFIED |
| M4-003 | Backend-only advisor provider abstraction | IMPLEMENTED — LOCAL VERIFIED |
| M4-004 | Disabled-by-default provider and result | IMPLEMENTED — LOCAL VERIFIED |
| M4-005 | Advisor orchestration on incident details | IMPLEMENTED — LOCAL VERIFIED |
| M4-006 | Human-reviewed advisory UI with no execution path | IMPLEMENTED — LOCAL VERIFIED |
| M4-007 | Explicit advisor request service | IMPLEMENTED — LOCAL VERIFIED |
| M4-008 | Authorized antiforgery-protected advisor POST | IMPLEMENTED — LOCAL VERIFIED |
| M4-009 | Per-incident advisor single-flight | IMPLEMENTED — LOCAL VERIFIED |
| M4-010 | Evidence-version advisor result cache | IMPLEMENTED — LOCAL VERIFIED |
| M4-011 | Bounded advisor timeout | IMPLEMENTED — LOCAL VERIFIED |
| M4-012 | Failure circuit breaker | IMPLEMENTED — LOCAL VERIFIED |
| M4-013 | Redacted advisor request audit | IMPLEMENTED — LOCAL VERIFIED |

## M5 — History and Operational Hardening

| Task | Description | State |
|---|---|---|
| M5-001 | Allowlisted snapshot history contract | IMPLEMENTED — LOCAL VERIFIED |
| M5-002 | 24-hour / 288-point retention | IMPLEMENTED — LOCAL VERIFIED |
| M5-003 | Timestamp dedupe and per-server isolation | IMPLEMENTED — LOCAL VERIFIED |
| M5-004 | Post-collection snapshot observer | IMPLEMENTED — LOCAL VERIFIED |
| M5-005 | Disabled-by-default validated schedule policy | IMPLEMENTED — LOCAL VERIFIED |
| M5-006 | Deterministic collection cycle | IMPLEMENTED — LOCAL VERIFIED |
| M5-007 | Fixed-window read-only trends page | IMPLEMENTED — LOCAL VERIFIED |
| M5-008 | Validated schedule configuration binding | IMPLEMENTED — LOCAL VERIFIED |
| M5-009 | Disabled-by-default hosted scheduler | IMPLEMENTED — LOCAL VERIFIED |
| M5-010 | Periodic no-overlap collection loop | IMPLEMENTED — LOCAL VERIFIED |
| M5-011 | Bounded parallel collection cycle | IMPLEMENTED — LOCAL VERIFIED |
| M5-012 | Per-server failure isolation | IMPLEMENTED — LOCAL VERIFIED |
| M5-013 | Exponential capped collection backoff | IMPLEMENTED — LOCAL VERIFIED |
| M5-014 | Allowlisted scheduler runtime status | IMPLEMENTED — LOCAL VERIFIED |
| M5-015 | Exactly-once successful snapshot observation | IMPLEMENTED — LOCAL VERIFIED |
| M5-016 | Bounded audit event contract | IMPLEMENTED — LOCAL VERIFIED |
| M5-017 | Append-only 1000-event audit store | IMPLEMENTED — LOCAL VERIFIED |
| M5-018 | Paginated administrator audit UI | IMPLEMENTED — LOCAL VERIFIED |
| M5-019 | Viewer/Operator/Administrator role foundation | IMPLEMENTED — LOCAL VERIFIED |
| M5-020 | Read policy for monitoring pages | IMPLEMENTED — LOCAL VERIFIED |
| M5-021 | Operator policy for incident commands | IMPLEMENTED — LOCAL VERIFIED |
| M5-022 | Administrator/advisor policies | IMPLEMENTED — LOCAL VERIFIED |
| M5-023 | Strict secure cookie policy | IMPLEMENTED — LOCAL VERIFIED |
| M5-024 | Baseline browser security headers | IMPLEMENTED — LOCAL VERIFIED |
| M5-025 | Partitioned login limiting and safe login audit | IMPLEMENTED — LOCAL VERIFIED |

## Delivery loop

Plan -> Design -> Implement -> Show -> Connect Real Data -> Verify -> Commit -> Push -> Update Plan.
