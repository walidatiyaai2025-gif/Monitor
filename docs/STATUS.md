# Project Status

**Updated:** 2026-08-10 12:34 +03:00  
**Branch:** `agent/m3-005-recommendation-engine`  
**Target:** M3-005 — Deterministic recommendation engine  
**Issue:** #27  
**PR:** TBD  
**Overall:** 🟡 M3-005 IMPLEMENTED — CI PENDING

## M3-005 — Deterministic recommendation engine

- Added immutable `HealthRecommendation`, ordered remediation-step and diagnostic-SQL proposal contracts.
- Added `IHealthRecommendationService` with deterministic mappings for every currently allowlisted health rule: stale snapshot, unavailable/suspect databases, backup gap, Agent failure, blocking, memory pressure and runnable-task pressure.
- Unsupported rule IDs fail closed with no invented recommendation.
- Recommendation evidence is never interpolated into SQL.
- Optional diagnostic SQL is application-owned, fixed and read-only. It intentionally excludes modification/repair/job-execution commands.
- Added an authorized read-only recommendation route from the incident center.
- The recommendation page shows problem, bounded evidence, confidence/rationale, ordered DBA steps and optional diagnostic SQL with a visible advisory-only boundary.
- Monitor exposes no endpoint to execute the recommendation SQL or remediation steps.
- Existing incident center copy was reconciled from the old development-preview wording to the real cached deterministic incident pipeline.
- Tests cover all current rule mappings, unsupported-rule behavior, SQL non-interpolation/read-only guardrails and recommendation lookup without creating a SQL collection target.
- ADR-018 records the advisory/non-executable recommendation boundary.

## M2 — Health Modules — CI VERIFIED THROUGH M2-013

- M2-001: CI `31372045546`.
- M2-002: CI `31372312362`.
- M2-003 through M2-007: CI `31372957383` — 38/38 tests, 0 build warnings/errors.
- M2-008 through M2-013: CI `31373849952` — part of the 45/45-test M2/M3 integration run.

## M3 — Incidents and Recommendations

- M3-001 through M3-004: CI `31373849952` — 45/45 tests, 0 build warnings/errors.
- M3-005: implementation complete; GitHub Actions verification pending.

## Stable architecture guardrails

- Browser/UI components never connect directly to monitored SQL Servers.
- Credentials remain outside browser models and repository registrations.
- Snapshot cache remains the shared read boundary for monitoring and incident surfaces.
- Findings and recommendations are deterministic and allowlisted.
- Browser/evidence input cannot provide or modify diagnostic SQL.
- Recommendation SQL is display-only; no autonomous remediation or production mutation exists.
- AI remains a later advisory layer over normalized evidence, not an execution path.

## Merge gate

Open the M3-005 PR and require GitHub Actions restore, Release build with warnings-as-errors and all tests to pass. Reconcile against any newer `main` changes before merge.

## Next action

Run CI on the complete M3-005 implementation, fix any build/Razor/test regression, then record the CI receipt and merge only when the branch remains clean.
