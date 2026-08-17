# P0.5 Repository Closeout

**Date:** 2026-08-17  
**Repository-side implementation:** COMPLETE  
**Durable RC.61 publication:** PENDING MANUAL #162  
**Real IIS acceptance:** PENDING EXTERNAL #116 / #111

Repository-side code and safety hardening is complete through:

- #258 / PR #259 — locked-session gate/finalization binding and immutable six-file sidecar identity;
- #260 — RC.61 byte-preserving sidecar compatibility boundary;
- #261 / PR #262 — clean exact-commit Acceptance Control Toolkit provenance, deterministic manifest/lock and independent verification;
- PR #263 — post-provenance P0.5 tracking reconciliation.

Exact tested toolkit source for cutover acceptance controls:

`b422eaaee53d931a62a43b3c36a53b68cd4f3e27`

PR #262 exact-head evidence:

- CI #1786 / `31992503009`: Green, 984/984;
- Windows production-candidate #186 / `31992502977`: Green end-to-end;
- squash merge `a448eb715af9b3a2fcfe89ce92807b71fc7e1127`.

Selected product remains RC.61 with product SHA-256:

`d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`

The repository cannot truthfully close #162 until the explicit manual promotion and separate read-only durable-release verification both run Green. It cannot close #116/#111 until the intended trusted-certificate Windows/IIS SingleNode host completes the real 15/15 acceptance workflow.
