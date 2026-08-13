# P0.5 Canonical Tracking Delta

This file records the exact canonical tracking delta that remains after Issue #162 / PR #163.

## Current truth

- Selected cutover candidate remains **RC.61**.
- Issue #159 / PR #160 completed durable publication hardening for future pushed version tags.
- Issue #162 implementation is complete via PR #163, squash-merged as `43d8a193205495f155bb8866532a4e99ed93b655`.
- `promote-existing-candidate` is active but has not been manually dispatched.
- `v0.1.0-rc.61` is not yet present as a GitHub Release.
- Issue #162 remains OPEN until the manual promotion run succeeds and the tag, exactly two release assets, and product SHA-256 are independently verified.
- Issues #116 and #111 remain OPEN; no external IIS gate is satisfied by release-retention work.

## Canonical files that require this state

`docs/STATUS.md`, `docs/FEATURE_CATALOG.md`, and `docs/IMPLEMENTATION_PLAN.md` should all express the same boundary:

1. repository release/durable-tag tooling is complete through #159 / PR #160;
2. existing selected-candidate promotion capability is implemented through #162 / PR #163;
3. RC.61 publication is still pending manual dispatch;
4. RC.61 remains selected unless #116 explicitly selects another equivalently verified candidate;
5. real Windows/IIS 15/15 acceptance remains pending external.

## Tooling limitation recorded

The connected GitHub contents API requires complete-file replacement for canonical Markdown updates. Full replacement of the long canonical files was rejected by the connector safety layer, so this delta is recorded explicitly instead of risking truncation or unsafe ref manipulation.

PR #164 remains the dedicated Green/mergeable handoff for `docs/P05_EXISTING_CANDIDATE_PROMOTION.md` and `deploy/RC61_DURABLE_PROMOTION.md`.
