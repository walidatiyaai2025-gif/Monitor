# Deploy Monitor on IIS

This is the preferred Windows deployment path for a single Monitor node behind IIS.

## Prerequisites

- Windows Server with IIS.
- .NET 8 ASP.NET Core Hosting Bundle installed on the server.
- A dedicated low-privilege domain/local service identity or isolated `ApplicationPoolIdentity` for the Monitor application pool.
- HTTPS certificate installed in `Cert:\LocalMachine\My` with an accessible private key.
- An IIS site and HTTPS binding already approved for the production host name.
- Published Monitor output produced from a CI-verified commit.
- A validated pre-cutover Monitor operational backup ID.

The deployment automation deliberately does **not** create service identities, install certificates, or invent bindings. Those are security/infrastructure prerequisites that must be approved before application cutover.

## Preferred operator-safe deployment flow

The P0.5 production path is **preflight -> plan -> apply -> acceptance -> recycle/rollback evidence**.

### 1. Read-only IIS preflight

Run from an elevated PowerShell session on the target Windows Server:

```powershell
.\scripts\Test-IisProductionPrerequisites.ps1 `
  -HostName monitor.example.internal `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -SiteName Monitor `
  -AppPoolName Monitor
```

The preflight is read-only. It fails closed unless:

- IIS WebAdministration is available;
- .NET 8 ASP.NET Core runtime and ANCM v2 are installed;
- the application pool exists, uses **No Managed Code**, and is not LocalSystem/LocalService/NetworkService;
- the site exists and points at the approved application pool;
- the machine certificate exists, has a private key, and is not near expiry;
- the HTTPS binding exists for the exact host/port and uses the approved certificate.

It does not create/change the site, binding, certificate, application pool, ACL or files.

### 2. Prepare secret-free production configuration

Start from `deploy/appsettings.Production.example.json` and create an approved `appsettings.Production.json` outside the repository checkout. Keep:

- `Deployment:Mode=SingleNode`;
- shared state and distributed coordination disabled for the first production release;
- exact production `AllowedHosts` (no wildcard);
- file-backed paths relative to the stable `App_Data` boundary.

Do **not** put `DevelopmentAdmin`, password, PBKDF2 salt/hash, connection string or other credential material in the JSON. Production administrator verifier values and any enabled secret/state connection material are supplied through approved environment/secret configuration.

### 3. Review deployment plan — no changes

`Deploy-ProductionSingleNode.ps1` is **PLAN ONLY by default**:

```powershell
.\scripts\Deploy-ProductionSingleNode.ps1 `
  -ArtifactPath 'C:\Deploy\Monitor\Monitor-0.1.0-rc.20-win-x64.zip' `
  -ChecksumPath 'C:\Deploy\Monitor\Monitor-0.1.0-rc.20-win-x64.zip.sha256' `
  -ReleaseVersion '0.1.0-rc.20' `
  -ProductionConfigPath 'C:\Deploy\Monitor\approved\appsettings.Production.json' `
  -OperationalBackupId '<validated-pre-cutover-backup-id>' `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -SiteName Monitor `
  -AppPoolName Monitor
```

Without `-Apply`, the script validates artifact/checksum, candidate package cleanliness, SingleNode configuration, IIS/HTTPS prerequisites, release/state paths and prints the exact cutover plan. **No IIS, filesystem, ACL, application-pool, binding, certificate, configuration or state changes are made.**

### 4. Apply the reviewed cutover

Only after the plan and pre-cutover backup are approved, run the same command with `-Apply`.

```powershell
# same arguments as the reviewed plan
.\scripts\Deploy-ProductionSingleNode.ps1 ... -Apply
```

Apply semantics are intentionally narrow:

1. Extract into a new immutable versioned directory under `C:\Program Files\Monitor\releases\<version>`; an existing version directory is never overwritten.
2. Keep durable state outside the release tree at `C:\ProgramData\Monitor\App_Data`.
3. Create `<release>\App_Data` as a junction to that stable state directory so registrations, Monitor-owned encrypted secrets, Data Protection keys, backups, audit/history/incidents and other operational files survive upgrades.
4. Copy only the separately approved secret-free `appsettings.Production.json` into the new release.
5. Grant the existing application-pool identity Modify on stable `App_Data` and Read/Execute on the versioned release.
6. Stop only the Monitor app pool, switch the IIS site `physicalPath`, and start it.
7. Run `scripts/Accept-ProductionSingleNode.ps1` over the real HTTPS URL with artifact SHA-256 validation.
8. If acceptance fails after the path switch, automatically restore the previous IIS `physicalPath` and restart the previous candidate.
9. On success, record a non-secret `deployment-current.json` pointer in stable state including current/previous physical paths, artifact hash, backup ID and acceptance evidence path.

The deploy script never deletes/replaces `StateRoot` and never accepts an administrator or SQL password argument.

## Manual publish fallback

For development or controlled diagnostics only, a local publish can still be produced with:

```powershell
dotnet restore .\Monitor.sln
dotnet test .\Monitor.sln -c Release --no-restore
dotnet publish .\src\Monitor.Web\Monitor.Web.csproj -c Release -o C:\Deploy\Monitor\candidate
```

Production cutover should use the versioned CI candidate ZIP and matching SHA-256 rather than an untracked local publish.

## IIS site and application pool baseline

1. Create a dedicated application pool, for example `Monitor`.
2. Set **.NET CLR Version** to **No Managed Code**.
3. Use `ApplicationPoolIdentity` or an approved dedicated SpecificUser identity; do not run as LocalSystem or an interactive administrator.
4. The deploy automation grants Modify only on the stable Monitor-owned `App_Data`; release binaries receive Read/Execute.
5. Create an IIS site and assign it to the approved application pool.
6. Bind HTTPS to the exact production host name and approved machine certificate. Do not expose an HTTP-only production binding.
7. Restrict network access to the intended operator networks and SQL endpoints.

The ASP.NET Core Module generated by `dotnet publish` starts the application. Keep the generated `web.config`; do not replace it with a hand-authored process command.

## Required configuration

Set production values through approved environment variables and the secret-free `appsettings.Production.json`. Sensitive values must use environment/secret configuration.

Common environment variables:

```text
ASPNETCORE_ENVIRONMENT=Production
DevelopmentAdmin__Username=<operator-admin-name>
DevelopmentAdmin__Iterations=<approved-pbkdf2-iteration-count>
DevelopmentAdmin__SaltBase64=<generated-salt>
DevelopmentAdmin__HashBase64=<generated-hash>
MONITOR_SHARED_STATE_SQL_CONNECTION=<dedicated-monitor-state-db-connection-string>
MONITOR_DP_KEK=<base64-encoded-256-bit-key-when-shared-key-ring-is-enabled>
MONITOR_NODE_ID=<stable-opaque-node-id-when-coordination-is-enabled>
```

For the initial P0 SingleNode release, shared state and coordination remain disabled. Never place environment-variable values in Git, `web.config`, deployment logs or command history.

## Reverse-proxy trust

IIS/ANCM normally supplies the original scheme to ASP.NET Core without requiring Monitor to trust arbitrary `X-Forwarded-*` headers. If another proxy/load balancer is placed in front of IIS or Monitor, configure only its explicit IP/CIDR under `WebSecurity:TrustedProxies` / `TrustedNetworks`. Empty arrays intentionally disable forwarded-header processing.

## First-start verification

The deploy automation calls `Accept-ProductionSingleNode.ps1`, which validates the candidate SHA-256 and checks:

- `/health/live` => `Live`;
- `/health/ready` => `Ready`;
- `/health` => `Ready`;
- the base URI is absolute HTTPS.

It writes machine-readable evidence while deliberately leaving recycle, durable-registration, protected-credential, deployed least-privilege, backup and rollback operator checks false until the actual environment work is performed.

Then verify the remaining production gates:

- `/login` renders over trusted HTTPS and authentication succeeds.
- Authentication cookie is Secure/HttpOnly/SameSite Strict.
- Administrator Settings reports expected topology/credential/backup readiness.
- One explicitly configured least-privilege SQL target passes **Test Connection** and explicit Refresh.
- Recycle the IIS application pool and prove the same registration/protected credential/audit/history/incident state survives.
- Re-run `scripts/Accept-ProductionSingleNode.ps1` / health and authenticated read checks after recycle.
- Perform the approved rollback rehearsal and verify health/auth/read again.

## Rollback

The deploy script retains the previous IIS physical path and automatically restores it when immediate post-cutover acceptance fails. For an operator-requested or later rollback, follow `docs/ROLLBACK_RUNBOOK.md`.

Never delete Data Protection keys, encrypted Monitor-owned secrets, registrations or operational files as a rollback shortcut. The stable `App_Data` boundary exists specifically so application-version rollback does not destroy state.

## Upgrade

Use `docs/UPGRADE_CHECKLIST.md`. Keep previous versioned release directories intact until the new candidate completes external IIS acceptance. On failure, use the recorded previous physical path and `docs/ROLLBACK_RUNBOOK.md` rather than modifying production binaries or durable state in place.
