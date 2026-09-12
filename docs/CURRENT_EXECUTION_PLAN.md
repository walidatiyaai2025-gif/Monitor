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

## Active feature work — 2026-09-12

Fresh work-start audit observed `main@2a172f553087a6658700b7a1f32d7b7f0c9650d3`, zero open product PRs, and only the protected P0/external gates #111, #116, #162 and #353 open. New work therefore proceeds on `feature/dba-command-center` without mutating those protected gates.

Current branch scope requested by the owner:

- expert DBA Command Center with explicit deep inspection of query CPU/logical I/O, active memory grants, per-database resource/memory/backup state and missing-index evidence;
- evidence-based DBA recommendations with visible investigation/fix SQL and validation SQL, never autonomous execution;
- review-only one-database/all-databases backup-management plans;
- authenticated read-only `/api/dba/summary` for a simple Flutter DBA dashboard and what/when/where action list;
- `apps/monitor_dba_flutter` mobile companion that preserves the web/SQL trust boundary;
- visible first-install Windows wizard for service installation, production admin credential derivation, port configuration and readiness validation;
- Admin > Monitor Upgrades staged-package workflow plus host-side checksum/backup/swap/readiness/rollback executor;
- explicit navigation/buttons; no hidden operational action.

State: **IMPLEMENTED ON FEATURE BRANCH / VALIDATION AND PR CONVERGENCE IN PROGRESS**. Do not represent this feature as integrated or production-accepted until exact-head CI and normal merge evidence exist. Normal `/dba` GET and mobile summary access remain cache/control-plane only; only the visible authorized `Run DBA inspection` POST may open the bounded server-side diagnostic SQL connection.

Canonical design/safety contract: `docs/DBA_COMMAND_CENTER.md`.

## Live convergence state — 2026-09-09

Post-#477 verified integration snapshot:

```text
Repository        walidatiyaai2025-gif/Monitor
Repository ID     1329517438
Observed main     f3dc1b2234b2105d1331d2476f75e8b7b0f2761c
Main push CI      34392925320 — success
#476              CLOSED / COMPLETED
PR #477           MERGED / squash f3dc1b2234b2105d1331d2476f75e8b7b0f2761c
Open product PRs  0 at post-merge audit
Owner-closed PR   #472 — optional browser verification, closed unmerged
Open gate issues  #162, #353, #116, #111
Releases          0
Tags              0
Rulesets          0
main protected    false
```

These values are audit evidence, not execution pins. Before any later mutation, fetch fresh `main`, current PR/issue state, branch currency and exact workflows again.

## #476 / PR #477 — COMPLETE / MERGED

The Dashboard configurable live database-status feature is integrated on `main`.

Closure evidence:

- exact final PR head: `ac9499fe54000de8a8a38865bd47d062b5985d4f`;
- normal CI `34392461369` — success;
- Real SQL `34392461363` — success;
- Windows production-candidate `34392461390` — success;
- protected-P0 metadata `34392461372` — success;
- protected-P0 commits `34392461395` — success;
- zero unresolved review threads immediately before merge;
- branch current with exact base `4d514daef24781724f24065accd95988b406d9e6` (`behind_by=0`);
- squash merge `f3dc1b2234b2105d1331d2476f75e8b7b0f2761c`;
- exact merged-main push CI `34392925320` — success;
- issue #476 closed completed by the merge.

Integrated behavior remains bounded to truthful cached Dashboard evidence: browser refresh re-reads authenticated `/dashboard` only, never monitored SQL/collector/`/refresh-snapshot`; cadence is restricted to 1/2/5/10/15/30 minutes and persisted client-side; redirect/non-HTML/missing evidence fails closed; overlapping refreshes and hidden-tab polling are prevented; reduced-motion is honored.

This merge does not satisfy or mutate #162, #116, #111 or #353.

## Owner-closed optional work — #472

PR #472 was optional authenticated browser verification for already-complete Website Monitoring. Its recovered exact head ultimately passed selected CI/browser/guard gates, but the owner explicitly directed **close without merge**. It is not an active merge target, its browser harness is not part of `main`, and its Green evidence does not reopen Website Monitoring or create a P0 prerequisite. Do not duplicate or revive it without new authoritative scope.

## Repository baseline

Required product/runtime repository-side code, tests, CI/security controls, Website Monitoring integration, Dashboard live database-status integration and P0.5 operator tooling are complete through `main@f3dc1b2234b2105d1331d2476f75e8b7b0f2761c`.

Existing historical branches are not active by themselves; only live PR/claim/defect evidence activates work. Any future exact-main regression or legitimate stale READY integration has priority over new feature work.

There is no additional cloud-actionable product implementation implied by the historical branch inventory or by already-closed issues.

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
