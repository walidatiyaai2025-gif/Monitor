# Monitor

**Monitor** is an ASP.NET Core SQL Server operations center focused on fast DBA situational awareness, snapshot-first monitoring, bounded production persistence and safe human-reviewed operations.

## Current state

M0 through M6 are CI verified. M7 production-readiness includes durable registration metadata, fail-closed external secret routing, durable bounded operational state, protected local SQL Login credentials, a topology guard and a dedicated Monitor shared-state SQL capability. M8 enforces zero-SQL monitoring GETs for monitored targets.

BATCH-100 is the active enterprise hardening program. Batches 1–7 deliver HA shared state/coordination, encrypted key management, operational backup/restore, production observability, deterministic performance/scale budgets, centralized DBA control-plane UX and web/application security hardening. `docs/BATCH_100.md` is the 100-task execution ledger. **B100-001..070 are CI verified.**

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

The optional **separate Monitor-owned state database** carries control-plane state and coordination only. Administrator Settings and readiness may probe that dedicated state provider when enabled; this is not a query against a monitored SQL target.

## DBA operations surface

Dashboard uses one centralized `IDbaOperationsSurfaceService` to project deployment readiness, an opaque node label, shared-state status/schema, operational-backup readiness and scheduler state. The node label is a SHA-256-derived `NODE-XXXXXXXX` token; Monitor does not render the machine name, configured distributed node ID or lease owner.

A registered server with no usable cached snapshot still opens Server Details. The page presents a recovery path to Connection Lab and the bounded refresh workflow without displaying current secret references, SQL usernames or passwords. Refresh feedback is PRG-safe and distinguishes refreshed/stale/throttled outcomes.

The Incident Center provides bounded status/severity/rule/page-size filtering and Previous/Next navigation. The shell includes a skip link, strong focus-visible treatment, live-status semantics and reduced-motion support. Large-display wallboard behavior is CSS-only and does not add polling, network fetches or SQL requests.

## Production health and observability

- `/health/live` — process liveness only; no external dependency checks.
- `/health/ready` — deployment/control-plane readiness only; never queries a monitored SQL target.
- `/health` — bounded aggregate application status plus safe runtime counters.
- `/observability` — Administrator aggregate collector/cache/scheduler/incident/auth telemetry.

Telemetry never stores SQL text, request bodies, usernames, passwords, IP addresses, secret references, connection strings, provider endpoints or raw provider exceptions. Collector failure telemetry is a strict allowlist of known `SnapshotCollectionFailure` values plus `Unexpected`; arbitrary values are reduced to `Unknown`.

`X-Correlation-ID` accepts only a bounded alphanumeric/`.`/`_`/`-` token. Unsafe or missing values are replaced with a server-generated ID. Structured completion logs record correlation scope, HTTP method, response status and elapsed time only.

## Web/application security policy

Browser trust and authentication lifetime are explicit configuration rather than hidden framework defaults:

```json
"WebSecurity": {
  "SessionIdleMinutes": 30,
  "SessionAbsoluteHours": 8,
  "HstsDays": 365,
  "HstsIncludeSubDomains": true,
  "HstsPreload": false,
  "TrustedProxies": [],
  "TrustedNetworks": []
}
```

Cookie authentication can renew inside the idle window, but an immutable session-start claim enforces the absolute lifetime. Missing or invalid session-start metadata fails closed.

Security headers are centralized. CSP denies frame/object embedding, constrains form/resources and uses a cryptographically random per-request nonce; `unsafe-inline` and `unsafe-eval` are not enabled. A regression test reflects across controllers and requires antiforgery on every `POST`, `PUT`, `PATCH` and `DELETE` action.

Forwarded headers remain disabled while both trusted-forwarder lists are empty. Reverse-proxy deployments must explicitly list trusted proxy IPs/CIDRs; enabled forwarding is limited to `X-Forwarded-For` / `X-Forwarded-Proto`, one hop and symmetric headers. Monitor does not opt into trust-all forwarding.

Login limiting uses opaque SHA-256-derived keys rather than retaining raw IP/username values. Audit fields are bounded/control-character normalized and credential/connection-string-shaped fields are redacted. SQL registration host/instance metadata rejects control characters and connection-string delimiter injection; connection strings continue to be composed only through `SqlConnectionStringBuilder`.

## Performance & scale governance

The default operating budgets are explicit and validated:

```json
"PerformanceScale": {
  "SnapshotCacheMaxEntries": 512,
  "HistoryMaxReadPoints": 100,
  "AuditMaxPageSize": 100,
  "IncidentMaxPageSize": 100,
  "ServerDefaultPageSize": 50,
  "ServerMaxPageSize": 100,
  "ManualRefreshMaxConcurrency": 4,
  "SqlMaxPoolSize": 4,
  "SqlPoolLifetimeSeconds": 300
}
```

The snapshot cache evicts the oldest retained snapshot once capacity is exceeded. Server estate navigation is paged and Peeks cached state only for the requested page. History/audit/incident projections are bounded. Manual refresh uses an application-wide concurrency permit in addition to per-registration throttling and distributed single-flight. Scheduler cycles use bounded deterministic jitter plus round-robin maximum-target batches.

Monitored background snapshot collection uses an explicitly capped SQL connection pool. **Test Connection remains non-pooled** so credential testing/rotation cannot appear valid because a previously authenticated pooled connection was reused.

CI uses deterministic budget tests for capacity, output/page limits, cache read counts, refresh concurrency, round-robin batching and pool configuration rather than machine-dependent microbenchmark timings.

## Deployment topology

```json
"Deployment": {
  "Mode": "SingleNode"
}
```

`SingleNode` remains the safe default. Shared repositories and distributed leases exist, but `MultiNode` remains fail-closed until every cross-field prerequisite is HA-safe. A READY shared-state database alone does not enable MultiNode.

## Shared-state provider

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

Deploy `scripts/sql/monitor_shared_state_v1.sql` to a **dedicated Monitor-owned SQL Server database**, set the named process environment variable to that database connection string, then enable `SharedState:Provider=SqlServer`. Runtime code does not perform DDL.

## SQL Login credentials and HA key management

Single-node defaults may use server-generated `local:v1` references protected by ASP.NET Data Protection. Shared key-ring mode encrypts Data Protection XML with AES-256-GCM before persistence; the 256-bit KEK is read only from process environment and is never stored by Monitor.

Credential-reference replacement follows Resolve → bounded Test Connection → metadata commit → owned-secret cleanup. Failed replacement preserves the existing registration/credential. Current and candidate references are never rendered or audited.

## Operational backup / restore

Operational backup contains only safe registration metadata plus opaque references, incidents, bounded history and bounded audit metadata. Each section is covered by SHA-256 manifest checksums. Protected credential ciphertext, Data Protection keys/KEKs, provider connection material, SQL usernames/passwords and monitored SQL text are excluded.

Administrator Settings exposes Create, Dry-run Validate and Restore commands. Restore targets the configured File/Shared backend, validates before mutation and rolls earlier sections back if a later section fails. File-backed restore requires application restart before operations resume.

## Architecture rules

- Browser/UI components never connect directly to monitored SQL Servers.
- Monitoring GETs and health/observability GETs are cache/control-plane only and never initiate monitored SQL collection.
- DBA Dashboard cards use one centralized control-plane projection rather than independent widget probes.
- Estate paging reads only the requested cache page; paging never widens monitored-SQL collection.
- Manual monitored-SQL refresh/collection is explicit, authorized, concurrency-bounded and backend-controlled.
- Snapshot cache remains the shared monitored-evidence/read boundary and has an explicit capacity limit.
- Recommendations and Advisor output remain advisory-only and cannot execute production SQL.
- Secret-provider routing stays behind `IConnectionSecretStore`.
- Shared-state SQL is a separate Monitor-owned control-plane database, never an implicitly reused monitored target.
- Operational backup excludes secret-bearing persistence and uses validated/staged restore semantics.
- Runtime telemetry/logging is bounded and redacted; free-form provider detail is not retained.
- Browser trust, authenticated lifetime and forwarded proxy acceptance are explicit fail-closed policies.
- MultiNode stays fail-closed until all remaining distributed login-security and snapshot-cache/delivery prerequisites are verified.
