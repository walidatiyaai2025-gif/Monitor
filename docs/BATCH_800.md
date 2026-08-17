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
| Servers | bounded server read model + policy metadata | Wired; bounded paging/filter contract regression-locked |
| Server Details | cached snapshot + refresh POST + metadata/history | B800: B300 identity/runtime-pressure projection wired; refresh PRG contract locked |
| Database Health | cached health-module read model + bounded per-database state evidence | **B800 exact database-state classification/actionable/worst-observed slice wired; omitted rows are not inferred** |
| Memory Health | shared cached health-module read model + bounded memory counters/configuration/clerk evidence | **B800 memory diagnostic slice wired; exact final validation pending** |
| Performance | cached health-module read model + bounded cumulative wait evidence | **B800 wait-stat projection wired and SQL-real validated on pre-canonical heads** |
| Backups | cached backup aggregate + explicit control-plane RPO policy metadata | **B800 policy configuration wired with no default RPO values; B300 compliance remains `Not evaluated` until per-database recovery-model/full/log evidence exists** |
| SQL Agent | cached aggregate + bounded recent job-summary history + current Agent activity evidence | **B800 run-history reliability and current next-run/running evidence wired; lateness explicitly not evaluated without canonical server time-zone + recurrence/expected-run semantics** |
| Storage | cached allocation + bounded logical-file I/O evidence | **B800 B400 file-I/O projection wired and SQL-real validated on pre-canonical heads** |
| Blocking | cached blocked-count/max-wait aggregate | Wired aggregate |
| Alerts | incident workflow/query + role-scoped transitions | B800 bounded filters/paging + PRG/conflict + role-visible mutation controls regression-locked |
| Recommendations | incident repository + deterministic recommendation engine | Wired; incident/Advisor mutation controls align with Operator/Administrator policy boundary |
| Reports | versioned CSV/ZIP/JSON endpoints | Wired existing exports; contextual history export remains explicitly server-scoped/read-only |
| Connection Lab | registration/test/credential workflow | B800 test-before-save, write-only secret and protected action wiring regression-locked |
| Audit | bounded audit store | Wired |
| History | stored snapshot trends | B800 bounded window/limit/paging navigation regression-locked |
| Fleet Intelligence | enterprise metadata/incidents projection | Existing surface; correlation expansion tracked later in B800 |
| Enterprise Operations | governance metadata/incidents control plane | B800 role matrix regression-locked: Read for viewers, Manage for metadata, Operate for incident collaboration |
| Observability | control-plane telemetry/readiness | Existing surface; validate source/readiness states |
| Settings | readiness + operational backup/restore POST workflows | B800 Administrator-only Create/Validate/Restore, exact `RESTORE` confirmation, audit and safe feedback regression-locked |
| Governance retention | dry-run/apply workflow | B800 destructive apply now requires exact typed `PRUNE`; rejection is audited and fails closed |
| Operator help/readiness | control-plane guidance | Existing read-only surfaces |

## Data-availability boundary discovered during inventory

The B800 branch extends `ServerHealthSnapshot` while preserving optional/backward-compatible shapes. The bounded collector now projects server identity/version/edition/uptime, database totals/states plus up to 50 user-database logical name/state rows with non-online states prioritized, OS/SQL process memory, max server memory, Total/Target Server Memory, PLE, Memory Grants Pending, dominant memory-clerk class/size, full-backup aggregate, SQL Agent aggregate, allocated storage, blocking count/max wait, bounded performance counts, up to 12 non-benign cumulative wait types from `sys.dm_os_wait_stats`, up to 12 logical database/file I/O counter rows from `sys.dm_io_virtual_file_stats` joined to `sys.master_files`, up to 50 recent SQL Agent job-summary history rows from `msdb.dbo.sysjobhistory` joined to `sysjobs`, and up to 50 current Agent activity rows from the latest `msdb.dbo.sysjobactivity` session.

The database-state detail contains logical user-database name plus `state_desc` only and remains bounded to 50 rows; no table data or physical path is collected. The memory, wait and file-I/O additions reuse the existing read-only server permission boundary (`VIEW SERVER PERFORMANCE STATE` on SQL Server 2022+ or `VIEW SERVER STATE` on older supported versions plus `VIEW ANY DEFINITION`). SQL Agent history adds read-only `SELECT` on `msdb.dbo.sysjobhistory`; current Agent activity adds read-only `SELECT` on `msdb.dbo.sysjobactivity`; no Agent operator/write role is granted. Wait evidence contains only wait type and bounded counters. File-I/O evidence contains database/logical-file identity plus cumulative read/write/stall/byte counters; `physical_name` is never selected. Agent run-history contains logical job name, owner, success/failure, run ordering key and duration only. Current Agent activity contains logical job name, server-local `next_scheduled_run_date` with `DateTimeKind.Unspecified`, and a running flag derived from current activity state. Step rows, commands, command text, recurrence definitions, proxies and credentials are not collected.

Wait and file-I/O counters are cumulative since SQL Server start and are normalized by collected uptime in pure cached projections. Agent run-history reliability is derived from bounded recent outcomes and durations. Current Agent next-run time is preserved only as server-local wall-clock evidence: it is not converted to UTC and is not classified Late/On-time because the snapshot does not carry canonical server time-zone identity or recurrence/expected-run policy. `AgentReliabilityProjection` therefore keeps `ScheduleLatenessEvaluated = false`. No SQL text, query plans, client identity, table data, physical filesystem paths, or configuration writes are collected.

B300 estate identity and runtime-pressure helpers are wired from cached evidence. B300 per-database state classification/actionable/worst-observed helpers are now wired only from retained exact database-state rows rather than reconstructing detail from aggregate `OfflineOrOther`. B400 wait intelligence, B400 file-I/O intelligence, and the run-history portions of B400 Agent reliability are wired from bounded cached evidence. Bounded current Agent activity/next-run metadata is also available, but lateness scoring remains disabled until the time-zone/recurrence/expected-run contract exists. B800-064 now provides explicit policy-backed Full/Log RPO configuration with no numeric defaults; the Backups page can display that policy metadata but deliberately does not invoke `Batch300BackupCompliance` because the snapshot still lacks per-database recovery model and last log-backup timestamps. B400 query-regression, TempDB, transaction-log and HA helpers still require explicit new snapshot evidence before they can be truthfully displayed.

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
- [x] B800-023 lock bounded GET filters/paging and filter preservation for Alerts, Servers, History and contextual Reports navigation (`docs/work/B800-023.md`).
- [x] B800-024 lock HTML mutation PRG/feedback behavior while preserving explicit conflict/not-found/validation outcomes (`docs/work/B800-024.md`).
- [x] B800-025 align visible incident/AI Advisor mutation controls with existing Operator/Administrator endpoint policies while preserving Viewer evidence (`docs/work/B800-025.md`).
- [x] B800-026 lock Connection Lab test-before-save, temporary credential cleanup, write-only secret and protected action wiring (`docs/work/B800-026.md`).
- [x] B800-027 lock Administrator-only Settings operational backup/restore wiring, exact `RESTORE` confirmation, audit and safe feedback (`docs/work/B800-027.md`).
- [x] B800-028 require exact typed `PRUNE` confirmation before Governance retention Apply, audit rejection and preserve dry-run-first behavior (`docs/work/B800-028.md`).
- [x] B800-029 lock Enterprise Operations role wiring: Read for viewers, Manage for server metadata, Operate for incident collaboration (`docs/work/B800-029.md`).
- [x] B800-030 close the B800-021..029 workflow tranche with a consolidated named-policy / POST / antiforgery regression owner and dedicated evidence closeout (`docs/work/B800-030.md`).

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
- [x] B800-059 add `AgentReliabilityProjection` using recent success rate, failure streak, P95 duration and duration regression; keep schedule lateness explicitly not evaluated until canonical server time-zone + recurrence/expected-run semantics exist.
- [x] B800-060 wire B400 run-history reliability into SQL Agent with explicit empty-evidence behavior and server drill-downs.
- [x] B800-061 add Agent collector/projection/UI regression coverage and least-privilege documentation.
- [ ] B800-062 validate Agent slice on exact-head CI/Real-SQL/Windows candidate.
- [x] B800-063 add bounded current Agent schedule/activity evidence before enabling lateness functions; preserve server-local next-run time and running state only, with lateness still disabled (`docs/work/B800-063.md`).
- [x] B800-064 add explicit policy-backed Full/Log backup RPO configuration with no default values; surface the policy but keep B300 compliance `Not evaluated` until per-database recovery/log evidence exists (`docs/work/B800-064.md`).
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

Protected workflow/navigation slice:
- `tests/Monitor.Web.Tests/B800WorkflowSafetyMatrixTests.cs`
- `tests/Monitor.Web.Tests/B800RazorPostWiringTests.cs`
- `tests/Monitor.Web.Tests/B800BoundedGetNavigationTests.cs`
- `tests/Monitor.Web.Tests/B800PrgFeedbackContractTests.cs`
- `tests/Monitor.Web.Tests/B800IncidentAdvisorRoleTests.cs`
- `tests/Monitor.Web.Tests/B800ConnectionLabWorkflowTests.cs`
- `tests/Monitor.Web.Tests/B800SettingsBackupRestoreTests.cs`
- `tests/Monitor.Web.Tests/B800GovernanceRetentionWorkflowTests.cs`
- `tests/Monitor.Web.Tests/B800EnterpriseOperationsRoleTests.cs`
- `docs/work/B800-021.md`
- `docs/work/B800-022.md`
- `docs/work/B800-023.md`
- `docs/work/B800-024.md`
- `docs/work/B800-025.md`
- `docs/work/B800-026.md`
- `docs/work/B800-027.md`
- `docs/work/B800-028.md`
- `docs/work/B800-029.md`

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

SQL Agent run-history/activity slice:
- `src/Monitor.Web/Models/ServerHealthSnapshot.cs`
- `src/Monitor.Web/Services/SqlServerSnapshotCollector.cs`
- `src/Monitor.Web/Services/AgentReliabilityProjection.cs`
- `src/Monitor.Web/Views/Operations/Jobs.cshtml`
- `scripts/sql/monitored_sql_least_privilege.sql`
- `tests/Monitor.Web.Tests/AgentReliabilityProjectionTests.cs`
- `tests/Monitor.Web.Tests/AgentSnapshotCollectorTests.cs`
- `docs/work/B800-063.md`

Backup RPO policy slice:
- `src/Monitor.Web/Services/BackupPolicyOptions.cs`
- `src/Monitor.Web/Program.cs`
- `src/Monitor.Web/Controllers/OperationsController.cs`
- `src/Monitor.Web/Views/Operations/Backups.cshtml`
- `src/Monitor.Web/appsettings.json`
- `deploy/appsettings.Production.example.json`
- `tests/Monitor.Web.Tests/BackupPolicyOptionsTests.cs`
- `tests/Monitor.Web.Tests/BackupPolicyWiringTests.cs`
- `docs/work/B800-064.md`

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
- B800-063 adds bounded current Agent activity/next-run metadata while deliberately keeping lateness unevaluated.
- B800-023..029 add bounded navigation, PRG, role visibility, Connection Lab, Settings backup/restore, Governance destructive-confirmation and Enterprise role-wiring contracts without introducing new monitored-SQL or production mutation paths.
- B800-030 follow-up pre-reconciliation head `ef3d0d34260e0a6f7574331d73aa53f375a7231a` passed CI #2256; final documentation head still requires exact-head CI before Ready/merge.
- B800-064 implementation head `129fddec046467bc853675871af8991f88fd404c` passed normal CI #2275 before BATCH reconciliation; final documentation head requires exact-head validation before Ready/merge.
- Canonical documentation must reflect the current focused B800 follow-up slice. Exact-head applicable gates remain authoritative for Ready/merge.

## PR #288 scope freeze / merge gate

PR #288 is frozen to the evidence-backed work already present through B800-029 plus B800-031..063 and B800-069. **Do not add B800-030+, B800-064..068, B800-070+ or other new functional scope to #288 after this reconciliation; continue those under #287 in a subsequent PR.**

`docs/FEATURE_CATALOG.md`, `docs/STATUS.md`, `docs/IMPLEMENTATION_PLAN.md`, and this ledger must describe the same frozen scope. Before PR #288 is marked Ready or merged, normal CI, applicable Real SQL and Windows production-candidate must all be Green on one exact final head, the branch must remain current with `main`, and review threads must remain resolved. Issue #287 remains OPEN after this partial slice.
