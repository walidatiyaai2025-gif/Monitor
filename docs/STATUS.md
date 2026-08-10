# Project Status

**Updated:** 2026-08-10 18:08 +03:00  
**Branch:** `agent/b100-051-060-dba-ux`  
**Target:** BATCH-100 / Batch 6 — DBA UX & operations surfaces  
**Issues:** #55 umbrella · #66 Batch 6  
**PR:** #67  
**Overall:** 🟢 M0–M6 VERIFIED · M7-001..M7-018 CI VERIFIED · M8 CI VERIFIED · B100-001..060 CI VERIFIED

## BATCH-100 / Batch 6 — CI VERIFIED

Authoritative implementation CI `31402491011`: **SUCCESS — Release build 0 warnings / 0 errors; 189/189 tests passed; Razor compiled.**

The first implementation run `31402167135` correctly stopped on a missing `Monitor.Web.Models` import for the new DBA operations projection. Run `31402312095` then reached the test project and found one incomplete `DashboardViewModel` fixture. Both issues were corrected on the same PR before verification; the product code and final acceptance suite are Green.

### B100-051..060 delivered

- Dashboard now has a centralized control-plane readiness ribbon and DBA cards for topology/node, shared state, operational backup and scheduler state.
- Node identity is deliberately opaque: a SHA-256-derived `NODE-XXXXXXXX` label is shown instead of the machine name, configured distributed node ID or lease owner.
- Shared-state status/schema reuses the single application-readiness snapshot rather than issuing a second readiness probe.
- Backup card exposes status, retained count, latest opaque backup ID fragment and time only; no filesystem path or secret-bearing content is rendered.
- Scheduler card exposes Disabled / Active cycle / Passive-idle plus bounded counts; distributed lease ownership remains private.
- Manual refresh PRG now carries status/freshness classification so refreshed, stale and throttled outcomes have distinct accessible feedback.
- Registered servers without a usable snapshot now open a recovery-aware Server Details page instead of returning 404. Recovery links to Connection Lab and the existing bounded refresh path without exposing current secret references or credential values.
- Incident Center adds bounded status/severity/rule/page-size filters and Previous/Next navigation that preserves safe query state.
- Application shell adds a skip link, focus-visible treatment, main-content focus target, semantic live-status regions and Administrator Observability navigation.
- Reduced-motion preferences suppress decorative animation, and large-screen DBA wallboard layout is CSS-only; no polling, network fetch or monitored-SQL behavior is added.

## BATCH-100 progress

Issue #55 and `docs/BATCH_100.md` define 100 tasks as ten batches of ten. **60/100 tasks are CI verified.** Batch 7 is B100-061..070 — web/application security hardening.

## Stable guardrails

- Dashboard DBA cards use cached/control-plane projections only and do not query monitored SQL targets.
- Control-plane status is centralized so shared-state readiness is not multiplied per widget.
- Recovery actions never render current SQL usernames/passwords or secret references.
- Visual wallboard/reduced-motion/accessibility changes are client/CSS behavior only and never affect collection frequency.
- Dedicated shared-state SQL remains Monitor-owned control-plane state only.
- Recommendations and Advisor remain advisory-only; no autonomous SQL execution path exists.
- MultiNode remains fail-closed until all remaining security/cache/delivery prerequisites are proven HA-safe.
- `main` remains stable; batch work merges only after final merge-result CI.

## Merge gate

Run GitHub Actions on the final code + canonical-docs head, verify `main` has not moved into overlapping DBA UX/control-plane code, then squash-merge PR #67 only if Release build with warnings-as-errors, Razor compilation and all tests remain Green.

## Next action

After Batch 6 merge, execute **B100-061..070 — web/application security hardening** from #55 / `docs/BATCH_100.md`.
