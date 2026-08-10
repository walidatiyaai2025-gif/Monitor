# Monitor

**Monitor** is an ASP.NET Core SQL Server operations center focused on fast DBA situational awareness, snapshot-first monitoring, bounded production persistence and safe human-reviewed operations.

## Current state

M0 through M6 are CI verified. M7 production-readiness includes durable registration metadata, fail-closed external secret routing, durable bounded operational state, protected local SQL Login credentials, a topology guard and a dedicated Monitor shared-state SQL capability. M8 enforces zero-SQL monitoring GETs for monitored targets.

BATCH-100 is the active enterprise hardening program. Batch 1 adds shared registration/audit/history/incident state plus distributed scheduler/manual-refresh coordination. Batch 2 adds shared encrypted Data Protection key management and safe SQL credential-reference migration/rotation. `docs/BATCH_100.md` is the 100-task execution ledger.

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

The optional **separate Monitor-owned state database** carries control-plane state and coordination only. Administrator Settings may probe that dedicated state provider when enabled; this is not a query against a monitored SQL target.

## Deployment topology

```json
"Deployment": {
  "Mode": "SingleNode"
}
```

`SingleNode` remains the safe default. BATCH-100 Batch 1 introduces shared repository adapters and distributed leases, but `MultiNode` remains fail-closed until every cross-field prerequisite is HA-safe. A READY shared-state database alone does not enable MultiNode.

## Shared-state provider

The provider is disabled by default:

```json
"SharedState": {
  "Provider": "Disabled",
  "ConnectionStringEnvironmentVariable": "MONITOR_SHARED_STATE_SQL_CONNECTION",
  "CommandTimeoutSeconds": 5
},
"HaState": {
  "UseSharedRegistrations": false,
  "ImportLocalRegistrationsWhenSharedEmpty": false,
  "UseSharedOperationalState": false
},
"Coordination": {
  "Enabled": false,
  "NodeIdEnvironmentVariable": "MONITOR_NODE_ID",
  "SchedulerLeaseSeconds": 90,
  "RefreshLeaseSeconds": 30,
  "MaxConflictRetries": 12
}
```

To prepare shared storage, deploy `scripts/sql/monitor_shared_state_v1.sql` to a **dedicated Monitor-owned SQL Server database**, set the named process environment variable to that database connection string, then set `SharedState:Provider` to `SqlServer`.

The connection-string value is read directly from the process environment. It is not read from appsettings, rendered in Settings, written to audit, or inferred from a monitored server registration.

The runtime application does **not** create or migrate the schema. The v1 deployment script is idempotent and refuses to overwrite an incompatible schema version.

The shared-state contract is a bounded versioned JSON document store with optimistic compare/exchange. Shared application adapters retain the existing service interfaces. Distributed scheduler and refresh coordination use expiring versioned leases in the same dedicated control-plane provider.

## SQL Login credentials and HA key management

Single-node defaults continue to allow server-generated `local:v1` credentials protected by ASP.NET Data Protection. Payloads are persisted in an encrypted atomic file outside `wwwroot`; the default Data Protection key ring is also outside `wwwroot`.

For HA preparation, key-ring storage can be switched explicitly:

```json
"DataProtectionKeyStore": {
  "Mode": "SharedState",
  "KeyEncryptionKeyEnvironmentVariable": "MONITOR_DP_KEK"
},
"CredentialPolicy": {
  "AllowLocalOwnedCredentials": false
}
```

`MONITOR_DP_KEK` must contain a base64-encoded 256-bit key-encryption key. Shared key-ring XML is AES-256-GCM encrypted before it enters the dedicated Monitor state provider. The KEK is read directly from the process environment and is never persisted by Monitor. Missing, invalid or wrong KEK material fails closed; an explicit SharedState key-ring configuration does not silently downgrade to local files.

Existing `env:<alias>` and legacy external secret references remain compatible. Connection Lab can replace an existing SQL Login reference with a tested external reference. The workflow resolves the candidate, runs bounded Test Connection, commits registration metadata only after success, then removes the old Monitor-owned local secret when safe. Failed replacement keeps the current registration/credential unchanged. Audit records only bounded actor/action/registration/outcome metadata; current and candidate references are never rendered or audited.

## Architecture rules

- Browser/UI components never connect directly to monitored SQL Servers.
- Monitoring GETs are cache-only and never initiate monitored SQL collection.
- Manual monitored-SQL refresh/collection is explicit, authorized and backend-controlled.
- Snapshot cache remains the shared monitored-evidence/read boundary.
- Recommendations and Advisor output remain advisory-only and cannot execute production SQL.
- Secret-provider routing stays behind `IConnectionSecretStore`.
- Shared-state SQL is a separate Monitor-owned control-plane database, never an implicitly reused monitored target.
- MultiNode stays fail-closed until all remaining distributed login-security and snapshot-cache/delivery prerequisites are verified.
