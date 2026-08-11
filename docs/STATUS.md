# Project Status

## CURRENT P0 — Real SQL Production MVP

**Updated:** 2026-08-12  
**Umbrella:** #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Production acceptance guide:** `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`  
**Active external release gate:** #116 / P0.5 First Production SingleNode  
**Repository finalization workflow:** COMPLETE through #147 / PR #148  
**Production target:** actual Windows/IIS trusted-HTTPS SingleNode acceptance.

### P0 release chain

| Release | State | Evidence |
|---|---|---|
| P0.1 / #112 | COMPLETE | PR #119; final CI `31476747212`; 501/501 |
| P0.2 / #113 | COMPLETE | PR #121; final CI `31478470867`; 505/505 |
| P0.3 / #114 | COMPLETE | PR #122 merged `245bb0770d7ec6e7a334f7763d3560cef80324fe`; final CI `31479311552`; 507/507 |
| P0.4 / #115 | COMPLETE | PR #124 merged `f4c08292734c293a6d0b865cc2a005b8c42b02a6`; normal `31481874425` 518/518; Real SQL `31481874501` 8/8 |
| P0.5 / #116 | ACTIVE | repository deployment/evidence/finalization tooling complete; external IIS/HTTPS acceptance pending |

## P0.5 repository preparation — COMPLETE · EXTERNAL IIS PENDING

- Acceptance tooling PR #127 merged as `9bdd96940454f2586c0e81ff0c25a524d7f1281c`.
- Production-candidate PR #126 merged as `d512ee156f07db566898a817f3c76dd3f46c1091`.
- Candidate/docs reconciliation PR #128 merged as `564f7655a1001da98addd793a000a15d069a243a`.
- Safe IIS SingleNode deployment automation merged as `7cb47945b47aab6558f7132dcfa818b9f02d2b20`.
- BATCH-500 added fail-closed production acceptance/recovery safety; BATCH-600 added live operator readiness/evidence orchestration without changing the external-acceptance boundary.
- Issue #141 / PR #142 COMPLETE: machine-verifiable exact 15-gate external acceptance pack + fail-closed closure validator, merged `5ee5431cce26e875d80a4cfb623553f762c8a161`.
- Issue #144 / PR #145 COMPLETE: explicit one-gate recorder `Set-ProductionAcceptanceGate.ps1`, merged `8a548c984c62b904a184e54415ea7bf491dc78fb`.
- Issue #147 / PR #148 COMPLETE: `Complete-ProductionAcceptance.ps1` removes the last manual `acceptedBy` / `acceptedAtUtc` edit and adds explicit final acknowledgement, prospective 15/15 validation, concurrent-pack mutation detection, atomic final metadata commit, authoritative revalidation/closure summary and fail-closed rollback. PR #148 squash-merged as `e15a9654fbe744e426c95d5965a5faba60868e14`.

### Final PR #148 / RC.43 repository evidence

- source head `d05bea3ea1372a6566eb9c237bb06e84de681014`;
- exact tested merge ref `0445ac9c8bbeafb075a506a06231dd87c4b1b27b`;
- normal CI `31537914600`: **Green**;
- Real SQL `31537914667`: **Green**, SQL Server 2022 + Agent + non-sysadmin least privilege, **8/8**;
- Windows production-candidate `31537914596`: **Green end-to-end**, Release **0 warnings / 0 errors**, **761/761 tests passed**;
- PowerShell parser, one-gate recorder runtime and finalizer runtime all Green;
- synthetic exact 15 gates were recorded through the recorder, then prospective 15/15 validation, authoritative 15/15 validation, final metadata commit and independent validator recheck all passed;
- negative premature/no-ack/unsafe-path/unsafe-operator/re-finalization/false-gate/tampered-hash/secret-bearing cases were rejected;
- HTTPS health + Administrator authentication passed before and after process restart;
- package validation proved `SingleNode=True`, `DevelopmentCredentialPublished=False`, `PersistedStatePublished=False`, `PackageValidated=True`;
- selected cutover candidate `Monitor-0.1.0-rc.43-win-x64.zip`;
- product SHA-256 `95d6d545cfa53fb514814fb22c82cfafc2c14cf28c1e07c15177852b677234aa`;
- Actions artifact ID `9119560465`.

RC.43 supersedes RC.41 unless a later equivalently verified candidate is explicitly selected on #116. The packaged `_operations` directory now contains IIS preflight/deploy/acceptance/smoke tooling, the 15-gate pack generator, one-gate recorder, explicit finalizer, closure validator and rollback runbooks. No manual JSON gate/final-acceptance editing is required.

**No external IIS gate is implied by repository CI. #116 and #111 remain OPEN until the real trusted-certificate Windows/IIS SingleNode target produces a valid real 15/15 evidence pack, explicit approved operator finalization and reviewed closure summary.**

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
| P0-049 versioned artifact/checksum/evidence workflow | **REPOSITORY/CI COMPLETE** — RC.43 + generator + recorder + finalizer + validator VERIFIED |
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
- Credentials/full connection strings/raw provider errors/arbitrary SQL text stay outside UI, audit, exports, diagnostics and production evidence.
- Suppression does not rewrite incident evidence.
- Maintenance affects scheduled collection only; manual refresh remains explicit and audited.
- Mutations remain POST + antiforgery + named authorization policy.
- MultiNode remains fail-closed and deferred until after stable SingleNode production acceptance.
- Concurrent team work must be preserved; external P0.5 acceptance cannot be inferred from CI.

**Overall:** 🟢 verified foundation · 🟢 P0.1–P0.4 COMPLETE · 🟢 P0.5 repository cutover/evidence/finalization workflow COMPLETE · 🟡 external IIS/HTTPS 15-gate acceptance pending · 🔴 production acceptance not yet granted
