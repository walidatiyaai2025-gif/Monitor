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
- Deployment automation includes read-only IIS preflight, plan-first/apply-gated versioned deployment, stable `App_Data`, automatic IIS `physicalPath` rollback, production PowerShell syntax validation and operations-bundle tooling.
- `7cb479...` evidence: normal CI `31487059992` Green; Windows production-candidate `31487060032` Green with 538/538 tests.
- BATCH-500 added a fail-closed production-safety layer without claiming external acceptance.
- BATCH-600 adds live operator readiness/evidence orchestration while preserving the same external-acceptance boundary.

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

## BATCH-600 — Live Operator Readiness & Evidence Orchestration

**Issue:** #134 — CLOSED / COMPLETED  
**PR:** #139 — squash-merged  
**Merge commit:** `08513eeae75d70b8a499124f6ed19628c8a27f19`  
**Task range:** B600-001..100  
**State:** **100/100 COMPLETE**.

B600 delivered deterministic fail-closed repository orchestration for:

- evidence freshness and source normalization;
- gate dependency graph and prerequisite readiness;
- operator action queue, ownership and priority;
- change-window/freeze/approval/backup/rollback-owner safety;
- candidate version/hash/commit validation and promotion safety;
- evidence completeness, confidence and contradiction detection;
- secret-safe operator summaries and export allowlists;
- fleet readiness aggregation and blast-radius reporting;
- acceptance snapshot versioning, monotonic sequence and deterministic ETag;
- versioned 100-task release contract.

### Final exact-head merge evidence

All final gates tested PR #139 source head `173f9dba6254f92c2e4725ad3f00810e5027a133` on exact merge ref `6cf3bb13fffb5593b12d78c766694f4a0bcc45ab` against then-current `main` `020f4f1d0576d42af74db88537ca0690ea3b8f47`.

- Normal CI `31500683477`: **Green**.
- Release build: **0 warnings / 0 errors**.
- Full suite: **738/738 passed**, 0 failed, 0 skipped.
- Real SQL `31500683511`: **Green**, SQL Server 2022, SQL Agent operational readiness, non-sysadmin least-privilege login, **8/8 RealSql passed**.
- Windows production-candidate `31500683448`: **Green end-to-end** on Windows Server 2025 with **738/738 tests passed**.
- Windows gate passed production PowerShell syntax checks, `win-x64` publish, secret-free SingleNode validation, HTTPS health/authentication before and after restart, clean package validation and artifact upload.
- Final tested candidate: `Monitor-0.1.0-rc.34-win-x64.zip`.
- Product ZIP SHA-256: `13a5f0997a1ece31264cb6b9df4e7b2a96af0b7b95243dcacfce70d7cc69a089`.
- GitHub Actions artifact ID: `9104965992`.
- Exactly 100 mapped B600 tests: `B600_001..B600_100`.
- Read-policy contract endpoint: `GET /production/v2/readiness-contract`.
- Detailed task ledger: `docs/BATCH_600.md`.

**BATCH-600 does not close P0.5.** Repository CI and Windows runner evidence do not prove the external Windows/IIS trusted-HTTPS deployment. #116 and #111 remain open until actual environment acceptance is PASS.

## BATCH-500 — Production Acceptance & Recovery Safety

**Issue:** #130 — CLOSED / COMPLETED  
**PR:** #131 — squash-merged  
**Merge commit:** `9d27491a9739ba05b8c3df1da3eb2e5d435d5cf6`  
**Task range:** B500-001..100  
**State:** **100/100 COMPLETE**.

B500 delivered deterministic fail-closed repository safety contracts for:

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

### Final exact-head merge evidence

All final gates tested PR #131 source head `10a072eaceb14f1aa8bc1f2070c65f26c5654ffd` on exact merge ref `8d2d580e7e43a47bd57f173d783111b885f28416` against then-current `main` `7cb47945b47aab6558f7132dcfa818b9f02d2b20`.

- Normal CI `31488431712`: **Green**.
- Release build: **0 warnings / 0 errors**.
- Full suite: **638/638 passed**, 0 failed, 0 skipped.
- Real SQL `31488431709`: **Green**, SQL Server 2022, SQL Agent operational readiness, non-sysadmin least-privilege login, **8/8 RealSql passed**.
- Windows production-candidate `31488431693`: **Green end-to-end** on Windows Server 2025 with **638/638 tests passed**.
- Windows gate passed production PowerShell syntax checks, `win-x64` publish, secret-free validation, HTTPS health/authentication before and after restart, package validation and artifact upload.
- Final tested candidate: `Monitor-0.1.0-rc.28-win-x64.zip`.
- Product ZIP SHA-256: `70d74dafe585959e32cc98b0daef82809abe857b25d37d07fd320c4faf740b70`.
- GitHub Actions artifact ID: `9100092563`.
- Exactly 100 mapped B500 tests: `B500_001..B500_100`.
- Read-policy contract endpoint: `GET /production/v1/acceptance-contract`.
- Detailed task ledger: `docs/BATCH_500.md`.

**BATCH-500 does not close P0.5.** Its release contract explicitly rejects a repository/CI claim that external IIS acceptance already occurred. #116 was kept/reopened and #111 remains open until the real environment evidence is PASS.

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
- BATCH-500: B500-001..100 COMPLETE; PR #131 merged as `9d27491a9739ba05b8c3df1da3eb2e5d435d5cf6`; final gates normal `31488431712` 638/638, Real SQL `31488431709` 8/8, Windows `31488431693` 638/638.
- BATCH-600: B600-001..100 COMPLETE; PR #139 squash-merged as `08513eeae75d70b8a499124f6ed19628c8a27f19`; final gates normal `31500683477` 738/738, Real SQL `31500683511` 8/8, Windows `31500683448` 738/738.
- Total completed batch task IDs across B100+B200+B300+B400+B500+B600: **610**.

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

**Overall:** 🟢 verified foundation · 🟢 P0.1–P0.4 COMPLETE · 🟢 P0.5 repository automation ready · 🟢 BATCH-500 100/100 COMPLETE · 🟢 BATCH-600 100/100 COMPLETE · 🟡 external IIS/HTTPS acceptance pending · 🔴 production acceptance not yet granted
