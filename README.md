# Monitor

**Monitor** is an ASP.NET Core SQL Server operations center focused on fast DBA situational awareness.

## Current milestone

`M0 — Visual Foundation` (`0.0.1-ui-preview`) is the active milestone. The first delivery intentionally prioritizes a visible, navigable product before deep SQL collection.

## Run

```bash
dotnet restore Monitor.sln
dotnet build Monitor.sln
cd src/Monitor.Web
dotnet run
```

Open the URL printed by ASP.NET Core, then sign in with the development Admin credential agreed outside the repository. The repository stores **only a PBKDF2 salt/hash**, never the plaintext password.

To activate the first real M1 snapshot card, configure `Monitor:PrimaryServer` metadata through environment variables or User Secrets (`Id`, `DisplayName`, `Host`, optional `Port`/`InstanceName`, `AuthenticationMode`, and opaque `SecretReference`). Store SQL login values separately under `ConnectionSecrets:{SecretReference}:Username` and `Password`. Never add passwords or full connection strings to `appsettings.json`.

## Initial screens

- Login
- SQL Command Center
- Servers
- Server Details
- Database Health
- Memory Health
- Alerts / Incidents
- Settings

All monitoring values in `0.0.1-ui-preview` are clearly marked **DEVELOPMENT DATA** until the first real SQL Server vertical slice is connected.

## Architecture rule

Frontend motion never creates SQL traffic. The intended data path is:

`SQL Server -> Central Collector -> Snapshot/Cache -> Backend -> UI`

See `docs/ARCHITECTURE.md`, `docs/IMPLEMENTATION_PLAN.md`, `docs/FEATURE_CATALOG.md`, and `docs/STATUS.md`.
