# Project Status

## CURRENT P0 — Real SQL Production MVP

**Updated:** 2026-08-11  
**Branch:** `main`  
**Umbrella:** #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Production acceptance guide:** `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`  
**Live candidate/environment evidence:** Issue #116  
**Active release gate:** #116 / P0.5 First Production SingleNode  
**Production target:** actual trusted-certificate IIS/HTTPS SingleNode release acceptance.

> Candidate run numbers, artifact names and SHA-256 values are intentionally maintained on Issue #116 rather than duplicated here. This file records stable release milestones and acceptance state so a newer verified RC does not make the canonical project status stale.

### Release chain

| Release | State | Stable evidence |
|---|---|---|
| P0.1 / #112 | COMPLETE | PR #119; final CI `31476747212`; 501/501 |
| P0.2 / #113 | COMPLETE | PR #121; final CI `31478470867`; 505/505 |
| P0.3 / #114 | COMPLETE | PR #122 merged `245bb0770d7ec6e7a334f7763d3560cef80324fe`; final CI `31479311552`; 507/507 |
| P0.4 / #115 | COMPLETE | PR #124 merged `f4c08292734c293a6d0b865cc2a005b8c42b02a6`; normal `31481874425` 518/518; Real SQL `31481874501` 8/8 |
| P0.5 / #116 | ACTIVE | PR #127 acceptance tooling + PR #126 Windows candidate pipeline + PR #129 IIS operator automation merged; external IIS/HTTPS acceptance pending |

### P0.5 repository/operator preparation — COMPLETE · EXTERNAL IIS PENDING

Stable merged milestones:

- PR #127 squash-merged as `9bdd96940454f2586c0e81ff0c25a524d7f1281c`; production acceptance evidence tooling added.
- PR #126 squash-merged as `d512ee156f07db566898a817f3c76dd3f46c1091`; the Windows `win-x64` production-candidate workflow proved Release build/test, secret-free SingleNode packaging, HTTPS health/authentication and process-restart behavior.
- PR #128 squash-merged as `564f7655a1001da98addd793a000a15d069a243a`; canonical P0 state reconciled after the candidate pipeline merged.
- PR #129 squash-merged as `7cb47945b47aab6558f7132dcfa818b9f02d2b20`; IIS operator automation added after final merge-ref normal CI and Windows production-candidate gates were Green.
- `scripts/Test-IisProductionPrerequisites.ps1` is read-only and validates IIS, .NET 8/ANCM, No Managed Code, safe application-pool identity, the exact HTTPS binding and approved machine certificate.
- `scripts/Deploy-ProductionSingleNode.ps1` is PLAN ONLY by default; `-Apply` is explicit, releases are immutable/versioned, durable `App_Data` stays outside release directories, and immediate post-cutover acceptance failure restores the previous IIS physical path.
- `scripts/Accept-ProductionSingleNode.ps1` validates the selected candidate SHA-256 and the actual HTTPS health endpoints and writes machine-readable acceptance evidence.
- The Windows production-candidate workflow syntax-checks the production PowerShell tooling and ships preflight/deploy/acceptance scripts plus IIS/production/rollback documentation in the candidate `_operations` bundle.
- Exact latest selected candidate filename, product ZIP SHA-256, Actions artifact ID and candidate-run evidence are maintained on #116.

### P0.5 task status

| Task | State |
|---|---|
| P0-041 SingleNode scope freeze | REPOSITORY/CI COMPLETE |
| P0-042 secret-free production configuration | REPOSITORY/CI COMPLETE |
| P0-043 actual IIS + trusted HTTPS deployment | **PENDING EXTERNAL**; preflight/deploy automation READY |
| P0-044 Data Protection / protected credentials after restart | CI process-restart VERIFIED; **IIS recycle pending external** |
| P0-045 durable registration/audit/history/incidents after IIS recycle | **PENDING EXTERNAL** |
| P0-046 deployment health smoke | CI HTTPS VERIFIED; acceptance tooling READY; **real IIS endpoint pending external** |
| P0-047 least-privilege monitored target | P0.4 prerequisite VERIFIED; **deployed IIS identity/target pending external** |
| P0-048 backup + rollback/recovery | code/unit/tooling VERIFIED; **production rehearsal pending external** |
| P0-049 versioned artifact/checksum/evidence | REPOSITORY/CI COMPLETE; **live selected artifact evidence on #116** |
| P0-050 final production acceptance | **PENDING EXTERNAL** |

### Management decision

- Repository-side P0.5 preparation and IIS operator automation are merged to `main`; the remaining active work is actual SingleNode IIS/HTTPS environment acceptance.
- #116 and umbrella #111 remain open. They must not close from repository CI, packaging, Windows runner validation, deployment automation or PR merge alone.
- On the real server, run the packaged `_operations/scripts/Test-IisProductionPrerequisites.ps1`, review `Deploy-ProductionSingleNode.ps1` in PLAN ONLY mode, then use explicit `-Apply` only after the approved backup/cutover review.
- Required external evidence: trusted HTTPS/IIS binding, intended application-pool identity, deployed authentication, least-privilege SQL Test/Refresh, IIS recycle durability, durable operational state, operational backup, rollback/recovery rehearsal and final health/auth/read verification.
- Exact dynamic artifact/run/checksum evidence belongs on #116; canonical docs should not be edited merely because a later equivalent RC is generated.
- MultiNode activation remains deferred until after the first stable SingleNode production release.

**Overall:** 🟢 verified foundation · 🟢 P0.1 COMPLETE · 🟢 P0.2 COMPLETE · 🟢 P0.3 COMPLETE · 🟢 P0.4 COMPLETE · 🟢 P0.5 repository/operator tooling complete · 🟡 external IIS/HTTPS acceptance pending · 🔴 production acceptance not yet granted

---

## P0.4 final result — COMPLETE

- Issue #115 closed completed.
- PR #124 squash-merged to `main` as `f4c08292734c293a6d0b865cc2a005b8c42b02a6`.
- Final same-head normal CI `31481874425`: Release build 0 warnings / 0 errors; 518/518 passed.
- Final same-head Real SQL `31481874501`: Release build 0 warnings / 0 errors; 8/8 passed.
- Full SQL Server 2022 application journey is verified: Add/Test/Register/Collect/View/Refresh/Restart/View.
- Controlled bad-password, TLS, closed-port, timeout, insufficient server-state and missing msdb/Agent-permission cases fail safely.
- The exact least-privilege deployment script is proven with a non-sysadmin login.
- Durable evidence is in `docs/REAL_SQL_ACCEPTANCE.md`.

## P0.3 final result — COMPLETE

- PR #122 squash-merged to `main` as `245bb0770d7ec6e7a334f7763d3560cef80324fe`.
- Issue #114 closed completed.
- Final CI `31479311552`: Release build 0 warnings / 0 errors; 507/507 passed.
- Server Details is evidence-first and normal GET remains cache-only.

## P0.2 final result — COMPLETE

- PR #121 squash-merged to `main` as `a294c6530d60f17e7c60e3a1ac070ce562af7b18`.
- Issue #113 closed completed.
- Final CI `31478470867`: Release build 0 warnings / 0 errors; 505/505 passed.
- Production mappings preserve absence as absence and do not publish fake numeric zero evidence.

## P0.1 final result — COMPLETE

- PR #119 squash-merged to `main` as `57ab5cae6b5bdd3a04adb5069008aae80a1f84e0`.
- Issue #112 closed completed.
- Final CI `31476747212`: Release build 0 warnings / 0 errors; 501/501 passed.
- Candidate Test precedes durable registration; failed/cancelled owned-secret candidates are compensated safely.

---

## BATCH-400 — Production DBA diagnostics continuation

- Issue #108 delivered **100 additional code tasks B400-011..110**, preserving portal/typography work merged by PR #107 as B400-001..010.
- Added deterministic wait-stat intelligence, query-regression scoring, TempDB pressure, transaction-log health, I/O latency, SQL Agent reliability, HA readiness, maintenance decision safety and fleet signal correlation.
- Added the Read-policy-protected `/intelligence/v2/contract` endpoint and a fail-closed continuation release contract.
- Clean implementation CI `31467831498`: Release build 0 warnings / 0 errors; 498/498 passed.
- Final PR CI `31468048589`: Release build 0 warnings / 0 errors; 498/498 passed.
- PR #109 squash-merged as `9345c4ca8b67e617a9aa9580bbb481819e5babb7`.
- Issue #108 closed completed.
- B400-011..110: 100/100 COMPLETE with 100 mapped acceptance tests.

## BATCH-400 — Portal completion and typography

- Dedicated Performance Health, Recommendations, and Reports & Diagnostics pages.
- Navigation organized around Operations, Health, Intelligence, Administration and Help.
- Fleet, Help, Readiness, Audit, History and enterprise export capabilities connected.
- Role-aware management links; dead standalone AI Advisor link removed.
- Self-hosted Inter Variable and Noto Sans Arabic Variable fonts under strict CSP.
- Responsive sidebar polish preserved the command-center visual identity.
- State: MERGED — PR #107.

## Historical batch baseline

- M0–M8 VERIFIED.
- BATCH-100: 100/100 COMPLETE.
- BATCH-200: 100/100 COMPLETE; final CI `31446970475`, 290/290.
- BATCH-300: 100/100 COMPLETE; PR #102 squash-merged as `385c2ee7a4d592c1e32e6e00a5c533c8790963b6`; reconciled final CI `31465013971`, 395/395.
- BATCH-400: B400-001..110 COMPLETE.

## Stable guardrails

- Navigation, reporting, diagnostics, fleet, help, readiness and intelligence GETs do not initiate monitored-SQL collection.
- No browser connects directly to monitored SQL.
- No autonomous remediation or AI SQL execution.
- Credentials/full connection strings/raw provider errors/SQL text remain outside UI, audit, exports and diagnostics.
- Suppression does not rewrite incident evidence.
- Maintenance affects scheduled collection only; manual refresh is explicit and audited.
- Mutations remain POST + antiforgery + named authorization policy.
- MultiNode remains fail-closed behind shared-state/security/credential readiness.
- Concurrent team lifecycle/reconnect and portal/typography work remain preserved.
