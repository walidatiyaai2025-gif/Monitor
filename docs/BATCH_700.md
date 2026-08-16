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
- [x] UI700-001..010 — PR #236 merged as `59a931cc031e19f162edfadc278dc8b9c6c842e3`; exact head `32dbcd56b14a58ebb193ef81c8fa9c715c31feb8` passed `ci` #1571, `real-sql-acceptance` #88 and `production-candidate` #133.

### Health #222 — COMPLETE
- [x] UI700-011..020 — PR #237 merged as `308a2f31a42500ce7354b1af2c2369d59be57455`; exact head `fa0353431bc02abbc7cf520fec04adf5418ecfc6` passed `ci` #1590 and `production-candidate` #134. Real-SQL was not applicable under its path filter.

### Audit / history #223 — COMPLETE
- [x] UI700-021..030 — PR #238 merged as `3864b4f8acc14d6e0bd259bfb1ab52d9fec07be1`; final synchronized head `473944f21ce4cabb0b96f6040edf5992605930b5` passed `ci` #1617 and `production-candidate` #135. Real-SQL was not applicable.

### Recommendations / reports #224 — IMPLEMENTED / CI PENDING
- [x] UI700-031 — bounded recommendation summary plus severity and exact normalized rule filters.
- [x] UI700-032 — semantic ordered guidance with explicit risk/caution hierarchy.
- [x] UI700-033 — incident and server evidence drill-down.
- [x] UI700-034 — no-data/filter-empty/mobile recommendation states.
- [x] UI700-035 — report format/version/access/scope metadata.
- [x] UI700-036 — standard reports separated from Administrator diagnostics.
- [x] UI700-037 — safe failure/permission disclosure without sensitive provider details.
- [x] UI700-038 — accessible download labels and bounded/redacted disclosures.
- [x] UI700-039 — Read/Manage policy regression coverage.
- [x] UI700-040 — global export discoverability plus contextual stored-history export.
- [ ] Exact-head applicable GitHub Actions must be Green before #224 closes and PR #239 merges.

### Enterprise / admin / final #225 — IN PROGRESS
Implementation is isolated in PR #240. Do not mark UI700-041..050 complete until its final synchronized head is Green and canonical docs are reconciled.

## Closure rule

Do not mark BATCH-700 complete from task bookkeeping alone. Every child batch requires its exact implementation PR, Release build warnings-as-errors, full tests, route/policy acceptance, and final docs reconciliation. BATCH-700 never substitutes for external production acceptance #116/#111.
