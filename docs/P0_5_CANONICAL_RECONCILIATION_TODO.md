# P0.5 Canonical Reconciliation TODO

**Created:** 2026-08-17  
**Scope:** documentation/tracking only  
**Source of truth:** `docs/P0_5_CANONICAL_TRACKING_DELTA.md`, Issues #162/#116/#111, merged PRs #259/#262/#263

The repository-side locked-session and Acceptance Control Toolkit provenance work is complete, but three long-lived canonical surfaces still contain stale wording from before PR #259/#262 completion.

## Required reconciliation

- [ ] `docs/STATUS.md`
  - mark Issue #258 / PR #259 COMPLETE;
  - record PR #259 merge `c22c4e5e4f59576cbb41b8fc46886474f8749ebb` and exact tested source `8d79361cccf98acfc0a1753d16de943458887389`;
  - add Issue #261 / PR #262 COMPLETE with merge `a448eb715af9b3a2fcfe89ce92807b71fc7e1127` and exact tested toolkit source `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`;
  - keep #162 OPEN / manual promotion pending and #116/#111 external.

- [ ] `docs/IMPLEMENTATION_PLAN.md`
  - replace language that says #258/#259 is merely tracked with COMPLETE evidence;
  - add provenance-complete #261/#262 and the immutable toolkit source `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`;
  - preserve the manual durable-promotion boundary under #162 and the real IIS acceptance boundary under #116/#111.

- [ ] `docs/FEATURE_CATALOG.md`
  - change `Locked-session gate/finalization binding` from repository-hardening/in-progress wording to Complete;
  - add `Acceptance Control Toolkit provenance` as Complete using #261 / PR #262 evidence;
  - keep `Selected existing candidate durable promotion` as implementation complete / publication pending.

## Verified facts to preserve

- PR #259 exact source `8d79361cccf98acfc0a1753d16de943458887389` passed CI #1751 / `31991194175`, Real SQL #112 / `31991194515`, and Windows production-candidate #170 / `31991194198`; squash merge `c22c4e5e4f59576cbb41b8fc46886474f8749ebb`.
- PR #262 exact source `b422eaaee53d931a62a43b3c36a53b68cd4f3e27` passed CI #1786 / `31992503009` (984/984) and Windows production-candidate #186 / `31992502977`; squash merge `a448eb715af9b3a2fcfe89ce92807b71fc7e1127`.
- PR #263 tracking reconciliation merged `f538cb9a26aa609ece5a2ede60735218d0773973`.
- RC.61 remains selected and byte-identical; artifact `9168574442` is still unexpired and independently rechecked.
- `promote-existing-candidate` still has zero runs and release/tag `v0.1.0-rc.61` is absent at the latest 2026-08-17 check.

## Safety boundary

This reconciliation must not dispatch workflows, create or mutate a release/tag, rebuild or repackage RC.61, deploy IIS, execute SQL, mark any real production gate PASS, or close #162/#116/#111.
