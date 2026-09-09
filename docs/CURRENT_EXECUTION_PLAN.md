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

Never revive a historical branch or create parallel work solely because the legacy implementation plan still describes an old merge gate.

## Live convergence snapshot — 2026-09-09

Original snapshot used to create this plan:

```text
Repository        walidatiyaai2025-gif/Monitor
Repository ID     1329517438
Observed main     ce3cf05cc22250d5bcc9543842cbe2af08c04b2f
Main push CI      34359879616 — success
Open PRs          0
Open issues       #162, #353, #116, #111
Releases          0
RC.61 tag         absent
Rulesets          0
main protected    false
```

The observed values above are an audit snapshot, not execution pins. Fresh live revalidation on 2026-09-09 later observed `main@4d514daef24781724f24065accd95988b406d9e6` with exact-main push CI `34382598596` Green, no releases/tags/rulesets, `main.protected=false`, and the same four open gate issues. Live GitHub state also identifies PR #472 / `task/website-monitoring-wm7-browser-evidence` as one legitimate optional verification line. Before this tracking reconciliation, its exact head `fce84a2c6743061d6ff402d3e6ba1d9e66ac1db6` was current with `main` (`behind_by=0`), had zero unresolved review threads, and passed CI `34384824308`, browser workflow `34384824489`, protected-P0 commits `34384824328`, and protected-P0 metadata `34384824478`. Browser artifact `10117386057` was retained with digest `sha256:6fa8cdd0bb834d4907c0bfd8fe73c32d1068f118476ebc06bd0f8ad8b6dac697`.

A later docs-only or verification-only commit must not make any owner handoff unusable. Before any mutation or merge, capture fresh remote `main`, exact branch head, review state and exact-head workflow evidence again.

## Repository-complete state

Required product/runtime repository-side code, tests, CI/security controls, Website Monitoring integration and P0.5 operator tooling are complete. PR #472 is optional authenticated browser/screenshot verification only; it does not reopen completed Website Monitoring runtime scope and is not a prerequisite for `#162 -> #116 -> #111`.

Existing historical branches are not active work by themselves. A branch becomes active only when current live evidence ties it to an open PR, explicit current claim/lease, or a newly discovered unmerged defect. Squash-merged historical branches must not be re-integrated merely because their commit graph is divergent from `main`.

Any cloud-actionable regression on exact `main`, or stale-but-legitimate READY integration such as an open branch whose tracking/evidence has drifted, has priority over new feature work and must be reconciled on that existing lawful line with exact-head validation before merge.

## Remaining direct gates

Only these direct execution gates remain:

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

Live artifact recheck on 2026-09-09: artifact `9168574442` is still present, `expired=false`, and expires `2026-09-12T04:41:34Z`; no GitHub Release exists and `v0.1.0-rc.61` is absent.

Execution must follow `deploy/REMAINING_OWNER_EXTERNAL_GATES.md`: preview -> explicit acknowledged promotion -> exact promotion run -> **separate** independent verifier -> exact run-ID readiness -> independent tag/assets/hash evidence.

### #353 — OWNER_ONLY / REPOSITORY_ADMIN — protect `main`

State: **OPEN / NOT PASS**.

Live public branch read reports `main.protected=false`; repository rulesets are empty. The administration-gated protection endpoint remains inaccessible to the connected integration. Repository implementation is complete, but only an authenticated repository-admin application plus independent read-back of the exact provider-bound policy can pass this gate.

### #116 — EXTERNAL_ENVIRONMENT — real trusted-IIS 15/15 acceptance

State: **OPEN / NOT PASS / blocked before production mutation by #162**.

Do not create or reuse production acceptance evidence as a substitute for actual execution. After #162 is really complete, use exact RC.61 product bytes plus the separately verified Acceptance Control Toolkit from commit `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`, create one fresh 0/15 session, execute real trusted HTTPS/IIS/least-privilege/recycle/durability/backup/rollback checks, explicitly attest each gate, finalize and independently validate 15/15.

### #111 — umbrella closure only

State: **OPEN / NOT PASS**.

#111 has no independent execution action. It closes only after #116 has real accepted external evidence.

## Required dependency order

```text
#162 -> #116 -> #111
```

#353 is an independent repository-governance owner gate and does not satisfy any production acceptance gate.

## Completion rule

`VERIFIED_FINAL_COMPLETE` is forbidden while any required owner-only or external gate lacks real evidence. Repository CI, synthetic acceptance, documentation, candidate packaging, a preview, a Green operator-tooling runtime, a Green optional browser-verification run, or a merged PR cannot manufacture owner/external PASS.
