# Project Status

## CURRENT P0 — Real SQL Production MVP

**Updated:** 2026-08-11 12:46 +03:00  
**Branch:** `agent/p0-3-server-details-source-of-truth`  
**Umbrella:** #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**P0.1:** COMPLETE — PR #119 / final CI `31476747212` / 501/501  
**P0.2:** COMPLETE — PR #121 MERGED `a294c6530d60f17e7c60e3a1ac070ce562af7b18` / final CI `31478470867` / 505/505  
**P0.3 implementation PR:** #122  
**P0.3 implementation CI:** `31479005170` — Release build 0 warnings / 0 errors; **507/507 tests passed**  
**Next gate after #122 merge:** #115 / P0.4 Real SQL End-to-End Acceptance  
**Production target:** first trustworthy IIS/HTTPS SingleNode release after #115 -> #116.

### P0.3 sprint result — IMPLEMENTATION CI VERIFIED

- Server Details is now an evidence-first DBA page rather than a composite scorecard.
- The synthetic numeric Health Score was removed. The page shows explicit availability state, cache freshness, collected-at timestamp and snapshot age instead.
- Instance name, SQL version, edition and uptime are surfaced from the cached snapshot evidence.
- Database availability includes online/total plus restoring, recovering, recovery pending, suspect, emergency and offline/other problem-state counts.
- Memory surfaces SQL process utilization/working-set evidence plus total/available physical memory and low-memory flags when collected; otherwise it says `Not collected`.
- Backup evidence surfaces the ≤24h covered count, missing full backups and the last observed full backup timestamp.
- SQL Agent exposes actual total/enabled/failed-last-run facts and explicitly avoids a synthetic healthy-job count.
- Storage allocation, blocked-request/max-wait evidence and active/runnable/pending-I/O runtime facts are visible on the same page.
- CPU remains explicitly `Not collected` because it is outside the v0.1 bounded snapshot contract; no proxy signal is substituted.
- Registered targets without a usable snapshot retain a clear recovery path; manual refresh remains an explicit protected POST.
- New P0.3 source acceptance tests fail if the synthetic Health Score returns, if any required evidence module disappears, or if the page stops stating its cache-only GET boundary.
- Implementation CI `31479005170`: Release build **0 warnings / 0 errors**, **507/507 passed**, 0 failed, 0 skipped.
- Final code+docs CI remains required before PR #122 is merged and #114 is closed.

### P0.2 sprint result — COMPLETE

- P0.2 PR #121 squash-merged to `main` as `a294c6530d60f17e7c60e3a1ac070ce562af7b18`.
- Issue #113 closed — completed.
- Final code+docs CI `31478470867`: Release build **0 warnings / 0 errors**, **505/505 passed**, 0 failed, 0 skipped.
- `ServerCard.CpuPercent` and `MemoryPercent` are nullable evidence: absence is represented as absence rather than numeric zero.
- Real SQL Agent uses actual `TotalJobs`, `EnabledJobs`, and `FailedLastRun` facts; no real synthetic healthy-job count is published.
- Real cards carry instance name, uptime and collected-at evidence, while Server Details receives the safe cached envelope for memory/database/backups/Agent/storage/blocking/runtime.
- Dashboard, Servers, Server Details and Memory Health render or aggregate only observed evidence.

### P0.1 sprint result — COMPLETE

- Initial SQL registration tests the candidate before durable registration commit; failed safe connection tests no longer leave a normal enabled target as a side effect.
- A newly-created Monitor-owned credential is compensated on failed/cancelled initial Test Connection and on durable-registration commit failure.
- External secret references are not mutated by failed initial registration.
- SQL passwords remain write-only and are cleared from failed/cancelled controller flows.
- Integrated Security continues without creating a credential reference.
- Final code+docs CI `31476747212`: Release build **0 warnings / 0 errors**, **501/501 passed**, 0 failed, 0 skipped.
- PR #119 squash-merged to `main` as `57ab5cae6b5bdd3a04adb5069008aae80a1f84e0`.
- Issue #112 closed — completed.

### Management decision

- The immediate delivery objective remains the end-to-end real SQL production journey rather than additional feature breadth.
- Until P0.5 is accepted, production-slice blockers remain higher priority than unrelated feature expansion.
- Production-visible values must be backed by cached collected evidence; absent evidence is explicit and never replaced with placeholder numeric zero.
- P0.1 and P0.2 are merged and complete.
- P0.3 Server Details implementation is CI verified in PR #122 and removes the remaining source-of-truth presentation gap.
- **P0.4 / #115 becomes ACTIVE / NEXT immediately after #122 merges.** This is the first gate that cannot be closed by deterministic CI alone: the exact user journey must be exercised against a real production-like SQL Server with least-privilege permissions plus controlled authentication/network/TLS/permission failures.
- First production deployment remains deliberately SingleNode; MultiNode activation is deferred until after the first stable production release.

### P0 release chain

| Order | Release | Issue | State |
|---|---|---|---|
| 1 | P0.1 Real SQL Registration | #112 | COMPLETE — PR #119 MERGED / FINAL CI GREEN |
| 2 | P0.2 First Real Snapshot + truthful mapping | #113 | COMPLETE — PR #121 MERGED / FINAL CI GREEN |
| 3 | P0.3 Server Details v0.1 source of truth | #114 | CI VERIFIED — PR #122 / FINAL CODE+DOCS CI PENDING |
| 4 | P0.4 Real SQL end-to-end acceptance | #115 | READY / NEXT AFTER #122 MERGE |
| 5 | P0.5 First Production SingleNode | #116 | BLOCKED BY #115 |

**Overall:** 🟢 verified foundation · 🟢 P0.1 COMPLETE · 🟢 P0.2 COMPLETE · 🟢 P0.3 implementation CI verified · 🟡 P0.4 next · 🔴 production acceptance not yet granted

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
