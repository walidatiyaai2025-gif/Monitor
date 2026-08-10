# Project Status

**Updated:** 2026-08-10 14:05 +03:00  
**Branch:** `agent/m7-002-environment-secret-provider`  
**Target:** M7-002 environment-injected enterprise secret provider  
**Issue:** #43  
**PR:** #44  
**Overall:** 🟢 M0–M6 VERIFIED — M7-001/M7-002 CI VERIFIED

## M7-002 — Environment-injected external SQL secret provider — CI VERIFIED

- Preserves the existing `IConnectionSecretStore`, `ConnectionSecretReference`, SQL probe/tester and collector contracts.
- Adds an external secret-provider routing boundary behind the existing backend secret store.
- References of the form `env:<alias>` are handled by `EnvironmentConnectionSecretProvider`.
- Aliases are bounded to 64 ASCII letters/digits/underscore characters and normalize to uppercase.
- Example `env:FINANCE_PROD` maps only to `MONITOR_SQL_SECRET_FINANCE_PROD_USERNAME` and `MONITOR_SQL_SECRET_FINANCE_PROD_PASSWORD`.
- Environment values are read directly from the process environment, not through `IConfiguration`.
- A recognized `env:` reference that is malformed, missing or partial fails closed and never falls back to `ConnectionSecrets` configuration.
- Runtime `runtime-*` credentials remain highest-priority and process-memory only.
- Existing non-`env:` `ConnectionSecrets:<reference>` resolution remains backward compatible.
- No vendor-specific cloud secret SDK, secret write endpoint or SQL behavior change was introduced.
- UI guidance now documents the environment naming convention and accurately describes runtime password input as write-only/non-repopulated.
- CI run `31381465706`: SUCCESS — Release build 0 warnings / 0 errors; 82/82 tests passed; Razor compiled in Release.

## M7-001 — Durable registration metadata persistence — CI VERIFIED

- Dynamic server registrations survive process restart through a configurable file-backed `IServerRegistrationRepository` implementation.
- Default store path is `App_Data/registrations.json`, outside `wwwroot`.
- Persisted data contains safe endpoint/authentication metadata and the opaque `ConnectionSecretReference` only; runtime credential values are not persisted.
- Corrupt persisted state fails closed.
- Final CI `31381074579`: 72/72 tests; Release build 0 warnings / 0 errors.

## Earlier verified milestones

- M0 visual foundation: USER ACCEPTED on 2026-08-10.
- M1 first real SQL vertical slice: CI verified through M1-007.
- M2 health modules: CI verified through M2-013.
- M3 incidents/recommendations: CI verified through M3-016.
- M4 Advisor boundary/hardening: CI verified through M4-013.
- M5-001 through M5-007: CI `31375034604`.
- M5-008 through M5-025: CI `31376448363`.
- M5-026: CI `31379998409`.
- M6-001 through M6-050: CI `31378848889`.
- M7-001: CI `31380699808`, final docs-head `31381074579`.
- M7-002 implementation: CI `31381465706`.

## Stable architecture guardrails

- Browser/UI components never connect directly to monitored SQL Servers.
- Snapshot cache remains the shared evidence/read boundary.
- Recommendations and Advisor output remain human-review only and cannot execute production SQL.
- Audit records contain bounded operational metadata, not unrestricted evidence or secrets.
- Registration persistence stores metadata/opaque references only, never SQL credential values.
- External secret providers remain behind `IConnectionSecretStore`; SQL probing/collection code does not know the provider type.
- A provider-owned reference fails closed inside that provider path; it cannot silently downgrade to a different secret source.
- Scheduled collection remains disabled unless explicitly enabled by validated configuration.

## Merge gate

Run GitHub Actions on the final documentation/UI head. Confirm `main` has not introduced an overlapping secret-provider change, then merge PR #44 only if restore, Release build with warnings-as-errors, Razor compilation and all tests remain Green.

## Next action

After M7-002 merge, execute M7-003: durable Monitor-owned operational state for audit/history/incidents behind stable boundaries, without storing monitored SQL text, credentials or unrestricted evidence.
