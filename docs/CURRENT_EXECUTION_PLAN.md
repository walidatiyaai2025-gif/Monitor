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

Fresh live base before current integration:

```text
Repository        walidatiyaai2025-gif/Monitor
Repository ID     1329517438
Observed main     4d514daef24781724f24065accd95988b406d9e6
Main push CI      34382598596 — success
Active PR         #477 / issue #476 — dashboard live database status
Owner-closed PR   #472 — optional browser verification, closed unmerged
Open gate issues  #162, #353, #116, #111
Releases          0
Tags              0
Rulesets          0
main protected    false
```

These values are audit evidence, not execution pins. Before merge or mutation, fetch fresh `main`, current PR head, branch currency, review state and exact workflows again.

## Current lawful cloud-actionable work — #476 / PR #477

PR #477 is the single legitimate implementation line for the Dashboard configurable live database-status requirement. It must preserve the existing cached/read-only Dashboard architecture:

- render database status from already-rendered cached Dashboard evidence;
- background refresh may re-read authenticated `/dashboard` only;
- no direct monitored-SQL, collector or `/refresh-snapshot` browser path;
- bounded persisted cadence 1/2/5/10/15/30 minutes;
- fail closed on redirect/non-HTML/missing evidence and retain last display;
- prevent overlapping refreshes and skip hidden-tab background fetches;
- honor `prefers-reduced-motion`;
- preserve existing owner/external P0 gates.

AGENTS.md requires material features to update `docs/STATUS.md` and `docs/FEATURE_CATALOG.md` in the same PR. Exact-head merge acceptance requires current base, zero unresolved review threads, normal CI, Windows production-candidate, and both protected-P0 guards Green. Earlier-head results do not authorize a changed head.

## Owner-closed optional work — #472

PR #472 was optional authenticated browser verification for already-complete Website Monitoring. Its recovered exact head ultimately passed selected CI/browser/guard gates, but the owner explicitly directed **close without merge**. It is not an active merge target, its browser harness is not part of `main`, and its Green evidence does not reopen Website Monitoring or create a P0 prerequisite. Do not duplicate or revive it without new authoritative scope.

## Repository baseline

Required pre-#476 product/runtime repository-side code, tests, CI/security controls, Website Monitoring integration and P0.5 operator tooling are complete through current `main`. Existing historical branches are not active by themselves; only live PR/claim/defect evidence activates work.

Any exact-main regression or legitimate stale READY integration has priority over new feature work.

## Remaining direct gates

### #162 — OWNER_ONLY — durable RC.61 release

State: **OPEN / NOT PASS**.

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

No GitHub Release or `v0.1.0-rc.61` tag exists. Follow `deploy/REMAINING_OWNER_EXTERNAL_GATES.md`: preview -> explicit acknowledged promotion -> exact promotion run -> **separate** independent verifier -> exact run-ID readiness -> independent tag/assets/hash evidence.

### #353 — OWNER_ONLY / REPOSITORY_ADMIN — protect `main`

State: **OPEN / NOT PASS**.

Live public read reports `main.protected=false`; repository rulesets are empty and the administration-gated protection endpoint is inaccessible to the connected integration. Only authenticated repository-admin application plus independent read-back of the exact provider-bound policy can pass this gate.

### #116 — EXTERNAL_ENVIRONMENT — real trusted-IIS 15/15 acceptance

State: **OPEN / NOT PASS / blocked before production mutation by #162**.

After #162 really completes, use exact RC.61 product bytes plus separately verified Acceptance Control Toolkit source `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`, create one fresh 0/15 session and execute real trusted HTTPS/IIS/least-privilege/recycle/durability/backup/rollback acceptance, then independently validate 15/15.

### #111 — umbrella closure only

State: **OPEN / NOT PASS**. It closes only after #116 has real accepted external evidence.

## Required dependency order

```text
#162 -> #116 -> #111
```

#353 is independent repository governance.

## Completion rule

`VERIFIED_FINAL_COMPLETE` is forbidden while any required owner-only or external gate lacks real evidence. Repository CI, optional browser evidence, documentation, candidate packaging, a preview or a merged feature PR cannot manufacture owner/external PASS.
