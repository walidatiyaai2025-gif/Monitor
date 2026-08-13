# P0.5 Existing Candidate Durable Promotion

Issue: #162  
Parent gate: #116 / #111  
Selected candidate: `Monitor-0.1.0-rc.61-win-x64.zip`

## Current state

Implementation is **COMPLETE** via PR #163, squash-merged as `43d8a193205495f155bb8866532a4e99ed93b655`.

The durable promotion itself is **PENDING MANUAL DISPATCH**. A direct release lookup after the merge confirmed that `v0.1.0-rc.61` does not yet exist, so Issue #162 must remain OPEN until the manual workflow runs successfully and the two durable GitHub Release assets are independently verified.

This is retention/recoverability hardening only; it does not rebuild the application, select a different candidate, deploy IIS, or satisfy any external production gate.

## Selected RC.61 identity

- version: `0.1.0-rc.61`
- source workflow run: `31667721306`
- Actions artifact ID: `9168574442`
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
- `expected_product_sha256`: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`
- `source_commit`: `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`
- `tested_merge_commit`: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- `release_tag`: `v0.1.0-rc.61`
- `acknowledge_promotion`: `true`

Do not substitute a later candidate, workflow run, artifact, commit, checksum or tag under Issue #162.

## Promotion contract

`.github/workflows/promote-existing-candidate.yml` is manual-only and requires explicit acknowledgement plus the exact candidate version, source run, artifact ID, expected product hash, source head, tested merge and release tag.

The workflow:

1. requires a successful `production-candidate.yml` source run with the expected source head;
2. requires exactly one matching, non-expired Actions artifact with the approved artifact ID;
3. downloads only that artifact from the selected source run;
4. calls `scripts/Test-ExistingCandidatePromotion.ps1` to verify the ZIP name, companion checksum, product SHA-256 and embedded release manifest identity;
5. creates or verifies `v<version>` against the embedded tested merge SHA;
6. accepts an existing release only when it contains exactly the same product ZIP and checksum bytes;
7. never runs build, test, publish, compression or repackaging in the promotion operation.

## Closure evidence required for #162

Do not close #162 merely because PR #163 merged. Close it only after all of the following are true:

- the manual `promote-existing-candidate` run completed successfully;
- tag `v0.1.0-rc.61` resolves to tested merge `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- the release contains exactly `Monitor-0.1.0-rc.61-win-x64.zip` and `Monitor-0.1.0-rc.61-win-x64.zip.sha256`;
- the durable ZIP SHA-256 is exactly `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- the companion checksum line matches the exact ZIP name and product hash;
- no rebuild, repackaging or alternate candidate was introduced.

## External acceptance boundary

Durable publication is not production acceptance. #116 and #111 stay open until the intended trusted-certificate Windows/IIS SingleNode target produces reviewed real 15/15 external evidence and explicit operator finalization.
