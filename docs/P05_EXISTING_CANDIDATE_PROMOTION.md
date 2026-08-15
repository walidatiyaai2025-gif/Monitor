# P0.5 Existing Candidate Durable Promotion

Issue: #162  
Parent gate: #116 / #111  
Selected candidate: `Monitor-0.1.0-rc.61-win-x64.zip`

## Current state

Implementation is **COMPLETE** via PR #163, with subsequent fail-closed hardening in #202/#204. The durable promotion itself remains **PENDING MANUAL DISPATCH**. Issue #162 must remain OPEN until the manual workflow runs successfully and the two durable GitHub Release assets are independently verified.

This is retention/recoverability hardening only; it does not rebuild the application, select a different candidate, deploy IIS, or satisfy any external production gate.

## Selected RC.61 identity

- version: `0.1.0-rc.61`
- source workflow run: `31667721306`
- Actions artifact ID: `9168574442`
- outer Actions artifact digest: `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`
- source head: `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`
- tested merge: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- product SHA-256: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`
- release tag: `v0.1.0-rc.61`
- observed Actions artifact expiry: `2026-09-12T04:41:34Z`

## Exact manual dispatch inputs

Run `.github/workflows/promote-existing-candidate.yml` from `main` with **exactly**:

- `candidate_version`: `0.1.0-rc.61`
- `source_run_id`: `31667721306`
- `source_artifact_id`: `9168574442`
- `expected_outer_artifact_digest`: `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`
- `expected_product_sha256`: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`
- `source_commit`: `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`
- `tested_merge_commit`: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- `release_tag`: `v0.1.0-rc.61`
- `acknowledge_promotion`: `true`

Do not substitute a later candidate, workflow run, artifact, digest, commit, checksum or tag under Issue #162.

## Promotion contract

`.github/workflows/promote-existing-candidate.yml` is manual-only and requires explicit acknowledgement plus the exact candidate version, source run, artifact ID, outer Actions artifact digest, expected product hash, source head, tested merge and release tag.

The workflow:

1. requires a completed successful `production-candidate.yml` source run with the expected source head;
2. requires source-run repository and head-repository identity to equal the current Monitor repository, rejecting fork/cross-repository provenance;
3. resolves the exact artifact ID and independently binds its run ID, head SHA, repository IDs, exact artifact name, non-expired state, positive size and outer `sha256:` digest;
4. downloads only that artifact from the selected source run;
5. calls `scripts/Test-ExistingCandidatePromotion.ps1` to verify the exact two-file payload, canonical checksum, product SHA-256, embedded release-manifest identity and fail-closed ZIP safety rules;
6. rejects unsafe Windows paths, reserved device names, Unicode/case collisions, symlink/reparse entries, oversized/overpopulated archives and suspicious compression ratios before reading the manifest;
7. creates or verifies `v<version>` against the embedded tested merge SHA;
8. accepts an existing release only when it contains exactly the same product ZIP and checksum bytes;
9. never runs build, publish, compression or repackaging in the promotion operation.

### ZIP resource/safety ceilings

Promotion rejects candidates exceeding any of these conservative limits:

- more than 4096 ZIP entries;
- any entry over 256 MiB uncompressed;
- more than 1 GiB total uncompressed bytes;
- compression ratio above 200:1 for an entry of at least 1 MiB;
- normalized path length over 240 characters.

The selected RC.61 package is far below these ceilings: 95 entries, about 12.7 MB total uncompressed, largest entry about 1.85 MB, longest path 62 characters, and observed maximum ratio about 3.3:1.

## Closure evidence required for #162

Do not close #162 merely because repository hardening merged. Close it only after all of the following are true:

- the manual `promote-existing-candidate` run completed successfully using the exact outer artifact digest above;
- tag `v0.1.0-rc.61` resolves to tested merge `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- the release contains exactly `Monitor-0.1.0-rc.61-win-x64.zip` and `Monitor-0.1.0-rc.61-win-x64.zip.sha256`;
- the durable ZIP SHA-256 is exactly `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- the companion checksum line matches the exact ZIP name and product hash;
- no rebuild, repackaging or alternate candidate was introduced.

## External acceptance boundary

Durable publication is not production acceptance. #116 and #111 stay open until the intended trusted-certificate Windows/IIS SingleNode target produces reviewed real 15/15 external evidence and explicit operator finalization.
