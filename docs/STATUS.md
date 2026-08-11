# Project Status

## CURRENT P0 — Real SQL Production MVP

**Updated:** 2026-08-11 12:16 +03:00  
**Branch:** `main`  
**Umbrella:** #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Priority PR:** #117 MERGED — `3674b370ca485c8fd86639f82f2a22e32bd2dacc`  
**Priority-plan CI:** `31474556468` — Green  
**P0.1 implementation PR:** #119 MERGED — `57ab5cae6b5bdd3a04adb5069008aae80a1f84e0`  
**P0.1 implementation CI:** `31476430643` — Release build 0 warnings / 0 errors; **501/501 tests passed**  
**P0.1 final code+docs CI:** `31476747212` — Release build 0 warnings / 0 errors; **501/501 tests passed**  
**Active next gate:** #113 / P0.2 First Real Snapshot + Truthful Mapping  
**Production target:** first trustworthy IIS/HTTPS SingleNode release after #113 -> #114 -> #115 -> #116.

### P0.1 sprint result — COMPLETE

- Initial SQL registration now tests the candidate before durable registration commit; failed safe connection tests no longer leave a normal enabled target as a side effect.
- A newly-created Monitor-owned credential is compensated on failed/cancelled initial Test Connection and on durable-registration commit failure.
- External secret references are not mutated by failed initial registration.
- SQL passwords remain write-only and are cleared from failed/cancelled controller flows.
- Integrated Security continues without creating a credential reference.
- Successful Test Connection commits the registration before first snapshot publication; a later snapshot-permission failure retains the successfully connected durable target and reports monitoring-data unavailability explicitly.
- Existing durable registration tests prove restart reload; protected secret-store tests prove encrypted credential resolution across store/key-ring restart without plaintext on disk.
- New real-server-journey tests prove the repository is still empty while the candidate Test Connection executes, plus failure/cancellation cleanup, external-reference preservation and Integrated Security behavior.
- Implementation CI `31476430643`: Release build **0 warnings / 0 errors**, **501/501 passed**, 0 failed, 0 skipped.
- Final code+docs CI `31476747212`: Release build **0 warnings / 0 errors**, **501/501 passed**, 0 failed, 0 skipped.
- PR #119 squash-merged to `main` as `57ab5cae6b5bdd3a04adb5069008aae80a1f84e0`.
- Issue #112 closed — completed.

### Management decision

- The repository has a strong verified platform foundation, but the immediate delivery objective remains the end-to-end real SQL production journey rather than additional feature breadth.
- Until P0.5 is accepted, production-slice blockers are higher priority than unrelated B300/B400 expansion.
- A production-visible value must be backed by collected evidence. Missing/uncollected data is rendered explicitly; placeholder numeric zero is not acceptable as observed production data.
- P0.1 registration ordering/credential-compensation blocker is resolved and merged.
- **P0.2 / #113 is now ACTIVE / NEXT.** Current `ServerCard` projection sets CPU to `0` although CPU is not collected by the bounded snapshot contract, and collected SQL Agent evidence is not projected into the card used by Server Details. These are the next production-trust blockers.
- Real-server acceptance is mandatory in #115; deterministic/fake-based CI alone does not close the production gate.
- First production deployment is deliberately SingleNode; MultiNode activation is deferred until after the first stable production release.

### P0 release chain

| Order | Release | Issue | State |
|---|---|---|---|
| 1 | P0.1 Real SQL Registration | #112 | COMPLETE — PR #119 MERGED / FINAL CI GREEN |
| 2 | P0.2 First Real Snapshot + truthful mapping | #113 | ACTIVE / NEXT |
| 3 | P0.3 Server Details v0.1 source of truth | #114 | BLOCKED BY #113 |
| 4 | P0.4 Real SQL end-to-end acceptance | #115 | BLOCKED BY #114 |
| 5 | P0.5 First Production SingleNode | #116 | BLOCKED BY #115 |

**Overall:** 🟢 verified foundation · 🟢 P0.1 COMPLETE · 🟡 P0.2 ACTIVE · 🔴 production acceptance not yet granted

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

## Verified historical baseline

**Prior canonical update:** 2026-08-11 10:15 +03:00  
**Prior target:** BATCH-400 — Production DBA Diagnostics & Decision Safety COMPLETE  
**Prior issues:** #108 CLOSED · **PR:** #109 MERGED  
**Foundation:** 🟢 M0–M8 VERIFIED · 🟢 BATCH-100 100/100 COMPLETE · 🟢 BATCH-200 100/100 COMPLETE · 🟢 BATCH-300 100/100 COMPLETE · 🟢 BATCH-400 B400-001..110 COMPLETE

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
