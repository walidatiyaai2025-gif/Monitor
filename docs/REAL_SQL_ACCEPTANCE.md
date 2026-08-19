# P0.4 — Real SQL Server Acceptance Evidence

This document is the durable acceptance record for Issue #115. It contains no SQL passwords, SA credentials, protected credential values, current secret references, connection strings, raw provider exceptions, or arbitrary SQL text from monitored workloads.

## Accepted environment

- Runner: GitHub Actions `ubuntu-24.04`.
- Engine: Microsoft SQL Server 2022 Developer on Linux.
- Image: `mcr.microsoft.com/mssql/server@sha256:ba4c8329f48fb8f02e1416be6a930ebfd71268caee78aa985f3af4315e457c89`.
- Image digest observed by the accepted run: `sha256:ba4c8329f48fb8f02e1416be6a930ebfd71268caee78aa985f3af4315e457c89`.
- SQL Agent: enabled and explicitly waited until service state `Running` before job seeding.
- Monitoring login: ephemeral SQL Login created for the run, verified not to be `sysadmin`.
- Credentials: generated at workflow runtime, immediately masked, destroyed with the container, and never committed.
- Least-privilege baseline: `scripts/sql/monitored_sql_least_privilege.sql` applied exactly through `sqlcmd -v MonitorLogin=...`.

## Accepted workflow evidence

### Foundation real-engine gate

PR #123 introduced the SQL Server 2022 acceptance workflow and production fixes discovered by the engine:

- removed an in-file `:setvar` that overrode the deployment-supplied monitor login;
- added the metadata visibility required by `sys.master_files` while preserving read-only monitoring semantics;
- classified structured cross-platform socket failures safely rather than matching provider message text;
- added explicit SQL Agent readiness to eliminate fixture timing races.

The first fully successful foundation real-engine workflow was run `31480624953`, with all four initial RealSql cases passing.

### Full P0.4 journey gate

Follow-up PR #124 extends the gate to the complete production journey and controlled failure matrix.

Accepted implementation runs on head before documentation reconciliation:

- Normal CI run `31481298862`: Release build 0 warnings / 0 errors; **518/518 tests passed**, 0 failed, 0 skipped.
- Real SQL workflow run `31481298848`: Release build 0 warnings / 0 errors; **8/8 RealSql tests passed**.

The real SQL run also verified:

- SQL Server engine readiness;
- SQL Server Agent readiness;
- acceptance estate seed including one backed-up and one intentionally unbacked database;
- one SQL Agent job;
- the exact monitored-SQL least-privilege script;
- the primary monitor login is not `sysadmin`;
- intentionally incomplete permission profiles used only for negative acceptance;
- ephemeral SQL Server cleanup after the run.

## P0.4 accepted cases

| Case | Evidence | Result |
|---|---|---|
| Real least-privilege connection + collector | Production `ServerConnectionTester` and `SqlServerSnapshotCollector` against SQL Server 2022 | PASS |
| Identity/version/uptime/databases | Actual server snapshot evidence | PASS |
| Memory evidence | Actual SQL/OS memory snapshot | PASS |
| Backup evidence | One database backed up and another intentionally unbacked | PASS |
| SQL Agent evidence | Real seeded Agent job | PASS |
| Storage/blocking/runtime evidence | Actual collector modules | PASS |
| Add → Test → Register → Collect → View | Actual `ConnectionLabController` through production persistence/collector/cache/read/controller path | PASS |
| Manual refresh | Actual `SnapshotRefreshService` bounded refresh | PASS |
| Restart recovery | Rebuild registration repo, protected secret store, persisted Data Protection key ring, tester, collector, cache, read service and Server Details controller | PASS |
| Durable registration identity | Same registration ID and opaque secret reference after restart | PASS |
| Credential confidentiality on disk | Username/password canaries absent from registration and encrypted secret files | PASS |
| Bad password | Safe `AuthenticationFailed` classification without secret echo | PASS |
| Self-signed TLS with trust disabled | Safe `CertificateRejected` classification | PASS |
| Closed TCP port | Safe `NetworkUnavailable` classification | PASS |
| Accepted but silent TCP endpoint | Safe bounded `TimedOut` classification | PASS |
| Missing server monitoring permissions | Test Connection succeeds; collector fails closed with bounded safe error | PASS |
| Missing msdb permissions | Server/master permissions present; collector fails closed safely without msdb rights | PASS |

## Least-privilege collector contract proven on SQL Server 2022

The accepted monitoring role requires only the evidence surfaces used by the bounded collector:

- SQL Server 2022 server performance-state visibility for the required DMVs;
- database visibility;
- metadata visibility required for `sys.master_files`;
- read access to `sys.master_files` in `master`;
- read access to `msdb.dbo.backupset`, `msdb.dbo.sysjobs`, and `msdb.dbo.sysjobservers`.

The acceptance login is not granted `sysadmin`, workload table data access, DML, DDL, BACKUP/RESTORE execution, SQL Agent operator control, arbitrary SQL execution, `CONTROL SERVER`, or browser-to-SQL access.

## Reproduction

Use the GitHub Actions workflow `real-sql-acceptance`. The workflow owns its disposable SQL Server target and generates all credentials at runtime. Do not add credentials to repository variables, source files, test fixtures, logs, comments, or this document.

The workflow is fail-closed: `MONITOR_REQUIRE_REAL_SQL=1` means the RealSql test suite fails if its required target environment is incomplete rather than silently skipping.

For P0.5 repository-side hardening, this workflow may also be required as an exact-head cross-platform regression gate even when the change does not alter SQL behavior. A Green P0.5 regression run preserves the already-proven real-engine contract; it does not create new production acceptance or replace the external Windows/IIS gates governed by #116.

### Cross-process operational-backup regression replay — #461 / PR #462

The #461 SingleNode operational-backup cross-process serialization closure changes no monitored-target SQL query, collector, permission grant, SQL credential behavior, or restore target contract. The PR nevertheless requests an exact-head `real-sql-acceptance` replay as a conservative cross-platform regression gate. Its result is implementation evidence only: it preserves the P0.4 real-engine contract and cannot satisfy or close RC.61 publication #162, trusted-IIS production acceptance #116, umbrella #111, or repository-admin branch-protection #353.

## P0.4 gate decision

P0.4 can be closed only after the final PR head has both normal Release CI and `real-sql-acceptance` Green and the canonical P0 plan/status documents record those final run IDs. P0.5 production deployment remains a separate gate; this document proves the real SQL application journey and monitored-target permission contract, not deployment to a user-owned IIS production host.
