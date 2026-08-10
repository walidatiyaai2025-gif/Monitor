# Implementation Plan

This is the canonical execution plan. Update it in the same PR as material implementation changes.

## M0 — Visual Foundation

| Task | Description | State |
|---|---|---|
| M0-001 | Bootstrap repository, solution and project tracking docs | DONE |
| M0-002 | Secure development Admin cookie authentication | IMPLEMENTED — CI PENDING |
| M0-003 | Global premium app shell and navigation | IMPLEMENTED — CI PENDING |
| M0-004 | SQL Command Center with centralized live area | IMPLEMENTED — CI PENDING |
| M0-005 | Servers + Server Details | IMPLEMENTED — CI PENDING |
| M0-006 | Database Health + Memory Health | IMPLEMENTED — CI PENDING |
| M0-007 | Alerts / Incidents + Settings | IMPLEMENTED — CI PENDING |
| M0-008 | Design system + controlled motion | IMPLEMENTED — CI PENDING |
| M0-009 | GitHub Actions restore/build verification | IMPLEMENTED — CI PENDING |
| M0-010 | Visual review and M0 verification | PENDING |

## M1 — First Real SQL Vertical Slice

| Task | Description | State |
|---|---|---|
| M1-001 | Server registration model and secure connection secret boundary | PLANNED |
| M1-002 | Test Connection workflow | PLANNED |
| M1-003 | Lightweight collector: name/version/edition/instance/uptime/database counts | PLANNED |
| M1-004 | `ServerHealthSnapshot` domain contract + cache | PLANNED |
| M1-005 | Replace one demo server with real snapshot data | PLANNED |
| M1-006 | Backend-controlled/throttled refresh | PLANNED |
| M1-007 | SignalR snapshot delivery evaluation | PLANNED |

## Delivery loop

Plan -> Design -> Implement -> Show -> Connect Real Data -> Verify -> Commit -> Push -> Update Plan.
