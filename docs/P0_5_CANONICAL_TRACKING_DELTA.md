# P0.5 Canonical Tracking Delta

This file records the exact canonical tracking delta after Issue #162 / PR #163 and the merged operator handoff PR #164.

## Current truth

- Selected cutover candidate remains **RC.61**.
- Issue #159 / PR #160 completed durable publication hardening for future pushed version tags.
- Issue #162 implementation is complete via PR #163, squash-merged as `43d8a193205495f155bb8866532a4e99ed93b655`.
- PR #164 merged as `930c057f431a36ab2b603d3dc39e70e8c31c744e` after exact-head normal CI `31726008394` and Windows production-candidate `31726008464` were Green.
- `promote-existing-candidate` is active but has not been manually dispatched.
- `v0.1.0-rc.61` is not yet present as a GitHub Release.
- Issue #162 remains OPEN until the manual promotion run succeeds and the tag, exactly two release assets, and product SHA-256 are independently verified.
- Issue #168 is **COMPLETE** via PR #171, squash-merged as `c9084dd32b12a9a078f953f85f39b253793e2343`. Exact implementation head `052e969b5ab450526ab996a2e77459f4087846c8` passed normal CI `31881105832`, Real SQL `31881105877`, and Windows production-candidate `31881105818` end-to-end. Every active external Action is pinned to an approved immutable commit SHA, a fail-closed regression test owns the pin allowlist, and the completed BATCH-100 one-shot write-capable merge workflow is removed. This hardening does not change RC.61 and does not satisfy #162/#116/#111 external/manual gates.
- Issues #116 and #111 remain OPEN; no external IIS gate is satisfied by release-retention or workflow-supply-chain work.

## Canonical reconciliation state

This reconciliation updates `docs/STATUS.md` and `docs/FEATURE_CATALOG.md` directly so both express the current retention and repository-hardening boundary:

1. repository release/durable-tag tooling is complete through #159 / PR #160;
2. existing selected-candidate promotion capability is implemented through #162 / PR #163;
3. the dedicated handoff docs are merged through PR #164;
4. RC.61 publication is still pending manual dispatch;
5. Issue #168 / PR #171 supply-chain hardening is complete and merged as `c9084dd32b12a9a078f953f85f39b253793e2343`, with normal CI `31881105832`, Real SQL `31881105877`, and Windows production-candidate `31881105818` Green on exact implementation head `052e969b5ab450526ab996a2e77459f4087846c8`;
6. RC.61 remains selected unless #116 explicitly selects another equivalently verified candidate;
7. real Windows/IIS 15/15 acceptance remains pending external.

`docs/IMPLEMENTATION_PLAN.md` already records durable-tag tooling through #159 / PR #160 and remains supplemented by this delta plus `docs/P05_EXISTING_CANDIDATE_PROMOTION.md` for the exact #162 publication state. The connected contents API requires complete-file replacement, while the full plan exceeds the safe complete-file response budget; it is therefore not rewritten here rather than risking truncation.

This delta does not grant production acceptance, does not close #162/#116/#111, and does not promote a later candidate over RC.61.
