# RC.61 Durable Promotion Inputs

Implementation status: **COMPLETE** via PR #163 / merge `43d8a193205495f155bb8866532a4e99ed93b655`.  
Execution status: **PENDING MANUAL DISPATCH** under Issue #162.  
Current release lookup: `v0.1.0-rc.61` not yet present after the implementation merge.

This file keeps the selected existing-candidate retention operation explicit and prevents accidental promotion of a different candidate.

Selected candidate identity:
- version: `0.1.0-rc.61`
- source workflow run: `31667721306`
- source Actions artifact: `9168574442`
- product SHA-256: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`
- source head: `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`
- tested merge: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- release tag: `v0.1.0-rc.61`

Manual workflow inputs for `.github/workflows/promote-existing-candidate.yml`:
- `candidate_version=0.1.0-rc.61`
- `source_run_id=31667721306`
- `source_artifact_id=9168574442`
- `expected_product_sha256=d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`
- `source_commit=e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`
- `tested_merge_commit=158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- `release_tag=v0.1.0-rc.61`
- `acknowledge_promotion=true`

The workflow downloads the existing candidate from the exact source run, validates the companion checksum and embedded release manifest through `scripts/Test-ExistingCandidatePromotion.ps1`, and creates or verifies durable release assets without rebuilding or repackaging.

#162 remains OPEN until the manual workflow is Green and the release is independently verified to contain exactly two assets: the approved ZIP and its `.sha256`, with the product hash above and tag bound to the tested merge commit.

This operation is retention/recoverability only. It does not deploy IIS and does not satisfy any external P0.5 acceptance gate. #116 remains the production acceptance authority.
