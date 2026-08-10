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

M1-003 adds `ISqlServerSnapshotCollector`. A single bounded SQL command reads server name, product version, edition, instance, uptime and database online/total counts into `SqlServerIdentitySnapshot`. The query scans the small `sys.databases` catalog once and joins the singleton `sys.dm_os_sys_info` row. It performs no per-database calls and exposes no credentials, endpoint or provider exception text. Complete results require `VIEW ANY DATABASE` plus `VIEW SERVER STATE` (SQL Server 2019 and earlier) or `VIEW SERVER PERFORMANCE STATE` (SQL Server 2022+).
