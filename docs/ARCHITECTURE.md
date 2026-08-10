# Architecture

## Monitored SQL data plane

```text
Monitored SQL Server
        |
        v
Bounded Central Collector
        |
        v
ServerHealthSnapshot
        |
        v
Capacity-bounded Snapshot Cache
        |
        +--> Dashboard / Servers / Health / Incidents / Trends
        |
        v
ASP.NET Core Backend
        |
        v
Browser UI
```

Browser monitoring navigation is cache/Peek-only and never directly connects to or initiates collection from monitored SQL Servers. Collection is explicit backend work through the scheduler or protected manual refresh.

## Scale boundaries

Batch 5 makes the data plane cost explicit:

- snapshot cache has a configured maximum entry count and deterministic oldest-entry eviction;
- estate GETs page registration metadata and Peek only the requested page;
- history/audit/incident projections have bounded windows/page sizes;
- manual refresh has an application-wide concurrency permit in addition to registration throttling and distributed single-flight;
- scheduler cycles use bounded deterministic jitter plus round-robin maximum-target batches;
- monitored background collection uses a capped SQL connection pool;
- Test Connection remains non-pooled to preserve credential-validation correctness.

These are deterministic operating budgets. CI asserts counts/output/concurrency rather than machine-specific elapsed-time microbenchmarks.

## Monitor control-plane persistence

Registration metadata, audit/history/incidents and protected local SQL credentials are Monitor-owned persistence. Local file implementations remain single-node.

The optional shared-state control-plane path is separate from monitored targets:

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

## Shared-state and HA adapters

Shared registrations, audit, incidents, snapshot history and scheduler runtime status retain the existing application interfaces while persisting through the versioned shared-document store. Distributed expiring leases provide scheduler leader ownership and cross-node manual-refresh single-flight.

Documents have a bounded key, monotonically increasing version, validated JSON payload and update timestamp. `CompareExchangeAsync` uses transactional optimistic concurrency; stale writers conflict instead of overwriting newer state.

A shared encrypted ASP.NET Data Protection key ring may also use this dedicated control-plane provider. The key XML is AES-256-GCM encrypted before persistence; the 256-bit KEK comes from process environment and is never stored by Monitor.

## Backup / restore boundary

Operational backup is a versioned canonical domain bundle rather than a raw file copy. It includes safe registration metadata/opaque references, incidents, bounded history and bounded audit. Each section is checksummed. Credential ciphertext, Data Protection keys/KEKs, provider connection material and monitored SQL text are excluded.

Restore validates before mutation, targets the configured File/Shared backend and stages previous durable state so applied sections can be rolled back in reverse order on failure.

## Health / observability boundary

```text
/health/live   -> process only
/health/ready  -> deployment + Monitor control-plane readiness
/health        -> safe aggregate health + bounded telemetry
/observability -> Administrator aggregate view
```

These routes never invoke monitored-SQL collection. Runtime telemetry contains aggregate counters, timestamps and allowlisted failure categories only. Correlation middleware accepts strict bounded IDs or generates a server ID. Structured request completion logging records method/status/elapsed time without request bodies, query text, credentials or raw provider exceptions.

The production runtime wires telemetry/readiness plus collector/cache/cycle/incident decorators in `Program.cs`; observability is not a unit-test-only subsystem.

## Schema lifecycle and readiness

`scripts/sql/monitor_shared_state_v1.sql` owns shared-state schema deployment. Runtime code does not perform DDL. The script is idempotent for version 1 and refuses to replace a different schema version.

Settings/readiness reports provider kind, readiness status and schema version only. Endpoint, connection string and raw provider failures are never rendered. If the provider is Disabled, no shared-state SQL call occurs.

## MultiNode activation boundary

Shared repositories, distributed leases and shared encrypted key-ring capability now exist, but `Deployment:MultiNode` remains fail-closed until every remaining node-local security/cache/delivery prerequisite is explicitly proven or externalized. A READY shared-state database by itself is never treated as HA readiness.
