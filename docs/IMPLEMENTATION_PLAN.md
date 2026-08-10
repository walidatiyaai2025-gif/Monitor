# Implementation Plan

This is the canonical execution plan. Update it in the same PR as material implementation changes.

## M0 — Visual Foundation — COMPLETE

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
| M0-006A | Database availability + Memory pressure command-view polish | VERIFIED — CI RUN 31367759961 |
| M0-007 | Alerts / Incidents + Settings | VERIFIED — CI |
| M0-008 | Design system + controlled motion | VERIFIED — CI |
| M0-009 | GitHub Actions restore/build verification | VERIFIED |
| M0-010 | Visual browser review and M0 acceptance | VERIFIED — USER ACCEPTED |
| M0-MERGE | Merge visual foundation into stable `main` | VERIFIED — PR #2 / dfbfa19cf37f82be0df4c8855bb214779c48fdc8 |

## M1 — First Real SQL Vertical Slice — ACTIVE

| Task | Description | State |
|---|---|---|
| M1-001 | Server registration model and secure connection secret boundary | IN PROGRESS — ISSUE #3 |
| M1-002 | Test Connection workflow | PLANNED |
| M1-003 | Lightweight collector: name/version/edition/instance/uptime/database counts | PLANNED |
| M1-004 | `ServerHealthSnapshot` domain contract + cache | PLANNED |
| M1-005 | Replace one demo server with real snapshot data | PLANNED |
| M1-006 | Backend-controlled/throttled refresh | PLANNED |
| M1-007 | SignalR snapshot delivery evaluation | PLANNED |

### M1-001 implementation contract

- Administrator-only server registration screen.
- Windows Integrated and SQL Login metadata modes.
- SQL Login password must never be persisted or rendered in plaintext.
- ASP.NET Core Data Protection protects SQL Login secrets before the temporary registration store receives them.
- Registration summaries expose only safe metadata and a boolean protected-credential indicator.
- Duplicate server/instance/port targets are rejected within the current application session.
- M1-001 performs no SQL connection; M1-002 owns connection testing and sanitized diagnostics.

## Delivery loop

Plan -> Design -> Implement -> Show -> Connect Real Data -> Verify -> Commit -> Push -> Update Plan.
