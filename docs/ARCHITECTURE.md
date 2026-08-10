# Architecture

## Target flow

```text
Monitored SQL Server
        |
        v
Central Collector
        |
        v
ServerHealthSnapshot
        |
        +--> Cache / Monitoring Store
        |
        v
ASP.NET Core Backend
        |
        +--> SignalR (delivery only)
        |
        v
Browser UI
```

The browser never connects directly to monitored SQL Servers. Individual cards/charts never issue monitoring SQL.

## M0 implementation

M0 intentionally uses `DemoMonitorService` as an in-memory snapshot provider so visual behavior can be reviewed before collectors are built. Client-side heartbeat/clock animation uses no network fetch and creates no SQL activity.

## Planned core contracts

`ServerHealthSnapshot` will eventually contain connection, overall, CPU, memory, disk, database, backup, jobs, blocking, alerts, and critical incident state with `CollectedAt`.

## Authentication

M0 uses ASP.NET Core cookie authentication with one development Administrator. The password is verified against a PBKDF2-SHA256 derived hash; plaintext credentials are not committed.

## M1 server registration and secret boundary

`ServerRegistration` stores validated endpoint and authentication metadata only. SQL login values are represented by an opaque `ConnectionSecretReference`, excluded from JSON, and resolved only inside the backend through `IConnectionSecretStore`. The development implementation reads values from .NET User Secrets or environment-backed configuration and fails closed when a reference is missing. No plaintext password or full connection string is stored in the repository or registration model.

`IServerConnectionTester` owns the M1-002 Test Connection workflow. The administrator endpoint accepts only a registration ID, resolves credentials inside the backend, and delegates provider access to `ISqlConnectionProbe`. The SQL client uses a five-second connection timeout inside a seven-second overall budget, disables pooling for the test, honors request cancellation, and returns only fixed redacted result categories. Provider exception text and connection strings never cross the service boundary.

M1-003 adds `ISqlServerSnapshotCollector`. A single bounded SQL command reads server name, product version, edition, instance, uptime and database online/total counts into `ServerHealthSnapshot`. The query scans the small `sys.databases` catalog once and joins the singleton `sys.dm_os_sys_info` row. It performs no per-database calls and exposes no credentials, endpoint or provider exception text. Complete results require `VIEW ANY DATABASE` plus `VIEW SERVER STATE` (SQL Server 2019 and earlier) or `VIEW SERVER PERFORMANCE STATE` (SQL Server 2022+).

M1-004 promotes the collector result to the canonical `ServerHealthSnapshot` and adds a per-registration cache. Snapshots are fresh for 30 seconds; a last-known-good snapshot may be returned as explicitly stale for up to five minutes when refresh fails. A single-flight gate ensures concurrent consumers share one collection task, so additional screens do not create additional SQL calls. Cancelling one caller stops only that wait, not collection needed by other callers.

M1-005 adds `MonitorReadService` between MVC and the cache. When `Monitor:PrimaryServer` metadata is configured, the first estate card and its details use the cached real snapshot; remaining cards stay explicitly Demo. Fresh, stale, mixed and development-only modes are labeled in the UI. CPU, memory and SQL Agent values are marked Not collected for the real identity slice rather than copied from demo data. Configuration contains only endpoint metadata and an opaque secret reference; credential values remain under external `ConnectionSecrets` configuration.

M1-006 adds an administrator-only, anti-forgery protected refresh endpoint. `SnapshotRefreshService` accepts only a registration ID, enforces a 15-second per-server minimum interval atomically, and delegates forced collection to the same cache single-flight. Throttled requests never call SQL; concurrent accepted consumers still share one backend collection task.

M1-007 evaluated SignalR delivery and deferred implementation. The current system creates snapshots on request and has no scheduled publisher, so a hub would add reconnect/authentication/state complexity without carrying independently produced updates. SignalR may be introduced only after a backend scheduler or monitoring store publishes snapshot-changed events; delivery must remain downstream-only and must never trigger collection.

M2-001 extends `ServerHealthSnapshot` with an optional immutable `MemoryHealthSnapshot`. The existing collector query cross joins the singleton system/process memory DMVs, so memory fields add no second SQL round trip. Values are validated for nonnegative totals, available <= total and utilization within 0..100; malformed rows fail through the existing redacted collector boundary.

M2-002 maps cached SQL process memory utilization into the existing server card and Memory Health page through `MonitorReadService`. The page uses the same cache read as the estate UI and labels mixed real/demo modes; it does not call the collector or SQL directly.

M2-003 through M2-007 extend the same immutable snapshot with bounded database-state, backup, SQL Agent, storage and blocking summaries. They run inside the existing command, connection, two-second command timeout and seven-second overall budget, then flow through the same single-flight cache. The query is application-owned and fixed; no endpoint, secret, SQL text, job command, physical path or provider message enters the snapshot. Complete visibility requires least-privilege catalog/DMV access plus explicit `msdb` read access for backup and Agent summaries.

M2-008 through M2-013 expose those facts through a shared cache-only read projection and add bounded active-request, runnable-task and pending-I/O counts to the central collector. Dedicated module pages are presentation routes over the same snapshot, not collection triggers.

M3-001 through M3-004 introduce a pure rule evaluator and an in-memory incident repository. Findings contain only allowlisted rule metadata and compact evidence. Stable registration/rule fingerprints deduplicate repeated observations; only newer fresh healthy evaluation resolves an incident. The authorized incident page evaluates cached snapshots and never executes remediation SQL.

M3-005 through M3-016 add idempotent observations, bounded querying, incident details, antiforgery-protected operator transitions and deterministic rule-owned recommendations. Recommendations are presentation-only and never reach a SQL execution service.

M4-001 through M4-006 establish a normalized backend advisor context and provider abstraction. The only registered provider is disabled and returns a fixed status. No network call, tool invocation, SQL execution or autonomous remediation exists.

M5-001 through M5-007 add bounded in-memory aggregate history, a shared observer, a deterministic backend collection cycle and fixed-window trend reads. Schedule policy is disabled by default and no background host is activated yet.

M5-008 through M5-025 activate the scheduler infrastructure while keeping collection disabled by default. The host has no immediate startup run, no overlapping cycles, bounded per-server concurrency, failure isolation, capped backoff and allowlisted runtime status. The same batch adds bounded append-only audit metadata, policy-based Viewer/Operator/Administrator authorization, hardened cookies/security headers and partitioned login limiting.

M5-026 enriches the existing incident transition audit without changing the workflow service contract. `OperationsController` receives the canonical `IHealthIncidentRepository` from dependency injection, observes the incident immediately before and after the existing atomic transition, and writes bounded state context to the existing `IAuditStore`. Missing actor identity fails closed before mutation. When repository state is unavailable, the audit result falls back to the established `applied` / `conflict` values rather than fabricating state. No incident evidence or monitored-SQL data is added to audit payloads.

M4-007 through M4-013 add the only advisor request path: an authorized antiforgery-protected POST by incident ID. Server-side context flows through single-flight, evidence-version cache, timeout and circuit boundaries. The provider remains disabled unless explicitly replaced; results remain advisory and disconnected from SQL execution.

M6 introduces the first complete real-server journey. Login routes an empty estate to Connections. The administrator submits safe endpoint metadata plus Integrated Security, a process-memory SQL Login credential, or an external secret reference. The backend registers, tests, collects and observes the first snapshot in order. Only a successful test reaches collection. Estate and Dashboard reads show all registrations and preserve unavailable targets without mixing demo cards into a real estate.

## M7 registration persistence

M7-001 preserves `IServerRegistrationRepository` as the application boundary and adds a file-backed implementation for durable Monitor-owned registration metadata. The default file is `App_Data/registrations.json` under the application content root and the startup wiring rejects any configured path that resolves under `wwwroot`.

The file store serializes endpoint metadata, authentication mode, enabled/created metadata and the opaque `ConnectionSecretReference`. It never has access to SQL username/password values or a full connection string. Runtime credential values remain in `ConfigurationConnectionSecretStore` process memory and intentionally disappear on restart; a persisted runtime reference therefore becomes unresolved rather than causing a credential value to be written to disk.

Mutations are serialized, written to a same-directory temporary file with write-through and flush-to-disk, then moved over the durable store. Failed persistence rolls the in-memory mutation back. Startup treats malformed JSON, unsupported format versions, duplicate IDs and invalid domain data as fatal configuration-state errors instead of silently replacing the estate with an empty repository. This local store is a single-node production-readiness step; a shared/HA store can later replace it behind the unchanged repository interface.
