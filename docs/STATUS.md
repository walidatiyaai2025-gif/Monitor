# Project Status

## CURRENT P0 — Real SQL Production MVP

**Updated:** 2026-08-11  
**Branch:** `main`  
**Umbrella:** #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Production acceptance guide:** `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`  
**Active release gate:** #116 / P0.5 First Production SingleNode  
**Production target:** actual IIS/HTTPS SingleNode release acceptance.

### Release chain

| Release | State | Evidence |
|---|---|---|
| P0.1 / #112 | COMPLETE | PR #119; final CI `31476747212`; 501/501 |
| P0.2 / #113 | COMPLETE | PR #121; final CI `31478470867`; 505/505 |
| P0.3 / #114 | COMPLETE | PR #122 merged `245bb0770d7ec6e7a334f7763d3560cef80324fe`; final CI `31479311552`; 507/507 |
| P0.4 / #115 | COMPLETE | PR #124 merged `f4c08292734c293a6d0b865cc2a005b8c42b02a6`; normal `31481874425` 518/518; Real SQL `31481874501` 8/8 |
| P0.5 / #116 | ACTIVE | PR #127 acceptance tooling merged; PR #126 production candidate merged `d512ee156f07db566898a817f3c76dd3f46c1091`; external IIS/HTTPS acceptance pending |

### P0.5 repository preparation — COMPLETE · EXTERNAL IIS PENDING

- Acceptance-tooling PR #127 squash-merged as `9bdd96940454f2586c0e81ff0c25a524d7f1281c`; CI `31485290730`: Release 0 warnings / 0 errors; 522/522 passed.
- Production-candidate PR #126 squash-merged to `main` as `d512ee156f07db566898a817f3c76dd3f46c1091`.
- Final tested PR head: `da69636f2ba3525426c43aaca17d17617244d9d4`.
- Exact tested merge ref including PR #127: `f65ed48cea537b3ee7524f244a75a62fb442bd55`.
- Normal CI `31485712373`: **Green**.
- Real SQL `31485712392`: **Green**; Release 0 warnings / 0 errors; 8/8 RealSql passed after SQL Agent operational-readiness hardening.
- Windows production-candidate `31485712370`: **Green end-to-end** on Windows Server 2025.
- Release build: **0 warnings / 0 errors**.
- Full Windows candidate suite: **531/531 passed**, 0 failed, 0 skipped.
- RID-specific `win-x64` restore/publish completed successfully.
- Clean publish validation proved: SingleNode enabled; shared state disabled; distributed coordination disabled; Development admin credential absent; persisted `App_Data` state absent.
- Runtime Production configuration used masked ephemeral administrator material only; no runtime credential material is included in the release package.
- Candidate ran over `https://localhost` with an ephemeral loopback certificate. The CI certificate-validation bypass is explicitly restricted to HTTPS loopback targets.
- `/health/live`, `/health/ready`, and `/health` passed over HTTPS before and after process restart.
- A real Administrator login passed using the production PBKDF2 verifier and antiforgery token; authenticated `/servers/connections` access was verified before and after restart.
- The local Data Protection key-ring directory was verified after restart.
- Runtime Production config and runtime state were deleted before packaging and the clean publish input was revalidated.
- Final candidate: `Monitor-0.1.0-rc.20-win-x64.zip`.
- Candidate SHA-256: `27743b8f3f162a43f8c1bb6b7b9d1977dc5d1c55e8a4664f0c84294b094150bc`.
- GitHub Actions artifact ID: `9099041392`; uploaded artifact size: 4,770,438 bytes.
- Release manifest records tested merge SHA, source head SHA, `win-x64`, `.NET 8`, Release configuration and SingleNode mode.

### P0.5 task status

| Task | State |
|---|---|
| P0-041 SingleNode scope freeze | REPOSITORY/CI COMPLETE |
| P0-042 secret-free production configuration | REPOSITORY/CI COMPLETE |
| P0-043 actual IIS + trusted HTTPS deployment | **PENDING EXTERNAL** |
| P0-044 Data Protection / protected credentials after restart | CI process-restart VERIFIED; **IIS recycle pending external** |
| P0-045 durable registration/audit/history/incidents after IIS recycle | **PENDING EXTERNAL** |
| P0-046 deployment health smoke | CI HTTPS VERIFIED; acceptance tooling READY; **real IIS endpoint pending external** |
| P0-047 least-privilege monitored target | P0.4 prerequisite VERIFIED; **deployed IIS identity/target pending external** |
| P0-048 backup + rollback/recovery | code/unit + tooling VERIFIED; **production rehearsal pending external** |
| P0-049 versioned artifact/checksum/evidence | COMPLETE — RC.20 + SHA-256 + artifact recorded |
| P0-050 final production acceptance | **PENDING EXTERNAL** |

### Management decision

- Repository-side P0.5 preparation is merged to `main`; the remaining active work is actual SingleNode IIS/HTTPS acceptance.
- #116 and umbrella #111 remain open. They must not close from repository CI, packaging, Windows runner validation, or PR merge alone.
- Use `scripts/Accept-ProductionSingleNode.ps1` and `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md` for the external cutover evidence.
- Required external evidence: trusted HTTPS/IIS binding, intended application-pool identity, IIS recycle durability, deployed least-privilege SQL behavior, operational backup, rollback/recovery rehearsal, and final health/auth/read verification.
- MultiNode activation remains deferred until after the first stable SingleNode production release.

**Overall:** 🟢 verified foundation · 🟢 P0.1 COMPLETE · 🟢 P0.2 COMPLETE · 🟢 P0.3 COMPLETE · 🟢 P0.4 COMPLETE · 🟢 P0.5 repository candidate merged · 🟡 external IIS/HTTPS acceptance pending · 🔴 production acceptance not yet granted

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
