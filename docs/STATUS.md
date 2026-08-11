# Project Status

## CURRENT P0 — Real SQL Production MVP

**Updated:** 2026-08-11  
**Umbrella:** #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Production acceptance guide:** `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`  
**Active external release gate:** #116 / P0.5 First Production SingleNode  
**Production target:** actual Windows/IIS trusted-HTTPS SingleNode acceptance.

### P0 release chain

| Release | State | Evidence |
|---|---|---|
| P0.1 / #112 | COMPLETE | PR #119; final CI `31476747212`; 501/501 |
| P0.2 / #113 | COMPLETE | PR #121; final CI `31478470867`; 505/505 |
| P0.3 / #114 | COMPLETE | PR #122 merged `245bb0770d7ec6e7a334f7763d3560cef80324fe`; final CI `31479311552`; 507/507 |
| P0.4 / #115 | COMPLETE | PR #124 merged `f4c08292734c293a6d0b865cc2a005b8c42b02a6`; normal `31481874425` 518/518; Real SQL `31481874501` 8/8 |
| P0.5 / #116 | ACTIVE | repository preparation + deployment automation ready; external IIS/HTTPS acceptance pending |

## P0.5 repository preparation

- Acceptance tooling PR #127 merged as `9bdd96940454f2586c0e81ff0c25a524d7f1281c`.
- Production-candidate PR #126 merged as `d512ee156f07db566898a817f3c76dd3f46c1091`.
- Candidate/docs reconciliation PR #128 merged as `564f7655a1001da98addd793a000a15d069a243a`.
- Safe IIS SingleNode deployment automation merged on `main` as `7cb47945b47aab6558f7132dcfa818b9f02d2b20`.
- Deployment automation adds read-only IIS preflight, plan-first/apply-gated versioned deployment, stable `App_Data`, automatic IIS `physicalPath` rollback, production PowerShell parse validation and operations-bundle tooling.
- `7cb479...` merge evidence: normal CI `31487059992` Green; Windows production-candidate `31487060032` Green with 538/538 tests.
- Final RC.20 candidate before deployment-automation continuation: `Monitor-0.1.0-rc.20-win-x64.zip`, SHA-256 `27743b8f3f162a43f8c1bb6b7b9d1977dc5d1c55e8a4664f0c84294b094150bc`, artifact ID `9099041392`.

### P0.5 task status

| Task | State |
|---|---|
| P0-041 SingleNode scope freeze | REPOSITORY/CI COMPLETE |
| P0-042 secret-free production configuration | REPOSITORY/CI COMPLETE |
| P0-043 actual IIS + trusted HTTPS deployment | **PENDING EXTERNAL** |
| P0-044 Data Protection / protected credentials after restart | CI process-restart VERIFIED; **IIS recycle pending external** |
| P0-045 durable registration/audit/history/incidents after IIS recycle | **PENDING EXTERNAL** |
| P0-046 deployment health smoke | CI HTTPS VERIFIED; tooling READY; **real IIS endpoint pending external** |
| P0-047 least-privilege monitored target | P0.4 prerequisite VERIFIED; **deployed IIS identity/target pending external** |
| P0-048 backup + rollback/recovery | code/unit/tooling VERIFIED; **production rehearsal pending external** |
| P0-049 versioned artifact/checksum/evidence | REPOSITORY/CI COMPLETE |
| P0-050 final production acceptance | **PENDING EXTERNAL** |

## BATCH-500 — Production Acceptance & Recovery Safety

**Issue:** #130  
**PR:** #131  
**Branch:** `agent/b500-production-safety`  
**Task range:** B500-001..100  
**State:** **100/100 IMPLEMENTED + CI VERIFIED; final docs-synchronized PR gate / squash merge pending.**

B500 adds deterministic fail-closed repository safety contracts for:

- deployment evidence validation;
- IIS configuration readiness;
- HTTPS certificate readiness including exact/one-label wildcard SAN policy;
- restart/recycle durability;
- backup and rollback safety;
- deployed least-privilege SQL policy;
- HTTPS health/authentication smoke;
- cutover/change-window Go/No-Go safety;
- evidence redaction/export safety;
- versioned 100-task release contract.

Verification on the implementation head merged virtually with current `main`:

- normal CI `31488078873`: **Green**;
- Release build: **0 warnings / 0 errors**;
- full suite: **638/638 passed**;
- Windows production-candidate `31488078882`: **Green end-to-end**;
- Windows gate passed Release build, 638 tests, production PowerShell syntax checks, `win-x64` publish, secret-free validation, HTTPS health/auth, restart/auth recovery, package validation and artifact upload;
- generated Windows artifact: `Monitor-0.1.0-rc.26-win-x64`, artifact ID `9099961916`;
- exactly 100 mapped B500 tests: `B500_001..B500_100`;
- Read-policy contract endpoint: `GET /production/v1/acceptance-contract`;
- detailed ledger: `docs/BATCH_500.md`.

**B500 does not close P0.5.** Its release gate explicitly rejects a repository/CI claim that external IIS acceptance already occurred. #116 and #111 remain open until the real environment evidence is PASS.

## BATCH-400 — Production DBA diagnostics + portal completion

- B400-001..010: Portal completion and typography via PR #107.
- B400-011..110: 100 production DBA diagnostic tasks via issue #108 / PR #109.
- Diagnostics include wait-stat intelligence, query regression, TempDB, transaction log, I/O, SQL Agent reliability, HA readiness, maintenance decision safety, fleet correlation and a Read-policy release contract.
- Final diagnostics PR CI `31468048589`: 498/498.
- B400-001..110: COMPLETE.

## Historical batch baseline

- M0–M8 VERIFIED.
- BATCH-100: B100-001..100 COMPLETE.
- BATCH-200: B200-001..100 COMPLETE; final CI `31446970475`, 290/290.
- BATCH-300: B300-001..100 COMPLETE; PR #102 merged as `385c2ee7a4d592c1e32e6e00a5c533c8790963b6`; reconciled CI `31465013971`, 395/395.
- BATCH-400: B400-001..110 COMPLETE.
- BATCH-500: B500-001..100 IMPLEMENTED + CI VERIFIED; PR #131 merge pending.

## Stable guardrails

- Monitoring/navigation GETs do not initiate monitored-SQL collection.
- No browser connects directly to monitored SQL.
- No autonomous remediation or AI SQL execution.
- Credentials/full connection strings/raw provider errors/arbitrary SQL text stay outside UI, audit, exports and diagnostics.
- Suppression does not rewrite incident evidence.
- Maintenance affects scheduled collection only; manual refresh remains explicit and audited.
- Mutations remain POST + antiforgery + named authorization policy.
- MultiNode remains fail-closed and deferred until after stable SingleNode production acceptance.
- Concurrent team work must be preserved; external P0.5 acceptance cannot be inferred from CI.

**Overall:** 🟢 verified foundation · 🟢 P0.1–P0.4 COMPLETE · 🟢 P0.5 repository automation ready · 🟢 BATCH-500 100/100 CI VERIFIED · 🟡 B500 final merge pending · 🟡 external IIS/HTTPS acceptance pending · 🔴 production acceptance not yet granted
