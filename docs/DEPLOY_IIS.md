# Deploy Monitor on IIS

This is the preferred Windows deployment path for a single Monitor node behind IIS.

## Governing production boundary

Repository tooling can prepare and validate the host, but it does not itself authorize production cutover or manufacture production acceptance.

The remaining P0 sequence stays strict:

`#162 durable RC.61 publication + independent verification -> #116 real trusted-IIS 15/15 acceptance -> #111 closure`

Do **not** run any production-mutating `-Apply` path while #162 is open. The one-command installer therefore requires `-AcknowledgeDurableReleasePrerequisite` together with `-Apply`. That switch is only an explicit operator guard; it does **not** verify, complete, or close #162. The operator must first confirm the actual #162 promotion, separate verifier, tag, exact-two release assets and durable product hash are complete.

Repository CI, Windows candidate validation, bootstrap dry-runs and package construction do not count as a #116 production PASS.

## Bootstrap/install layer

Two scripts wrap the existing production deployment without replacing its safety contracts:

- `scripts/Bootstrap-IisProductionSingleNode.ps1` — idempotent Windows Server/IIS bootstrap. **PLAN ONLY by default**; explicit `-Apply` is required for mutation.
- `scripts/Install-ProductionSingleNode.ps1` — one entrypoint that prepares PowerShell 7 first when required, then runs **bootstrap -> existing authoritative preflight -> existing deploy**.
- `scripts/Test-IisProductionPrerequisites.ps1` — remains the authoritative read-only IIS/HTTPS readiness gate after bootstrap.
- `scripts/Deploy-ProductionSingleNode.ps1` — remains the authoritative package/cutover implementation for SHA-256, immutable releases, stable external `App_Data`, HTTPS acceptance and automatic application-path rollback.

The bootstrap can prepare supported infrastructure only. It never creates SQL credentials, embeds passwords, publishes/releases RC.61, dispatches GitHub workflows, or records external acceptance evidence.

### Bootstrap dry-run with an existing machine certificate

Run from Windows Server PowerShell. Without `-Apply`, no Windows feature, runtime, IIS, certificate, filesystem or ACL mutation occurs:

```powershell
.\scripts\Bootstrap-IisProductionSingleNode.ps1 `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>'
```

The plan reports missing IIS roles, .NET 8 Hosting Bundle requirements, application-pool/site/binding work and filesystem/ACL work. Re-running a prepared host should validate existing state rather than recreate it.

Direct bootstrap `-Apply` requires PowerShell 7. Use the combined installer below on a fresh server so PowerShell 7 is prepared before any IIS mutation.

### PowerShell 7 prerequisite

The authoritative production deployment scripts use PowerShell 7 semantics, so `Install-ProductionSingleNode.ps1` handles PowerShell 7 **before** running the IIS bootstrap. The default Online path uses the fixed official x64 MSI `PowerShell-7.4.16-win-x64.msi`, pins SHA-256 `2C0C2036B0032375AD4F7809A92D0B6FA4A8E4EE89A75211514C4CF55AE22495`, and requires a valid Microsoft Corporation Authenticode signature.

PLAN ONLY may be run from Windows PowerShell 5.1. If PowerShell 7 is missing, the plan reports that requirement without touching IIS. On explicit Apply, only the PowerShell prerequisite is installed in that Windows PowerShell process; the script then stops and instructs the operator to rerun the same approved command from an elevated `pwsh` session. This avoids partially configuring IIS under the wrong PowerShell runtime.

Online mode is the default:

```powershell
.\scripts\Install-ProductionSingleNode.ps1 ... `
  -PowerShellMode Online
```

For a disconnected server, provide the exact approved MSI locally:

```powershell
.\scripts\Install-ProductionSingleNode.ps1 ... `
  -PowerShellMode Offline `
  -PowerShellMsiInstallerPath 'C:\Deploy\Prereqs\PowerShell-7.4.16-win-x64.msi'
```

A missing/wrong MSI, SHA-256 drift, invalid Microsoft signature, installer failure or restart request stops before IIS mutation. If a PowerShell installation reports exit code `3010`, reboot and rerun from `pwsh`.

### Online Hosting Bundle mode

If .NET 8 ASP.NET Core Runtime or ANCM v2 is missing, Online mode requires an explicit approved Microsoft HTTPS URL. Supported download hosts are constrained in the script; redirects do not turn this into a general-purpose downloader. An independently obtained SHA-256 can be supplied and the downloaded installer must also have a valid Microsoft Authenticode signature.

Dry-run:

```powershell
.\scripts\Bootstrap-IisProductionSingleNode.ps1 `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -HostingBundleMode Online `
  -HostingBundleDownloadUrl 'https://download.visualstudio.microsoft.com/<approved-dotnet-8-hosting-bundle-path>' `
  -HostingBundleSha256 '<optional-64-hex-approved-installer-sha256>'
```

After reviewing the plan, repeat the exact approved command with `-Apply` from PowerShell 7. The installer is run silently with `/install /quiet /norestart`. A platform restart request stops the sequence fail-closed; reboot through the approved operating procedure and rerun the same command before cutover.

If the Hosting Bundle must be installed on a server where IIS already existed, bootstrap does **not** silently restart shared IIS services. Either restart WAS/W3SVC through the normal approved maintenance process and rerun, or add `-AllowIisServiceRestart` only during an approved maintenance window. A fresh host whose IIS roles were installed by this same bootstrap may restart WAS/W3SVC as part of activating ANCM before the authoritative preflight.

### Offline Hosting Bundle mode

Offline mode performs no Internet download. Supply the installer locally through the approved software-transfer process. SHA-256 pinning is optional but recommended; Microsoft Authenticode remains mandatory.

```powershell
.\scripts\Bootstrap-IisProductionSingleNode.ps1 `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -HostingBundleMode Offline `
  -HostingBundleInstallerPath 'C:\Deploy\Prereqs\dotnet-hosting-8-approved.exe' `
  -HostingBundleSha256 '<optional-64-hex-approved-installer-sha256>'
```

This command is still PLAN ONLY. Append `-Apply` only after review and from PowerShell 7. If the runtime and ANCM are already present, the local installer is not executed.

### Existing certificate thumbprint

For a production certificate already present in `Cert:\LocalMachine\My`, use its approved thumbprint:

```powershell
.\scripts\Bootstrap-IisProductionSingleNode.ps1 `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>'
```

Apply verifies that the certificate exists, has an accessible private key and is not expired/near expiry. An existing HTTPS binding with a different certificate fails closed rather than being silently overwritten.

### Explicit PFX certificate

A PFX may be supplied explicitly. Keep the password out of command history and source by using a `SecureString`:

```powershell
$pfxPassword = Read-Host 'PFX password' -AsSecureString

.\scripts\Bootstrap-IisProductionSingleNode.ps1 `
  -HostName 'monitor.example.internal' `
  -CertificatePfxPath 'C:\Deploy\Certificates\monitor-production.pfx' `
  -CertificatePfxPassword $pfxPassword
```

The bootstrap reads the PFX leaf certificate to resolve the expected thumbprint. If that exact certificate is already present in `Cert:\LocalMachine\My`, it is not imported again. If both `-CertificateThumbprint` and `-CertificatePfxPath` are supplied, their thumbprints must match.

### What bootstrap creates or validates

On explicit Apply, and only when missing, the bootstrap can:

1. install required IIS roles and management scripting support;
2. install the approved .NET 8 ASP.NET Core Hosting Bundle when runtime/ANCM are missing;
3. activate the newly installed ANCM with an explicit IIS-service restart policy rather than silently restarting an existing shared IIS host;
4. create `Monitor` application pool with **No Managed Code** and `ApplicationPoolIdentity`;
5. validate an existing pool using `ApplicationPoolIdentity` or an approved `SpecificUser`, while rejecting LocalSystem, LocalService and NetworkService;
6. require the Monitor application pool to be a **dedicated application pool** and fail if another IIS site shares it;
7. create/validate the `Monitor` IIS site and exact SNI HTTPS binding with `sslFlags=1`; unexpected existing SSL binding semantics fail closed rather than being rewritten;
8. create `C:\Program Files\Monitor\releases`, `C:\ProgramData\Monitor\App_Data` and the bootstrap site root;
9. grant only the missing filesystem rights: Modify on stable `App_Data`, Read/Execute on release/bootstrap roots;
10. run `Test-IisProductionPrerequisites.ps1` and require it to pass before declaring bootstrap readiness.

Unexpected existing infrastructure is not forcibly rewritten. A shared pool, wrong pool identity, managed CLR, site/pool association, SNI drift or binding certificate drift fails closed so the discrepancy can be resolved through the approved infrastructure process.

## One-command bootstrap + deployment entrypoint

`Install-ProductionSingleNode.ps1` accepts the existing package/deployment inputs plus prerequisite/bootstrap inputs. It does not reimplement deployment. The fixed ordering is:

1. PowerShell 7 prerequisite check/install; if installation/relaunch is required, stop before IIS mutation and rerun the same command under `pwsh`;
2. `Bootstrap-IisProductionSingleNode.ps1`;
3. `Test-IisProductionPrerequisites.ps1` — authoritative preflight;
4. `Deploy-ProductionSingleNode.ps1`.

### Combined PLAN ONLY

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

If PowerShell 7 or bootstrap changes are required, PLAN ONLY reports them and stops before the authoritative preflight/deploy phase. Nothing is mutated. When the host is already ready and the command is running under PowerShell 7, the entrypoint continues through the authoritative preflight and the existing deployment plan, still without Apply.

### Combined Offline example

For a disconnected fresh host, provide both prerequisite installers locally. The PowerShell MSI is still SHA-256 pinned and Microsoft-signed; the Hosting Bundle is Microsoft-signed and may also be independently SHA-256 pinned:

```powershell
.\scripts\Install-ProductionSingleNode.ps1 `
  -ArtifactPath 'C:\Deploy\Monitor\Monitor-<version>-win-x64.zip' `
  -ChecksumPath 'C:\Deploy\Monitor\Monitor-<version>-win-x64.zip.sha256' `
  -ReleaseVersion '<version>' `
  -ProductionConfigPath 'C:\Deploy\Monitor\approved\appsettings.Production.json' `
  -OperationalBackupId '<validated-pre-cutover-backup-id>' `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -PowerShellMode Offline `
  -PowerShellMsiInstallerPath 'C:\Deploy\Prereqs\PowerShell-7.4.16-win-x64.msi' `
  -HostingBundleMode Offline `
  -HostingBundleInstallerPath 'C:\Deploy\Prereqs\dotnet-hosting-8-approved.exe' `
  -HostingBundleSha256 '<optional-64-hex-approved-installer-sha256>'
```

### Combined Online example

```powershell
.\scripts\Install-ProductionSingleNode.ps1 `
  -ArtifactPath 'C:\Deploy\Monitor\Monitor-<version>-win-x64.zip' `
  -ChecksumPath 'C:\Deploy\Monitor\Monitor-<version>-win-x64.zip.sha256' `
  -ReleaseVersion '<version>' `
  -ProductionConfigPath 'C:\Deploy\Monitor\approved\appsettings.Production.json' `
  -OperationalBackupId '<validated-pre-cutover-backup-id>' `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -PowerShellMode Online `
  -HostingBundleMode Online `
  -HostingBundleDownloadUrl 'https://download.visualstudio.microsoft.com/<approved-dotnet-8-hosting-bundle-path>' `
  -HostingBundleSha256 '<optional-64-hex-approved-installer-sha256>'
```

### Combined PFX example

```powershell
$pfxPassword = Read-Host 'PFX password' -AsSecureString

.\scripts\Install-ProductionSingleNode.ps1 `
  -ArtifactPath 'C:\Deploy\Monitor\Monitor-<version>-win-x64.zip' `
  -ChecksumPath 'C:\Deploy\Monitor\Monitor-<version>-win-x64.zip.sha256' `
  -ReleaseVersion '<version>' `
  -ProductionConfigPath 'C:\Deploy\Monitor\approved\appsettings.Production.json' `
  -OperationalBackupId '<validated-pre-cutover-backup-id>' `
  -HostName 'monitor.example.internal' `
  -CertificatePfxPath 'C:\Deploy\Certificates\monitor-production.pfx' `
  -CertificatePfxPassword $pfxPassword
```

### Combined Apply after #162 is actually complete

Only after the real #162 durable-release closure rule has been satisfied, review the same exact command and append both switches:

```powershell
.\scripts\Install-ProductionSingleNode.ps1 ... `
  -AcknowledgeDurableReleasePrerequisite `
  -Apply
```

If this first Apply only installs PowerShell 7, it deliberately stops before IIS mutation. Open an elevated PowerShell 7 (`pwsh`) console and rerun the same approved command. If Hosting Bundle installation on a pre-existing IIS server requires activation, use the approved maintenance process or add `-AllowIisServiceRestart` only inside the approved maintenance window.

`-AcknowledgeDurableReleasePrerequisite` does not query GitHub and is not evidence. It exists to prevent accidental production mutation while the governing #162 gate is still open.

## Prerequisites

For direct use of the existing preflight/deploy scripts, the host must already have:

- Windows Server with PowerShell 7;
- IIS and WebAdministration scripting tools;
- .NET 8 ASP.NET Core Hosting Bundle;
- a dedicated low-privilege `ApplicationPoolIdentity` or approved `SpecificUser` identity;
- HTTPS certificate in `Cert:\LocalMachine\My` with accessible private key;
- IIS site and exact trusted SNI HTTPS binding with `sslFlags=1`;
- published Monitor candidate from a CI-verified commit;
- validated pre-cutover Monitor operational backup ID.

The **direct** `Deploy-ProductionSingleNode.ps1` deliberately does not install Windows roles/runtimes/certificates or invent IIS objects. The combined installer/bootstrap wrapper may prepare those approved prerequisites; the existing deployment script remains narrow.

## Preferred operator-safe deployment flow

After #162 is complete, the #116 production path remains:

**PowerShell 7 readiness -> bootstrap/readiness -> authoritative preflight -> deployment plan -> explicit apply -> immediate HTTPS acceptance -> recycle/rollback evidence -> explicit 15-gate finalization**.

### 1. Read-only IIS preflight

Run from an elevated PowerShell 7 session on the target Windows Server:

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
  -ArtifactPath 'C:\Deploy\Monitor\Monitor-<version>-win-x64.zip' `
  -ChecksumPath 'C:\Deploy\Monitor\Monitor-<version>-win-x64.zip.sha256' `
  -ReleaseVersion '<version>' `
  -ProductionConfigPath 'C:\Deploy\Monitor\approved\appsettings.Production.json' `
  -OperationalBackupId '<validated-pre-cutover-backup-id>' `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -SiteName Monitor `
  -AppPoolName Monitor
```

Without `-Apply`, the script validates artifact/checksum, candidate package cleanliness, SingleNode configuration, IIS/HTTPS prerequisites, release/state paths and prints the exact cutover plan. **No IIS, filesystem, ACL, application-pool, binding, certificate, configuration or state changes are made.**

### 4. Apply the reviewed cutover

Only after #162 is complete and the plan/pre-cutover backup are approved, run the reviewed direct deploy command with `-Apply`, or use the combined installer Apply path above.

Apply semantics remain intentionally narrow:

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

1. Use a dedicated application pool, for example `Monitor`; do not share it with unrelated IIS sites.
2. Set **.NET CLR Version** to **No Managed Code**.
3. Use `ApplicationPoolIdentity` or an approved dedicated `SpecificUser`; do not run as LocalSystem/LocalService/NetworkService or an interactive administrator.
4. Grant Modify only on stable Monitor-owned `App_Data`; release binaries receive Read/Execute.
5. Use an IIS site assigned to the approved application pool.
6. Bind HTTPS to the exact production host name and approved machine certificate using SNI `sslFlags=1`. Do not expose an HTTP-only production binding.
7. Restrict network access to intended operator networks and SQL endpoints.

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

- `/login` renders over trusted HTTPS and authentication succeeds;
- Authentication cookie is Secure/HttpOnly/SameSite Strict;
- Administrator Settings reports expected topology/credential/backup readiness;
- one explicitly configured least-privilege SQL target passes **Test Connection** and explicit Refresh;
- recycle the IIS application pool and prove the same registration/protected credential/audit/history/incident state survives;
- re-run `scripts/Accept-ProductionSingleNode.ps1` / health and authenticated read checks after recycle;
- perform the approved rollback rehearsal and verify health/auth/read again.

Those steps become #116 evidence only when executed against the intended real production environment and retained in the governed immutable acceptance session.

## Rollback

The deploy script retains the previous IIS physical path and automatically restores it when immediate post-cutover acceptance fails. For an operator-requested or later rollback, follow `docs/ROLLBACK_RUNBOOK.md`.

Never delete Data Protection keys, encrypted Monitor-owned secrets, registrations or operational files as a rollback shortcut. The stable `App_Data` boundary exists specifically so application-version rollback does not destroy state.

## Upgrade

Use `docs/UPGRADE_CHECKLIST.md`. Keep previous versioned release directories intact until the new candidate completes external IIS acceptance. On failure, use the recorded previous physical path and `docs/ROLLBACK_RUNBOOK.md` rather than modifying production binaries or durable state in place.
