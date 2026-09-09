# Current Execution Plan

This file is the live execution authority for work selection. Historical implementation detail remains in `docs/IMPLEMENTATION_PLAN.md`, but stale historical PR/branch wording there must **not** be interpreted as an active queue.

## Authority order

For every iteration, use this order:

1. exact live GitHub `main` and repository identity;
2. `AGENTS.md`;
3. live open PRs/issues, current branches/claims and exact workflow/check results;
4. this file;
5. `docs/STATUS.md`, `docs/PRODUCTION_MVP.md`, `docs/FEATURE_CATALOG.md` and the relevant runbooks/handoffs;
6. `docs/IMPLEMENTATION_PLAN.md` as historical implementation context only unless a section has been explicitly revalidated against current live state.

Never revive a historical branch or create parallel work solely because an old implementation plan, branch ref or Green CI run still exists.

## Live convergence state — 2026-09-09

Fresh live revalidation before this reconciliation:

```text
Repository        walidatiyaai2025-gif/Monitor
Repository ID     1329517438
Observed main     4d514daef24781724f24065accd95988b406d9e6
Main push CI      34382598596 — success
Open PRs          0 after owner-directed closure of #472
Open issues       #162, #353, #116, #111
Releases          0
Tags              0
Rulesets          0
main protected    false
```

These observed values are audit evidence, not execution pins. Before any future mutation or merge, fetch fresh remote `main`, current open work and exact workflow state again.

## Repository-complete state

Required product/runtime repository-side code, tests, CI/security controls, Website Monitoring integration and P0.5 operator tooling are complete through current `main`.

PR #472 was an optional authenticated-browser verification follow-up. Its existing branch was recovered and exact head `fce84a2c6743061d6ff402d3e6ba1d9e66ac1db6` ultimately passed CI `34384824308`, browser `34384824489`, protected-P0 commits `34384824328` and protected-P0 metadata `34384824478` with zero unresolved review threads. The owner then explicitly directed **close without merge**. Therefore #472 is not an active merge target, its browser harness is not part of `main`, and its Green evidence does not reopen completed Website Monitoring or create a P0 prerequisite.

Existing historical branches are not active work by themselves. A branch becomes active only when current live evidence ties it to an open PR, explicit current claim/lease, or a newly discovered unmerged defect. Owner-closed or squash-merged historical branches must not be re-integrated merely because their commit graphs diverge from `main`.

Any future cloud-actionable regression on exact `main` has priority over new feature work and must be repaired on one lawful current-main branch with exact-head validation before merge.

## Remaining direct gates

### #162 — OWNER_ONLY — durable RC.61 release

State: **OPEN / NOT PASS**.

Locked product identity:

```text
Version                    0.1.0-rc.61
Tag                        v0.1.0-rc.61
ZIP                        Monitor-0.1.0-rc.61-win-x64.zip
Checksum                   Monitor-0.1.0-rc.61-win-x64.zip.sha256
Product SHA-256            d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
Source run                 31667721306
Actions artifact ID        9168574442
Outer artifact digest      sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382
Source commit              e28158da67b36dfc5dbf8f4c38b5c43d99c7c728
Tested merge               158148d8bfd05f724014541bc7a0b1eab5dae1b5
```

The source artifact remains the selected candidate, but no GitHub Release or `v0.1.0-rc.61` tag exists. Execution must follow `deploy/REMAINING_OWNER_EXTERNAL_GATES.md`: preview -> explicit acknowledged promotion -> one exact promotion run -> **separate** independent verifier -> exact run-ID readiness -> independent tag/assets/hash evidence.

### #353 — OWNER_ONLY / REPOSITORY_ADMIN — protect `main`

State: **OPEN / NOT PASS**.

Public live read reports `main.protected=false`; repository rulesets are empty, and the administration-gated protection endpoint is inaccessible to the connected integration. Repository-side helper/tests/docs are complete. Only authenticated repository-admin application plus independent read-back of the exact provider-bound policy can pass this gate.

### #116 — EXTERNAL_ENVIRONMENT — real trusted-IIS 15/15 acceptance

State: **OPEN / NOT PASS / blocked before production mutation by #162**.

After #162 really completes, use exact RC.61 product bytes plus the separately verified Acceptance Control Toolkit from commit `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`, create one fresh 0/15 session, execute real trusted HTTPS/IIS/least-privilege/recycle/durability/backup/rollback checks, explicitly attest each gate, finalize and independently validate 15/15.

### #111 — umbrella closure only

State: **OPEN / NOT PASS**.

#111 has no independent execution action. It closes only after #116 has real accepted external evidence.

## Required dependency order

```text
#162 -> #116 -> #111
```

#353 is an independent repository-governance owner gate and does not satisfy any production acceptance gate.

## Completion rule

`VERIFIED_FINAL_COMPLETE` is forbidden while any required owner-only or external gate lacks real evidence. Repository CI, synthetic acceptance, documentation, optional browser evidence, candidate packaging, a preview or a merged PR cannot manufacture owner/external PASS.
