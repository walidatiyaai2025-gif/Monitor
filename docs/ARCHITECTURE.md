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

`ServerRegistration` stores validated endpoint and authentication metadata only. SQL login values are represented by an opaque `ConnectionSecretReference`, excluded from JSON, and resolved only inside the backend through `IConnectionSecretStore`. No plaintext password or full connection string is stored in the repository or registration model.

`IServerConnectionTester` owns the Test Connection workflow. The administrator endpoint accepts only a registration ID, resolves credentials inside the backend, and delegates provider access to `ISqlConnectionProbe`. Provider exception text and connection strings never cross the service boundary.

A single bounded collector command creates reusable `ServerHealthSnapshot` facts. The cache provides fresh/stale behavior and per-registration single-flight so UI surfaces do not fan out SQL calls. Manual refresh remains administrator-controlled and throttled. SignalR delivery was evaluated and deferred until independently produced snapshots exist.

## M2 health modules

Memory, database state, backup coverage, SQL Agent, storage, blocking and bounded performance facts extend the same immutable snapshot and shared cache read boundary. Dedicated module pages are presentation routes over that shared snapshot, not collection triggers.

## M3 incidents and recommendations

A deterministic rule evaluator emits allowlisted bounded findings. Stable registration/rule fingerprints deduplicate incident observations. Only newer fresh healthy evidence resolves an incident. Operator transitions are explicit authorized POST actions with antiforgery protection. Recommendations are rule-owned, human-reviewed presentation data and never reach SQL execution.

## M4 Advisor boundary

Advisor context contains normalized bounded evidence and deterministic recommendation material. Provider requests are explicit, authorized, bounded by single-flight/cache/timeout/circuit behavior and audited with redacted metadata. Provider output remains advisory and disconnected from SQL execution.

## M5 operational hardening

Bounded snapshot history, optional scheduled collection infrastructure, audit metadata, named RBAC policies, hardened cookies/security headers and login limiting are implemented. Scheduled collection remains disabled unless explicitly configured.

M5-026 enriches canonical `incident.transition` audit outcomes with bounded before/after state when repository evidence is available while keeping the incident workflow contract unchanged. Missing authenticated actor identity fails closed before mutation.

## M6 real-server journey

Login routes an empty estate to Connections. The administrator submits safe endpoint metadata plus Integrated Security, a process-memory SQL Login credential, or an external secret reference. The backend registers, tests, collects and observes the first snapshot in order. Only a successful test reaches collection. Estate and Dashboard reads show all registrations and preserve unavailable targets without mixing demo cards into a real estate.

## M7 registration persistence

M7-001 preserves `IServerRegistrationRepository` and adds a file-backed implementation for durable Monitor-owned registration metadata. The default file is `App_Data/registrations.json`; startup rejects a configured path inside `wwwroot`. The file contains endpoint/auth metadata and an opaque secret reference only, never SQL credential values or full connection strings.

Mutations are serialized, written to a same-directory temporary file with write-through and disk flush, then atomically moved into place. Failed persistence restores the prior in-memory state. Corrupt, unsupported or invalid stored metadata fails closed.

## M7 external secret-provider routing

M7-002 preserves `IConnectionSecretStore` and introduces `IExternalConnectionSecretProvider` behind it. Resolution order is runtime process-memory secret, external provider ownership, then legacy configuration only when no external provider owns the reference.

`EnvironmentConnectionSecretProvider` owns `env:<alias>`. It reads only `MONITOR_SQL_SECRET_<ALIAS>_USERNAME` and `_PASSWORD` directly from the process environment. If the reference is malformed or the secret is missing/partial, resolution fails closed and does not downgrade to `ConnectionSecrets` configuration.

## M7 durable operational state

M7-003 keeps `IAuditStore`, `ISnapshotHistoryStore` and `IHealthIncidentRepository` unchanged and selects in-memory or file-backed implementations from `OperationalStore:Mode`. File mode defaults to `App_Data/operational` outside `wwwroot`.

Audit, history and incidents use independent versioned files. Each mutation is prepared as candidate state, durably written and atomically moved into place before the live in-process reference is replaced. Audit preserves its 1,000-event bound, history preserves allowlisted 24-hour / 288-point-per-server aggregates, and incidents preserve stable identity, older-evidence ignore semantics, fresh reconciliation and compare-and-set transitions. Invalid persisted state fails closed.

## M7 HA topology guard

M7-004 introduces `Deployment:Mode` as an explicit production topology contract. `SingleNode` is the default and currently supported mode. `MultiNode` is a recognized intent but startup validation rejects it before Monitor activates persistence/services because the application still contains node-local state and coordination.

The remaining node-local boundaries include registration/operational file or in-memory stores, runtime SQL Login credentials, login-attempt limiting, snapshot cache/single-flight gates and scheduler ownership/backoff/runtime status. A read-only Administrator Settings projection exposes this readiness without offering a topology mutation path.

The guard is intentionally conservative: a local file, a network-share path or multiple copies of the current process do not constitute distributed coordination. Multi-node support may be enabled only after real shared registration/operational providers plus required scheduler/cache coordination are implemented behind stable boundaries. Validation messages contain no secret or monitored endpoint values.
