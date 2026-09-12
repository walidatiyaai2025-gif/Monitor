# Monitor DBA Command Center

## Purpose

The DBA Command Center turns Monitor into an operator-facing SQL Server diagnostic cockpit without moving SQL credentials or DMV access into a browser or mobile client.

The design has four non-negotiable boundaries:

1. Normal page loads and the Flutter companion are **cache/control-plane only**.
2. A new SQL diagnostic connection is opened only by the visible, authorized and audited **Run DBA inspection** action.
3. Monitor can generate diagnostic and remediation SQL, but it **never auto-executes tuning, index, kill-session, configuration, backup or restore commands**.
4. Upgrade packages are staged by the web application, but replacement of running binaries is performed by a separate host-side updater with checksum verification, backup, readiness validation and rollback.

## DBA Command Center surface

Route: `/dba`

Visible actions on each registered SQL Server:

- **Run DBA inspection** — explicit server-side bounded DMV collection; requires `Operate` authorization and writes audit events.
- **Refresh base snapshot** — uses the existing lightweight snapshot refresh.
- **Backup plan - all databases** — produces a review-only plan for inspected user databases.
- **Backup plan** — per-database review-only plan.
- **Open server** — opens the existing full server evidence page.

The page displays:

- SQL Server version and edition;
- database online/total state;
- SQL process memory percentage and OS available memory;
- memory grants pending;
- blocking, runnable tasks and pending I/O;
- latest observed full backup;
- user-database data/log size;
- user-database buffer-pool residency as the per-database memory indicator;
- recovery model and last full backup per database;
- cumulative top query CPU, average CPU, elapsed time, logical reads and writes;
- active query memory grants;
- missing-index DMV candidates;
- evidence-based recommendations with investigation/fix SQL and validation SQL;
- a DBA to-do list with priority, what, when, where and evidence.

### Query text privacy

DMV query text can contain literals. Before a query snippet is retained in the in-process diagnostic cache, Monitor:

- replaces SQL string literals with `?`;
- replaces numeric literals with `?`;
- collapses whitespace;
- caps the retained snippet at 600 characters.

No full execution plan is persisted by this feature.

### Diagnostic bounds

A DBA inspection is deliberately bounded:

- overall inspection timeout: 8 seconds;
- command timeout: 5 seconds;
- top query consumers: 20;
- active memory grants: 20;
- user-database resource rows: 100;
- missing-index candidates: 20;
- cached server inspections: 200 maximum.

An unavailable inspection does not erase or disable the existing base monitoring snapshot.

## SQL permissions

The registered Monitor SQL principal must have the permissions required by the existing base collector plus the SQL Server version-appropriate server-state permission required for the DMV queries used by the deep inspection. On modern SQL Server versions this can include `VIEW SERVER STATE` or `VIEW SERVER PERFORMANCE STATE` depending on the DMV and product version.

Grant only the minimum rights required by the deployed SQL Server version. The browser and Flutter application do not receive these permissions or SQL credentials.

## Recommendation policy

Recommendations are evidence driven and explicitly advisory. Current categories include:

- memory pressure and pending memory grants;
- blocking chains;
- runnable scheduler / CPU pressure;
- pending I/O and database-file latency;
- backup compliance gaps;
- grant-heavy queries;
- cumulative high-CPU queries;
- missing-index candidates.

Every recommendation contains:

- severity;
- category;
- evidence;
- recommended DBA action;
- investigation/fix SQL;
- validation SQL;
- a `REVIEW ONLY - NEVER AUTO-EXECUTE` safety label.

Missing-index DMV output is never treated as an automatic `CREATE INDEX` instruction. The DBA must compare existing indexes, Query Store/plan evidence and write overhead before any change.

## Backup management plans

Monitor generates a review-only backup-management template for one inspected database or all inspected user databases. The template includes:

- recommended full backup cadence: daily at 23:00;
- differential backup cadence: every 6 hours;
- transaction-log cadence: every 15 minutes for `FULL` / `BULK_LOGGED` recovery;
- `CHECKSUM` and compression;
- `RESTORE VERIFYONLY` after full backup;
- weekly `DBCC CHECKDB` guidance;
- explicit `<BACKUP_ROOT>`, `<DIFF_FILE>` and `<LOG_FILE>` placeholders where an operator must choose the approved target.

This feature does not run the backup plan. Backup storage, retention, encryption, off-site copy, restore testing, RPO and RTO remain deployment-specific DBA decisions.

## Flutter DBA companion

Folder: `apps/monitor_dba_flutter`

The Flutter application is intentionally simple and read-only. It shows:

- instance count;
- critical/high recommendation count;
- query-hotspot count;
- DBA to-do count;
- database online/total counts;
- SQL memory utilization;
- blocking, runnable tasks and pending I/O;
- backup gaps;
- top recommendations;
- DBA to-do items with what/when/where.

The app signs in through the existing Monitor web login using an embedded web view, then calls authenticated `GET /api/dba/summary`. It does not store a SQL connection string or connect to SQL Server.

## First installation

Script: `deploy/installer/Install-MonitorWizard.ps1`

The Windows wizard provides visible Back / Next / Install controls and performs:

1. Administrator/elevation validation.
2. Published application folder selection.
3. Install-directory selection.
4. HTTP port selection.
5. Production administrator username/password entry and confirmation.
6. PBKDF2-SHA256 derivation with a random 32-byte salt and 210,000 iterations.
7. Application copy to the selected install directory.
8. Installation of the existing `Monitor` Windows Service hosting mode.
9. Service-scoped production environment variables for ASP.NET Core and the administrator credential material.
10. Windows Service automatic start and recovery configuration.
11. Installation of the `Monitor Upgrade Agent` scheduled task under `SYSTEM`.
12. Service start and `/health/ready` readiness validation.

The installer does not ask for monitored SQL Server credentials; those remain a post-install Connections workflow.

## In-app upgrade workflow

Admin route: `/admin/upgrades`

Visible workflow:

1. **Upload, verify and stage** a `.zip` release package.
2. Provide the release SHA-256 published with the package.
3. Monitor verifies the streamed upload checksum and validates archive paths, entry count, maximum expanded size, manifest and `app/` payload.
4. **Request safe apply** creates a host-side pending request only after a second checksum verification.
5. The installed `Monitor Upgrade Agent` detects the request.
6. The agent verifies the checksum and archive again.
7. It backs up the current installation.
8. It stops the Monitor Windows Service.
9. It replaces application binaries while preserving machine-local data/log directories and `appsettings.Production.json`.
10. It starts Monitor and requires `/health/ready` to return HTTP 200.
11. If readiness fails, it attempts rollback to the pre-upgrade installation and records the result.

### Upgrade package format

```text
monitor-upgrade-manifest.json
app/
  Monitor.Web.dll
  ...published Monitor files...
```

Example manifest:

```json
{
  "version": "1.2.3",
  "notes": "DBA command center release"
}
```

The web process never replaces its own running binaries.

## Operational validation

Before production acceptance, validate at minimum:

- Read-only users can view cached DBA evidence but cannot run an inspection.
- Operators can run an inspection and the audit trail records the action.
- Browser GET `/dba` does not create a monitored SQL connection.
- SQL literals in retained query snippets are redacted.
- DBA inspection failure leaves base monitoring available.
- Per-database backup plan targets the selected database only.
- Flutter authentication cannot bypass Monitor authorization.
- Installer rejects non-elevated execution and duplicate service installation.
- Production credential guard accepts the installer-created credential and rejects the development baseline.
- Upgrade staging rejects hash mismatch, path traversal, missing manifest and missing `app/` payload.
- Host updater rollback is tested on a disposable Windows host before production use.
- Upgrade preserves local operational data and production configuration.
