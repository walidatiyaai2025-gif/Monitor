# Project Status

**Updated:** 2026-08-10 12:50 +03:00  
**Branch:** `agent/m4-m5-hardening-25-task-batch`  
**Target:** 25 tasks — `M4-007` through `M5-025`  
**Issue:** TBD  
**PR:** #33  
**Overall:** 🟡 25-TASK SCHEDULER / SECURITY / ADVISOR HARDENING BATCH — LOCAL VERIFIED

## M4-007 through M5-025 — 25-task hardening continuation

- Disabled-by-default hosted scheduler with bounded parallelism, no overlap, backoff and runtime status.
- Append-only bounded audit trail for login, incident and advisor request metadata.
- Viewer/Operator/Administrator named authorization policies.
- Strict cookie settings, baseline browser security headers and partitioned login limiting.
- Explicit advisor POST with single-flight, evidence-version cache, timeout, circuit breaker and audit.
- Release build succeeds with warnings-as-errors; 59 tests pass locally.

## PR #28 — M3/M4/M5 batch — CI VERIFIED

- Stable PR #28 completed 25 tracked tasks from M3-005 through M5-007.
- Incident workflow now supports bounded queries, summaries, filters, details and administrator-only antiforgery-protected acknowledge/resolve/reopen transitions.
- Deterministic recommendations are rule-owned, human-reviewed and have no SQL execution path.
- The AI Advisor backend boundary is normalized and registered with a disabled-by-default provider; no external network/model call is enabled.
- Snapshot history is bounded to allowlisted aggregate facts with 24-hour / 288-point retention, timestamp dedupe and per-server isolation.
- Collection cycle and fixed-window trends exist, while background scheduling remains disabled and no hosted timer is registered.
- CI run `31375034604`: SUCCESS — Release build 0 warnings / 0 errors; 54/54 tests passed; Razor views compiled in Release.

## M3 — Incidents and Recommendations — VERIFIED THROUGH M3-016

- M3-001 through M3-004: CI `31373849952`.
- M3-005 through M3-016: CI `31375034604`.
- Operator transitions remain explicit commands and recommendations remain advisory-only.

## M4 — AI Advisor Boundary — VERIFIED THROUGH M4-006

- M4-001 through M4-006: CI `31375034604`.
- Provider remains disabled by default.
- No AI output can reach SQL execution, incident mutation or collector configuration.

## M5 — History and Operational Hardening

- M5-001 through M5-007: CI `31375034604`.
- History/trend reads are bounded and read-only.
- Scheduler policy is validated but disabled; no hosted background timer is active.
- **Next:** M5-008 — immutable operator audit trail for protected incident transitions.

## Earlier verified baseline

- M2-003 through M2-007: CI `31372957383` — 38/38 tests, 0 build warnings/errors.
- M2-008 through M2-013 and M3-001 through M3-004: CI `31373849952` — 45/45 tests, 0 build warnings/errors.
- M1 first real SQL vertical slice is verified; SignalR remains intentionally deferred by ADR-013.
- SQL Connection Lab is merged into stable `main` and preserves the external-secret boundary.

## Stable architecture guardrails

- Browser/UI components never connect directly to monitored SQL Servers.
- Credentials remain outside browser models and repository registrations.
- Snapshot cache is the shared read boundary for monitoring and incident evidence.
- Stale/failed evidence cannot resolve incidents.
- Recommendations and Advisor output are human-review only and cannot execute production SQL.
- History excludes endpoints, credentials, SQL text, provider errors and other sensitive/raw payloads.
- Mock/development values are never presented as production facts.

## Verification evidence

- PR #20 CI `31372957383`: 38 passed; 0 failed; Release build 0 warnings / 0 errors.
- PR #24 CI `31373849952`: 45 passed; 0 failed; Release build 0 warnings / 0 errors.
- PR #28 CI `31375034604`: 54 passed; 0 failed; 0 skipped; Release build 0 warnings / 0 errors.
- M0 visual acceptance: USER ACCEPTED on 2026-08-10.

## Merge gate

This reconciliation is documentation-only. Run GitHub Actions on the final docs head, confirm the branch remains clean against `main`, then merge.

## Next action

Begin M5-008 after reconciliation: create an immutable bounded audit trail for successful protected incident state transitions, capturing authenticated operator identity and before/after state without storing credentials, SQL text, provider errors or unrestricted evidence.
