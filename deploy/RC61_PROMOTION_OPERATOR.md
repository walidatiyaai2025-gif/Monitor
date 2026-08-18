# RC.61 explicit durable-promotion operator helper

This helper reduces copy/paste risk for Issue #162 while preserving the required manual GitHub Actions boundary.

It does **not** create an alternate trigger. It uses the existing `workflow_dispatch` contract for `.github/workflows/promote-existing-candidate.yml` and requires an explicit local acknowledgement before dispatch.

## Safety boundary

- RC.61 remains exactly `0.1.0-rc.61`.
- The selected source run/artifact/hash/source/tested-merge/tag remain locked by `Test-Rc61DurablePromotionPreflight.ps1`.
- The helper runs the read-only Step 0 preflight before any dispatch.
- Without `-AcknowledgePromotion`, the helper performs no workflow dispatch and returns `READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT`.
- With explicit acknowledgement, it dispatches **only** `promote-existing-candidate.yml` from `main` using the locked inputs.
- It captures the exact promotion run ID from the returned Actions URL when available; otherwise it uses a before/after workflow-run snapshot and fails closed if discovery is ambiguous.
- After dispatch, any ambiguity, timeout, or failed conclusion is a **do not redispatch** condition. Inspect the exact run instead.
- The helper does not dispatch the independent verifier.
- The helper does not call `gh release create`, create tags directly, rebuild/repackage RC.61, deploy IIS, touch SQL, record any external acceptance PASS, or close #162/#116/#111.

## Step A — preview only

From an authenticated operator shell with `gh` available, run:

```powershell
.\scripts\Invoke-Rc61DurablePromotion.ps1
```

Proceed only when the returned status is:

```text
READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT
```

This path is read-only and reports:

```text
WorkflowDispatchPerformed       = False
IndependentVerificationDispatched = False
ProductionMutationPerformed     = False
MutatedGitHubState              = False
```

If the helper reports any preflight drift, existing durable state, artifact expiry, authentication/API ambiguity, or another failure, stop and investigate.

## Step B — explicit promotion dispatch

After reviewing Step A, run:

```powershell
.\scripts\Invoke-Rc61DurablePromotion.ps1 -AcknowledgePromotion
```

The helper dispatches exactly one `promote-existing-candidate.yml` workflow using the locked RC.61 values and captures its exact run ID.

If the exact run completes Green, the helper returns:

```text
PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED
```

and includes:

- `PromotionRunId`
- `PromotionRunUrl`
- `IndependentVerificationCommand`
- `PostVerificationReadinessCommand`

If the helper instead returns `PROMOTION_DISPATCHED_CHECK_EXACT_RUN`, inspect the exact `PromotionRunId`/`PromotionRunUrl`. **Do not redispatch.**

If the exact run fails, the helper also fails closed and tells the operator to inspect that exact run. **Do not redispatch automatically.**

## Step C — separate independent verification

Only after the promotion run is Green, copy and execute the exact `IndependentVerificationCommand` returned by the helper.

That command dispatches the separate read-only `.github/workflows/verify-durable-release.yml` workflow from `main` with the locked RC.61 verification inputs.

The promotion helper intentionally does not execute this command. Separate dispatch is part of #162's closure contract.

Record the independent verification run ID after it completes Green.

## Step D — deterministic handoff to #116 readiness

Run the post-verification readiness gate with the two explicit workflow run IDs:

```powershell
.\scripts\Test-Rc61CutoverReadiness.ps1 `
  -PromotionRunId <PROMOTION_RUN_ID> `
  -VerificationRunId <VERIFICATION_RUN_ID>
```

Proceed toward #162 closeout only when it returns:

```text
Status                               = READY_FOR_P0_5_PRE_CUTOVER_PREPARATION
DurableReleasePrerequisiteSatisfied  = True
ExternalGatesPassed                  = 0
ProductionMutationPerformed          = False
MutatedGitHubState                   = False
```

This result proves the durable-release prerequisite/handoff only. It does not close #162 by itself and does not authorize IIS or SQL mutation until the issue's durable tag, exact-two assets, approved product hash/checksum, promotion run and independent verification evidence are all recorded and reviewed.
