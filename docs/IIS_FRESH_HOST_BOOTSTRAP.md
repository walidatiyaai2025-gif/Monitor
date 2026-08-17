# Fresh-host PowerShell 7 and IIS bootstrap

This runbook complements `docs/DEPLOY_IIS.md` for a clean Windows Server or an operator session that starts in Windows PowerShell 5.1. It does not change the production dependency: **#162 durable RC.61 publication + independent verification must complete before any #116 production mutation**.

## Safety model

- `Install-ProductionSingleNode.ps1` and `Bootstrap-IisProductionSingleNode.ps1` are **PLAN ONLY by default**.
- `-Apply` is explicit and still requires `-AcknowledgeDurableReleasePrerequisite` on the top-level installer.
- That acknowledgement does not verify or satisfy #162; it only records that the operator is deliberately proceeding after the separate #162 process is actually complete.
- PowerShell 7 prerequisite installation is completed **before** the first IIS/bootstrap mutation. The operator must relaunch the same approved command under `pwsh` after installation.
- A prerequisite installer or Windows feature result that requires reboot stops the operation before application cutover. Reboot, reopen an elevated PowerShell 7 console, then rerun the same reviewed command.

## PowerShell 7 prerequisite

The default Online mode is pinned to the official PowerShell v7.4.16 x64 MSI:

```text
https://github.com/PowerShell/PowerShell/releases/download/v7.4.16/PowerShell-7.4.16-win-x64.msi
SHA-256: 2c0c2036b0032375ad4f7809a92d0b6fa4a8e4ee89a75211514c4cf55ae22495
```

The installer enforces:

1. HTTPS.
2. Host `github.com`.
3. Release path under `/PowerShell/PowerShell/releases/download/`.
4. x64 MSI filename.
5. exact SHA-256 match.
6. valid Microsoft Corporation Authenticode signature.

### Online PlanOnly

```powershell
.\scripts\Install-ProductionSingleNode.ps1 `
  -ArtifactPath 'C:\Deploy\Monitor\Monitor-<version>-win-x64.zip' `
  -ChecksumPath 'C:\Deploy\Monitor\Monitor-<version>-win-x64.zip.sha256' `
  -ReleaseVersion '<version>' `
  -ProductionConfigPath 'C:\Deploy\Monitor\approved\appsettings.Production.json' `
  -OperationalBackupId '<validated-pre-cutover-backup-id>' `
  -HostName 'monitor.example.internal' `
  -CertificateThumbprint '<approved-machine-cert-thumbprint>' `
  -PowerShellMode Online
```

If PowerShell 7 is missing, PlanOnly reports the prerequisite and stops before IIS preflight/deploy. Nothing is installed or mutated.

### Online Apply

Only after #162 is actually complete and the reviewed operation is approved:

```powershell
# Same reviewed arguments as PlanOnly
.\scripts\Install-ProductionSingleNode.ps1 ... `
  -PowerShellMode Online `
  -AcknowledgeDurableReleasePrerequisite `
  -Apply
```

If PowerShell 7 is installed during this invocation, the script stops and instructs the operator to reopen an elevated PowerShell 7 (`pwsh`) console. It does not continue into IIS mutation in the original Windows PowerShell process.

## Offline PowerShell 7 mode

Transfer the approved official x64 MSI through the normal controlled software-transfer process. The built-in SHA-256 and Microsoft Authenticode checks still apply:

```powershell
.\scripts\Install-ProductionSingleNode.ps1 `
  ... `
  -PowerShellMode Offline `
  -PowerShellMsiInstallerPath 'C:\Deploy\Prereqs\PowerShell-7.4.16-win-x64.msi'
```

For Apply, add `-AcknowledgeDurableReleasePrerequisite -Apply` only when the production dependency permits mutation.

## Hosting Bundle on a fresh host

`Bootstrap-IisProductionSingleNode.ps1` detects:

- required IIS Windows features;
- .NET executable from PATH or the well-known Program Files location;
- `Microsoft.AspNetCore.App 8.x`;
- ANCM v2 (`aspnetcorev2.dll`, with out-of-process module fallback);
- application pool/site/HTTPS/certificate state;
- stable release/state/bootstrap roots and ACL readiness.

### Offline Hosting Bundle

```powershell
.\scripts\Install-ProductionSingleNode.ps1 `
  ... `
  -HostingBundleMode Offline `
  -HostingBundleInstallerPath 'C:\Deploy\Prereqs\dotnet-hosting-8-approved.exe' `
  -HostingBundleSha256 '<64-hex-approved-installer-sha256>'
```

### Online Hosting Bundle

Online mode requires an explicit HTTPS URL from one of the approved Microsoft download hosts:

- `download.visualstudio.microsoft.com`
- `builds.dotnet.microsoft.com`

Example:

```powershell
.\scripts\Install-ProductionSingleNode.ps1 `
  ... `
  -HostingBundleMode Online `
  -HostingBundleDownloadUrl 'https://download.visualstudio.microsoft.com/<approved-path>' `
  -HostingBundleSha256 '<64-hex-approved-installer-sha256>'
```

The Hosting Bundle executable must have a valid Microsoft Authenticode signature. When a SHA-256 is supplied it must match before execution.

## Existing IIS service restart boundary

Installing ANCM after IIS already exists can require WAS/W3SVC restart. The bootstrap will **not silently restart shared IIS services**. If the Hosting Bundle was installed on an existing IIS server, either:

- perform the restart through the approved maintenance procedure and rerun; or
- during an approved maintenance window, explicitly add `-AllowIisServiceRestart` to the top-level installer so it can be forwarded to bootstrap.

A reboot-required result remains fail-closed and is not converted into a live cutover.

## Certificate/PFX idempotency

The existing machine-certificate flow continues to use `-CertificateThumbprint`. For PFX import:

```powershell
$pfxPassword = Read-Host 'PFX password' -AsSecureString
.\scripts\Install-ProductionSingleNode.ps1 `
  ... `
  -CertificatePfxPath 'C:\Deploy\Certificates\monitor-production.pfx' `
  -CertificatePfxPassword $pfxPassword
```

The PFX leaf thumbprint is checked against an independently supplied thumbprint when both are provided. A matching certificate already present in `Cert:\LocalMachine\My` is reused rather than reimported. Existing HTTPS binding/certificate drift fails closed instead of being silently replaced.

## Final execution order

After prerequisites are stable and the operator is running elevated PowerShell 7, the top-level entrypoint retains this fixed sequence:

1. `Bootstrap-IisProductionSingleNode.ps1`
2. `Test-IisProductionPrerequisites.ps1` — authoritative read-only production preflight
3. `Deploy-ProductionSingleNode.ps1` — existing immutable release/SHA-256/external `App_Data`/acceptance/rollback implementation

Repository CI can verify these contracts. It cannot prove real trusted IIS, real certificate trust, real app-pool identity, deployed SQL least privilege, recycle durability, backup/rollback rehearsal, or the #116 15/15 evidence pack.
