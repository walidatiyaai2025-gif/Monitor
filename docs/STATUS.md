# Project Status

**Updated:** 2026-08-10 15:14 +03:00  
**Branch:** `agent/m7-017-shared-sql-state`  
**Target:** M7-017 dedicated Monitor shared-state SQL capability  
**Issue:** #52  
**PR:** #54  
**Overall:** 🟢 M0–M6 VERIFIED · M7-001..M7-017 CI VERIFIED · M8 CI VERIFIED

## M7-017 — Shared-state capability + dedicated Monitor SQL Server provider — CI VERIFIED

- Adds public `ISharedStateDocumentStore` with bounded versioned reads and optimistic compare/exchange.
- Adds real `SqlServerSharedStateDocumentStore` using the existing Microsoft.Data.SqlClient dependency.
- Provider connection string is read only from the configured process-environment variable; appsettings stores only its name and timeout.
- No monitored-server registration is implicitly reused for shared state.
- Keys are allowlisted/bounded to 128 characters; JSON payloads are validated and capped at 1 MiB.
- SQL compare/exchange uses `SERIALIZABLE` plus `UPDLOCK/HOLDLOCK`; stale expected versions return Conflict rather than overwrite newer state.
- The returned write result is captured while the transaction lock is held, avoiding a post-commit re-read race.
- Provider failures are redacted to a fixed unavailable exception/status; connection strings and raw provider errors do not enter UI/audit.
- Adds `scripts/sql/monitor_shared_state_v1.sql`: idempotent schema v1 deployment, incompatible schema refusal, no runtime DDL.
- Administrator Settings shows provider/schema readiness only and omits endpoint/credentials.
- Registration/audit/history/incidents and distributed scheduler coordination are **not** migrated in M7-017.
- `Deployment:MultiNode` remains fail-closed.
- Implementation CI `31386867949`: SUCCESS — Release build 0 warnings / 0 errors; **120/120 tests passed**; Razor compiled.

## Existing verified production-readiness baseline

- M7-004 topology guard: CI `31385935255` — 99/99 tests.
- M7-005..M7-016 protected local credentials: CI `31384727247` — 94/94 tests.
- M8 zero-SQL monitored reads/operator refresh: CI `31383991126` — 91/91 tests.
- M7-001 registration persistence: final CI `31381074579`.
- M7-002 env secret provider: final CI `31382052980`.
- M7-003 operational state: final CI `31383226721`.

## Stable guardrails

- Monitoring browser GETs never trigger monitored SQL collection.
- Shared-state SQL, when enabled, is a separate Monitor-owned control-plane database.
- Shared-state readiness does not make MultiNode safe by itself.
- Protected local SQL credentials/key ring remain node-local.
- MultiNode stays blocked until M7-018 migrates required state and adds distributed coordination.

## Merge gate

Run GitHub Actions on this final docs head, re-check `main` for overlap, then merge PR #54 only if Release build with warnings-as-errors, Razor compilation and all tests remain Green.

## Next action

After M7-017 merge, execute **M7-018 — shared repository migration + distributed scheduler ownership/cross-node single-flight**, preserving zero-SQL monitored GET semantics.
