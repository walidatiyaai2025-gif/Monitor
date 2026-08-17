# P0.5 Existing Candidate Durable Promotion

Issue: #162  
Parent gate: #116 / #111  
Selected candidate: `Monitor-0.1.0-rc.61-win-x64.zip`

## Current state

Implementation is **COMPLETE** via PR #163, with subsequent fail-closed hardening in #202/#204, independent-verification readiness in #216, and the read-only operator preflight in #266 / PR #267. The durable promotion itself remains **PENDING MANUAL DISPATCH**. Issue #162 must remain OPEN until the manual promotion workflow runs successfully **and a separate read-only `verify-durable-release` run independently verifies the resulting tag and two durable GitHub Release assets**.

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

## Step 0 — read-only fail-closed pre-dispatch verification

Before the first durable publication attempt, use a trusted authenticated operator checkout and run:

```powershell
./scripts/Test-Rc61DurablePromotionPreflight.ps1 | Format-List
```

The preflight is pinned to `walidatiyaai2025-gif/Monitor` and the exact RC.61 identity above. It requires GitHub CLI authentication, verifies the repository default branch remains `main`, rechecks the selected successful source run, verifies the artifact ID/name/source run/head/repository IDs/positive size/non-expired state/outer digest, and probes the tag and release without mutating GitHub state.

The durable-state probe is fail-closed: **only an actual 404 is treated as absence**. Authentication failures, network failures, permission failures, rate/API errors, malformed responses, or any other ambiguous probe result stop the operation rather than being interpreted as a missing tag/release.

For a **first publication attempt**, require all of these output values before continuing:

- `Status=READY_FOR_EXPLICIT_MANUAL_PROMOTION`
- `MutatedGitHubState=False`
- `TagExists=False`
- `ReleaseExists=False`

Also inspect the emitted `PromotionCommand` and `IndependentVerificationCommand`; they must contain the exact locked repository/version/run/artifact/digest/hash/source/tested-merge/tag values in this document.

If the preflight reports `DURABLE_STATE_EXISTS_VERIFY_OR_INVESTIGATE`, the selected Actions artifact is expired, provenance or digest identity drifts, or any GitHub probe is ambiguous, **stop and investigate instead of dispatching a first publication attempt**. An existing durable state must be independently verified before deciding any next action; it is not permission to overwrite or recreate assets.

A successful preflight is read-only preparation. It does not create the tag/release and does not satisfy #162, #116, or #111.

## Exact manual promotion inputs

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

## Independent read-only verification after promotion

After the promotion run is Green, run `.github/workflows/verify-durable-release.yml` **separately from `main`**. This workflow has read-only `contents: read` permissions, uses a non-persisting checkout, executes the shared `scripts/Verify-DurableRelease.sh`, stores downloaded verification bytes only under runner-temporary storage, and performs no release/tag mutation or artifact republishing.

Use **exactly** these verification inputs for RC.61:

- `release_version`: `0.1.0-rc.61`
- `release_tag`: `v0.1.0-rc.61`
- `expected_commit`: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- `expected_product_sha256`: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`

A Green independent run proves, through the shared fail-closed verifier, that:

- the exact Git tag still resolves to the approved tested merge;
- release tag/title/draft/prerelease metadata match the version contract;
- exactly two release assets exist with the exact expected names;
- REST asset IDs, upload state, sizes, SHA-256 digests and browser-download URLs are exact;
- exact-ID downloaded ZIP/checksum bytes match the approved product SHA-256 and canonical checksum line;
- tag/release security metadata remains unchanged across the verifier's second snapshots.

Retain the Green verification run URL and its Step Summary as the independent #162 closure evidence. Do not use the promotion run's own post-publication verification as a substitute for this separate read-only run.

## Closure evidence required for #162

Do not close #162 merely because repository hardening merged or because the promotion workflow itself reports success. Close it only after all of the following are true:

- the manual `promote-existing-candidate` run completed successfully using the exact outer artifact digest above;
- a separate `verify-durable-release` run from `main` completed successfully using the exact RC.61 verification inputs above;
- tag `v0.1.0-rc.61` resolves to tested merge `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- the release contains exactly `Monitor-0.1.0-rc.61-win-x64.zip` and `Monitor-0.1.0-rc.61-win-x64.zip.sha256`;
- the durable ZIP SHA-256 is exactly `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- the companion checksum line matches the exact ZIP name and product hash;
- no rebuild, repackaging or alternate candidate was introduced.

## External acceptance boundary

Durable publication is not production acceptance. #116 and #111 stay open until the intended trusted-certificate Windows/IIS SingleNode target produces reviewed real 15/15 external evidence and explicit operator finalization.
