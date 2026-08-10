# Monitor

**Monitor** is an ASP.NET Core SQL Server operations center focused on fast DBA situational awareness, snapshot-first monitoring, bounded production persistence and safe human-reviewed operations.

## Current state

M0 through M6 are CI verified. M7 production-readiness now includes durable registration metadata, external environment secret routing, durable audit/history/incidents, protected local SQL Login credentials, and an explicit fail-closed deployment-topology guard. M8 enforces zero-SQL monitoring GETs: normal browser navigation reads cached evidence only.

## Run

```bash
dotnet restore Monitor.sln
dotnet build Monitor.sln
cd src/Monitor.Web
dotnet run
```

The development Admin password is represented only by a PBKDF2 salt/hash in source control.

## Zero-SQL monitoring reads

Dashboard, Servers, Server Details, health modules and incident navigation consume cached snapshot state. Opening a page does **not** initiate monitored SQL collection. Collection remains an explicit backend action: Operator/Administrator manual refresh POST or the configured backend scheduler.

## Deployment topology

```json
"Deployment": {
  "Mode": "SingleNode"
}
```

`SingleNode` is currently supported. `MultiNode` is a recognized intent but fails startup until shared registration/operational state and distributed coordination actually exist. Local files, local Data Protection key rings, process memory and network-share paths are not treated as distributed coordination.

Administrator **Settings** shows the effective topology and the remaining node-local state without exposing a mutation control.

## SQL Login credentials

SQL Login credentials entered through Connections receive server-generated `local:v1` references. The credential payload is protected with ASP.NET Data Protection using reference-scoped purposes and is stored outside `wwwroot` in an atomically replaced encrypted file. The Data Protection key ring is also persisted outside `wwwroot`.

The persisted secret JSON contains ciphertext only; registration metadata stores only the opaque reference. A lost/different key ring or tampered ciphertext fails closed. Existing `env:<alias>` and legacy external references remain compatible.

Because the protected local credential file and key ring are node-local, they do **not** make the application HA-safe.

## Registration persistence

```json
"RegistrationStore": {
  "Mode": "File",
  "Path": "App_Data/registrations.json"
}
```

Registration persistence contains safe endpoint/authentication metadata and opaque secret references only. It never stores plaintext SQL credentials or full connection strings.

## Operational-state persistence

```json
"OperationalStore": {
  "Mode": "File",
  "RootPath": "App_Data/operational"
}
```

Audit, history and incident state use independent versioned files. Candidate state is durably written before becoming live in-process. Invalid/corrupt state fails closed.

## External environment secrets

`env:FINANCE_PROD` resolves only from:

```text
MONITOR_SQL_SECRET_FINANCE_PROD_USERNAME
MONITOR_SQL_SECRET_FINANCE_PROD_PASSWORD
```

A provider-owned `env:` reference never falls through to appsettings when missing or partial.

## Architecture rules

- Browser/UI components never connect directly to monitored SQL Servers.
- Monitoring GETs are cache-only and never initiate SQL collection.
- Manual refresh/collection is explicit, authorized and backend-controlled.
- Snapshot cache remains the shared evidence/read boundary.
- Recommendations and Advisor output remain advisory-only and cannot execute production SQL.
- Secret-provider routing stays behind `IConnectionSecretStore`.
- Monitor-owned persistence never uses a monitored SQL Server as its state/configuration write target.
- `MultiNode` stays fail-closed until shared state and distributed coordination are real.

Next production-readiness task: **M7-017 — Shared-state capability + dedicated Monitor SQL Server provider** (Issue #52).
