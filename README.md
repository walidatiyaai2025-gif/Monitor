# Monitor

**Monitor** is an ASP.NET Core SQL Server operations center focused on fast DBA situational awareness, snapshot-first monitoring, bounded production persistence and safe human-reviewed operations.

## Current state

M0 through M6 are CI verified. M7 production-readiness now includes durable registration metadata, fail-closed external secret routing, durable bounded operational state, protected local SQL Login credentials, a fail-closed SingleNode/MultiNode topology guard, and the first real shared-state storage capability. M8 enforces zero-SQL monitoring GETs for monitored targets.

## Run

```bash
dotnet restore Monitor.sln
dotnet build Monitor.sln
cd src/Monitor.Web
dotnet run
```

The development Admin password is represented only by a PBKDF2 salt/hash in source control.

## Zero-SQL monitored-server reads

Dashboard, Servers, Server Details, health modules and incident navigation consume cached snapshot state. Opening those pages does **not** initiate monitored SQL collection. Collection remains an explicit backend action through Operator/Administrator manual refresh or the validated scheduler.

M7-017 adds an optional **separate Monitor-owned state database**. Administrator Settings may probe that dedicated state provider when enabled; this is control-plane storage readiness, not a query against a monitored SQL target.

## Deployment topology

```json
"Deployment": {
  "Mode": "SingleNode"
}
```

`SingleNode` remains the only enabled topology. `MultiNode` fails startup until application repositories and distributed coordination are actually migrated to shared implementations. A READY M7-017 storage provider by itself does not enable MultiNode.

## Shared-state provider — M7-017

The provider is disabled by default:

```json
"SharedState": {
  "Provider": "Disabled",
  "ConnectionStringEnvironmentVariable": "MONITOR_SHARED_STATE_SQL_CONNECTION",
  "CommandTimeoutSeconds": 5
}
```

To prepare shared storage, deploy `scripts/sql/monitor_shared_state_v1.sql` to a **dedicated Monitor-owned SQL Server database**, set the named process environment variable to that database connection string, then set `SharedState:Provider` to `SqlServer`.

The connection-string value is read directly from the process environment. It is not read from appsettings, rendered in Settings, written to audit, or inferred from a monitored server registration.

The runtime application does **not** create or migrate the schema. The v1 deployment script is idempotent and refuses to overwrite an incompatible schema version.

The shared-state contract is a bounded versioned JSON document store with optimistic compare/exchange. SQL Server writes use `SERIALIZABLE` plus `UPDLOCK/HOLDLOCK`; a stale expected version returns Conflict rather than overwriting newer state.

M7-017 is storage capability only. Registration, audit, history, incidents, scheduler ownership and cross-node single-flight are still on their existing boundaries and are not migrated by this task.

## SQL Login credentials

UI-entered SQL Login credentials use server-generated `local:v1` references. Payloads are protected with ASP.NET Data Protection using reference-scoped purposes and persisted in an encrypted atomic file outside `wwwroot`; the Data Protection key ring is also persisted outside `wwwroot`. Lost/different keys or tampered ciphertext fail closed.

The protected local secret store and key ring remain node-local. Existing `env:<alias>` and legacy external references remain compatible.

## Architecture rules

- Browser/UI components never connect directly to monitored SQL Servers.
- Monitoring GETs are cache-only and never initiate monitored SQL collection.
- Manual monitored-SQL refresh/collection is explicit, authorized and backend-controlled.
- Snapshot cache remains the shared monitored-evidence/read boundary.
- Recommendations and Advisor output remain advisory-only and cannot execute production SQL.
- Secret-provider routing stays behind `IConnectionSecretStore`.
- Shared-state SQL is a separate Monitor-owned control-plane database, never an implicitly reused monitored target.
- `MultiNode` stays fail-closed until M7-018 migrates required state and distributed coordination.
