# IIS Bootstrap Installer for Production SingleNode

This runbook describes the repository-only P0.5 bootstrap layer around the existing IIS production deployment tooling. It prepares a Windows Server for Monitor without changing the durable RC.61 publication gate or manufacturing any real production acceptance evidence.

The dependency boundary remains unchanged:

`#162 durable RC.61 publication + independent verification -> #116 real trusted-IIS acceptance -> #111 closure`

Do not use this repository work to bypass #162 or to claim any #116 gate PASS. Running the examples below against a real production server is an operator action and remains blocked until the governing release/acceptance sequence allows it.

## Scripts

- `scripts/Initialize-IisProductionHost.ps1` — idempotent host bootstrap. **PLAN ONLY by default**; `-Apply` is required for mutation.
- `scripts/Test-IisProductionPrerequisites.ps1` — existing read-only, fail-closed authoritative IIS/HTTPS preflight.
- `scripts/Install-ProductionSingleNode.ps1` — one operator entrypoint that runs bootstrap, then authoritative preflight, then the existing deployment script.
- `scripts/Deploy-ProductionSingleNode.ps1` — existing immutable release deployment, stable `App_Data`, SHA-256 validation, HTTPS acceptance and automatic application-path rollback. Its semantics are unchanged.

## What bootstrap can prepare

When explicitly applied from an elevated Windows Server PowerShell session, the bootstrap can:

1. Check and install the required IIS roles plus management scripting support.
2. Detect the .NET 8 ASP.NET Core Runtime and ANCM v2.
3. Install the .NET 8 Hosting Bundle from either an operator-supplied offline installer or an explicit approved Microsoft HTTPS download URL.
4. Optionally verify the Hosting Bundle installer with `-HostingBundleSha256` before execution.
5. Create the `Monitor` application pool with **No Managed Code** and `ApplicationPoolIdentity` when the pool does not already exist.
6. Validate an existing pool and fail closed if it uses LocalSystem, LocalService or NetworkService.
7. Create/validate the `Monitor` IIS site and SNI HTTPS binding.
8. Use an existing certificate in `Cert:\LocalMachine\My` by `-CertificateThumbprint`, or import an explicitly supplied PFX using a `SecureString` password.
9. Create the stable filesystem roots and grant the app-pool identity Modify on `App_Data` and Read/Execute on release/bootstrap roots.

It does not store plaintext credentials, create SQL credentials, publish a GitHub release, dispatch a workflow, rebuild RC.61, or alter external acceptance evidence.

## 1. Dry-run with an existing machine certificate

The bootstrap is PlanOnly unless `-Apply` is present:

```powershell
.\scripts\Initialize-IisProductionHost.ps1 `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>'
```

The output lists missing IIS features, Hosting Bundle work, IIS objects and filesystem/ACL work. No Windows feature, runtime, IIS, certificate, binding, filesystem or ACL mutation occurs.

## 2. Online Hosting Bundle mode

Use Online mode only with an explicit Microsoft download URL approved by the operator. The script rejects non-HTTPS URLs and download hosts outside its Microsoft allowlist. Pinning the installer SHA-256 is recommended:

```powershell
.\scripts\Initialize-IisProductionHost.ps1 `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -HostingBundleMode Online `
  -HostingBundleUrl 'https://download.visualstudio.microsoft.com/<approved-dotnet-8-hosting-bundle-path>' `
  -HostingBundleSha256 '<64-hex-approved-installer-sha256>'
```

Review the PLAN ONLY output first. To perform the approved bootstrap, repeat the exact command with `-Apply` from an elevated session.

The downloaded installer is executed with `/install /quiet /norestart`. Exit codes `0` and `3010` are accepted; `3010` is surfaced as `RebootRequired=true`. The bootstrap re-detects both the ASP.NET Core runtime and ANCM before reporting readiness.

## 3. Offline Hosting Bundle mode

Offline mode performs no Internet download. Copy the approved Hosting Bundle installer to the server through the normal controlled software-transfer path, verify its independently obtained SHA-256, then dry-run:

```powershell
.\scripts\Initialize-IisProductionHost.ps1 `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -HostingBundleMode Offline `
  -HostingBundleInstallerPath 'C:\Deploy\Prereqs\dotnet-hosting-8-approved.exe' `
  -HostingBundleSha256 '<64-hex-approved-installer-sha256>'
```

After review, append `-Apply` to install the missing prerequisites and IIS baseline. Re-running the same command is idempotent: already-present IIS/runtime/site/pool/binding state is validated instead of blindly replaced.

## 4. Existing certificate thumbprint

For a certificate already installed in `Cert:\LocalMachine\My`:

```powershell
.\scripts\Initialize-IisProductionHost.ps1 `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -Apply
```

The certificate must have an accessible private key and must not expire within 24 hours. An existing HTTPS binding using a different certificate fails closed; it is not silently replaced.

## 5. Explicit PFX import

Do not pass a plaintext PFX password on the command line. Read it interactively or from an approved secret mechanism into a `SecureString`:

```powershell
$pfxPassword = Read-Host 'PFX password' -AsSecureString

.\scripts\Initialize-IisProductionHost.ps1 `
  -HostName 'monitor.example.internal' `
  -PfxPath 'C:\Deploy\Certificates\monitor-production.pfx' `
  -PfxPassword $pfxPassword
```

That command is still PLAN ONLY. After review, repeat with `-Apply`. For non-interactive approved automation, construct the `SecureString` from the platform secret provider rather than a literal. `ConvertTo-SecureString` may be used only with secret-provider output; do not embed the secret in source, documentation, command history or logs.

## 6. Single bootstrap + deploy entrypoint

`Install-ProductionSingleNode.ps1` accepts the existing release/deployment inputs plus bootstrap options. It keeps the existing deployment implementation authoritative rather than reimplementing package/cutover logic.

Dry-run on an already prepared host:

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

If bootstrap changes are required, the PlanOnly entrypoint prints the combined bootstrap/deployment sequence and stops before authoritative preflight because the host is not yet prepared. Nothing is mutated.

For an approved end-to-end apply, use the same arguments and add `-Apply`. The ordering is fixed:

1. `Initialize-IisProductionHost.ps1 -Apply`
2. `Test-IisProductionPrerequisites.ps1` — authoritative fail-closed preflight
3. `Deploy-ProductionSingleNode.ps1 -Apply`

The existing deployment script still owns artifact/checksum SHA-256 validation, immutable versioned release staging, external durable `App_Data`, production configuration placement, HTTPS acceptance and automatic application-path rollback.

Offline all-in-one example:

```powershell
.\scripts\Install-ProductionSingleNode.ps1 `
  -ArtifactPath 'C:\Deploy\Monitor\Monitor-<version>-win-x64.zip' `
  -ChecksumPath 'C:\Deploy\Monitor\Monitor-<version>-win-x64.zip.sha256' `
  -ReleaseVersion '<version>' `
  -ProductionConfigPath 'C:\Deploy\Monitor\approved\appsettings.Production.json' `
  -OperationalBackupId '<validated-pre-cutover-backup-id>' `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -HostingBundleMode Offline `
  -HostingBundleInstallerPath 'C:\Deploy\Prereqs\dotnet-hosting-8-approved.exe' `
  -HostingBundleSha256 '<64-hex-approved-installer-sha256>' `
  -Apply
```

PFX all-in-one example uses `-PfxPath` and `-PfxPassword $pfxPassword` instead of `-CertificateThumbprint`.

## Fail-closed behavior

The bootstrap refuses to continue when it finds incompatible pre-existing state, including:

- a forbidden app-pool identity;
- an app pool using a managed CLR instead of No Managed Code;
- an existing site assigned to a different application pool;
- an existing target HTTPS binding using a different certificate;
- a missing/expired/no-private-key machine certificate;
- a non-Microsoft or non-HTTPS Online Hosting Bundle URL;
- an offline installer that is missing or does not match the supplied SHA-256;
- a Hosting Bundle install that completes without detectable .NET 8 ASP.NET Core Runtime and ANCM.

The script does not delete/recreate mismatched IIS production objects to force convergence. Resolve the infrastructure discrepancy through the approved operator process, then rerun PlanOnly.

## Acceptance boundary

Repository CI can prove parser/static safety and Windows-candidate behavior for these scripts. It cannot prove the real production IIS certificate chain, app-pool identity, SQL least privilege, recycle durability or rollback rehearsal. Those remain external #116 evidence after #162 is fully closed.
