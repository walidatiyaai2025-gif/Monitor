# RC.61 post-promotion cutover readiness

This handoff is a **read-only bridge from #162 to #116**. It is used only after the exact manual RC.61 promotion and the separate independent durable-release verification have both completed Green.

It does not dispatch either workflow, create or mutate a GitHub tag/release, rebuild or repackage RC.61, deploy IIS, execute SQL, record any production acceptance gate, or close #162/#116/#111.

## Preconditions

Complete the #162 sequence first:

1. `Test-Rc61DurablePromotionPreflight.ps1` reports the approved pre-dispatch state.
2. `promote-existing-candidate.yml` is manually dispatched from `main` with the exact selected RC.61 inputs and completes Green.
3. `verify-durable-release.yml` is separately dispatched from `main` after promotion completes and completes Green.

Record the two concrete GitHub Actions run IDs. Do not substitute "latest" or infer them from a moving branch.

## Run the read-only handoff gate

From an authenticated operator shell with GitHub CLI available:

```powershell
./scripts/Test-Rc61CutoverReadiness.ps1 `
  -PromotionRunId <green-promote-existing-candidate-run-id> `
  -VerificationRunId <green-verify-durable-release-run-id>
```

The gate fails closed unless all of the following remain true:

- repository identity is exactly `walidatiyaai2025-gif/Monitor` with default branch `main`;
- the two supplied run IDs are distinct, completed/successful `workflow_dispatch` runs from `main`;
- the first run is `.github/workflows/promote-existing-candidate.yml`;
- the second run is `.github/workflows/verify-durable-release.yml` and was created only after the promotion run completed;
- `v0.1.0-rc.61` resolves to approved tested merge `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- the durable release is `Monitor 0.1.0-rc.61`, non-draft and prerelease;
- it contains exactly `Monitor-0.1.0-rc.61-win-x64.zip` plus `Monitor-0.1.0-rc.61-win-x64.zip.sha256`;
- both assets are fully uploaded with positive IDs/sizes and canonical API SHA-256 digests;
- the ZIP API digest equals selected product SHA-256 `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- browser-download URLs are bound to the exact repository/tag/asset names;
- the exact Acceptance Control Toolkit source commit `b422eaaee53d931a62a43b3c36a53b68cd4f3e27` is still retrievable together with its exporter, verifier and six acceptance-control scripts.

A successful result is:

```text
Status                              READY_FOR_P0_5_PRE_CUTOVER_PREPARATION
DurableReleasePrerequisiteSatisfied True
ExternalGatesPassed                 0
ProductionMutationPerformed         False
MutatedGitHubState                  False
```

The output also records the exact promotion/verification run URLs, release/asset IDs and API digests, tested merge, product SHA-256 and `OperatorToolingCommit` for the operator evidence record.

## What success means

`READY_FOR_P0_5_PRE_CUTOVER_PREPARATION` is supporting evidence that the #162 durable-release prerequisite is in the expected post-verification state. It **does not close #162 by itself**; close #162 only after the run evidence and durable tag/assets/hash have been independently reviewed under the issue closure rule.

It also proves **0/15 external #116 gates**. It does not authorize skipping the remaining pre-cutover controls.

After #162 is formally complete, the #116 operator sequence remains:

1. obtain and verify the exact durable RC.61 ZIP/checksum;
2. validate the operational backup and rollback point;
3. use a clean checkout of exact toolkit source `b422eaaee53d931a62a43b3c36a53b68cd4f3e27` to export and independently verify the Acceptance Control Toolkit;
4. preserve `OperatorToolingCommit` and `OperatorToolkitManifestSha256` independently;
5. create a fresh sidecar-owned acceptance session, preserve `SessionManifestSha256`, and verify `PreparedFailClosed` / 0/15;
6. only then proceed through the candidate-bundled IIS prerequisite, PLAN ONLY deployment review and explicit production cutover governed by `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`.

The two-identity boundary remains mandatory: RC.61 candidate-bundled `_operations` owns candidate-specific IIS/preflight/deploy/HTTPS operations; the exact verified sidecar owns session/gate/finalizer/reviewer acceptance-control state.
