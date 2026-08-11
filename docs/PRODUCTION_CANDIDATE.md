# P0.5 — SingleNode Production Candidate

This document is the durable acceptance record for Issue #116. P0.5 intentionally separates **repository/candidate verification** from **actual IIS/HTTPS deployment acceptance**.

## Scope freeze

The first production release is **SingleNode only**.

The candidate baseline must keep:

- `Deployment:Mode = SingleNode`
- `SharedState:Provider = Disabled`
- `HaState:UseSharedRegistrations = false`
- `HaState:UseSharedOperationalState = false`
- `Coordination:Enabled = false`
- `DataProtectionKeyStore:Mode = LocalFile`

MultiNode, shared-state activation and shared key management are explicitly deferred until after the first stable SingleNode release.

## Candidate security contract

- Production administrator credentials are process-environment values, never a source-controlled production fallback.
- The checked-in development PBKDF2 derivation is rejected in Production even if copied into environment variables.
- `appsettings.Development.json` contains development-only credential material and is excluded from publish output.
- Published `appsettings.json` contains no `DevelopmentAdmin` or `ConnectionSecrets` section.
- The candidate package contains no actual `appsettings.Production.json`; the target environment creates its own production configuration from the secret-free example during deployment.
- The candidate package contains no persisted `App_Data` registration, operational, backup, encrypted-secret or Data Protection key-ring state.
- Persistent application state is preserved by the deployment environment, not shipped inside a new binary package.

## Automated Windows candidate gate

PR #125 adds `.github/workflows/production-candidate.yml`. The gate must be Green before the repository candidate is accepted.

It performs:

1. .NET 8 restore.
2. Release build with warnings-as-errors.
3. Full test suite.
4. Framework-dependent `win-x64` publish.
5. `scripts/Test-ProductionCandidate.ps1` secret/topology/state validation.
6. Temporary Production runtime configuration and runtime-generated administrator credential derivation.
7. Published-process startup on Windows.
8. `/health/live`, `/health/ready`, `/health` smoke through `scripts/Smoke-Monitor.ps1` on isolated HTTP loopback.
9. Process stop/restart and the same health smoke again.
10. Verification that the local Data Protection key-ring state exists across the process restart.
11. Removal of runtime `appsettings.Production.json` and all generated `App_Data` before packaging.
12. `_operations` bundle with production configuration example, smoke/validation scripts and IIS/upgrade/rollback/real-SQL acceptance docs.
13. Versioned release manifest.
14. ZIP + SHA-256 generation and checksum revalidation.
15. Candidate artifact upload.

The tagged `release.yml` is aligned with the same validated package shape so the tested candidate and release package do not diverge structurally.

## Inherited real-SQL evidence

P0.4 is complete and remains an input to P0.5:

- final normal CI `31481874425`: 518/518 Green;
- final SQL Server 2022 real-engine run `31481874501`: 8/8 RealSql Green;
- non-sysadmin least-privilege monitored-SQL role proven;
- full Add → Test → Register → Collect → View → Refresh → Restart → View journey proven;
- auth/network/timeout/TLS/server-permission/msdb-permission failure matrix proven;
- durable evidence in `docs/REAL_SQL_ACCEPTANCE.md`.

## Repository candidate acceptance — pending final CI evidence

Record here after PR #125 final same-head verification:

- source SHA;
- normal CI run and test count;
- Windows production-candidate run;
- candidate version;
- artifact ID/name;
- ZIP size;
- SHA-256;
- first Production health smoke result;
- restart health smoke result;
- package validation result.

Do not mark these items Green without the exact Actions evidence.

## Actual IIS/HTTPS acceptance — mandatory external gate

Repository CI **cannot** close Issue #116 by itself. Before #116 and umbrella #111 close, an actual Windows Server/IIS SingleNode deployment must record:

- target environment identifier that is safe to document;
- deployed candidate version and SHA-256 match;
- IIS application pool using No Managed Code and the approved low-privilege identity;
- HTTPS binding/certificate active;
- approved production administrator environment configuration present;
- persistent `App_Data` ACLs applied;
- first `/health/live`, `/health/ready`, `/health` HTTPS smoke Green;
- administrator login successful;
- readiness/settings page acceptable;
- protected real SQL Test Connection successful with the approved least-privilege login;
- Server Details displays trustworthy cached evidence;
- IIS application-pool recycle performed;
- second HTTPS health smoke Green after recycle;
- durable registrations/credential resolution/operational state confirmed after recycle;
- operational backup created/validated before cutover;
- rollback target and rollback steps verified;
- final production acceptance decision recorded.

## Gate decision

P0.5 remains **OPEN** until both layers are complete:

1. repository production candidate verification; and
2. actual IIS/HTTPS SingleNode deployment acceptance.

A Green CI artifact is a deployable candidate, not proof that an external production host was deployed successfully.
