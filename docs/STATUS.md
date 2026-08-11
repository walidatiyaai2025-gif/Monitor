# Project Status

**Updated:** 2026-08-11 09:22 +03:00  
**Branch:** `agent/b300`  
**Target:** BATCH-300 — SQL Estate Intelligence & Safe Operations  
**Issue:** #97 · **PR:** #102  
**Overall:** 🟢 M0–M8 VERIFIED · 🟢 BATCH-100 100/100 COMPLETE · 🟢 BATCH-200 100/100 COMPLETE · 🟢 BATCH-300 B300-001..100 CI VERIFIED

## BATCH-300 — 100-task implementation verification

- GitHub Actions implementation run: `31464569180`.
- Release build: **Green — 0 warnings / 0 errors** with `--warnaserror`.
- Tests: **390/390 passed; 0 failed** before concurrent-main reconciliation.
- B300-specific acceptance suite: **100 mapped tests** for B300-001..100.
- B300-001..100: **CI VERIFIED**.
- Final merge is gated on a second CI pass after reconciling concurrent team changes from `main`.

## Concurrent team additions — preserved outside B300-001..100 ledger

### Daily target lifecycle — LOCAL VERIFIED

- Administrators can pause and resume each registered target from Connection Lab.
- Pausing persists `IsEnabled=false`, evicts the cached snapshot and prevents an older in-flight collection from republishing evidence.
- Resuming preserves registration ID, endpoint, credential reference, creation time, history and incidents.
- Repeated commands are idempotent; committed transitions emit bounded audit metadata.
- Local Release gate recorded by the contributing team: 0 warnings / 0 errors; 293/293 tests passed.

### Protected credential reconnect — LOCAL VERIFIED

- Administrators can provide a new write-only SQL username/password for the existing target.
- The encrypted candidate is tested before registration metadata changes.
- Failed/cancelled candidates are compensated; the previous reference remains active.
- A successful replacement preserves registration ID, endpoint, timestamps, history and incidents, then removes the old Monitor-owned secret when safe.
- Local Release gate recorded by the contributing team: 0 warnings / 0 errors; 295/295 tests passed.

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
- Fail-closed BATCH-300 release invariants plus Read-policy-protected `/intelligence/contract` endpoint.

## BATCH-200 final verification

- GitHub Actions final release-candidate run: `31446970475`.
- Release build: **Green** with `--warnaserror`.
- Tests: **290/290 passed; 0 failed**.
- B200-001..100: **CI VERIFIED**.
- BATCH-200: **100/100 COMPLETE**.

## Stable guardrails

- Navigation, reporting, diagnostics, fleet, help, readiness and BATCH-300 intelligence GETs do not initiate monitored-SQL collection.
- No browser connects directly to monitored SQL.
- No autonomous remediation or AI SQL execution.
- Credentials/full connection strings/raw provider errors/SQL text remain outside UI, audit, exports and diagnostics.
- Suppression does not rewrite incident evidence.
- Maintenance affects scheduled collection only; manual refresh is explicit and audited.
- Mutations remain POST + antiforgery + named authorization policy.
- MultiNode remains fail-closed behind shared-state/security/credential readiness.
- Concurrent team lifecycle/reconnect work is preserved during BATCH-300 reconciliation.
