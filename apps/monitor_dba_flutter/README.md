# Monitor DBA Flutter Dashboard

A deliberately small, read-only Flutter companion for Monitor administrators.

## What it shows

- registered SQL Server instances;
- online/total database counts;
- SQL memory utilization;
- blocked requests, runnable tasks and pending I/O;
- full-backup compliance gaps;
- top DBA recommendations;
- DBA to-do items with **what / when / where**;
- a visible Refresh action and link to the full web DBA Command Center.

The mobile app **never connects to SQL Server**. It only reads `GET /api/dba/summary` from the Monitor web application, so SQL credentials and DMV permissions remain server-side.

## Authentication

The app opens the existing Monitor login page in an embedded web view. After the administrator signs in, **Open DBA dashboard with this session** reads the authenticated Monitor cookies and uses them only for API requests in the current app process. This implementation does not persist the password or a Monitor session cookie to application storage.

For enterprise deployments, publish Monitor over HTTPS and keep the same reverse-proxy, network and identity restrictions used for the web portal.

## Run

```bash
cd apps/monitor_dba_flutter
flutter pub get
flutter run
```

Enter the HTTPS base URL of the deployed Monitor instance, sign in, then open the native dashboard.

## Scope

This companion is intentionally operational/read-only. Deep inspection, backup-plan generation, upgrade staging and any privileged workflow stay in the full Monitor web UI where authorization, anti-forgery checks and audit records already exist.
