# Project Status

**Updated:** 2026-08-10 12:17 +03:00  
**Branch:** `agent/m2-003a-database-health-ui`  
**Target:** `M2-003A`  
**Issue:** #22  
**PR:** #23  
**Overall:** 🟢 M2-003A CI VERIFIED — READY TO MERGE

## M2-003A — Cached Database Health UI

- `/database-health` now reads through `IMonitorReadService` and the shared snapshot cache instead of `IDemoMonitorService` directly.
- `ServerHealthSnapshot.Databases` is projected into the UI read model without changing the canonical M2-003 contract.
- Real restoring, recovering, recovery-pending, suspect, emergency and offline/other counts are surfaced when cached detail exists.
- Mixed real/demo and development-only modes are labeled explicitly; demo cards never fabricate detailed database-state values.
- Existing client-side filtering remains local and adds no SQL call or polling path.
- Snapshot collector timeout classification is hardened so provider timeout remains `SnapshotCollectionFailure.TimedOut`.
- Focused tests cover cached database-detail projection, demo non-fabrication and timeout preservation.
- CI run `31373761997`: SUCCESS — Release build 0 warnings / 0 errors; 41/41 tests passed.

## M2-003 through M2-007 — Health modules batch

- Canonical snapshot carries validated database state, backup, Agent, storage and blocking summaries.
- Collection remains one fixed backend command under the existing cache and timeout boundary.
- No UI polling, per-widget SQL, credentials, job commands, physical paths or raw provider errors.
- Invalid cross-field ranges fail through the existing safe redacted result.
- M2-003A deliberately reuses these merged contracts instead of introducing parallel module models.

## M2-002 — Real memory health UI

- Memory Health reads through `MonitorReadService` and the shared snapshot cache.
- Real SQL process utilization maps to the configured server card.
- Mixed real/demo and development-only modes are labeled explicitly.
- No browser polling, direct SQL call or extra collector query was added.
- CI run `31372312362`: SUCCESS.

## M2-001 — Memory snapshot contract and projection

- Optional immutable `MemoryHealthSnapshot` is part of the canonical server snapshot.
- Total/available physical memory and SQL process memory are captured.
- SQL process utilization and physical/virtual low-memory flags are captured.
- Existing collector command was extended without a browser query path.
- CI run `31372045546`: SUCCESS.

## M1 — First real SQL vertical slice — VERIFIED

- M1-001 registration + secret boundary: CI `31368239695`.
- M1-002 secure Test Connection: CI `31368995784`.
- M1-003 lightweight collector: CI `31369800023`.
- M1-004 snapshot cache: CI `31370422613`.
- M1-005 first real cached snapshot UI: CI `31371256976`.
- M1-006 throttled backend refresh: CI `31371676834`.
- M1-007 SignalR evaluation: deferred by ADR-013 until scheduled backend publication exists.
- M1-002A SQL Connection Lab merged to stable `main` in PR #18 at `2d5bf3d888280ce53b73d5675aea5c135476d0a7`; pre-merge CI passed 36/36 tests with zero build warnings/errors.

## Stable architecture guardrails

- Browser/UI components never connect directly to monitored SQL Servers.
- Credentials remain outside browser models and repository registrations.
- Snapshot cache is the shared read boundary for real monitoring surfaces.
- UI motion and filtering are client-side only and do not alter collection frequency.
- Mock/development values remain explicitly labeled and are never presented as production facts.

## Verification evidence

- M2-003A merge-result CI run `31373761997`: SUCCESS.
- `dotnet build Monitor.sln --configuration Release --no-restore --warnaserror`: 0 warnings, 0 errors.
- `dotnet test Monitor.sln --configuration Release --no-build`: 41 passed, 0 failed, 0 skipped.
- M0 visual acceptance: USER ACCEPTED on 2026-08-10.

## Merge gate

PR #23 is code/test verified. Run GitHub Actions once more on this documentation head and confirm mergeability before merging to stable `main`.

## Next action

Merge PR #23 after the final docs-head gate, then continue from the first unexposed M2 health summary without duplicating the already-merged collector contracts.
