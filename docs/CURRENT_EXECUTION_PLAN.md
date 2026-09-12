# Current Execution Plan

This file is the live execution authority for work selection. Always fetch exact GitHub state before mutation; historical plans, branches, artifacts and Green runs are evidence only and must never reopen completed work by themselves.

## Live reconciliation — 2026-09-12

Work-start evidence for this reconciliation:

```text
Repository                 walidatiyaai2025-gif/Monitor
Observed main              0cc2087aa9da887046986d413ab46df2bcbab735
DBA feature merge          0cc2087aa9da887046986d413ab46df2bcbab735 (#479)
Open product PRs           0
Open required gate issues  #111, #116, #162, #353
GitHub Releases            0
Tags                       0
main protected             false
```

PR #479 is **COMPLETE / MERGED**. The DBA Command Center, explicit Advanced DBA analysis, Flutter DBA dashboard, first-install wizard and Admin > Monitor Upgrades path are integrated. Exact PR-head CI, Real SQL Server 2022 acceptance, Windows production-candidate and both protected-P0 guards passed before merge. Do not recreate this feature on another branch.

## Repository-side product state

All currently requested application implementation is integrated. No open product PR or known exact-main product regression remains from the DBA/installer/mobile/upgrade scope.

The remaining work is release/governance/production acceptance. Repository tooling must make those gates executable and fail closed, but repository CI is not allowed to manufacture owner/external PASS.

## Selected release candidate

Canonical identity: `docs/SELECTED_RELEASE_CANDIDATE.md`.

RC.61 is historical only because Actions artifact `9168574442` expired at `2026-09-12T04:41:36Z`. It is forbidden to recreate different bytes under `v0.1.0-rc.61` or pretend the expired artifact remains promotable.

The currently selected candidate is:

```text
Version             0.1.0-rc.854
Tag                 v0.1.0-rc.854
Product ZIP         Monitor-0.1.0-rc.854-win-x64.zip
Product SHA-256     b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7
Source run          34710820438
Artifact ID         10303396821
Outer digest        sha256:e1f0b7facc756758a13653c3ad2bfa5a4af9107b02e14f4682aaaabb286f01e3
Artifact expiry     2026-10-12T18:22:15Z
Source PR           #479
Source head         ef7209cbf099da65330887508ab4380a8b4196d2
Tested merge        e1d0daedf8b2209934a1bcd01bff5d46229df20a
Integrated merge    0cc2087aa9da887046986d413ab46df2bcbab735
```

Use `scripts/Invoke-SelectedDurablePromotion.ps1`. Preview must verify the live source run, unexpired artifact, exact outer digest, nested product SHA-256 and embedded release manifest, and must make no production mutation. Actual durable publication requires explicit `-AcknowledgePromotion` and must uniquely bind one promotion workflow run. Failure or ambiguity is fail-closed; never auto-redispatch.

## Remaining required gates

### #162 — OWNER_ONLY — durable publication

State: **OPEN / NOT PASS**.

Repository-side selection/preflight/promotion tooling is complete. The connected automation does not expose GitHub Actions workflow dispatch, so actual promotion must be explicitly initiated by the repository owner. Publish only the exact selected rc.854 bytes and then run a separate independent durable-release verifier. Record exact promotion/verifier run IDs and independently read back tag, release assets and hash.

### #116 — EXTERNAL_ENVIRONMENT — real production acceptance

State: **OPEN / NOT PASS** and blocked from production mutation until #162 genuinely passes.

After durable publication, execute the real trusted HTTPS/IIS/least-privilege/recycle/durability/backup/rollback acceptance against exact product SHA-256 `b0370b3e...f0f27b7`. A fresh acceptance session starts at 0/15 and closes only with genuine 15/15 external evidence and independent validation.

### #111 — umbrella closure

State: **OPEN / NOT PASS**. No independent repository action remains. Close only after genuine #116 acceptance, including the required real least-privilege production operator evidence.

### #353 — OWNER_ONLY / REPOSITORY_ADMIN — protect `main`

State: **OPEN / NOT PASS**.

The connected GitHub integration cannot read or mutate the administration-gated protection endpoint; live repository metadata still reports `main.protected=false`. Issue #353 intentionally requires exactly these provider-bound checks:

- `build`
- `protected-p0-pr-metadata`
- `protected-p0-pr-commits`

Real SQL and Windows production-candidate remain separate acceptance gates and are intentionally **not** additional branch-protection contexts. Strict up-to-date checks, admin enforcement and conversation resolution must be enabled; force pushes and deletion must be disabled; independent repository-admin read-back is required.

## Dependency order

```text
#162 -> #116 -> #111
```

#353 is independent governance.

## Completion rule

Repository-side/cloud-actionable work is complete when the selected-candidate reconciliation change is integrated and exact-head/main CI is Green. `VERIFIED_FINAL_COMPLETE` remains forbidden while any of #162, #116, #111 or #353 lacks genuine owner/external evidence.
