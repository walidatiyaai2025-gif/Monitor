# Project Status

**Updated:** 2026-08-10 13:49 +03:00  
**Branch:** `agent/m7-001-durable-registration-store`  
**Target:** M7-001 durable registration metadata persistence  
**Issue:** #41  
**PR:** #42  
**Overall:** 🟢 M0–M6 VERIFIED — M7-001 CI VERIFIED

## M7-001 — Durable registration metadata persistence — CI VERIFIED

- Dynamic server registrations now survive process restart through a configurable file-backed `IServerRegistrationRepository` implementation.
- The existing repository contract remains unchanged; current controllers, commissioning flow, scheduler and read services do not need a new API.
- Default store path is `App_Data/registrations.json`, resolved under the application content root and explicitly rejected if it resolves inside `wwwroot`.
- Writes are serialized under a repository lock, written to a same-directory temporary file with write-through + flush-to-disk, then atomically moved over the durable file.
- If persistence fails, the in-memory mutation is rolled back so memory and disk do not silently diverge.
- Persisted data contains endpoint/authentication metadata and the opaque `ConnectionSecretReference` only. SQL usernames, passwords and full connection strings are not part of the persisted contract.
- Runtime SQL Login credential values remain process-memory only. After restart, a registration with a `runtime-*` reference remains visible but cannot resolve the expired runtime credential; operators can re-enter credentials or use an external secret reference.
- Corrupt JSON, unsupported format version, duplicate IDs and invalid domain metadata fail closed during repository construction instead of silently starting with an empty estate.
- CI run `31380699808`: SUCCESS — Release build 0 warnings / 0 errors; 72/72 tests passed; Razor compiled in Release.

## M6-001 through M6-050 — Real SQL server user journey — CI VERIFIED

- Empty-estate login routes administrators to Connections.
- Register → Test Connection → first cached snapshot → observer → real Servers estate is implemented as one deliberate backend journey.
- Integrated Security, process-memory SQL Login credentials and external secret references remain behind the backend secret boundary.
- Failed Test Connection prevents first collection.
- Real registrations remain visible when snapshots are unavailable; demo cards are excluded once a real estate exists.
- Dashboard and health pages use real cache-backed projections.
- PR #39 CI run `31378848889`: SUCCESS — Release build 0 warnings / 0 errors; 62/62 tests passed; Razor compiled in Release.

## M5-026 — Incident transition audit enrichment — CI VERIFIED

- Reuses the canonical `IAuditStore`; no parallel audit repository or event contract was introduced.
- Missing authenticated actor identity fails closed before incident mutation.
- State-aware bounded transition outcomes are recorded when repository evidence is available; legacy `applied/conflict` remains the fallback.
- CI run `31379998409`: SUCCESS — Release build 0 warnings / 0 errors; 66/66 tests passed.

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
- M7-001: CI `31380699808`.

## Stable architecture guardrails

- Browser/UI components never connect directly to monitored SQL Servers.
- Snapshot cache remains the shared evidence/read boundary.
- Recommendations and Advisor output remain human-review only and cannot execute production SQL.
- Audit records contain bounded operational metadata, not unrestricted evidence or secrets.
- Role policies separate read, operator, connection-management and advisor-request capabilities.
- Scheduled collection remains disabled unless explicitly enabled by validated configuration.
- Registration persistence is Monitor-owned metadata only; monitored SQL Servers are never used as a configuration write target.
- Runtime SQL Login values are never persisted by M7-001.

## Merge gate

Run GitHub Actions on the final documentation head. Confirm `main` has not introduced an overlapping persistence change, then merge PR #42 only if restore, Release build with warnings-as-errors, Razor compilation and all tests remain Green.

## Next action

After M7-001 merge, execute M7-002: enterprise secret-provider integration behind the existing `IConnectionSecretStore` boundary, without changing registration JSON or allowing secret values into logs/audit/UI.
