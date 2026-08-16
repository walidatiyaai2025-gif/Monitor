# RC.61 Durable Promotion Inputs

Implementation status: **COMPLETE** via PR #163 / merge `43d8a193205495f155bb8866532a4e99ed93b655`, with subsequent durable-release hardening through PR #219.  
Execution status: **PENDING MANUAL DISPATCH** under Issue #162.  
Current release lookup: `v0.1.0-rc.61` is not yet present.

This file is the short operator handoff for the selected existing-candidate retention operation. It must stay aligned with the current hardened promotion and independent-verification workflows.

## Selected candidate identity — do not substitute

- version: `0.1.0-rc.61`
- source workflow run: `31667721306`
- source Actions artifact: `9168574442`
- outer Actions artifact digest: `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`
- product SHA-256: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`
- source head: `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`
- tested merge: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- release tag: `v0.1.0-rc.61`

## Step 1 — manual durable promotion

Dispatch `.github/workflows/promote-existing-candidate.yml` **from `main`**. The workflow fails closed for any other dispatch ref.

Use exactly these current workflow inputs:

- `candidate_version=0.1.0-rc.61`
- `source_run_id=31667721306`
- `source_artifact_id=9168574442`
- `expected_outer_artifact_digest=sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`
- `expected_product_sha256=d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`
- `source_commit=e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`
- `tested_merge_commit=158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- `release_tag=v0.1.0-rc.61`
- `acknowledge_promotion=true`

The promotion workflow requires the exact completed successful production-candidate run, exact artifact ID/name/source repository/head SHA, non-expired artifact state, positive size and exact outer digest. It downloads only the selected artifact and validates the companion checksum, product hash, embedded release manifest and hardened ZIP-safety rules. It creates or verifies the durable tag/release without rebuilding, publishing, compressing or repackaging RC.61.

A Green promotion run is necessary but is **not sufficient** to close #162.

## Step 2 — independent read-only verification

After the promotion run is Green, separately dispatch `.github/workflows/verify-durable-release.yml` **from `main`**.

Use exactly:

- `release_version=0.1.0-rc.61`
- `release_tag=v0.1.0-rc.61`
- `expected_commit=158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- `expected_product_sha256=d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`

This second workflow is read-only. It independently verifies tag provenance, release metadata, exact-two asset identity, REST asset metadata/digests/URLs, exact-ID downloaded bytes and the canonical checksum while detecting tag/release mutation across verification snapshots.

Retain the Green promotion run and the separate Green verification run as #162 closure evidence.

## #162 closure requirements

Keep #162 OPEN until all are independently true:

- manual `promote-existing-candidate` run from `main` is Green with the exact inputs above;
- separate `verify-durable-release` run from `main` is Green with the exact inputs above;
- tag `v0.1.0-rc.61` resolves to tested merge `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- release contains exactly `Monitor-0.1.0-rc.61-win-x64.zip` and `Monitor-0.1.0-rc.61-win-x64.zip.sha256`;
- durable ZIP SHA-256 is exactly `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- companion checksum line matches that exact hash and ZIP filename;
- no alternate candidate, rebuild or repackaging was introduced.

For the expanded contract and safety ceilings, see `docs/P05_EXISTING_CANDIDATE_PROMOTION.md`.

## External production boundary

This operation is retention/recoverability only. It does not deploy IIS, configure the trusted production certificate or app-pool identity, exercise the real monitored SQL target, prove IIS recycle durability, rehearse production rollback, or satisfy the real 15/15 evidence gate.

#116 remains the production acceptance authority, and #111 remains open until #116 is accepted.
