# Project Status

**Updated:** 2026-08-10 14:18 +03:00  
**Branch:** `agent/m7-003-durable-operational-state`  
**Target:** M7-003 durable Monitor-owned operational state  
**Issue:** #45  
**PR:** #46  
**Overall:** 🟢 M0–M6 VERIFIED — M7-001..M7-003 CI VERIFIED

## M7-003 — Durable Monitor-owned operational state — CI VERIFIED

- Preserves `IAuditStore`, `ISnapshotHistoryStore` and `IHealthIncidentRepository`; current controllers, observer, scheduler, Advisor and incident workflow consumers keep the same contracts.
- Adds `OperationalStore` File/InMemory mode. File mode is the default and resolves `App_Data/operational` outside `wwwroot`.
- Uses independent versioned `audit.json`, `history.json` and `incidents.json` files so unrelated operational state does not share one mutation transaction.
- Mutations build a candidate state, write/flush a same-directory temporary file, atomically replace the durable file, then publish the candidate in process.
- Corrupt, unsupported or domain-invalid persisted state fails closed on startup.
- Audit retains bounded metadata and max 1,000 events with newest-first paging.
- History persists only allowlisted aggregates, deduplicates registration/timestamp, keeps 24 hours and max 288 points/server.
- Incidents preserve stable registration/rule identity, older-evidence ignore semantics, fresh reconciliation resolution and compare-and-set status transitions.
- No SQL text, credentials, monitored-server endpoints, provider errors, job commands or arbitrary request payloads are part of the operational persistence contract.
- Initial CI exposed one incorrect test expectation for `RunnableTasks`; production code compiled successfully. The assertion was corrected without changing production behavior.
- CI run `31382770932`: SUCCESS — Release build 0 warnings / 0 errors; 89/89 tests passed; Razor compiled in Release.

## M7-002 — External SQL secret provider — CI VERIFIED

- `env:<alias>` routes directly to strict process-environment variables behind `IConnectionSecretStore`.
- Provider-owned missing/partial references fail closed without configuration fallback.
- Final CI `31382052980`: 82/82 tests; Release build 0 warnings / 0 errors.

## M7-001 — Durable registration metadata — CI VERIFIED

- Dynamic registrations survive restart without persisting SQL credential values.
- Final CI `31381074579`: 72/72 tests; Release build 0 warnings / 0 errors.

## Stable architecture guardrails

- Browser/UI components never connect directly to monitored SQL Servers.
- Snapshot cache remains the shared evidence/read boundary.
- Recommendations and Advisor output remain human-review only and cannot execute production SQL.
- Secret-provider routing remains behind `IConnectionSecretStore`.
- Registration persistence stores metadata/opaque references only.
- Operational persistence is Monitor-owned and bounded; it never uses monitored SQL Servers as a configuration/state write target.
- M7 file stores provide single-node durability only; shared/HA state is deferred to M7-004.

## Merge gate

Run GitHub Actions on the final docs head. Confirm `main` has not introduced an overlapping operational-store change, then merge PR #46 only if restore, Release build with warnings-as-errors, Razor compilation and all tests remain Green.

## M8 — Zero-SQL reads and operator refresh — LOCAL VERIFIED

- Monitoring GETs use a synchronous cache Peek and never initiate SQL collection.
- Incident pages are read-only; findings are observed only after a successful refresh/collection path.
- Manual refresh now observes the committed snapshot exactly once, so history and incidents become immediately consistent.
- Server Details exposes a policy-protected, antiforgery-protected POST refresh with PRG feedback.
- Registered targets without a snapshot are labeled `REGISTERED · NOT COLLECTED`, never stale.

## M7-005..M7-016 — Protected local SQL credentials — LOCAL VERIFIED

- SQL Login credentials entered in Connections now receive server-generated `local:v1` references.
- Username/password payloads are encrypted with ASP.NET Data Protection and reference-scoped purposes.
- The encrypted file and Data Protection key ring persist outside `wwwroot`; restarts with the same key ring can resolve credentials.
- A missing/different key ring or tampered ciphertext fails closed and never falls back to configuration.
- Writes use a same-directory candidate file and atomic replacement; persisted JSON contains ciphertext only.
- Existing `env:` and legacy external references remain compatible.

## Next action

Push the protected-credential slice, verify GitHub Actions, then add credential replacement/recovery and lifecycle commands before M7-004 HA work.
