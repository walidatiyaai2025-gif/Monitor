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
