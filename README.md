# Monitor

**Monitor** is an ASP.NET Core SQL Server operations center focused on fast DBA situational awareness, snapshot-first monitoring and safe human-reviewed operations.

## Current state

M0 through M6 are CI verified. The project now includes the premium command-center UI, real SQL Server onboarding, bounded collection/cache health modules, deterministic incidents/recommendations, guarded Advisor boundaries, audit/RBAC/security hardening, multi-server real estate projection and the first complete Register → Test → Collect → View journey.

M7 is the active production-readiness milestone. `M7-001` adds durable local server-registration metadata persistence, and `M7-002` adds an environment-injected external SQL secret provider while keeping credential values outside registration persistence, source control, UI and audit.

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

This works well with IIS/Windows Service environment configuration, container/Kubernetes secret injection, deployment tooling, or an external secret manager that projects secrets into process environment variables. No vendor-specific secret SDK is required by this slice.

Existing non-`env:` references remain backward compatible with:

```text
ConnectionSecrets:<reference>:Username
ConnectionSecrets:<reference>:Password
```

Use those legacy configuration references only where appropriate for the deployment. Never commit their values to source control.

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

A configured `Monitor:PrimaryServer` can still seed/upsert a registration at startup. The secret reference can use `env:<alias>` or an existing non-`env:` reference. Runtime `runtime-*` references may remain in persisted registration metadata after restart, but their process-memory credential value intentionally does not survive; that registration will remain visible and unavailable until credentials are re-entered or the reference is changed to a durable external secret source.

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

Recommendations and Advisor output are advisory-only and have no SQL execution path. Monitor-owned registration persistence is separate from monitored SQL Servers. Secret-provider routing remains behind the backend `IConnectionSecretStore` boundary; SQL probing/collection code never knows which provider resolved a credential.

See `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`, `docs/IMPLEMENTATION_PLAN.md`, `docs/FEATURE_CATALOG.md`, `docs/STATUS.md`, and `docs/DECISIONS.md`.
