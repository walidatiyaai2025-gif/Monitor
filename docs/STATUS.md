# Project Status

**Updated:** 2026-08-10 12:58 +03:00  
**Branch:** `agent/m6-real-server-journey-50-task-batch`  
**Target:** 50 tasks — `M6-001` through `M6-050`  
**Issue:** #35  
**PR:** TBD  
**Overall:** 🟡 50-TASK REAL SQL SERVER USER JOURNEY — LOCAL VERIFIED

## M6-001 through M6-050 — Real server user journey

- Empty-estate login routes directly to Connections.
- Administrator can enter Integrated Security, runtime SQL Login credentials, or an external secret reference.
- Save performs Test Connection, then exactly one first snapshot collection and observation.
- Successful commissioning redirects to the real Servers estate.
- Every real registration is shown; collection failure remains `RegisteredUnavailable` and never becomes demo.
- Dashboard reads real cache-backed servers, database totals and incidents.
- Runtime passwords are never echoed, serialized into registrations or retained after process restart.
- Release build succeeds with warnings-as-errors; 62 tests pass locally.

## PR #33 — Scheduler / security / advisor hardening — CI VERIFIED

- Stable PR #33 completed M4-007 through M4-013 and M5-008 through M5-025.
- Advisor requests are explicit authorized POST operations with antiforgery protection, per-incident single-flight, evidence-version cache, bounded timeout, circuit breaker and redacted audit metadata.
- Hosted scheduled collection is implemented but remains disabled by default; when enabled it uses one no-overlap loop, bounded parallelism, per-server failure isolation and capped exponential backoff.
- The canonical audit system is a bounded append-only `IAuditStore` with a 1,000-event in-memory implementation and Administrator read UI.
- Viewer, Operator and Administrator roles/policies are established; web security adds strict cookie settings, CSP/frame/nosniff/referrer headers and partitioned login limiting.
- CI run `31376448363`: SUCCESS — Release build 0 warnings / 0 errors; 59/59 tests passed; Razor views compiled in Release.

## M4 — Advisor hardening

- M4-001 through M4-006: CI `31375034604`.
- M4-007 through M4-013: CI `31376448363`.
- External provider behavior remains configuration-controlled and advisory-only; no provider output can execute SQL or mutate monitored systems.

## M5 — History and Operational Hardening

- M5-001 through M5-007: CI `31375034604`.
- M5-008 through M5-025: CI `31376448363`.
- Scheduled collection stays disabled unless explicitly enabled by validated configuration.
- Audit, RBAC, browser-security and login-throttling foundations are now part of stable `main`.
- **Next:** M5-026 — enrich incident-transition audit using the existing `IAuditStore`, authenticated actor identity and bounded before/after state metadata.

## Identified M5-026 gap

- Current transition audit records action/target plus a generic `applied` or `conflict` outcome.
- The controller currently falls back to actor `unknown` when the principal name is absent.
- The next slice should fail closed when authenticated actor identity is unavailable and should record successful transitions with bounded `PreviousState -> NewState` context using the existing audit store, not a parallel repository.
- No incident evidence, SQL text, credentials, endpoints, provider errors, job commands or arbitrary request payloads should enter the audit record.

## Stable architecture guardrails

- Browser/UI components never connect directly to monitored SQL Servers.
- Snapshot cache remains the shared evidence/read boundary.
- Recommendations and Advisor output remain human-review only and cannot execute production SQL.
- Audit records contain bounded operational metadata, not unrestricted evidence or secrets.
- Role policies separate read, operator, connection-management and advisor-request capabilities.
- Scheduler and external/provider activity remain disabled unless explicitly configured.

## Verification evidence

- PR #28 CI `31375034604`: 54 passed; 0 failed; Release build 0 warnings / 0 errors.
- PR #33 CI `31376448363`: 59 passed; 0 failed; 0 skipped; Release build 0 warnings / 0 errors.
- M0 visual acceptance: USER ACCEPTED on 2026-08-10.

## Merge gate

This reconciliation is documentation-only. Run GitHub Actions on the final docs head, confirm the branch remains clean against `main`, then merge.

## Next action

After reconciliation, implement M5-026 as a focused hardening change on top of the canonical `IAuditStore`/RBAC architecture; do not create a second audit store.
