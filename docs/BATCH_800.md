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
| Server Details | cached snapshot + refresh POST + metadata/history | B800: B300 identity/runtime-pressure projection wired |
| Database Health | cached health-module read model + bounded per-database state evidence | **B800 exact database-state classification/actionable/worst-observed slice wired; omitted rows are not inferred** |
| Memory Health | shared cached health-module read model + bounded memory counters/configuration/clerk evidence | **B800 memory diagnostic slice wired; exact final validation pending** |
| Performance | cached health-module read model + bounded cumulative wait evidence | **B800 wait-stat projection wired and SQL-real validated on pre-canonical heads** |
| Backups | cached backup aggregate | Wired aggregate; policy-backed RPO compliance still requires explicit configuration/evidence contract |
| SQL Agent | cached aggregate + bounded recent job-summary run history | **B800 run-history reliability wired; schedule lateness explicitly not evaluated until schedule evidence exists** |
| Storage | cached allocation + bounded logical-file I/O evidence | **B800 B400 file-I/O projection wired and SQL-real validated on pre-canonical heads** |
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

The B800 branch extends `ServerHealthSnapshot` while preserving optional/backward-compatible shapes. The bounded collector now projects server identity/version/edition/uptime, database totals/states plus up to 50 user-database logical name/state rows with non-online states prioritized, OS/SQL process memory, max server memory, Total/Target Server Memory, PLE, Memory Grants Pending, dominant memory-clerk class/size, full-backup aggregate, SQL Agent aggregate, allocated storage, blocking count/max wait, bounded performance counts, up to 12 non-benign cumulative wait types from `sys.dm_os_wait_stats`, up to 12 logical database/file I/O counter rows from `sys.dm_io_virtual_file_stats` joined to `sys.master_files`, and up to 50 recent SQL Agent job-summary history rows from `msdb.dbo.sysjobhistory` joined to `sysjobs`.

The database-state detail contains logical user-database name plus `state_desc` only and remains bounded to 50 rows; no table data or physical path is collected. The memory, wait and file-I/O additions reuse the existing read-only server permission boundary (`VIEW SERVER PERFORMANCE STATE` on SQL Server 2022+ or `VIEW SERVER STATE` on older supported versions plus `VIEW ANY DEFINITION`). SQL Agent history adds read-only `SELECT` on `msdb.dbo.sysjobhistory`; no Agent operator/write role is granted. Wait evidence contains only wait type and bounded counters. File-I/O evidence contains database/logical-file identity plus cumulative read/write/stall/byte counters; `physical_name` is never selected. Agent history contains logical job name, owner, success/failure, run ordering key and duration only; step rows, commands, command text, schedules, proxies and credentials are not collected.

Wait and file-I/O counters are cumulative since SQL Server start and are normalized by collected uptime in pure cached projections. Agent run-history reliability is derived from bounded recent outcomes and durations; schedule lateness is deliberately excluded until schedule evidence is collected. No SQL text, query plans, client identity, table data, physical filesystem paths, or configuration writes are collected.

B300 estate identity and runtime-pressure helpers are wired from cached evidence. B300 per-database state classification/actionable/worst-observed helpers are now wired only from retained exact database-state rows rather than reconstructing detail from aggregate `OfflineOrOther`. B400 wait intelligence, B400 file-I/O intelligence, and the run-history portions of B400 Agent reliability are wired from bounded cached evidence. B400 query-regression, TempDB, transaction-log, Agent schedule lateness and HA helpers still require explicit new snapshot evidence before they can be truthfully displayed. Backup RPO compliance also requires an explicit policy/configuration contract; B800 will not invent RPO values or placeholder inputs.

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
- [x] B800-015 surface existing backup/database/Agent/storage intelligence consistently across server and module pages (`docs/work/B800-015.md`).
- [x] B800-016 add safe cross-links between aggregate health pages and server evidence (`docs/work/B800-016.md`).
- [x] B800-017 normalize stale/unavailable classification for derived intelligence (`docs/work/B800-017.md`).
- [x] B800-018 add controller/view integration coverage for Server Details role variants (`docs/work/B800-018.md`).
- [x] B800-019 document the exact non-browser acceptance boundary because the repository carries no browser automation stack (`docs/work/B800-019.md`).
- [ ] B800-020 close the first vertical slice with canonical docs + exact-head CI evidence.

### B800-021..030 — cross-page actions and workflow completion

- [x] B800-021 enforce an assembly-wide protected POST workflow matrix: antiforgery + authorization + named-policy boundary (`docs/work/B800-021.md`).
- [x] B800-022 verify visible Razor tag-helper POST forms resolve to real controller POST endpoints (`docs/work/B800-022.md`).
- [ ] B800-023..030 complete the remaining action/drill-down/filter/PRG/role matrix across existing pages without inventing unsupported runtime behavior.

### B800-031..050 — bounded snapshot expansion

- [x] B800-031 extend optional memory snapshot evidence with max server memory, Total/Target Server Memory, PLE, Memory Grants Pending and dominant clerk class/size.
- [x] B800-032 append the memory projections to the existing one-statement bounded collector without shifting existing evidence ordinals.
- [x] B800-033 validate all optional memory numerics and clerk evidence fail-closed before snapshot publication.
- [x] B800-034 keep the monitored-SQL query free of SQL text/query plans and preserve the existing two-second command / seven-second overall timeout boundary.
- [x] B800-035 project detailed Memory evidence through `GetHealthModulesAsync`; normal Memory Health GET remains cache-only.
- [x] B800-036 replace Memory Health `Planned` placeholders with real Max Memory / Total-Target / PLE / grants / dominant clerk evidence and explicit Not collected states.
- [x] B800-037 add deterministic `MemoryIntelligenceProjection` recommendations with no automatic tuning/configuration write.
- [x] B800-038 update least-privilege documentation and regression coverage for the expanded read-only memory evidence.
- [ ] B800-039 obtain final exact-head Green CI/Real-SQL/Windows candidate evidence after canonical reconciliation.
- [ ] B800-040 reconcile/close the memory slice after canonical docs and review.
- [x] B800-041 extend optional Performance snapshot evidence with bounded cumulative wait samples.
- [x] B800-042 append a top-12 non-benign `sys.dm_os_wait_stats` projection to the existing bounded collector without collecting SQL text or client identity.
- [x] B800-043 validate wait type/counters fail-closed and preserve backward-compatible optional snapshot behavior.
- [x] B800-044 preserve the existing read-only SQL Server DMV permission boundary and document wait-stat coverage explicitly.
- [x] B800-045 add pure `WaitIntelligenceProjection` over cached Performance evidence plus SQL Server uptime.
- [x] B800-046 wire bounded B400 wait intelligence into the Performance page with explicit `Not collected` behavior and a cumulative-since-start interpretation boundary.
- [x] B800-047 add regression coverage for collector wait evidence, projection behavior and Performance UI wiring.
- [x] B800-048 validate the wait/file-I/O pre-canonical head with CI #2046, Real SQL #169 and Windows production-candidate #265 Green.
- [x] B800-049 reconcile the completed diagnostic material into canonical `IMPLEMENTATION_PLAN`, `STATUS`, and `FEATURE_CATALOG`.
- [ ] B800-050 close the bounded diagnostic slice after exact-head validation and review.

### B800-051..070 — dedicated diagnostics surfaces

- [x] B800-051 extend Storage snapshot evidence with up to 12 bounded logical-file cumulative I/O rows; no physical path is collected.
- [x] B800-052 append `sys.dm_io_virtual_file_stats` + logical `sys.master_files` identity to the existing single bounded collector.
- [x] B800-053 validate file identity/counters fail-closed and cap the payload at 12 rows.
- [x] B800-054 add pure `IoLatencyProjection` to normalize cumulative I/O counters by collected SQL Server uptime and reuse B400 latency scoring.
- [x] B800-055 wire B400 file latency/throughput/hotspot intelligence into Storage while retaining allocation/free-space interpretation boundaries.
- [x] B800-056 add unit/source tests proving logical-only identity, no `physical_name`, no browser SQL, missing-evidence behavior and bounded mapping.
- [x] B800-057 validate the Storage/I/O slice on CI #2046, Real SQL #169 and Windows candidate #265.
- [x] B800-058 add bounded SQL Agent job-summary run-history evidence (max 50 rows/server) and read-only `sysjobhistory` permission; never collect job step commands/text.
- [x] B800-059 add `AgentReliabilityProjection` using recent success rate, failure streak, P95 duration and duration regression; explicitly exclude schedule lateness until schedule evidence exists.
- [x] B800-060 wire B400 run-history reliability into SQL Agent with explicit empty-evidence behavior and server drill-downs.
- [x] B800-061 add Agent collector/projection/UI regression coverage and least-privilege documentation.
- [ ] B800-062 validate Agent slice on exact-head CI/Real-SQL/Windows candidate.
- [ ] B800-063 add bounded Agent schedule evidence before enabling lateness functions.
- [ ] B800-064 add policy-backed backup RPO configuration before claiming B300 backup compliance.
- [ ] B800-065 add bounded TempDB evidence.
- [ ] B800-066 add bounded transaction-log evidence.
- [ ] B800-067 add HA readiness evidence.
- [ ] B800-068 evaluate a privacy-safe query-regression evidence contract without SQL text/plans.
- [x] B800-069 add per-database bounded state evidence before using B300 worst/actionable state classifications that cannot be derived truthfully from `OfflineOrOther` aggregate (`docs/work/B800-069.md`).
- [ ] B800-070 project only evidence-backed diagnostics into the remaining pages and server/fleet drill-downs.

### B800-071..080 — fleet / routing / maintenance intelligence

- [ ] wire only evidence-supported correlation, routing and maintenance decision support; no autonomous action.

### B800-081..090 — reports and exports

- [ ] add bounded, versioned, redacted exports for new evidence where operator value is clear.

### B800-091..100 — final acceptance

- [ ] end-to-end/controller-service contracts, role/antiforgery tests, no-fake-data tests, responsive/accessibility review, canonical docs, exact-head CI and final closeout.

## Implementation evidence

Branch: `agent/b800-functional-screen-wiring`.

Server Details / cross-page contract slice:
- `src/Monitor.Web/Services/ServerIntelligenceProjection.cs`
- `src/Monitor.Web/Views/Operations/ServerDetails.cshtml`
- `tests/Monitor.Web.Tests/ServerIntelligenceProjectionTests.cs`
- `tests/Monitor.Web.Tests/B800ServerDetailsRoleIntegrationTests.cs`
- `tests/Monitor.Web.Tests/B800CrossPageEvidenceConsistencyTests.cs`
- `docs/work/B800-015.md`
- `docs/work/B800-016.md`
- `docs/work/B800-017.md`
- `docs/work/B800-018.md`
- `docs/work/B800-019.md`

Protected workflow contract slice:
- `tests/Monitor.Web.Tests/B800WorkflowSafetyMatrixTests.cs`
- `tests/Monitor.Web.Tests/B800RazorPostWiringTests.cs`
- `docs/work/B800-021.md`
- `docs/work/B800-022.md`

Memory Health slice:
- `src/Monitor.Web/Models/ServerHealthSnapshot.cs`
- `src/Monitor.Web/Models/MonitorModels.cs`
- `src/Monitor.Web/Services/SqlServerSnapshotCollector.cs`
- `src/Monitor.Web/Services/MonitorReadService.cs`
- `src/Monitor.Web/Services/MemoryIntelligenceProjection.cs`
- `src/Monitor.Web/Controllers/OperationsController.cs`
- `src/Monitor.Web/Views/Operations/MemoryHealth.cshtml`
- `scripts/sql/monitored_sql_least_privilege.sql`
- `tests/Monitor.Web.Tests/SqlServerSnapshotCollectorTests.cs`
- `tests/Monitor.Web.Tests/MemoryIntelligenceProjectionTests.cs`

Performance wait slice:
- `src/Monitor.Web/Models/ServerHealthSnapshot.cs`
- `src/Monitor.Web/Models/MonitorModels.cs`
- `src/Monitor.Web/Services/SqlServerSnapshotCollector.cs`
- `src/Monitor.Web/Services/MonitorReadService.cs`
- `src/Monitor.Web/Services/WaitIntelligenceProjection.cs`
- `src/Monitor.Web/Controllers/PortalController.cs`
- `src/Monitor.Web/Views/Portal/Performance.cshtml`
- `tests/Monitor.Web.Tests/SqlServerSnapshotCollectorTests.cs`
- `tests/Monitor.Web.Tests/WaitIntelligenceProjectionTests.cs`

Storage/file-I/O slice:
- `src/Monitor.Web/Models/ServerHealthSnapshot.cs`
- `src/Monitor.Web/Services/SqlServerSnapshotCollector.cs`
- `src/Monitor.Web/Services/IoLatencyProjection.cs`
- `src/Monitor.Web/Views/Operations/Storage.cshtml`
- `scripts/sql/monitored_sql_least_privilege.sql`
- `tests/Monitor.Web.Tests/IoLatencyProjectionTests.cs`
- `tests/Monitor.Web.Tests/IoSnapshotCollectorTests.cs`

SQL Agent run-history slice:
- `src/Monitor.Web/Models/ServerHealthSnapshot.cs`
- `src/Monitor.Web/Services/SqlServerSnapshotCollector.cs`
- `src/Monitor.Web/Services/AgentReliabilityProjection.cs`
- `src/Monitor.Web/Views/Operations/Jobs.cshtml`
- `scripts/sql/monitored_sql_least_privilege.sql`
- `tests/Monitor.Web.Tests/AgentReliabilityProjectionTests.cs`
- `tests/Monitor.Web.Tests/AgentSnapshotCollectorTests.cs`

Per-database state slice:
- `src/Monitor.Web/Models/ServerHealthSnapshot.cs`
- `src/Monitor.Web/Services/SqlServerSnapshotCollector.cs`
- `src/Monitor.Web/Services/DatabaseStateProjection.cs`
- `src/Monitor.Web/Views/Operations/DatabaseHealth.cshtml`
- `tests/Monitor.Web.Tests/DatabaseStateProjectionTests.cs`
- `tests/Monitor.Web.Tests/DatabaseStateSnapshotCollectorTests.cs`
- `docs/work/B800-069.md`

## Validation chronology

- Initial Server Details slice: CI Green.
- First Memory Health head: CI build failed on one nullable conditional expression in `MemoryIntelligenceProjection` (`CS0173`). The nullable type was corrected explicitly to `long?`; no product/safety contract was weakened.
- Wait-slice head `5dc585fad80f24dfa2bacdd729fc0b1b1d3f26fe`: CI #2029 and Real SQL #161 Green.
- Storage/I/O head `ffc7b307e99558d92500e7278ff62ec721796e7f`: CI #2046, Real SQL #169 and Windows production-candidate #265 all Green. Real SQL validation includes application of the read-only monitored-SQL role and execution against SQL Server 2022.
- Database-state source head `895297809d5dcb656cb3e6bc064aba96d02e58b1`: CI #2120 and Real SQL #205 Green; later shared-branch commits supersede it as merge evidence.
- Canonical documentation is now reconciled in this PR. Only CI, Real SQL and Windows production-candidate on the final exact head count for Ready/merge.

## Documentation / merge gate

`docs/FEATURE_CATALOG.md`, `docs/STATUS.md`, and `docs/IMPLEMENTATION_PLAN.md` are reconciled in PR #288, and this ledger reflects implemented B800-015/016/017/019/021/022/069 work without claiming unsupported diagnostics. Before PR #288 is marked Ready or merged, applicable CI must be green on the exact final head, the branch must remain current with `main`, and review threads must remain resolved. Issue #287 remains OPEN after this partial slice.
