# Project Status

**Live-state authority:** exact GitHub repository state first, followed by `AGENTS.md`, `docs/CURRENT_EXECUTION_PLAN.md` and the current gate evidence. Historical branch/artifact references are evidence only and must not be treated as an active queue.

## Current status — 2026-09-12

Repository/application work requested by the owner is integrated. The DBA Command Center scope from PR #479 is on `main` via merge `0cc2087aa9da887046986d413ab46df2bcbab735`, which was also the exact observed `main` at this reconciliation work start.

Integrated DBA/operations capability includes:

- `/dba` command center with explicit, audited deep SQL Server inspection;
- query CPU/logical read/logical write evidence with sanitized retained query snippets;
- active memory grants and per-database data/log/buffer-pool/recovery/backup evidence;
- missing-index evidence and review-only recommendation/fix/validation SQL;
- explicit Advanced DBA analysis for filtered waits, memory clerks, plan-cache/single-use pressure, TempDB layout/usage/growth, active requests/blocking and key SQL configuration;
- one-database/all-databases backup-management planning;
- authenticated read-only Flutter DBA dashboard in `apps/monitor_dba_flutter` with server/database KPIs and DBA what/when/where to-do items;
- first-install Windows wizard;
- Admin > Monitor Upgrades with SHA-256/ZIP validation and host-side backup/stop/swap/start/readiness/rollback behavior;
- visible buttons/routes for operational functionality; no intentionally hidden DBA action.

Exact PR #479 source head `ef7209cbf099da65330887508ab4380a8b4196d2` passed normal CI `34710820477`, Real SQL Server 2022 acceptance `34710820497`, production-candidate `34710820438`, protected-P0 metadata `34710820492` and protected-P0 commits `34710820496`, with zero unresolved review threads before merge.

## Selected production candidate

Canonical authority: `docs/SELECTED_RELEASE_CANDIDATE.md`.

RC.61 is no longer executable release input. GitHub Actions artifact `9168574442` expired on `2026-09-12T04:41:36Z`. Its historical identity is retained for audit only; it must not be reconstructed or silently replaced.

The fresh selected candidate is:

```text
Version                    0.1.0-rc.854
Tag                        v0.1.0-rc.854
ZIP                        Monitor-0.1.0-rc.854-win-x64.zip
Checksum                   Monitor-0.1.0-rc.854-win-x64.zip.sha256
Product SHA-256            b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7
Source run                 34710820438
Actions artifact ID        10303396821
Outer artifact digest      sha256:e1f0b7facc756758a13653c3ad2bfa5a4af9107b02e14f4682aaaabb286f01e3
Artifact expires           2026-10-12T18:22:15Z
Source PR                  #479
Source head                ef7209cbf099da65330887508ab4380a8b4196d2
Tested PR merge            e1d0daedf8b2209934a1bcd01bff5d46229df20a
Integrated main merge      0cc2087aa9da887046986d413ab46df2bcbab735
```

The Actions artifact was independently downloaded and inspected. The live artifact metadata matched the locked run/head/repository identity and outer digest; the nested ZIP SHA-256 matched the checksum, and `_operations/release-manifest.json` matched version/source/tested-merge/runtime/deployment identity.

Use `scripts/Invoke-SelectedDurablePromotion.ps1` for the owner handoff. It verifies the live successful source run, exact non-expired artifact, exact outer digest, nested product hash, checksum and embedded release manifest before allowing an explicitly acknowledged promotion. Preview does not mutate production. Ambiguity/failure/expiration is fail-closed and automatic redispatch is forbidden.

## Remaining required gates — all genuine external/owner gates

### #162 — OWNER_ONLY durable publication — OPEN / NOT PASS

No GitHub Release or selected tag exists yet. Repository-side tooling is complete. The connected integration exposes workflow evidence but not workflow-dispatch mutation, so the owner must explicitly run the selected promotion helper (or the equivalent manual workflow dispatch) and then run a **separate** durable-release verifier. Exact promotion/verifier run IDs, tag target, assets and SHA-256 must be independently read back before this gate passes.

### #116 — EXTERNAL_ENVIRONMENT production acceptance — OPEN / NOT PASS

Blocked from production mutation until #162 genuinely passes. Then run fresh real trusted HTTPS/IIS/least-privilege/recycle/durability/backup/rollback acceptance against exact selected rc.854 bytes. A new session begins at 0/15 and requires genuine 15/15 external evidence plus independent validation.

### #111 — umbrella/real least-privilege closure — OPEN / NOT PASS

Repository and Real-SQL CI evidence cannot substitute for the required real production operator/estate evidence. Close only after #116 is genuinely accepted and the required least-privilege production evidence is recorded without committing credentials.

### #353 — OWNER_ONLY / REPOSITORY_ADMIN branch protection — OPEN / NOT PASS

Live repository metadata still reports `main.protected=false`. The connected integration has no administration permission to apply/read back the required policy. Issue #353 intentionally requires exactly these provider-bound checks:

- `build`
- `protected-p0-pr-metadata`
- `protected-p0-pr-commits`

Real SQL and Windows production-candidate are separate acceptance gates and are intentionally not additional branch-protection contexts. Strict up-to-date checks, admin enforcement and conversation resolution must be enabled; force-push and deletion must be disabled; independent admin read-back is required.

## Dependency order

```text
#162 -> #116 -> #111
```

#353 is independent governance.

## Stable truth/safety boundaries

- browser/mobile clients do not connect directly to monitored SQL;
- missing/stale/uncollected evidence remains explicit and never becomes synthetic healthy/zero truth;
- no autonomous tuning, index creation, session killing, cache clearing, SQL configuration mutation, backup execution or restore execution from recommendations;
- secrets, credentials and arbitrary sensitive SQL/data are not exposed in client evidence;
- write actions remain named-authorization + POST + antiforgery bounded;
- release identity is immutable and selected by exact run/artifact/digest/hash/manifest evidence;
- repository CI cannot manufacture OWNER_ONLY or EXTERNAL_ENVIRONMENT PASS.

## Completion status

**Repository product/runtime implementation:** COMPLETE for the currently requested scope.  
**Repository-side selected-candidate/tooling reconciliation:** COMPLETE in this change, subject to normal PR/main CI integration.  
**Owner/external acceptance:** NOT COMPLETE — #162, #116, #111 and #353 remain real gates.  
**VERIFIED_FINAL_COMPLETE:** **FORBIDDEN** until those four issues contain genuine closure evidence.
