# RC.61 Durable Promotion Inputs

This file exists to make the selected existing-candidate retention operation explicit and to keep changes to that operation inside the Windows production-candidate PR gate.

Selected candidate identity:
- version: `0.1.0-rc.61`
- source workflow run: `31667721306`
- source Actions artifact: `9168574442`
- product SHA-256: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`
- source head: `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`
- tested merge: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- release tag when explicitly promoted: `v0.1.0-rc.61`

Use `.github/workflows/promote-existing-candidate.yml` only with explicit acknowledgement and these exact approved values. The workflow downloads the existing candidate from the exact source run, validates the companion checksum and embedded release manifest through `scripts/Test-ExistingCandidatePromotion.ps1`, and creates or verifies durable release assets without rebuilding or repackaging.

This operation is retention/recoverability only. It does not deploy IIS and does not satisfy any external P0.5 acceptance gate. #116 remains the production acceptance authority.
