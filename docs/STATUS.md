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
| P0.5 / #116 | ACTIVE | repository preparation + deployment/evidence automation ready; external IIS/HTTPS acceptance pending |

## P0.5 repository preparation

- Acceptance tooling PR #127 merged as `9bdd96940454f2586c0e81ff0c25a524d7f1281c`.
- Production-candidate PR #126 merged as `d512ee156f07db566898a817f3c76dd3f46c1091`.
- Candidate/docs reconciliation PR #128 merged as `564f7655a1001da98addd793a000a15d069a243a`.
- Safe IIS SingleNode deployment automation merged on `main` as `7cb47945b47aab6558f7132dcfa818b9f02d2b20`.
- Deployment automation includes read-only IIS preflight, plan-first/apply-gated versioned deployment, stable `App_Data`, automatic IIS `physicalPath` rollback, production PowerShell syntax validation and operations-bundle tooling.
- BATCH-500 added a fail-closed production-safety layer without claiming external acceptance.
- BATCH-600 added live operator readiness/evidence orchestration while preserving the same external-acceptance boundary.
- P0.5 Issue #141 is **CLOSED / COMPLETED**. PR #142 squash-merged to `main` as `5ee5431cce26e875d80a4cfb623553f762c8a161` and ships a machine-verifiable external acceptance evidence pack plus fail-closed closure validator.
- The evidence-pack generator creates exactly 15 required external gates with every gate `false`; it cannot create a completed acceptance. The validator requires exact gate/property sets, UTC timestamps, relative evidence files, matching SHA-256, exact SingleNode candidate/environment metadata, operator acceptance metadata and secret-safe evidence before producing a PASS closure summary.
- Final synchronized PR #142 evidence tested source head `a6844da6f9e77f9842075ff815681f35aa006908` on exact merge ref `34461998c22c8c1f80158d99de9ecb670622e632` against then-current `main` `fb5109e19fe6c1475dbeaf7af1a9406156e1511c`.
- Normal CI `31527104922`: **Green**; Release build/test gate Green.
- Real SQL `31527104916`: **Green**, SQL Server 2022 + SQL Agent operational readiness, non-sysadmin least-privilege login, **8/8 RealSql passed**, Release **0 warnings / 0 errors**.
- Windows production-candidate `31527104897`: **Green end-to-end**, Release **0 warnings / 0 errors**, **746/746 tests passed**, production PowerShell parser Green, synthetic **15/15 evidence closure PASS**, false-gate/tampered-hash/secret-bearing negative packs rejected, HTTPS health/authentication passed before and after restart, package validation and upload Green.
- Current selected repository-verified cutover candidate: `Monitor-0.1.0-rc.39-win-x64.zip`; product SHA-256 `5e354bc0cf8b5935b078f1cacc5b7f06f2c5226fe58228a92f050376f382c1cb`; Actions artifact ID `9115424834`.
- RC.39 `_operations` includes `production-acceptance-evidence.example.json`, `New-ProductionAcceptanceEvidencePack.ps1`, and `Test-ProductionAcceptanceEvidence.ps1` in addition to IIS preflight/deploy/acceptance/smoke and rollback tooling.
- **Repository evidence tooling is complete; no external gate is implied by CI. #116 and #111 remain OPEN until the real trusted-certificate Windows/IIS SingleNode target produces a valid 15/15 external evidence pack and closure summary.**

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
| P0-049 versioned artifact/checksum/evidence | **REPOSITORY/CI COMPLETE**; RC.39 + 15-gate closure tooling VERIFIED |
| P0-050 final production acceptance | **PENDING EXTERNAL** |

## BATCH-600 — Live Operator Readiness & Evidence Orchestration

**Issue:** #134 — CLOSED / COMPLETED  
**PR:** #139 — squash-merged  
**Merge commit:** `08513eeae75d70b8a499124f6ed19628c8a27f19`  
**Task range:** B600-001..100  
**State:** **100/100 COMPLETE**.

B600 delivered deterministic fail-closed repository orchestration for evidence freshness, gate dependency graph, operator action queue, change-window safety, candidate promotion, evidence completeness, secret-safe summaries, fleet readiness, acceptance snapshot versioning/ETag and a versioned release contract.

### Final exact-head merge evidence

- Source head `173f9dba6254f92c2e4725ad3f00810e5027a133`; exact merge ref `6cf3bb13fffb5593b12d78c766694f4a0bcc45ab`.
- Normal CI `31500683477`: **Green**, Release **0 warnings / 0 errors**, **738/738**.
- Real SQL `31500683511`: **Green**, **8/8**.
- Windows production-candidate `31500683448`: **Green**, **738/738**.
- Candidate `Monitor-0.1.0-rc.34-win-x64.zip`; product SHA-256 `13a5f0997a1ece31264cb6b9df4e7b2a96af0b7b95243dcacfce70d7cc69a089`; artifact ID `9104965992`.
- Exactly 100 mapped tests `B600_001..B600_100`; Read-policy endpoint `GET /production/v2/readiness-contract`.

## BATCH-500 — Production Acceptance & Recovery Safety

**Issue:** #130 — CLOSED / COMPLETED  
**PR:** #131 — squash-merged as `9d27491a9739ba05b8c3df1da3eb2e5d435d5cf6`  
**Task range:** B500-001..100  
**State:** **100/100 COMPLETE**.

- Final normal CI `31488431712`: 638/638.
- Real SQL `31488431709`: 8/8.
- Windows production-candidate `31488431693`: 638/638.
- Candidate `Monitor-0.1.0-rc.28-win-x64.zip`; product SHA-256 `70d74dafe585959e32cc98b0daef82809abe857b25d37d07fd320c4faf740b70`; artifact ID `9100092563`.

## BATCH-400 — Production DBA diagnostics + portal completion

- B400-001..010: Portal completion and typography via PR #107.
- B400-011..110: 100 production DBA diagnostic tasks via issue #108 / PR #109.
- Final diagnostics PR CI `31468048589`: 498/498.
- B400-001..110: COMPLETE.

## Historical batch baseline

- M0–M8 VERIFIED.
- BATCH-100: B100-001..100 COMPLETE.
- BATCH-200: B200-001..100 COMPLETE; final CI `31446970475`, 290/290.
- BATCH-300: B300-001..100 COMPLETE; PR #102 merged as `385c2ee7a4d592c1e32e6e00a5c533c8790963b6`; reconciled CI `31465013971`, 395/395.
- BATCH-400: B400-001..110 COMPLETE.
- BATCH-500: B500-001..100 COMPLETE.
- BATCH-600: B600-001..100 COMPLETE.
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

**Overall:** 🟢 verified foundation · 🟢 P0.1–P0.4 COMPLETE · 🟢 P0.5 repository automation/evidence tooling COMPLETE · 🟢 BATCH-500 100/100 COMPLETE · 🟢 BATCH-600 100/100 COMPLETE · 🟢 #141 COMPLETE · 🟡 external IIS/HTTPS 15-gate acceptance pending · 🔴 production acceptance not yet granted
