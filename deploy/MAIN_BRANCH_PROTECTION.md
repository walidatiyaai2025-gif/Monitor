# Main branch protection operator handoff

`main` must enforce the repository checks that protect P0 tracking and normal build/test truth. This control is repository governance only; it does not publish RC.61, mutate production IIS/SQL, or satisfy any external gate.

## Required checks

The exact required check names are:

- `build`
- `protected-p0-pr-metadata`
- `protected-p0-pr-commits`

The two dedicated workflows intentionally expose unique stable job names so branch protection can require them independently.

The helper does **not** trust names alone. Before any mutation it proves that the current `main` head resolves to exactly one recently merged same-repository PR, reads the check runs from that PR's exact head SHA, requires all three checks to be completed/successful, and requires one unambiguous GitHub App provider identity. The resulting branch-protection payload uses `required_status_checks.checks` with exact `context + app_id` bindings instead of unbound legacy contexts.

## Preview first — no mutation

From a trusted authenticated repository-admin checkout with GitHub CLI available:

```powershell
.\scripts\Set-MainBranchProtection.ps1
```

If protection is not already exact, require:

```text
Status = READY_FOR_EXPLICIT_BRANCH_PROTECTION_ACKNOWLEDGEMENT
MutationPerformed = False
ExternalProductionGatesPassed = 0
```

Review:

- `CurrentProtected`
- `CurrentRequiredCheckBindings`
- `RequiredCheckBindings`
- each binding's `Context`, `AppId`, `EvidencePullRequest`, `EvidenceHeadSha`, and `EvidenceCheckRunId`

Do not proceed on repository identity/default-branch/API ambiguity, current-`main`/merged-PR mismatch, missing or failed required checks, or ambiguous provider identity. Any of those conditions fails before the protection `PUT`.

If the policy is already exact and provider-bound, the helper returns:

```text
Status = ALREADY_PROTECTED_AS_REQUIRED
MutationPerformed = False
ExternalProductionGatesPassed = 0
```

## Explicit repository-admin application

After reviewing the preview:

```powershell
.\scripts\Set-MainBranchProtection.ps1 -AcknowledgeProtection
```

The helper applies only the exact `main` branch-protection policy and then independently reads it back. Require:

```text
Status = BRANCH_PROTECTION_APPLIED_AND_VERIFIED
StrictRequiredChecks = True
EnforceAdmins = True
ConversationResolutionRequired = True
ForcePushesAllowed = False
DeletionsAllowed = False
ExternalProductionGatesPassed = 0
```

The exact provider-bound required-check set must remain the three names above with the App IDs proven during preview. Read-back mismatch fails closed.

## Safety boundary

This helper:

- pins repository `walidatiyaai2025-gif/Monitor`, repository ID `1329517438`, and branch `main`;
- requires the live current `main` head to bind to exactly one recently merged same-repository PR before mutation;
- requires all three expected checks to be completed/successful on that PR's exact head SHA;
- rejects missing, failed, provider-less, or provider-ambiguous check evidence;
- binds required checks to the observed GitHub App provider via `context + app_id`;
- performs no mutation without explicit `-AcknowledgeProtection`;
- performs one branch-protection `PUT` only;
- reads the resulting policy back and requires the exact provider-bound policy;
- does not create/delete issues, tags, releases, workflows, branches, or production resources;
- does not dispatch RC.61 promotion or durable-release verification;
- does not change #162 -> #116 -> #111 dependency truth;
- does not count repository protection as a production acceptance gate.

After application, independently confirm GitHub reports `main` as protected and the three provider-bound required status checks are enforced before treating #353 as complete.
