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
| M0-007 | Alerts / Incidents + Settings | VERIFIED — CI |
| M0-008 | Design system + controlled motion | VERIFIED — CI |
| M0-009 | GitHub Actions restore/build verification | VERIFIED |
| M0-010 | Visual browser review and M0 acceptance | VERIFIED — USER ACCEPTED |

## M1 — First Real SQL Vertical Slice — ACTIVE

| Task | Description | State |
|---|---|---|
| M1-001 | Server registration model and secure connection secret boundary | VERIFIED — CI RUN 31368239695 |
| M1-002 | Test Connection backend workflow | VERIFIED — CI RUN 31368995784 |
| M1-002A | SQL Connection Lab UI + timeout/retry semantics | CI VERIFIED — RUN 31370363183 / VISUAL REVIEW PENDING |
| M1-003 | Lightweight collector: name/version/edition/instance/uptime/database counts | VERIFIED — CI RUN 31369800023 / MERGED TO MAIN |
| M1-004 | `ServerHealthSnapshot` domain contract + cache | PLANNED — NEXT |
| M1-005 | Replace one demo server with real snapshot data | PLANNED |
| M1-006 | Backend-controlled/throttled refresh | PLANNED |
| M1-007 | SignalR snapshot delivery evaluation | PLANNED |

### M1-002A contract

- Administrator-only `/servers/connections` visual workflow.
- Registration reuses the existing M1-001 domain model and repository.
- SQL Login UI accepts an opaque external secret reference only; there is no SQL password field.
- Registered-target summaries expose only whether a secret reference exists, never its raw value.
- Test action calls the existing M1-002 `IServerConnectionTester`.
- UI adds no fetch, polling, collector timer or background SQL activity.
- Provider timeout `SqlException -2` maps to `TimedOut` rather than network failure.
- Shared connection-string factory explicitly disables connection retries with `ConnectRetryCount=0`.
- Reconciled on top of merged M1-003 without reverting its shared factory or collector.
- CI run `31370363183`: Release build 0 warnings / 0 errors; 23 tests passed.

## Delivery loop

Plan -> Design -> Implement -> Show -> Connect Real Data -> Verify -> Commit -> Push -> Update Plan.
