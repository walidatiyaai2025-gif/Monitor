# BATCH-800 — Full Functional Wiring

**Umbrella:** Issue #287  
**Task range:** B800-001..100  
**State:** IN PROGRESS  
**Goal:** move from route/UI completeness to real UI → controller → service → state/evidence wiring across the visible product.

## Completion definition

A screen is not considered functionally complete because it renders. For every visible workflow we require a traceable contract:

`UI control / route -> controller endpoint -> authorization + antiforgery boundary -> service/read model -> persisted or cached evidence -> explicit success/error/unavailable state -> regression evidence`

Browser GET navigation remains cache/control-plane only. Where a diagnostic dimension is not collected, the UI must say so; no missing evidence may be converted to zero or healthy state.

## Safety boundaries

- No browser-to-monitored-SQL access.
- No autonomous remediation or AI-generated SQL execution.
- No plaintext credentials, connection strings, SQL text, raw provider errors, unsafe filesystem paths or exception detail in UI/audit/export.
- POST mutations retain named role policies, antiforgery and existing audit/PRG contracts.
- New collector work must remain bounded, snapshot-first and least-privilege.
- BATCH-800 repository/product work does not publish or supersede selected RC.61 and cannot satisfy #162/#116/#111.

## Functional inventory baseline

| Surface | Existing runtime path | B800 assessment |
|---|---|---|
| Login | AccountController + PBKDF2 verifier + cookie auth | Wired; include in end-to-end matrix |
| Dashboard | IMonitorReadService + IDbaOperationsSurfaceService | Wired; validate every card/drill-down |
| Servers | bounded server read model + policy metadata | Wired; validate paging/actions |
| Server Details | cached snapshot + refresh POST + metadata/history | **B800 first slice: adding B300 intelligence projection** |
| Database Health | cached health-module read model | Wired aggregate; deeper diagnostics depend on collector scope |
| Memory Health | cached server read model | Wired aggregate |
| Performance | cached health-module read model | Wired aggregate; B400 waits/query/TempDB/log/I/O not yet collected in current snapshot |
| Backups | cached backup aggregate | Wired aggregate; B300/B400 compliance detail requires evidence expansion |
| SQL Agent | cached Agent aggregate | Wired aggregate; reliability detail requires evidence expansion |
| Storage | cached allocated-byte aggregate | Wired aggregate; file latency/log/TempDB detail requires evidence expansion |
| Blocking | cached blocked-count/max-wait aggregate | Wired aggregate |
| Alerts | incident workflow/query + role-scoped transitions | Wired; validate all transition/feedback paths |
| Recommendations | incident repository + deterministic recommendation engine | Wired; validate acknowledgement/drill-down integration |
| Reports | versioned CSV/ZIP/JSON endpoints | Wired existing exports; extend with new evidence only after collection contracts exist |
| Connection Lab | registration/test/credential workflow | Existing functional onboarding surface; validate full control flow |
| Audit | bounded audit store | Wired |
| History | stored snapshot trends | Wired |
| Fleet Intelligence | enterprise metadata/incidents projection | Existing surface; correlation expansion tracked later in B800 |
| Enterprise Operations | governance metadata control plane | Existing surface; validate mutations and cross-links |
| Observability | control-plane telemetry/readiness | Existing surface; validate source/readiness states |
| Settings | readiness + operational backup/restore POST workflows | Existing functional controls; validate role/antiforgery/feedback |
| Governance retention | dry-run/apply workflow | Existing protected workflow; validate destructive confirmation/audit |
| Operator help/readiness | control-plane guidance | Existing read-only surfaces |

## Data-availability boundary discovered during inventory

The current `ServerHealthSnapshot` provides server identity/version/edition/uptime, database totals/states, memory, full-backup aggregate, SQL Agent aggregate, allocated storage, blocking count/max wait, and bounded performance counts.

Therefore B300 estate identity and runtime-pressure helpers can be wired immediately from existing cached evidence. In contrast, B400 wait-stat, query-regression, TempDB, transaction-log, per-file I/O, detailed Agent reliability and HA helpers require explicit new snapshot evidence before they can be truthfully displayed. B800 will not project those helpers from invented placeholder inputs.

## Task program

### B800-001..010 — inventory and contracts

- [x] B800-001 enumerate visible operator surfaces from current main.
- [x] B800-002 distinguish route/UI completion from functional wiring completion.
- [x] B800-003 map existing server details read path to cached evidence.
- [x] B800-004 inventory existing protected POST workflows.
- [x] B800-005 inventory existing reports/download entry points.
- [x] B800-006 classify immediately wireable B300 functions.
- [x] B800-007 classify B400 functions that require collector expansion.
- [x] B800-008 preserve zero-monitored-SQL GET boundary in the execution contract.
- [x] B800-009 preserve missing-evidence/no-synthetic-zero boundary.
- [x] B800-010 create GitHub umbrella + batch ledger.

### B800-011..020 — existing cached server intelligence

- [x] B800-011 add reusable `ServerIntelligenceProjection` over `ServerDetailsViewModel`.
- [x] B800-012 wire SQL major/version-family/support + edition + uptime classification into Server Details.
- [x] B800-013 wire deterministic composite runtime pressure from cached memory/blocking/performance evidence.
- [x] B800-014 fail explicit when any composite runtime-pressure evidence is absent; never replace missing input with zero.
- [ ] B800-015 surface existing backup/database/Agent/storage intelligence consistently across server and module pages.
- [ ] B800-016 add safe cross-links between aggregate health pages and server evidence.
- [ ] B800-017 normalize stale/unavailable classification for derived intelligence.
- [ ] B800-018 add controller/view integration coverage for Server Details role variants.
- [ ] B800-019 add full browser-level functional harness if repository tooling supports it; otherwise document the exact non-browser acceptance boundary.
- [ ] B800-020 close the first vertical slice with canonical docs + exact-head CI evidence.

### B800-021..030 — cross-page actions and workflow completion

- [ ] complete action/drill-down/filter/PRG/role matrix across existing pages.

### B800-031..050 — bounded snapshot expansion

- [ ] add only the evidence required for B400 wait/query/TempDB/log/I/O/Agent/HA diagnostics, with bounded least-privilege collection and truthful optional fields.

### B800-051..070 — dedicated diagnostics surfaces

- [ ] project the new cached diagnostics into server and dedicated operator pages; GET remains cache-only.

### B800-071..080 — fleet / routing / maintenance intelligence

- [ ] wire only evidence-supported correlation, routing and maintenance decision support; no autonomous action.

### B800-081..090 — reports and exports

- [ ] add bounded, versioned, redacted exports for new evidence where operator value is clear.

### B800-091..100 — final acceptance

- [ ] end-to-end/controller-service contracts, role/antiforgery tests, no-fake-data tests, responsive/accessibility review, canonical docs, exact-head CI and final closeout.

## First slice implementation evidence

Branch: `agent/b800-functional-screen-wiring`.

Initial code paths:

- `src/Monitor.Web/Services/ServerIntelligenceProjection.cs`
- `src/Monitor.Web/Views/Operations/ServerDetails.cshtml`
- `tests/Monitor.Web.Tests/ServerIntelligenceProjectionTests.cs`

The projection is deterministic and consumes the existing Server Details read model only. It has no collector, SQL connection, credential or mutation dependency.

## Documentation / merge gate

This batch ledger is evidence of active work, not completion. Before any B800 PR is marked Ready or merged, material work must be reconciled into the canonical `docs/IMPLEMENTATION_PLAN.md`, `docs/STATUS.md`, and `docs/FEATURE_CATALOG.md`, and applicable CI must be green on the exact head.
