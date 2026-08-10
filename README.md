# Monitor

**Monitor** is an ASP.NET Core SQL Server operations center focused on fast DBA situational awareness, snapshot-first monitoring and safe human-reviewed operations.

## Current state

M0 through M6 are CI verified. The project now includes the premium command-center UI, real SQL Server onboarding, bounded collection/cache health modules, deterministic incidents/recommendations, guarded Advisor boundaries, audit/RBAC/security hardening, multi-server real estate projection and the first complete Register → Test → Collect → View journey.

M7 is the active production-readiness milestone. `M7-001` adds durable local server-registration metadata persistence while keeping SQL credential values outside the persisted registration store.

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

Runtime SQL Login username/password values are process-memory only and disappear on restart. For restart-safe production credentials, configure an external secret reference and keep the actual values outside registration metadata, logs, audit and source control.

## Registration persistence

The default M7 registration store is:

```json
"RegistrationStore": {
  "Mode": "File",
  "Path": "App_Data/registrations.json"
}
```

The path is resolved from the application content root and must remain outside `wwwroot`. The file contains safe registration metadata plus an opaque `ConnectionSecretReference` when required; it never contains SQL usernames, passwords or full connection strings. Writes use a same-directory temporary file followed by an atomic move, and corrupt persisted state fails closed on startup.

Set `RegistrationStore:Mode` to `InMemory` only for intentionally ephemeral deployments/tests.

A configured `Monitor:PrimaryServer` can still seed/upsert a registration at startup. Store external SQL login values separately under `ConnectionSecrets:{SecretReference}:Username` and `Password` or a future enterprise secret-provider implementation. Never add passwords or full connection strings to `appsettings.json`.

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

Recommendations and Advisor output are advisory-only and have no SQL execution path. Monitor-owned registration persistence is separate from monitored SQL Servers.

See `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`, `docs/IMPLEMENTATION_PLAN.md`, `docs/FEATURE_CATALOG.md`, `docs/STATUS.md`, and `docs/DECISIONS.md`.
