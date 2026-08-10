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
| M0-MERGE | Merge visual foundation into stable `main` | VERIFIED — PR #2 |

## M1 — First Real SQL Vertical Slice — ACTIVE

| Task | Description | State |
|---|---|---|
| M1-001 | Server registration model and secure connection secret boundary | VERIFIED — PR #4 / CI RUN 31368239695 |
| M1-002 | Test Connection workflow | CI VERIFIED — RUN 31369329964 / VISUAL REVIEW PENDING |
| M1-003 | Lightweight collector: name/version/edition/instance/uptime/database counts | PLANNED |
| M1-004 | `ServerHealthSnapshot` domain contract + cache | PLANNED |
| M1-005 | Replace one demo server with real snapshot data | PLANNED |
| M1-006 | Backend-controlled/throttled refresh | PLANNED |
| M1-007 | SignalR snapshot delivery evaluation | PLANNED |

### M1-002 verified implementation contract

- Administrator-only `/servers/connections` Connection Lab.
- Safe target metadata registration using the existing M1-001 `ServerRegistration` domain model/repository.
- No SQL password input exists in the browser UI.
- SQL Login uses an opaque `ConnectionSecretReference`; credentials resolve only inside the backend profile factory.
- Integrated Security never requests a SQL Login secret.
- Backend connection strings are never returned to browser-facing models.
- One deliberate probe per Test Connection action: 5-second connect timeout, pooling disabled, connection retries disabled.
- Fixed sanitized result categories/messages for success, authentication, timeout, network, certificate, invalid configuration and unexpected failure.
- No raw provider exception text is rendered to the user.
- M1-002 does not run collector queries; M1-003 owns lightweight SQL identity collection.
- CI run `31369329964`: Release build succeeded with 0 warnings / 0 errors and 11 tests passed.

## Delivery loop

Plan -> Design -> Implement -> Show -> Connect Real Data -> Verify -> Commit -> Push -> Update Plan.
