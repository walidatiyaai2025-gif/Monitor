# P0.5 Existing Candidate Durable Promotion

Issue: #162  
Parent gate: #116 / #111  
Selected candidate: `Monitor-0.1.0-rc.61-win-x64.zip`

## Purpose

Preserve the exact already-verified RC.61 bytes as durable GitHub Release assets before the source Actions artifact expires. This is retention/recoverability hardening only; it does not rebuild the application, select a different candidate, deploy IIS, or satisfy any external production gate.

## Selected RC.61 identity

- version: `0.1.0-rc.61`
- source workflow run: `31667721306`
- Actions artifact ID: `9168574442`
- source head: `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`
- tested merge: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- product SHA-256: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`
- observed Actions artifact expiry: `2026-09-12T04:41:34Z`

## Promotion contract

`.github/workflows/promote-existing-candidate.yml` is manual-only and requires explicit acknowledgement plus the exact candidate version, source run, artifact ID, expected product hash, source head, tested merge and release tag.

The workflow:

1. requires a successful `production-candidate.yml` source run with the expected source head;
2. requires exactly one matching, non-expired Actions artifact with the approved artifact ID;
3. downloads only that artifact from the selected source run;
4. calls `scripts/Test-ExistingCandidatePromotion.ps1` to verify the ZIP name, companion checksum, product SHA-256 and embedded release manifest identity;
5. creates or verifies `v<version>` against the embedded tested merge SHA;
6. accepts an existing release only when it contains exactly the same product ZIP and checksum bytes;
7. never runs build, test, publish, compression, repackaging, release upload or clobber operations.

## External acceptance boundary

Durable publication is not production acceptance. #116 and #111 stay open until the intended trusted-certificate Windows/IIS SingleNode target produces reviewed real 15/15 external evidence and explicit operator finalization.
