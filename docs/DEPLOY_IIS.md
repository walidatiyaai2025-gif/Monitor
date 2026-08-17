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

The existing read-only preflight and deployment script remain authoritative. For a new server, repository tooling now includes an idempotent bootstrap layer that can prepare these prerequisites only after explicit operator review and `-Apply`.

## Optional idempotent IIS/bootstrap installer

`scripts/Bootstrap-IisProductionSingleNode.ps1` is **PLAN ONLY by default**. It checks the required Windows Server IIS features, WebAdministration support, .NET 8 ASP.NET Core Runtime/ANCM, app pool, site, HTTPS binding, machine certificate and filesystem/ACL baseline. It creates or installs missing approved prerequisites only when the operator repeats the reviewed command with `-Apply`.

This repository bootstrap is not permission to start production cutover. The governing dependency remains `#162 durable RC publication + independent verification -> #116 real trusted-IIS acceptance`. Do not run an Apply operation against the real production target while #162 is open.

### Dry-run using an existing machine certificate

```powershell
.\scripts\Bootstrap-IisProductionSingleNode.ps1 `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>'
```

No IIS, Windows feature, runtime, certificate, filesystem or ACL change is made without `-Apply`.

### Online Hosting Bundle mode

When the .NET 8 ASP.NET Core Runtime or ANCM is missing, Online mode requires an explicit HTTPS Microsoft download URL. The downloaded executable must carry a valid Microsoft Authenticode signature; an independently approved SHA-256 can also be pinned with `-HostingBundleSha256`.

```powershell
.\scripts\Bootstrap-IisProductionSingleNode.ps1 `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -HostingBundleMode Online `
  -HostingBundleDownloadUrl 'https://download.visualstudio.microsoft.com/<approved-hosting-bundle-path>' `
  -HostingBundleSha256 '<64-hex-approved-installer-sha256>'
```

Review the PLAN ONLY output first, then repeat the same approved command with `-Apply` if the #162/#116 operating boundary allows the real server mutation.

### Offline Hosting Bundle mode

Offline mode performs no Internet download. Supply the approved local Hosting Bundle installer; SHA-256 pinning is supported and recommended.

```powershell
.\scripts\Bootstrap-IisProductionSingleNode.ps1 `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -HostingBundleMode Offline `
  -HostingBundleInstallerPath 'C:\Deploy\Prereqs\dotnet-hosting-8-approved.exe' `
  -HostingBundleSha256 '<64-hex-approved-installer-sha256>'
```

### Explicit PFX certificate

The bootstrap can import an explicitly supplied PFX into `Cert:\LocalMachine\My`. The password parameter is a `SecureString`; never store or pass a plaintext password in Git, scripts, documentation, command history or logs.

```powershell
$pfxPassword = Read-Host 'PFX password' -AsSecureString
.\scripts\Bootstrap-IisProductionSingleNode.ps1 `
  -HostName 'monitor.example.internal' `
  -CertificatePfxPath 'C:\Deploy\Certificates\monitor-production.pfx' `
  -CertificatePfxPassword $pfxPassword
```

An independently supplied `-CertificateThumbprint` can be provided together with the PFX; when both are supplied the PFX leaf certificate must match that approved thumbprint or bootstrap fails closed.

### Single bootstrap + deploy entrypoint

`scripts/Install-ProductionSingleNode.ps1` provides one operator entrypoint while preserving the existing implementation boundaries. Its order is fixed:

1. `Bootstrap-IisProductionSingleNode.ps1`
2. `Test-IisProductionPrerequisites.ps1`
3. `Deploy-ProductionSingleNode.ps1`

PlanOnly example:

```powershell
.\scripts\Install-ProductionSingleNode.ps1 `
  -ArtifactPath 'C:\Deploy\Monitor\Monitor-<version>-win-x64.zip' `
  -ChecksumPath 'C:\Deploy\Monitor\Monitor-<version>-win-x64.zip.sha256' `
  -ReleaseVersion '<version>' `
  -ProductionConfigPath 'C:\Deploy\Monitor\approved\appsettings.Production.json' `
  -OperationalBackupId '<validated-pre-cutover-backup-id>' `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>'
```

If bootstrap changes are required, PlanOnly stops before authoritative preflight/deployment and reports the missing prerequisites. After #162 is actually complete and the production operation is approved, an Apply invocation additionally requires `-AcknowledgeDurableReleasePrerequisite`:

```powershell
# Same reviewed inputs, only after #162 is actually closed with durable verification evidence.
.\scripts\Install-ProductionSingleNode.ps1 ... `
  -AcknowledgeDurableReleasePrerequisite `
  -Apply
```

`-AcknowledgeDurableReleasePrerequisite` does not itself verify or satisfy #162. It is only an explicit operator acknowledgement and cannot manufacture #116 acceptance evidence. The existing deployment script continues to own package SHA-256 validation, immutable release staging, stable external `App_Data`, HTTPS acceptance and automatic physical-path rollback.

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

For the standard control-plane smoke contract, run or re-run:

```powershell
.\scripts\Smoke-Monitor.ps1 -BaseUri https://monitor.example.internal
```

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
