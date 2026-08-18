# Main branch protection operator handoff

`main` must enforce the repository checks that protect P0 tracking and normal build/test truth. This control is repository governance only; it does not publish RC.61, mutate production IIS/SQL, or satisfy any external gate.

## Required checks

The exact required check names are:

- `build`
- `protected-p0-pr-metadata`
- `protected-p0-pr-commits`

The two dedicated workflows intentionally expose unique stable job names so branch protection can require them independently.

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

Review `CurrentProtected`, `CurrentRequiredChecks`, and the exact `RequiredChecks`. Do not proceed on repository identity/default-branch/API ambiguity.

If the policy is already exact, the helper returns:

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

The exact required check set must be the three names above. Read-back mismatch fails closed.

## Safety boundary

This helper:

- pins repository `walidatiyaai2025-gif/Monitor`, repository ID `1329517438`, and branch `main`;
- performs no mutation without explicit `-AcknowledgeProtection`;
- performs one branch-protection `PUT` only;
- does not create/delete issues, tags, releases, workflows, branches, or production resources;
- does not dispatch RC.61 promotion or durable-release verification;
- does not change #162 -> #116 -> #111 dependency truth;
- does not count repository protection as a production acceptance gate.

After application, independently confirm GitHub reports `main` as protected and the three required status checks are enforced before treating the governance gap as closed.
