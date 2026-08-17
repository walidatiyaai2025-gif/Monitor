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
| Alerts | incident workflow/query + role-scoped transitions | **B800 bounded filters/paging + PRG/conflict + role-visible mutation controls regression-locked; B800-074 routes operator paging/summary through `IHealthIncidentRepository.Read(...)`, and B800-075 gives File/Shared/Telemetry production repositories native `Read(...)` paths instead of the compatibility `GetAll()` fallback** |
| Recommendations | incident repository + deterministic recommendation engine | Wired; incident/Advisor mutation controls align with Operator/Administrator policy boundary |
| Reports | versioned CSV/ZIP/JSON endpoints | **Existing exports remain wired; B800-081 adds a Viewer+ bounded/versioned/redacted Fleet decision-support CSV from cache/control-plane evidence only, with explicit unavailable state and no per-incident routing identifiers** |
| Connection Lab | registration/test/credential workflow | B800 test-before-save, write-only secret and protected action wiring regression-locked |
| Audit | bounded audit store | Wired |
| History | stored snapshot trends | B800 bounded window/limit/paging navigation regression-locked |
| Fleet Intelligence | enterprise metadata/incidents projection + B300/B400 decision helpers | **B800-071 correlation/routing is merged; B800-073 bounds active incident decision evidence; B800-074/075 provide bounded native repository reads; B800-076 makes operator-policy availability explicit; B800-078 projects the existing deterministic B300 incident-risk summary only when the same bounded active-incident and required policy evidence is complete; B800-079 summarizes existing B300 route decisions across that full complete bounded Fleet decision population while retaining deterministic top-20 row detail; B800-080 summarizes the complete current B400 correlation-cluster population while retaining deterministic top-20 cluster detail and fails explicit outside the B400 coverage bound** |
| Enterprise Operations | governance metadata/incidents control plane | **B800 role matrix regression-locked; B800-072 maintenance decision support is merged, B800-073 fails explicit on incident overflow, B800-074/075 source that evidence through bounded native repository reads, and B800-077 makes Maintenance policy availability fail explicit** |
| Observability | control-plane telemetry/readiness | **B800-075 active-incident telemetry uses exact bounded-query `TotalMatched` instead of `GetAll().Count(...)`; no incident evidence text is copied into telemetry** |
| Settings | readiness + operational backup/restore POST workflows | B800 Administrator-only Create/Validate/Restore, exact `RESTORE` confirmation, audit and safe feedback regression-locked |
| Governance retention | dry-run/apply workflow | B800 destructive apply now requires exact typed `PRUNE`; rejection is audited and fails closed |
| Operator help/readiness | control-plane guidance | Existing read-only surfaces |

## Data-availability boundary discovered during inventory

The B800 branch extends `ServerHealthSnapshot` while preserving optional/backward-compatible shapes. The bounded collector now projects server identity/version/edition/uptime, database totals/states plus up to 50 user-database logical name/state rows with non-online states prioritized, OS/SQL process memory, max server memory, Total/Target Server Memory, PLE, Memory Grants Pending, dominant memory-clerk class/size, full-backup aggregate, SQL Agent aggregate, allocated storage, blocking count/max wait, bounded performance counts, up to 12 non-benign cumulative wait types from `sys.dm_os_wait_stats`, up to 12 logical database/file I/O counter rows from `sys.dm_io_virtual_file_stats` joined to `sys.master_files`, up to 50 recent SQL Agent job-summary history rows from `msdb.dbo.sysjobhistory` joined to `sysjobs`, and up to 50 current Agent activity rows from the latest `msdb.dbo.sysjobactivity` session.

The database-state detail contains logical user-database name plus `state_desc` only and remains bounded to 50 rows; no table data or physical path is collected. The memory, wait and file-I/O additions reuse the existing read-only server permission boundary (`VIEW SERVER PERFORMANCE STATE` on SQL Server 2022+ or `VIEW SERVER STATE` on older supported versions plus `VIEW ANY DEFINITION`). SQL Agent history adds read-only `SELECT` on `msdb.dbo.sysjobhistory`; current Agent activity adds read-only `SELECT` on `msdb.dbo.sysjobactivity`; no Agent operator/write role is granted. Wait evidence contains only wait type and bounded counters. File-I/O evidence contains database/logical-file identity plus cumulative read/write/stall/byte counters; `physical_name` is never selected. Agent run-history contains logical job name, owner, success/failure, run ordering key and duration only. Current Agent activity contains logical job name, server-local `next_scheduled_run_date` with `DateTimeKind.Unspecified`, and a running flag derived from current activity state. Step rows, commands, command text, recurrence definitions, proxies and credentials are not collected.

Wait and file-I/O counters are cumulative since SQL Server start and are normalized by collected uptime in pure cached projections. Agent run-history reliability is derived from bounded recent outcomes and durations. Current Agent next-run time is preserved only as server-local wall-clock evidence: it is not converted to UTC and is not classified Late/On-time because the snapshot does not carry canonical server time-zone identity or recurrence/expected-run policy. `AgentReliabilityProjection` therefore keeps `ScheduleLatenessEvaluated = false`. No SQL text, query plans, client identity, table data, physical filesystem paths, or configuration writes are collected.

B300 estate identity and runtime-pressure helpers are wired from cached evidence. B300 per-database state classification/actionable/worst-observed helpers are now wired only from retained exact database-state rows rather than reconstructing detail from aggregate `OfflineOrOther`. B400 wait intelligence, B400 file-I/O intelligence, and the run-history portions of B400 Agent reliability are wired from bounded cached evidence. Bounded current Agent activity/next-run metadata is also available, but lateness scoring remains disabled until the time-zone/recurrence/expected-run contract exists. B800-064 now provides explicit policy-backed Full/Log RPO configuration with no numeric defaults; the Backups page can display that policy metadata but deliberately does not invoke `Batch300BackupCompliance` because the snapshot still lacks per-database recovery model and last log-backup timestamps. B400 query-regression, TempDB, transaction-log and HA helpers still require explicit new snapshot evidence before they can be truthfully displayed.

B800-071 and B800-072 deliberately consume only repository/control-plane evidence. Fleet routing/correlation uses current incidents plus registered-server environment, suppression, maintenance and optional assignee metadata. Maintenance decision support uses enabled registration, server environment, observed configured maintenance-window activity and current open critical-incident count. Governed approval, rollback-plan, independently approved-window, replica-readiness and policy-backed recent-backup facts remain nullable; the maintenance surface returns `NotEvaluated` whenever the selected operation actually requires an unavailable fact. An observed configured window is never promoted into approval evidence.

B800-073 centralizes the incident evidence admitted into Fleet/Maintenance operator decisions through `BoundedIncidentReadModel`. The default decision-input limit is 100, matching the existing `PerformanceScaleOptions.IncidentMaxPageSize` default. Active incidents are scoped to the relevant registration set, deterministically ordered and retained only up to the bound; overflow remains explicit. A truncated set is never treated as complete: Fleet withholds B400 correlation/B300 routing and rule hot-spots, while Maintenance supplies `null` for active Critical incident count so the B800-072 wrapper remains `NotEvaluated` instead of inferring zero.

B800-074 introduces `IncidentRepositoryQuery` / `IncidentRepositoryReadResult` and routes Alerts plus the B800-073 Fleet/Maintenance decision reads through `IHealthIncidentRepository.Read(...)`. The contract returns only the requested deterministic page while preserving exact global incident summary, exact filtered match count and `HasMore`; status/severity/rule/server filters are applied before paging. InMemory has a native `Read` implementation. `GetAll()` remains intentionally available for full-state workflows such as operational backup. PR #306 exact final reconciled head `2b845173ae0a260b01a3b7fae9f95e28019b7d87` passed CI `32048271534`, Real SQL `32048271523`, and Windows production-candidate `32048271563`; PR #306 squash-merged as `7f388f04da3b1d681f1464f2ee77a361183e542d`.

B800-075 specializes the remaining production repository/decorator paths without changing persistence formats. `FileHealthIncidentRepository.Read(...)` projects from the already-loaded `_items.Values` under the existing repository lock, avoiding the extra ordered `GetAll()` materialization/copy but not claiming disk-indexed queries. `SharedHealthIncidentRepository.Read(...)` reads/deserializes/validates the existing single `monitor:incidents:v1` document once and projects from that state; it does not make SharedState row-queryable or server-query-bounded at the physical provider level. `TelemetryHealthIncidentRepository.Read(...)` forwards directly to the inner repository and active-incident observation uses `Read(...ExcludeResolved...).TotalMatched` instead of `GetAll().Count(...)`. `GetAll()` remains available for explicit full-state backup/export workflows. PR #307 exact final reconciled head `b4ac0fa9ff1969438bb14f877b9febc7a4768d66` passed CI `32050338379`, Real SQL `32050338400`, and Windows production-candidate `32050338383`; PR #307 squash-merged as `e29890ecfcf6a8b04e1451e335959621b41e26f7`.

B800-076 reuses the existing `OperatorPolicyReadService`, `ServerOperatorPolicyState` and `IncidentOperatorPolicyState` availability contract in Fleet Intelligence. Registration/cache/risk/advanced evidence remains renderable when operator metadata is corrupt or shared-state metadata reads are unavailable. Environment/group/tag buckets admit only readable server policy states, and maintenance/suppression totals are withheld in the UI whenever server policy evidence is incomplete rather than converting an unavailable policy read into a default fact. Active-incident rule hot-spots plus B300 routing/B400 correlation require complete bounded incident evidence and readable server/incident policy states for the decision population. A readable `Assignee == null` remains legitimate unassigned evidence; a failed metadata read is a distinct unavailable state and blocks decision support instead of becoming `null` assignee. No monitored-SQL, notification, paging, mutation, remediation or new browser collection path is introduced. PR #308 exact final reconciled head `62cfd95f974a45f33b63d52a5a86a17e9d39aaf6` passed CI `32053753000`, Real SQL `32053753184`, and Windows production-candidate `32053753230`; PR #308 squash-merged as `a5799ea01ff3dc388a3a904206e72c18418d774f`.

B800-077 applies the same availability boundary to Maintenance Decision Support. The controller now consumes `IOperatorPolicyReadService` instead of calling `IOperatorMetadataStore` directly. `MaintenanceDecisionEvidence.IsProduction` and observed maintenance-window activity are nullable. Unreadable server policy leaves environment/window evidence unavailable and returns `NotEvaluated` with `environment-class` rather than treating unknown environment as non-production or an unavailable window as inactive. Successful `ServerOperatorPolicyState` carries the metadata already read by `OperatorPolicyReadService` as an optional payload so configured-window detail can render without a second store read; unavailable states carry no payload. Existing bounded incident evidence remains independent, and configured windows remain observed scheduling facts rather than approval evidence. No monitored-SQL, maintenance execution, notification, mutation, paging or remediation is introduced. PR #309 squash-merged as `66adf070f446a49a7df8bf4bbdb62620a323f473`.

B800-078 reuses the existing deterministic `Batch300FleetRisk` contract on the visible Fleet Intelligence surface. The score is derived only from the same complete bounded active-incident population already admitted to B800-073/B800-076 decision support and only when every retained incident has readable server + incident operator-policy state. Finding severity is mapped through the existing B400 severity weights; suppression and observed maintenance policy feed only the existing B300 weighting logic. The result is nullable and withheld together with correlation/routing/hot-spots whenever incident evidence is truncated or required policy evidence is unavailable. A complete empty active-incident population remains valid evidence and may truthfully summarize as `0 / Healthy`. The UI is read-only and exposes score, level, actionable/suppressed counts and safe deterministic top rule keys; it sends no notification and performs no mutation or remediation.

B800-079 closes a coverage gap inside the existing B300 routing decision support without changing its algorithm or evidence boundary. `FleetDecisionSupport.Build(...)` evaluates `Batch300AlertRouting.Decide(...)` once for every valid incident admitted by the already-complete bounded Fleet decision population, then builds `FleetRoutingSummary` over that full population. `Page`, `Notify`, `Queue` and `None` are mutually exclusive route buckets and sum exactly to `EvaluatedIncidents`; suppression, observed maintenance and unassigned-owner counts are separate coverage facts and may overlap. The row-level routing table remains a deterministic top-20 detail view only. The UI states that distinction explicitly and does not claim global or unbounded incident coverage. No sender, notification, incident/suppression mutation, maintenance execution or remediation is introduced. PR #311 exact final reconciled head `a718eaa029b11ddfc74d290e3a50c87d77e1715a` passed CI `32061583643` / #2555, Real SQL `32061583619` / #323, and Windows production-candidate `32061583623` / #443; PR #311 squash-merged as `4e71a708ca31874146a56594f4d61f0298fb9de0`.

B800-080 closes the analogous bounded-correlation coverage gap without changing the B400 algorithm. The existing B400 `Correlate(...)` clamp of 100 is named `MaxClusterLimit = 100`. Because the normal B800-073 Fleet decision population is capped at 100 active incidents, a complete current Fleet population can produce at most 100 correlation clusters; `FleetDecisionSupport.Build(...)` therefore requests up to that existing maximum, derives `FleetCorrelationSummary` across all returned clusters, and retains only the deterministic top-20 clusters for row detail. The summary reports evaluated incidents, total/Critical/Warning/Info clusters, multi-server clusters, maximum affected-server count and highest existing B400 score. If a direct caller supplies more than 100 valid incidents, `CorrelationSummary` is withheld while top-20 detail remains visible, explicitly avoiding a false completeness claim. No score formula, threshold, bucketing rule, notification, failover, mutation, maintenance execution or remediation is introduced.

B800-081 begins the reports/exports tranche by projecting the already-available Fleet decision-support snapshot through the existing `EnterpriseReportContract`. Viewer+ `GET /reports/fleet-decision-support.csv` uses `FleetIntelligenceService.Read()` and therefore registration/operator metadata plus cached/control-plane evidence only. The export records evidence availability, aggregate Fleet risk/routing/correlation facts and deterministic top-20 correlation detail. It deliberately does not read per-incident routing suggestions and excludes incident/server IDs, assignees, credentials, connection strings, SQL text/plans, raw provider errors, monitored-SQL payloads and filesystem paths. Missing/truncated decision support remains explicit `Unavailable`; the shared `monitor-export-v2` row/byte/cell bounds and formula-safety apply. No monitored-SQL, action, mutation or remediation is added.

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

- [x] B800-071 wire bounded B400 fleet-correlation clusters and B300 routing recommendations from current incident/control-plane facts into Fleet Intelligence as read-only decision support; no sender, incident mutation or remediation (`docs/work/B800-071.md`, PR #303 merged as `3821d1a1ebd15039a3c93b1e77ff7bac210e0b08`).
- [x] B800-072 expose B400 maintenance-safety rules as GET-only operator decision support with nullable required-evidence gating; observed configured maintenance windows are not treated as approval, and no maintenance operation can execute (`docs/work/B800-072.md`, PR #304 merged as `ce81b47ee4de09ced03e4ae275e639a93d1fecb9`).
- [x] B800-073 bound active incident evidence admitted into Fleet/Maintenance decisions; overflow is explicit and prevents partial-set correlation/routing/hot-spot or false-zero maintenance decisions (`docs/work/B800-073.md`, PR #305 merged as `96e27b17de51e89f1e989fe2a9484f0226f2e53f`).
- [x] B800-074 add a bounded repository incident query contract and route Alerts/Fleet/Maintenance operator reads through it while preserving full-state `GetAll()` for explicit export/backup workflows (`docs/work/B800-074.md`, PR #306 merged as `7f388f04da3b1d681f1464f2ee77a361183e542d`).
- [x] B800-075 specialize File/Shared/Telemetry production incident repository `Read(...)` implementations so persisted/decorated operator reads no longer depend on the compatibility `GetAll()` fallback; preserve the single-document SharedState truth (`docs/work/B800-075.md`, PR #307 merged as `e29890ecfcf6a8b04e1451e335959621b41e26f7`).
- [x] B800-076 fail explicit on unreadable Fleet operator-policy metadata by reusing existing `PolicyReadable` states; keep cache/risk evidence visible, withhold unavailable policy totals/buckets and block incident decision support instead of fabricating defaults (`docs/work/B800-076.md`, PR #308 merged as `a5799ea01ff3dc388a3a904206e72c18418d774f`).
- [x] B800-077 fail explicit on unreadable Maintenance operator-policy metadata; nullable environment/window evidence keeps B400 decision support `NotEvaluated` instead of assuming non-production/inactive window (`docs/work/B800-077.md`, PR #309 merged as `66adf070f446a49a7df8bf4bbdb62620a323f473`).
- [x] B800-078 wire the existing deterministic B300 Fleet risk summary from complete bounded active incidents plus readable policy evidence; fail closed on truncation/unreadable policy and keep the surface read-only (`docs/work/B800-078.md`, PR #310 merged as `2dbf248e1af51878c61bbeb14313ca17d19e85a4`).
- [x] B800-079 summarize existing deterministic B300 routing across the full complete bounded Fleet decision population while retaining deterministic top-20 row detail; expose exhaustive route distribution plus suppression/maintenance/unassigned coverage without executing any route (`docs/work/B800-079.md`, PR #311 merged as `4e71a708ca31874146a56594f4d61f0298fb9de0`).
- [x] B800-080 summarize all existing B400 correlation clusters supported by the complete current bounded Fleet decision population while retaining deterministic top-20 cluster detail; withhold aggregate completeness outside the named B400 100-cluster bound and add no autonomous action (`docs/work/B800-080.md`, PR #312 merged as `142f8ed52b507b7807830378e63743ed2596b585`).

### B800-081..090 — reports and exports

- [x] B800-081 add a Viewer+ bounded/versioned/redacted Fleet decision-support CSV using existing cache/control-plane Fleet evidence only; export explicit evidence availability, aggregate risk/routing/correlation facts and safe top-20 correlation detail while excluding per-incident routing IDs/owners and sensitive payloads (`docs/work/B800-081.md`, PR #313).
- [ ] B800-082..090 continue bounded, versioned, redacted exports only where existing evidence and operator value justify them.

### B800-091..100 — final acceptance

- [ ] end-to-end/controller-service contracts, role/antiforgery tests, no-fake-data tests, responsive/accessibility review, canonical docs, exact-head CI and final closeout.

## Implementation evidence

The original broad implementation branch was `agent/b800-functional-screen-wiring`; later slices are intentionally delivered through focused follow-up branches under #287.

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

Fleet correlation/routing decision-support slice:
- `src/Monitor.Web/Services/FleetDecisionSupport.cs`
- `src/Monitor.Web/Services/FleetIntelligenceService.cs`
- `src/Monitor.Web/Views/FleetIntelligence/Index.cshtml`
- `src/Monitor.Web/Views/Shared/_FleetDecisionSupport.cshtml`
- `tests/Monitor.Web.Tests/B800FleetDecisionSupportTests.cs`
- `tests/Monitor.Web.Tests/B800FleetDecisionSupportSurfaceTests.cs`
- `docs/work/B800-071.md`

Maintenance safety decision-support slice:
- `src/Monitor.Web/Services/MaintenanceDecisionSupport.cs`
- `src/Monitor.Web/Controllers/MaintenanceDecisionSupportController.cs`
- `src/Monitor.Web/Views/MaintenanceDecisionSupport/Index.cshtml`
- `src/Monitor.Web/Views/EnterpriseOperations/Overview.cshtml`
- `tests/Monitor.Web.Tests/B800MaintenanceDecisionSupportTests.cs`
- `tests/Monitor.Web.Tests/B800MaintenanceDecisionSupportSurfaceTests.cs`
- `docs/work/B800-072.md`

Bounded incident decision-evidence slice:
- `src/Monitor.Web/Services/BoundedIncidentReadModel.cs`
- `src/Monitor.Web/Services/FleetIntelligenceService.cs`
- `src/Monitor.Web/Controllers/MaintenanceDecisionSupportController.cs`
- `src/Monitor.Web/Views/FleetIntelligence/Index.cshtml`
- `src/Monitor.Web/Views/MaintenanceDecisionSupport/Index.cshtml`
- `tests/Monitor.Web.Tests/B800BoundedIncidentReadModelTests.cs`
- `tests/Monitor.Web.Tests/B800FleetDecisionSupportSurfaceTests.cs`
- `tests/Monitor.Web.Tests/B800MaintenanceDecisionSupportSurfaceTests.cs`
- `docs/work/B800-073.md`

Repository-bounded incident query slice:
- `src/Monitor.Web/Services/IncidentRepositoryRead.cs`
- `src/Monitor.Web/Services/HealthIncidentService.cs`
- `src/Monitor.Web/Services/BoundedIncidentReadModel.cs`
- `tests/Monitor.Web.Tests/B800IncidentRepositoryQueryTests.cs`
- `tests/Monitor.Web.Tests/B800BoundedIncidentReadModelTests.cs`
- `docs/work/B800-074.md`

Persisted incident read specialization slice:
- `src/Monitor.Web/Services/PersistedIncidentRepositoryRead.cs`
- `src/Monitor.Web/Services/DurableOperationalStores.cs`
- `src/Monitor.Web/Services/SharedHaFoundation.cs`
- `src/Monitor.Web/Services/ProductionObservability.cs`
- `tests/Monitor.Web.Tests/B800PersistedIncidentReadTests.cs`
- `docs/work/B800-075.md`

Fleet operator-policy availability slice:
- `src/Monitor.Web/Services/FleetIntelligenceService.cs`
- `src/Monitor.Web/Views/FleetIntelligence/Index.cshtml`
- `tests/Monitor.Web.Tests/B800FleetOperatorPolicyAvailabilityTests.cs`
- `tests/Monitor.Web.Tests/B800FleetDecisionSupportSurfaceTests.cs`
- `docs/work/B800-076.md`

Maintenance operator-policy availability slice:
- `src/Monitor.Web/Services/OperatorPolicyServices.cs`
- `src/Monitor.Web/Services/MaintenanceDecisionSupport.cs`
- `src/Monitor.Web/Controllers/MaintenanceDecisionSupportController.cs`
- `src/Monitor.Web/Views/MaintenanceDecisionSupport/Index.cshtml`
- `tests/Monitor.Web.Tests/B800MaintenancePolicyAvailabilityTests.cs`
- `tests/Monitor.Web.Tests/B800MaintenanceDecisionSupportTests.cs`
- `tests/Monitor.Web.Tests/B800MaintenanceDecisionSupportSurfaceTests.cs`
- `docs/work/B800-077.md`

Bounded Fleet incident-risk slice:
- `src/Monitor.Web/Services/FleetIntelligenceService.cs`
- `src/Monitor.Web/Views/FleetIntelligence/Index.cshtml`
- `tests/Monitor.Web.Tests/B800FleetOperatorPolicyAvailabilityTests.cs`
- `tests/Monitor.Web.Tests/B800FleetDecisionSupportSurfaceTests.cs`
- `docs/work/B800-078.md`

Full bounded Fleet routing-coverage slice:
- `src/Monitor.Web/Services/FleetDecisionSupport.cs`
- `src/Monitor.Web/Views/Shared/_FleetDecisionSupport.cshtml`
- `tests/Monitor.Web.Tests/B800FleetDecisionSupportTests.cs`
- `tests/Monitor.Web.Tests/B800FleetDecisionSupportSurfaceTests.cs`
- `docs/work/B800-079.md`

Full bounded Fleet correlation-coverage slice:
- `src/Monitor.Web/Services/Batch400FleetCorrelation.cs`
- `src/Monitor.Web/Services/FleetDecisionSupport.cs`
- `src/Monitor.Web/Views/Shared/_FleetDecisionSupport.cshtml`
- `tests/Monitor.Web.Tests/B800FleetDecisionSupportTests.cs`
- `tests/Monitor.Web.Tests/B800FleetDecisionSupportSurfaceTests.cs`
- `docs/work/B800-080.md`

Bounded Fleet decision-support export slice:
- `src/Monitor.Web/Services/FleetDecisionSupportExport.cs`
- `src/Monitor.Web/Services/EnterpriseReportingServices.cs`
- `src/Monitor.Web/Controllers/EnterpriseReportsController.cs`
- `src/Monitor.Web/Services/EnterpriseSecurityPolicy.cs`
- `src/Monitor.Web/Views/Portal/Reports.cshtml`
- `tests/Monitor.Web.Tests/B800FleetDecisionSupportExportTests.cs`
- `docs/work/B800-081.md`

## Validation chronology

- Initial Server Details slice: CI Green.
- First Memory Health head: CI build failed on one nullable conditional expression in `MemoryIntelligenceProjection` (`CS0173`). The nullable type was corrected explicitly to `long?`; no product/safety contract was weakened.
- Wait-slice head `5dc585fad80f24dfa2bacdd729fc0b1b1d3f26fe`: CI #2029 and Real SQL #161 Green.
- Storage/I/O head `ffc7b307e99558d92500e7278ff62ec721796e7f`: CI #2046, Real SQL #169 and Windows production-candidate #265 all Green. Real SQL validation includes application of the read-only monitored-SQL role and execution against SQL Server 2022.
- Database-state source head `895297809d5dcb656cb3e6bc064aba96d02e58b1`: CI #2120 and Real SQL #205 Green; later shared-branch commits supersede it as merge evidence.
- B800-063 adds bounded current Agent activity/next-run metadata while deliberately keeping lateness unevaluated.
- B800-023..029 add bounded navigation, PRG, role visibility, Connection Lab, Settings backup/restore, Governance destructive-confirmation and Enterprise role-wiring contracts without introducing new monitored-SQL or production mutation paths.
- B800-030 follow-up pre-reconciliation head `ef3d0d34260e0a6f7574331d73aa53f375a7231a` passed CI #2256; later focused slices supersede that head as current batch evidence.
- B800-064 implementation head `129fddec046467bc853675871af8991f88fd404c` passed normal CI #2275 before later BATCH work.
- B800-071 exact final head `5a18b5167cc24cd292ce7826fb144434762c7eae` passed CI #2393 and Windows production-candidate #393; Real SQL was not selected because the slice added no monitored-SQL query/collector/permission path. PR #303 squash-merged as `3821d1a1ebd15039a3c93b1e77ff7bac210e0b08`.
- B800-072 exact final head `4b57a688150f974f8f3cd5b7255912b7e3328260` passed CI run `32028002814`, Real SQL run `32028002795`, and Windows production-candidate run `32028002783`; PR #304 squash-merged as `ce81b47ee4de09ced03e4ae275e639a93d1fecb9`.
- B800-073 exact final reconciled head `443eccf16fb1fbcfde1cf5ff3f10864d487fd19b` passed CI `32030485150`, Real SQL `32030485078`, and Windows production-candidate `32030485093`; PR #305 squash-merged as `96e27b17de51e89f1e989fe2a9484f0226f2e53f`.
- B800-074 exact final reconciled head `2b845173ae0a260b01a3b7fae9f95e28019b7d87` passed CI `32048271534`, Real SQL `32048271523`, and Windows production-candidate `32048271563`; PR #306 squash-merged as `7f388f04da3b1d681f1464f2ee77a361183e542d`.
- B800-075 pre-canonical implementation head `b811e226b62ee65b29377afe94a2d30f16d334a1` passed CI `32049330852` and Windows production-candidate `32049330212`. Real SQL was not selected because no monitored-SQL query/collector/permission path changed. B800-075 exact final reconciled head `b4ac0fa9ff1969438bb14f877b9febc7a4768d66` passed CI `32050338379`, Real SQL `32050338400`, and Windows production-candidate `32050338383`; PR #307 squash-merged as `e29890ecfcf6a8b04e1451e335959621b41e26f7`.
- B800-076 pre-canonical implementation head `f3e5c37535fa655be1b5b76209b6aa329517b4ac` passed CI `32052187627` and Windows production-candidate `32052187635`; Real SQL was not selected. Exact final reconciled head `62cfd95f974a45f33b63d52a5a86a17e9d39aaf6` passed CI `32053753000`, Real SQL `32053753184`, and Windows production-candidate `32053753230`; PR #308 squash-merged as `a5799ea01ff3dc388a3a904206e72c18418d774f`.
- B800-077 pre-canonical implementation head `4ee47dbf5770443709e75590fb0534b01e121e42` passed CI `32054786245` and Windows production-candidate `32054786197`; Real SQL was not selected on that implementation head. PR #309 subsequently squash-merged as `66adf070f446a49a7df8bf4bbdb62620a323f473` after its reconciled-head gate set completed.
- B800-078 implementation head `5ea5f097d2508afff4fc0fd3677d32f67c0fb55c` passed CI `32058330149` / #2523 and Windows production-candidate `32058330150` / #430. Exact final reconciled head `d7e94c23c5189273bd905c206ff178b07d5237cf` passed CI `32059355185` / #2535, Real SQL `32059355193` / #319, and Windows production-candidate `32059355317` / #436; PR #310 squash-merged as `2dbf248e1af51878c61bbeb14313ca17d19e85a4`.
- B800-079 implementation head `38d0bf73844675bc0bbb039d7c8a02f90f9c6df5` passed CI `32060063245` / #2543 and Windows production-candidate `32060063147` / #437. Exact final reconciled head `a718eaa029b11ddfc74d290e3a50c87d77e1715a` passed CI `32061583643` / #2555, Real SQL `32061583619` / #323, and Windows production-candidate `32061583623` / #443; PR #311 squash-merged as `4e71a708ca31874146a56594f4d61f0298fb9de0`.
- B800-080 implementation head `98013dc4b291aba6a91208b23aced27e625dc65a` passed CI `32062311326` / #2564 and Windows production-candidate `32062311341` / #444, including Release build/full suite, production tooling, win-x64 publish, secret-free validation, production smoke before/after restart, clean package validation, ZIP/SHA-256 and artifact upload. Real SQL was not selected because this slice changes no monitored-SQL query, collector or permission path. Exact final reconciled head `7a4289cfe1dd514e53bdad2274cd4e4c6dd1b96c` passed CI `32063280874` / #2576, Real SQL `32063280897` / #328, and Windows production-candidate `32063280918` / #450; PR #312 squash-merged as `142f8ed52b507b7807830378e63743ed2596b585`.
- B800-081 implementation head `950b455ca40c9a9d94df93035c646644ec57832c` passed CI `32064517286` / #2592 and Windows production-candidate `32064517289` / #456, including Release build/full suite, production tooling, win-x64 publish, secret-free validation, HTTPS/auth smoke before and after restart, clean package validation, ZIP/SHA-256 and artifact upload. Real SQL was not selected because this slice changes no monitored-SQL query, collector or permission path. Commit `935b70bf4312422912370542610a0048615984ae` changes only `docs/work/B800-081.md` to record implementation validation; no source/test drift entered after that implementation head.

## Current B800-081 merge gate

PR #313 is the current focused B800 slice and closes B800-081 only. `docs/FEATURE_CATALOG.md`, `docs/STATUS.md`, `docs/IMPLEMENTATION_PLAN.md`, and this ledger must describe B800-080 / PR #312 as merged and B800-081 as the current bounded Fleet decision-support export slice. Before PR #313 is marked Ready or merged, every repository-selected required workflow must be Green on the same exact final reconciled head, the branch must remain current with `main`, review threads must remain resolved, and the effective diff must stay bounded to B800-081 implementation/tests/work-note plus canonical reconciliation. Real SQL is required only if repository path policy selects it. Issue #287 remains OPEN for B800-082+.