# Architecture

## Monitored SQL data plane

```text
Monitored SQL Server
        |
        v
Central Collector
        |
        v
ServerHealthSnapshot
        |
        +--> Cache
        |
        v
ASP.NET Core Backend
        |
        v
Browser UI
```

Browser monitoring navigation is cache/Peek-only and never directly connects to or initiates collection from monitored SQL Servers. Collection is explicit backend work.

## Monitor control-plane persistence

Registration metadata, audit/history/incidents and protected local SQL credentials are Monitor-owned persistence. Local file implementations remain single-node.

M7-017 adds a distinct optional shared-state control-plane path:

```text
Monitor ASP.NET Core
        |
        v
ISharedStateDocumentStore
        |
        v
SqlServerSharedStateDocumentStore
        |
        v
Dedicated Monitor-owned SQL Server database
```

This provider is never inferred from a monitored server registration. Its connection-string **value** is resolved only from a named process environment variable. App configuration contains provider type, environment-variable name and bounded command timeout only.

## Shared-state document contract

Documents have a bounded key, monotonically increasing version, validated JSON payload and update timestamp. Maximum key length is 128 characters; payloads are capped at 1 MiB UTF-8.

`CompareExchangeAsync(key, expectedVersion, payload)` provides optimistic concurrency. The SQL Server backend executes with `SERIALIZABLE` isolation and `UPDLOCK, HOLDLOCK`. Creating a document requires expected version 0; updating requires the current version. Stale writers receive Conflict and the current document rather than overwriting newer state.

The SQL batch captures the applied/conflict result inside the same locked transaction before commit, so the result does not depend on a post-commit re-read.

## Schema lifecycle and readiness

`scripts/sql/monitor_shared_state_v1.sql` owns schema deployment. Runtime code does not perform DDL. The script is idempotent for version 1 and refuses to replace a different schema version.

The read-only Settings readiness path reports provider kind, readiness status and schema version only. Endpoint, connection string and raw provider failures are never rendered.

If the provider is Disabled, no shared-state SQL call occurs. If configured but missing/unavailable/mismatched, readiness is safely Not Ready.

## HA boundary

M7-017 is storage capability only. Existing registration/audit/history/incident repositories, protected-local-secret key ring, login limiter, snapshot cache/single-flight and scheduler ownership are not migrated by this task. `Deployment:MultiNode` therefore remains fail-closed.

M7-018 must migrate the required repositories and add distributed scheduler ownership/cross-node single-flight before MultiNode can be considered safe.
