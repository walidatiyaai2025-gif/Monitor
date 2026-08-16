# BATCH-700 — Full UI Completion

**Parent:** #220  
**Foundation:** #221  
**Health:** #222  
**Audit/history:** #223  
**Recommendations/reports:** #224  
**Enterprise/admin/final:** #225

## Objective

Close the gap between backend capability completion and a complete visible operator product. A route is not considered UI-complete merely because a Razor view exists.

## Non-negotiable boundaries

- Browser GETs for monitored data remain cache/control-plane only.
- Missing evidence is explicit; no synthetic zero or fake health state.
- No autonomous remediation or AI SQL execution.
- No credential, connection-string, SQL text, raw provider-error, filesystem-path, or exception-detail disclosure.
- Mutations retain role policies and antiforgery.
- Desktop, tablet, and 390px mobile are acceptance targets.
- Status uses text as well as color; keyboard/focus behavior is required.

## Batch status

### Foundation #221 — COMPLETE
- [x] UI700-001..010 — PR #236 merged as `59a931cc031e19f162edfadc278dc8b9c6c842e3`; final exact head `32dbcd56b14a58ebb193ef81c8fa9c715c31feb8` passed `ci` #1571, `real-sql-acceptance` #88, and `production-candidate` #133.

### Health #222 — COMPLETE
- [x] UI700-011 — dedicated Database Health surface.
- [x] UI700-012 — dedicated Backup Health surface.
- [x] UI700-013 — dedicated SQL Agent surface.
- [x] UI700-014 — dedicated Storage Allocation surface.
- [x] UI700-015 — dedicated Blocking surface.
- [x] UI700-016 — complete cache-only Performance dashboard.
- [x] UI700-017 — shared live/stale/unavailable/demo source states.
- [x] UI700-018 — server-detail drill-down from health pages.
- [x] UI700-019 — role-aware actionable empty states.
- [x] UI700-020 — regression/build acceptance; PR #237 merged as `308a2f31a42500ce7354b1af2c2369d59be57455`, exact head `fa0353431bc02abbc7cf520fec04adf5418ecfc6`, `ci` #1590 and `production-candidate` #134 Green. Real-SQL workflow was not applicable under its path filter.

### Audit / history #223 — IMPLEMENTED / CI PENDING
- [x] UI700-021 — Audit empty state and bounded paging controls.
- [x] UI700-022 — safe actor/action/outcome filters over the already-bounded audit page.
- [x] UI700-023 — Audit hierarchy, outcome badges, semantic time and mobile layout.
- [x] UI700-024 — History bounded window selector.
- [x] UI700-025 — History page-size and Previous/Next controls.
- [x] UI700-026 — History server context/back-link with explicit missing/stale presentation.
- [x] UI700-027 — History empty state.
- [x] UI700-028 — evidence-only history summary cards.
- [x] UI700-029 — bounded store/control-plane GET contract preserved; no monitored SQL collection dependency.
- [ ] UI700-030 — exact-head regression/build acceptance must be Green before #223 closes and PR #238 merges.

## Closure rule

Do not mark BATCH-700 complete from task bookkeeping alone. Every child batch requires its exact implementation PR, Release build warnings-as-errors, full tests, route/policy acceptance, and final docs reconciliation. BATCH-700 never substitutes for external production acceptance #116/#111.
