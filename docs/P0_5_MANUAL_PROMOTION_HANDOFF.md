# P0.5 Manual Promotion Handoff

**Selected candidate:** `0.1.0-rc.61`  
**State:** ready for explicit manual dispatch; not yet published  
**Issue:** #162

## Promotion inputs

Run `.github/workflows/promote-existing-candidate.yml` from `main` with:

- `candidate_version`: `0.1.0-rc.61`
- `source_run_id`: `31667721306`
- `source_artifact_id`: `9168574442`
- `expected_outer_artifact_digest`: `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`
- `expected_product_sha256`: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`
- `source_commit`: `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`
- `tested_merge_commit`: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- `release_tag`: `v0.1.0-rc.61`
- `acknowledge_promotion`: `true`

Latest 2026-08-17 pre-dispatch verification confirms source run success, exact artifact identity/digest, `expired=false`, exact two outer artifact members, canonical companion checksum, product SHA-256 match, 95 product files, 19 `_operations` entries, and schema-2 release manifest with the approved source/tested-merge identity.

## Independent verification inputs

Only after promotion is Green, separately run `.github/workflows/verify-durable-release.yml` from `main` with:

- `release_version`: `0.1.0-rc.61`
- `release_tag`: `v0.1.0-rc.61`
- `expected_commit`: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- `expected_product_sha256`: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`

Do not close #162 until both runs are Green and the durable tag, exact-two assets and product hash are independently verified. Do not infer #116/#111 production acceptance from durable publication.
