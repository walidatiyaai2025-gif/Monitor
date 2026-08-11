# Project Status

## BATCH-400 — Production DBA diagnostics continuation

- Issue #108 delivered **100 additional code tasks B400-011..110**, preserving the portal/typography work already merged by PR #107 as B400-001..010.
- Added deterministic wait-stat intelligence, query-regression scoring, TempDB pressure, transaction-log health, I/O latency, SQL Agent reliability, HA readiness, maintenance decision safety and fleet signal correlation.
- Added the Read-policy-protected `/intelligence/v2/contract` endpoint and a fail-closed 100-task continuation release contract.
- Clean implementation CI on top of PR #107: `31467831498` — Release build **0 warnings / 0 errors**, **498/498 tests passed**.
- Final PR CI on merge ref: `31468048589` — Release build **0 warnings / 0 errors**, **498/498 tests passed**.
- PR #109: **squash-merged** to `main` as `9345c4ca8b67e617a9aa9580bbb481819e5babb7`.
- Issue #108: **closed — completed**.
- B400-011..110: **100/100 COMPLETE** with 100 mapped acceptance tests.

## BATCH-400 — Portal completion and Google typography

- Added dedicated Performance Health, Recommendations, and Reports & Diagnostics pages.
- Reorganized navigation around Operations, Health, Intelligence, Administration, and contextual Help.
- Connected previously orphaned Fleet, Help, Readiness, Audit, History, and enterprise export capabilities.
- Made management links role-aware and removed the dead standalone AI Advisor link.
- Adopted self-hosted Google Fonts: Inter Variable for the Latin UI and Noto Sans Arabic Variable for Arabic glyphs.
- Kept the strict CSP by serving font assets locally with `font-src 'self'`.
- Added bounded desktop/mobile sidebar scrolling while preserving the existing command-center visual identity.
- Local verification: Release build **0 warnings / 0 errors**, **398/398 tests passed**, desktop and 390px browser acceptance passed with no console warnings/errors.
- State: **MERGED — PR #107**.

**Updated:** 2026-08-11 10:15 +03:00  
**Branch:** `main`  
**Target:** BATCH-400 — Production DBA Diagnostics & Decision Safety COMPLETE  
**Issues:** #108 CLOSED · **PR:** #109 MERGED  
**Overall:** 🟢 M0–M8 VERIFIED · 🟢 BATCH-100 100/100 COMPLETE · 🟢 BATCH-200 100/100 COMPLETE · 🟢 BATCH-300 100/100 COMPLETE · 🟢 BATCH-400 B400-001..110 COMPLETE

## BATCH-300 final verification

- Implementation CI: `31464569180` — Release build **0 warnings / 0 errors**, **390/390 tests passed**.
- Reconciled final CI after preserving concurrent `main` work: `31465013971` — Release build **0 warnings / 0 errors**, **395/395 tests passed**.
- B300-specific acceptance coverage: **100 mapped tests**, one for every B300-001..100 task.
- PR #102: **squash-merged** to `main` as `385c2ee7a4d592c1e32e6e00a5c533c8790963b6`.
- Issue #97: **closed — completed**.
- BATCH-300: **100/100 COMPLETE**.

## Concurrent team additions — preserved outside B300-001..100 ledger

### Daily target lifecycle

- Administrators can pause and resume each registered target from Connection Lab.
- Pausing persists `IsEnabled=false`, evicts the cached snapshot and prevents an older in-flight collection from republishing evidence.
- Resuming preserves registration ID, endpoint, credential reference, creation time, history and incidents.
- Repeated commands are idempotent; committed transitions emit bounded audit metadata.

### Protected credential reconnect

- Administrators can provide a new write-only SQL username/password for the existing target.
- The encrypted candidate is tested before registration metadata changes.
- Failed/cancelled candidates are compensated; the previous reference remains active.
- A successful replacement preserves registration ID, endpoint, timestamps, history and incidents, then removes the old Monitor-owned secret when safe.

## BATCH-300 delivered

- Defensive SQL estate identity/version/edition/uptime primitives with opaque stable identifiers.
- Bounded capacity growth and threshold forecasting helpers.
- Recovery-model-aware full/log backup compliance scoring and reasons.
- Deterministic database state, availability and failover-readiness intelligence.
- Runtime memory/blocking/scheduler/I/O pressure scoring and hotspot detection.
- Age/suppression/maintenance-aware fleet risk aggregation.
- Safe deterministic alert routing, escalation, cooldown and deduplication policy helpers.
- Operator input safety, secret-shape detection, formula neutralization, fingerprints and diagnostics allowlists.
- Versioned bounded UTF-8 export contracts with SHA-256 checksums and deterministic ordering.
- Fail-closed release invariants plus Read-policy-protected `/intelligence/contract` endpoint.

## BATCH-200 final verification

- GitHub Actions final release-candidate run: `31446970475`.
- Release build: **Green** with `--warnaserror`.
- Tests: **290/290 passed; 0 failed**.
- B200-001..100: **CI VERIFIED**.
- BATCH-200: **100/100 COMPLETE**.

## Stable guardrails

- Navigation, reporting, diagnostics, fleet, help, readiness and intelligence GETs do not initiate monitored-SQL collection.
- No browser connects directly to monitored SQL.
- No autonomous remediation or AI SQL execution.
- Credentials/full connection strings/raw provider errors/SQL text remain outside UI, audit, exports and diagnostics.
- Suppression does not rewrite incident evidence.
- Maintenance affects scheduled collection only; manual refresh is explicit and audited.
- Mutations remain POST + antiforgery + named authorization policy.
- MultiNode remains fail-closed behind shared-state/security/credential readiness.
- Concurrent team lifecycle/reconnect and portal/typography work remain preserved.
