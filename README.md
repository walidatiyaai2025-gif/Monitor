# Monitor

**Monitor** is an ASP.NET Core SQL Server operations center focused on fast DBA situational awareness, snapshot-first monitoring and safe human-reviewed operations.

## Current state

M0 through M6 are CI verified. The project includes the premium command-center UI, real SQL Server onboarding, bounded collection/cache health modules, deterministic incidents/recommendations, guarded Advisor boundaries, audit/RBAC/security hardening, multi-server real estate projection and the complete Register → Test → Collect → View journey.

M7 is the active production-readiness milestone. `M7-001` adds durable server-registration metadata, `M7-002` adds fail-closed environment-injected SQL secret references, and `M7-003` makes Monitor-owned audit/history/incident state durable across process restarts while preserving the existing service contracts.

## Run

```bash
dotnet restore Monitor.sln
dotnet build Monitor.sln
cd src/Monitor.Web
dotnet run
```

Open the URL printed by ASP.NET Core, then sign in with the development Admin credential agreed outside the repository. The repository stores **only a PBKDF2 salt/hash**, never the plaintext password.

## Register a real SQL Server

Administrators can use **Connections** to register a SQL Server, run a bounded Test Connection and collect the first cached snapshot. Integrated Security, a runtime SQL Login credential, or an external opaque secret reference can be used.

Runtime SQL Login username/password values are process-memory only and disappear on restart. For restart-safe production credentials, prefer an external `env:<alias>` secret reference and keep the actual values outside registration metadata, logs, audit and source control.

### Environment-injected SQL Login secret

Example registration reference:

```text
env:FINANCE_PROD
```

Monitor resolves that reference directly from the process environment:

```text
MONITOR_SQL_SECRET_FINANCE_PROD_USERNAME
MONITOR_SQL_SECRET_FINANCE_PROD_PASSWORD
```

Aliases accept only ASCII letters, digits and underscore and are normalized to uppercase. The values are read directly from the process environment rather than through `IConfiguration`. Therefore an `env:` reference cannot silently resolve from `appsettings.json`; if either environment variable is missing, the secret stays unavailable and Monitor fails the connection path closed.

Existing non-`env:` references remain backward compatible with `ConnectionSecrets:<reference>:Username` and `ConnectionSecrets:<reference>:Password`. Never commit those values to source control.

## Registration persistence

The default registration store is:

```json
"RegistrationStore": {
  "Mode": "File",
  "Path": "App_Data/registrations.json"
}
```

The path is resolved from the application content root and must remain outside `wwwroot`. The file contains safe registration metadata plus an opaque `ConnectionSecretReference` when required; it never contains SQL usernames, passwords or full connection strings. Writes use a same-directory temporary file followed by an atomic move, and corrupt persisted state fails closed on startup.

## Operational-state persistence

M7-003 defaults Monitor-owned operational state to:

```json
"OperationalStore": {
  "Mode": "File",
  "RootPath": "App_Data/operational"
}
```

The root must remain outside `wwwroot`. Monitor keeps three independent versioned files: `audit.json`, `history.json`, and `incidents.json`. Each mutation is written to a same-directory temporary file and flushed to disk before the live in-process candidate state is published. Corrupt or unsupported state fails closed on startup.

The durable contracts remain bounded: audit keeps at most 1,000 metadata-only events; history keeps allowlisted aggregate points for 24 hours with at most 288 points per server; incidents keep the deterministic registration/rule lifecycle state and bounded rule evidence. These files never contain SQL credentials, SQL text, monitored-server endpoints, provider errors or job commands.

Set either store mode to `InMemory` only for intentionally ephemeral deployments/tests. M7-004 owns shared/HA state; the M7-001/M7-003 file stores are single-node durability steps.

## Main operator surfaces

- Login / first-run routing
- SQL Command Center / Dashboard
- Connections
- Servers / Server Details
- Database & Backup Health
- Memory Health
- SQL Agent Jobs
- Storage
- Blocking
- Alerts / Incidents
- Recommendation / Advisor review
- Audit
- Settings

## Architecture rules

Frontend motion never creates SQL traffic. Browser widgets never connect directly to monitored SQL Servers. The main data path is:

`SQL Server -> Central Collector -> Snapshot/Cache -> Backend -> UI`

Recommendations and Advisor output are advisory-only and have no SQL execution path. Monitor-owned persistence is separate from monitored SQL Servers. Secret-provider routing remains behind `IConnectionSecretStore`; SQL probing/collection code never knows which provider resolved a credential.

See `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`, `docs/IMPLEMENTATION_PLAN.md`, `docs/FEATURE_CATALOG.md`, `docs/STATUS.md`, and `docs/DECISIONS.md`.
