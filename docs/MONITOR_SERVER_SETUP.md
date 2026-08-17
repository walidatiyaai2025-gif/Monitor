# Monitor Windows Server setup package

This runbook covers the repository-packaged bootstrap layer for the existing Monitor SingleNode IIS deployment toolchain.

> Safety boundary: this tooling is repository preparation only. It does not publish RC.61, dispatch issue #162, manufacture production acceptance evidence, or bypass the required `#162 -> #116` external production gate.

## What the package contains

A verified production candidate includes the existing application files plus the following operator files under `_operations`:

```text
_operations/
  deploy/
    appsettings.Production.example.json
  scripts/
    Setup-MonitorServer.ps1
    Install-Monitor.ps1
    Test-IisProductionPrerequisites.ps1
    Deploy-ProductionSingleNode.ps1
    Accept-ProductionSingleNode.ps1
  docs/
    MONITOR_SERVER_SETUP.md
    DEPLOY_IIS.md
    ROLLBACK_RUNBOOK.md
```

`Setup-MonitorServer.ps1` prepares the Windows Server. `Install-Monitor.ps1` is the one-command entry point that verifies the candidate, bootstraps prerequisites, runs the existing authoritative IIS preflight, then delegates the actual versioned deployment and rollback behavior to `Deploy-ProductionSingleNode.ps1`.

## Requirements supplied by the operator

The installer never invents production security material. Before an apply run, prepare:

- the verified Monitor candidate ZIP and matching `.sha256` file;
- an approved secret-free `appsettings.Production.json` derived from `deploy/appsettings.Production.example.json`;
- the validated pre-cutover operational backup ID;
- the exact production DNS host name;
- the approved HTTPS certificate thumbprint;
- either that certificate already installed in `Cert:\LocalMachine\My`, or an approved PFX containing that exact thumbprint;
- production secret values through approved environment/secret configuration, never through package JSON or command-line password parameters.

## Plan first — no mutation

Run from an elevated Windows PowerShell or PowerShell 7 console. Do not add `-Apply` yet:

```powershell
$ops = 'C:\Deploy\Monitor\_operations'

& "$ops\scripts\Install-Monitor.ps1" `
  -ArtifactPath 'C:\Deploy\Monitor\Monitor-0.1.0-rc.XX-win-x64.zip' `
  -ChecksumPath 'C:\Deploy\Monitor\Monitor-0.1.0-rc.XX-win-x64.zip.sha256' `
  -ReleaseVersion '0.1.0-rc.XX' `
  -ProductionConfigPath 'C:\Deploy\Monitor\approved\appsettings.Production.json' `
  -OperationalBackupId '<validated-pre-cutover-backup-id>' `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>'
```

The default is **PLAN ONLY**. It validates the candidate checksum and reports the current prerequisite state. It must not download installers, enable Windows features, import certificates, create IIS objects, change ACLs, or deploy the application.

## Online apply

After reviewing the plan and the production change window:

```powershell
& "$ops\scripts\Install-Monitor.ps1" `
  -ArtifactPath 'C:\Deploy\Monitor\Monitor-0.1.0-rc.XX-win-x64.zip' `
  -ChecksumPath 'C:\Deploy\Monitor\Monitor-0.1.0-rc.XX-win-x64.zip.sha256' `
  -ReleaseVersion '0.1.0-rc.XX' `
  -ProductionConfigPath 'C:\Deploy\Monitor\approved\appsettings.Production.json' `
  -OperationalBackupId '<validated-pre-cutover-backup-id>' `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -Apply
```

When missing, the bootstrap layer installs:

1. PowerShell 7 from the SHA-256-pinned official Windows x64 MSI used by this release of the tooling;
2. required IIS server/management scripting features;
3. the .NET 8 ASP.NET Core Hosting Bundle so the ASP.NET Core 8 runtime and ANCM v2 are available to IIS.

The Hosting Bundle is installed/repaired **after IIS** when IIS had to be added, so ANCM registration is not skipped.

## Existing IIS servers and service restart

Installing or repairing the Hosting Bundle can require WAS/W3SVC to be restarted. On a server where IIS was already installed, the bootstrap script refuses to perform that restart unless the operator explicitly supplies:

```powershell
-AllowIisServiceRestart
```

Use that switch only inside an approved maintenance window after checking the other IIS workloads on the host. Alternatively, restart WAS/W3SVC through the normal infrastructure change process and rerun the installer.

## Offline apply

For a disconnected server, put the prerequisites beside the deployment files and supply their local paths:

```powershell
& "$ops\scripts\Install-Monitor.ps1" `
  -ArtifactPath 'C:\Deploy\Monitor\Monitor-0.1.0-rc.XX-win-x64.zip' `
  -ChecksumPath 'C:\Deploy\Monitor\Monitor-0.1.0-rc.XX-win-x64.zip.sha256' `
  -ReleaseVersion '0.1.0-rc.XX' `
  -ProductionConfigPath 'C:\Deploy\Monitor\approved\appsettings.Production.json' `
  -OperationalBackupId '<validated-pre-cutover-backup-id>' `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -Offline `
  -PowerShellMsiPath 'C:\Deploy\Monitor\prerequisites\PowerShell-7.4.16-win-x64.msi' `
  -HostingBundlePath 'C:\Deploy\Monitor\prerequisites\dotnet-hosting-8.x-win.exe' `
  -Apply
```

`PowerShellMsiSha256` is pinned by default for the bundled tooling version. For a replacement MSI, pass the matching approved SHA-256 explicitly. For the Hosting Bundle, the operator may additionally pin the exact approved installer with:

```powershell
-HostingBundleSha256 '<64-hex-sha256>'
```

In `-Offline` mode, missing local installer paths fail closed; the script does not fall back to the Internet.

## Importing the approved HTTPS certificate

The preferred production path is to provision the certificate through the normal PKI/infrastructure process before application setup. If the approved certificate must be imported during this bootstrap, obtain the PFX password as a `SecureString` and pass the PFX path:

```powershell
$pfxPassword = Read-Host 'PFX password' -AsSecureString

& "$ops\scripts\Install-Monitor.ps1" `
  ... `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -CertificatePfxPath 'C:\Deploy\Monitor\cert\monitor.pfx' `
  -CertificatePfxPassword $pfxPassword `
  -Apply
```

The script reads the PFX before import and refuses it unless it contains the exact approved thumbprint. It never creates a self-signed production certificate.

## IIS baseline created or validated

The bootstrap layer is idempotent. It creates missing Monitor-owned objects and validates existing ones:

- application pool `Monitor` by default;
- **No Managed Code**;
- `ApplicationPoolIdentity` for a newly created pool;
- rejects LocalSystem, LocalService and NetworkService;
- accepts an existing approved `SpecificUser` pool identity without accepting its password as an installer argument;
- HTTPS-only Monitor binding for the exact host/port and approved machine certificate;
- stable state root `C:\ProgramData\Monitor\App_Data`;
- immutable release root `C:\Program Files\Monitor\releases`;
- Modify ACL only on stable `App_Data`, with Read/Execute on release/bootstrap roots.

An existing IIS site assigned to another application pool is not taken over automatically; setup fails for operator review.

## Restart-required behavior

If Windows Features, the PowerShell MSI, or the Hosting Bundle reports that a server restart is required, the installer stops before application deployment and prints that condition. Reboot the server and run the same command again with `-Apply`.

The second run is idempotent: already-satisfied prerequisites are detected instead of being blindly reinstalled.

## Deployment and rollback behavior

Once bootstrap succeeds without a pending restart, `Install-Monitor.ps1` runs the existing `Test-IisProductionPrerequisites.ps1` and then `Deploy-ProductionSingleNode.ps1 -Apply`.

The existing deployment contract remains authoritative:

- verify candidate SHA-256;
- stage a new immutable versioned release;
- keep `App_Data` outside the release tree;
- switch only the Monitor IIS physical path;
- run HTTPS health acceptance;
- automatically restore the previous physical path if immediate acceptance fails;
- retain the documented production acceptance and rollback gates.

See `DEPLOY_IIS.md`, `ROLLBACK_RUNBOOK.md`, and `PRODUCTION_SINGLENODE_ACCEPTANCE.md` for the remaining external production evidence workflow.
