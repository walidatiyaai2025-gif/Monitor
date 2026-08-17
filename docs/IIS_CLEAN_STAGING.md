# Clean IIS staging start

This guide is for a disposable/test Windows Server IIS host. It does not satisfy or bypass the production dependency `#162 -> #116 -> #111` and must not be used to mutate the selected RC.61 production state.

## Goal

Run Monitor under a dedicated IIS application pool with a truthful empty estate. `DemoData:Enabled` is `false` in the base/production configuration; local Development explicitly opts into the sample estate.

## 1. Stop console execution

If `Monitor.Web.exe` is running interactively, stop it before IIS owns the process.

## 2. Use a dedicated site and app pool

Example staging values:

- site: `MonitorHealth`
- app pool: `MonitorHealth`
- physical path: `C:\inetpub\wwwroot\Health`
- HTTP port: `8080` for local/internal staging only

The app pool must use **No Managed Code** and **ApplicationPoolIdentity** and must not be shared with another IIS site.

## 3. Set runtime configuration

For IIS staging use `ASPNETCORE_ENVIRONMENT=Production`. Non-Development startup requires independently provisioned administrator PBKDF2 values through these process environment variables:

- `DevelopmentAdmin__Username`
- `DevelopmentAdmin__Iterations`
- `DevelopmentAdmin__SaltBase64`
- `DevelopmentAdmin__HashBase64`

Also set `DemoData__Enabled=false`. Never put the plaintext administrator password in configuration.

## 4. Clean local state

Stop the site and app pool first. For a disposable staging instance, rename or remove `App_Data` and recreate it with Modify rights only for `IIS AppPool\MonitorHealth`.

Deleting `App_Data` intentionally removes local registrations, protected SQL connection secrets, local Data Protection keys, audit/history/incidents, and local operational backups. Existing authentication cookies become invalid because the local key ring is reset. Back up the directory before destructive cleanup when any state matters.

Do not use this reset as a production migration procedure.

## 5. Start and verify

Start the app pool and site, then verify `/login`. After login, a clean instance contains no SQL targets and redirects the operator to Connection Lab until a real SQL Server is added/tested/saved.

HTTP is acceptable only for isolated staging. A real deployment must use the repository's trusted HTTPS/SNI/certificate bootstrap and preflight flow.
