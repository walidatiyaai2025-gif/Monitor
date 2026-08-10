# Deploy Monitor as a Windows Service

Monitor enables the .NET Windows Service lifetime. This path is appropriate when IIS is not required and HTTPS is terminated by a trusted reverse proxy or by Kestrel with an explicitly managed certificate.

## Publish

For a framework-dependent Windows x64 deployment:

```powershell
dotnet restore .\Monitor.sln
dotnet test .\Monitor.sln -c Release --no-restore
dotnet publish .\src\Monitor.Web\Monitor.Web.csproj -c Release -r win-x64 --self-contained false -o C:\Deploy\Monitor\candidate
```

Copy `deploy/appsettings.Production.example.json` to `appsettings.Production.json` in the candidate directory and apply non-secret production values. Secrets remain service/machine environment variables.

## Service identity

Use a dedicated low-privilege domain or local service account. Grant it:

- Read/Execute on the application directory.
- Modify only on Monitor-owned local state folders when local persistence is enabled (`App_Data/registrations.json`, `App_Data/operational`, `App_Data/backups`, `App_Data/secrets.json`, `App_Data/keyring`).
- Network access only to the intended monitored SQL instances, the optional dedicated Monitor state database and the reverse proxy/management networks.

Do not run Monitor as LocalSystem, Domain Admin, SQL sysadmin or an interactive administrator.

## Bind locally behind a reverse proxy

Set a loopback URL for Kestrel, for example:

```text
ASPNETCORE_URLS=http://127.0.0.1:5080
ASPNETCORE_ENVIRONMENT=Production
```

Then terminate HTTPS at IIS, a load balancer or another approved proxy. Configure only that proxy IP/CIDR in `WebSecurity:TrustedProxies` / `TrustedNetworks`. Do not expose the loopback HTTP listener externally.

## Install the service

From an elevated PowerShell prompt:

```powershell
$root = 'C:\Program Files\Monitor'
$exe  = Join-Path $root 'Monitor.Web.exe'

New-Service `
  -Name 'Monitor' `
  -BinaryPathName ('"{0}"' -f $exe) `
  -DisplayName 'Monitor SQL Operations Center' `
  -Description 'Monitor SQL Server operations and cached health state.' `
  -StartupType Automatic
```

Set the service Log On identity through your approved service-account process. Do not place passwords in the command line or deployment script.

Configure recovery in Services.msc or with your standard infrastructure automation so unexpected process failure restarts the service with bounded backoff.

## Start and verify

```powershell
Start-Service Monitor
Get-Service Monitor
.\scripts\Smoke-Monitor.ps1 -BaseUri https://monitor.example.internal
```

The service is not considered ready for operator traffic until `/health/ready` is healthy.

## Stop / upgrade

```powershell
Stop-Service Monitor
```

Follow `docs/UPGRADE_CHECKLIST.md`. Deploy to a versioned directory and update the service binary path only after backup/config checks. Keep the previous version for rollback. Follow `docs/ROLLBACK_RUNBOOK.md` on smoke-test failure.

## Uninstall

```powershell
Stop-Service Monitor -ErrorAction SilentlyContinue
sc.exe delete Monitor
```

Uninstalling the service does not delete Monitor-owned state. State/backup deletion must be an explicit separate administrative action.
