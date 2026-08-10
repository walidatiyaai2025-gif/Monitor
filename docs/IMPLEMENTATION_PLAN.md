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
| M4-007 | Explicit advisor request service | VERIFIED — CI RUN 31376448363 |
| M4-008 | Authorized antiforgery-protected advisor POST | VERIFIED — CI RUN 31376448363 |
| M4-009 | Per-incident advisor single-flight | VERIFIED — CI RUN 31376448363 |
| M4-010 | Evidence-version advisor result cache | VERIFIED — CI RUN 31376448363 |
| M4-011 | Bounded advisor timeout | VERIFIED — CI RUN 31376448363 |
| M4-012 | Failure circuit breaker | VERIFIED — CI RUN 31376448363 |
| M4-013 | Redacted advisor request audit | VERIFIED — CI RUN 31376448363 |

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
| M5-008 | Validated schedule configuration binding | VERIFIED — CI RUN 31376448363 |
| M5-009 | Disabled-by-default hosted scheduler | VERIFIED — CI RUN 31376448363 |
| M5-010 | Periodic no-overlap collection loop | VERIFIED — CI RUN 31376448363 |
| M5-011 | Bounded parallel collection cycle | VERIFIED — CI RUN 31376448363 |
| M5-012 | Per-server failure isolation | VERIFIED — CI RUN 31376448363 |
| M5-013 | Exponential capped collection backoff | VERIFIED — CI RUN 31376448363 |
| M5-014 | Allowlisted scheduler runtime status | VERIFIED — CI RUN 31376448363 |
| M5-015 | Exactly-once successful snapshot observation | VERIFIED — CI RUN 31376448363 |
| M5-016 | Bounded audit event contract | VERIFIED — CI RUN 31376448363 |
| M5-017 | Append-only 1000-event audit store | VERIFIED — CI RUN 31376448363 |
| M5-018 | Paginated administrator audit UI | VERIFIED — CI RUN 31376448363 |
| M5-019 | Viewer/Operator/Administrator role foundation | VERIFIED — CI RUN 31376448363 |
| M5-020 | Read policy for monitoring pages | VERIFIED — CI RUN 31376448363 |
| M5-021 | Operator policy for incident commands | VERIFIED — CI RUN 31376448363 |
| M5-022 | Administrator/advisor policies | VERIFIED — CI RUN 31376448363 |
| M5-023 | Strict secure cookie policy | VERIFIED — CI RUN 31376448363 |
| M5-024 | Baseline browser security headers | VERIFIED — CI RUN 31376448363 |
| M5-025 | Partitioned login limiting and safe login audit | VERIFIED — CI RUN 31376448363 |
| M5-026 | Enrich incident transition audit with authenticated actor and bounded before/after state | VERIFIED — CI RUN 31379998409 |

## M6 — Real Server User Journey

| Task | Description | State |
|---|---|---|
| M6-001 | First-login destination routes administrators without servers to Connections | VERIFIED — CI RUN 31378848889 |
| M6-002 | Safe local return URL remains highest-priority post-login destination | VERIFIED — CI RUN 31378848889 |
| M6-003 | First-server Connections onboarding entry point | VERIFIED — CI RUN 31378848889 |
| M6-004 | Administrator Connections navigation | VERIFIED — CI RUN 31378848889 |
| M6-005 | Remove duplicate Coming Soon health navigation | VERIFIED — CI RUN 31378848889 |
| M6-006 | Register → Test → Collect → View journey banner | VERIFIED — CI RUN 31378848889 |
| M6-007 | Bounded display-name input | VERIFIED — CI RUN 31378848889 |
| M6-008 | Bounded host input and normalization | VERIFIED — CI RUN 31378848889 |
| M6-009 | Validated TCP port input | VERIFIED — CI RUN 31378848889 |
| M6-010 | Port / named-instance mutual exclusion | VERIFIED — CI RUN 31378848889 |
| M6-011 | Integrated Security / SQL Login choice | VERIFIED — CI RUN 31378848889 |
| M6-012 | Encrypt-on TLS default and trust-certificate disclosure | VERIFIED — CI RUN 31378848889 |
| M6-013 | Runtime SQL username input | VERIFIED — CI RUN 31378848889 |
| M6-014 | Password-only non-repopulated SQL credential input | VERIFIED — CI RUN 31378848889 |
| M6-015 | External secret-reference fallback | VERIFIED — CI RUN 31378848889 |
| M6-016 | Server-generated opaque runtime secret reference | VERIFIED — CI RUN 31378848889 |
| M6-017 | Process-memory runtime credential store | VERIFIED — CI RUN 31378848889 |
| M6-018 | Credentials excluded from registration model/JSON | VERIFIED — CI RUN 31378848889 |
| M6-019 | Failed form/test never echoes password | VERIFIED — CI RUN 31378848889 |
| M6-020 | Canonical duplicate endpoint rejection | VERIFIED — CI RUN 31378848889 |
| M6-021 | Registration metadata creation | VERIFIED — CI RUN 31378848889 |
| M6-022 | Automatic bounded Test Connection after save | VERIFIED — CI RUN 31378848889 |
| M6-023 | Fixed redacted connection outcomes | VERIFIED — CI RUN 31378848889 |
| M6-024 | Failed Test prevents first collection | VERIFIED — CI RUN 31378848889 |
| M6-025 | Successful Test triggers first cached collection | VERIFIED — CI RUN 31378848889 |
| M6-026 | Successful first snapshot passes through shared observer | VERIFIED — CI RUN 31378848889 |
| M6-027 | Successful commissioning redirects to real Servers estate | VERIFIED — CI RUN 31378848889 |
| M6-028 | Safe monitoring-permission recovery message | VERIFIED — CI RUN 31378848889 |
| M6-029 | Registered server remains visible when snapshot unavailable | VERIFIED — CI RUN 31378848889 |
| M6-030 | Multi-server live estate projection | VERIFIED — CI RUN 31378848889 |
| M6-031 | Deterministic registration ordering | VERIFIED — CI RUN 31378848889 |
| M6-032 | Demo cards excluded when real registrations exist | VERIFIED — CI RUN 31378848889 |
| M6-033 | Async cache-backed Dashboard read | VERIFIED — CI RUN 31378848889 |
| M6-034 | Dashboard displays every registered real server | VERIFIED — CI RUN 31378848889 |
| M6-035 | Dashboard derives real database/server availability totals | VERIFIED — CI RUN 31378848889 |
| M6-036 | Dashboard reads real deterministic incidents | VERIFIED — CI RUN 31378848889 |
| M6-037 | Empty active-incident state no longer crashes Dashboard | VERIFIED — CI RUN 31378848889 |
| M6-038 | Live/Demo Dashboard source banner | VERIFIED — CI RUN 31378848889 |
| M6-039 | Explicit RegisteredUnavailable data-source state | VERIFIED — CI RUN 31378848889 |
| M6-040 | Real server identity routes to Server Details | VERIFIED — CI RUN 31378848889 |
| M6-041 | Health pages share registered-server cache projection | VERIFIED — CI RUN 31378848889 |
| M6-042 | Role-aware Connections navigation visibility | VERIFIED — CI RUN 31378848889 |
| M6-043 | Role-aware signed-in user chip | VERIFIED — CI RUN 31378848889 |
| M6-044 | Accurate SQL Snapshot Mode environment chrome | VERIFIED — CI RUN 31378848889 |
| M6-045 | Antiforgery-protected registration workflow | VERIFIED — CI RUN 31378848889 |
| M6-046 | Administrator-only connection management | VERIFIED — CI RUN 31378848889 |
| M6-047 | Canary password redaction assertions | VERIFIED — CI RUN 31378848889 |
| M6-048 | Register/Test/Collect/Observe success journey test | VERIFIED — CI RUN 31378848889 |
| M6-049 | Failed Test/no-collection recovery journey test | VERIFIED — CI RUN 31378848889 |
| M6-050 | Multi-server live/unavailable/no-demo acceptance test | VERIFIED — CI RUN 31378848889 |

## M7 — Production Persistence & Deployment Readiness

| Task | Description | State |
|---|---|---|
| M7-001 | Durable local server-registration metadata store with atomic writes and corruption fail-closed behavior | VERIFIED — CI RUN 31380699808 |
| M7-002 | Environment-injected external SQL secret provider behind the existing secret-store boundary | VERIFIED — CI RUN 31381465706 |
| M7-003 | Durable Monitor-owned audit/history/incident operational store | VERIFIED — CI RUN 31382770932 |
| M7-004 | Shared-state / HA deployment strategy and implementation slice | PLANNED |

## Delivery loop

Plan -> Design -> Implement -> Show -> Connect Real Data -> Verify -> Commit -> Push -> Update Plan.
