# Project Status

## CURRENT P0 — Real SQL Production MVP

**Updated:** 2026-08-13  
**Umbrella:** #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Production acceptance guide:** `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`  
**Active external release gate:** #116 / P0.5 First Production SingleNode  
**Repository cutover/evidence/finalization/session workflow:** COMPLETE through #150 / PR #151  
**Release-integrity hardening:** #154 / PR #155 IN PROGRESS — tagged/manual releases are being routed through the verified Windows production-candidate workflow; RC.53 remains selected until exact-head replacement gates are Green.  
**Production target:** actual Windows/IIS trusted-HTTPS SingleNode acceptance.

### P0 release chain

| Release | State | Evidence |
|---|---|---|
| P0.1 / #112 | COMPLETE | PR #119; final CI `31476747212`; 501/501 |
| P0.2 / #113 | COMPLETE | PR #121; final CI `31478470867`; 505/505 |
| P0.3 / #114 | COMPLETE | PR #122 merged `245bb0770d7ec6e7a334f7763d3560cef80324fe`; final CI `31479311552`; 507/507 |
| P0.4 / #115 | COMPLETE | PR #124 merged `f4c08292734c293a6d0b865cc2a005b8c42b02a6`; normal `31481874425` 518/518; Real SQL `31481874501` 8/8 |
| P0.5 / #116 | ACTIVE | core repository deployment/evidence/session/finalization tooling complete; #154 release-package parity hardening in progress; external IIS/HTTPS acceptance pending |

## P0.5 repository preparation — CORE COMPLETE · RELEASE PARITY HARDENING ACTIVE · EXTERNAL IIS PENDING

- Acceptance tooling PR #127 merged as `9bdd96940454f2586c0e81ff0c25a524d7f1281c`.
- Production-candidate PR #126 merged as `d512ee156f07db566898a817f3c76dd3f46c1091`.
- Candidate/docs reconciliation PR #128 merged as `564f7655a1001da98addd793a000a15d069a243a`.
- Safe IIS SingleNode deployment automation merged as `7cb47945b47aab6558f7132dcfa818b9f02d2b20`.
- BATCH-500 added fail-closed production acceptance/recovery safety; BATCH-600 added live operator readiness/evidence orchestration without changing the external-acceptance boundary.
- Issue #141 / PR #142 COMPLETE: machine-verifiable exact 15-gate external acceptance pack + fail-closed closure validator, merged `5ee5431cce26e875d80a4cfb623553f762c8a161`.
- Issue #144 / PR #145 COMPLETE: explicit one-gate recorder `Set-ProductionAcceptanceGate.ps1`, merged `8a548c984c62b904a184e54415ea7bf491dc78fb`.
- Issue #147 / PR #148 COMPLETE: `Complete-ProductionAcceptance.ps1` removes manual final `acceptedBy` / `acceptedAtUtc` edits and adds explicit final acknowledgement, prospective 15/15 validation, concurrent-pack mutation detection, atomic final metadata commit, authoritative revalidation/closure summary and fail-closed rollback. PR #148 squash-merged as `e15a9654fbe744e426c95d5965a5faba60868e14`.
- Issue #150 is **CLOSED / COMPLETED**. PR #151 squash-merged as `9a76abe61422502c4889b04ce8b6a59f18ac04f4` and adds `New-ProductionAcceptanceSession.ps1`: one fresh immutable candidate-bound workspace with verified candidate/checksum bytes, a SHA-locked non-secret manifest, the canonical fail-closed 15-gate pack at 0/15 and deterministic operator next steps. It performs no IIS/SQL/GitHub/gate-PASS/final-acceptance side effect.
- Issue #154 / PR #155 is **IN PROGRESS** after direct RC.53 artifact audit found release-package drift: `release.yml` independently published/zipped instead of reusing the verified Windows production-candidate path, while RC manifests labeled fixed P0.4 prerequisite runs ambiguously. PR #155 makes the candidate workflow reusable, routes tagged/manual releases through it, and changes manifest schema to distinguish historical P0.4 prerequisite evidence from candidate-specific evidence tracked on #116. No external gate is implied.

### Final selected PR #151 / RC.53 repository evidence

- source head `b2b004e1a811dfe0eb4197be893aac5116c58cc2`;
- exact tested merge ref `68cd8f25819f82a9cb7205ed81523f4beb55d5e5`;
- normal CI `31540968009`: **Green**;
- Real SQL `31540967997`: **Green**, SQL Server 2022 + Agent + non-sysadmin least privilege, **8/8**;
- Windows production-candidate `31540968010`: **Green end-to-end**, Release **0 warnings / 0 errors**, **769/769 tests passed**;
- immutable session initializer runtime Green: candidate-bound workspace created at **0/15**, with reuse/tampered-checksum/non-ZIP/secret/relative/traversal negative cases rejected;
- PowerShell parser, one-gate recorder runtime and finalizer runtime Green;
- synthetic exact 15 gates were recorded through the recorder, followed by prospective and authoritative 15/15 validation, final metadata commit and independent validator recheck;
- HTTPS health + Administrator authentication passed before and after process restart;
- package validation proved `SingleNode=True`, `DevelopmentCredentialPublished=False`, `PersistedStatePublished=False`, `PackageValidated=True`;
- selected cutover candidate `Monitor-0.1.0-rc.53-win-x64.zip`;
- product SHA-256 `466e056a85b1389b817fcbd9c622aeacd448c77596e2d5b3a6e450a7f0afca00`;
- Actions artifact ID `9120696113`.

Direct artifact audit on 2026-08-13 independently re-hashed the RC.53 product ZIP to the same SHA-256, confirmed the companion checksum line, and confirmed all 19 expected `_operations` entries are present. RC.53 remains selected until #154/PR #155 produces an equivalently verified replacement candidate and that candidate is explicitly promoted on #116.

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
| P0-049 versioned artifact/checksum/evidence workflow | **CORE REPOSITORY/CI COMPLETE** — RC.53 + immutable session + generator + recorder + finalizer + validator VERIFIED; #154 tagged-release parity hardening IN PROGRESS |
| P0-050 final production acceptance | **PENDING EXTERNAL** |

## BATCH-200 baseline reconciliation — ACTIVE IN PR #156

A current-main audit found a real historical mismatch: `STATUS` reported BATCH-200 complete while `docs/BATCH_200.md` still had B200-051..060 and B200-071..090 as PLANNED, and the corresponding governance/security/scale source files were absent from `main`.

PR #156 selectively restores only those missing B200 capabilities and mapped tests while preserving later `IServerTargetLifecycleService`, BATCH-300 and P0 behavior. Initial current-main implementation CI `31667610170` was Green. The branch also preserves PR #155 release-parity changes after `main` advanced to `8d8ae2c5f35e8a1d774c5a9480f582e432e5dc03`; exact-head final CI is required before merge.

This baseline correction does **not** change #116, does not claim production acceptance and does not automatically replace RC.53 as the selected P0 cutover artifact.

## BATCH-600 — Live Operator Readiness & Evidence Orchestration

**Issue:** #134 — CLOSED / COMPLETED  
**PR:** #139 — squash-merged  
**Merge commit:** `08513eeae75d70b8a499124f6ed19628c8a27f19`  
**Task range:** B600-001..100  
**State:** **100/100 COMPLETE**.

B600 delivered deterministic fail-closed repository orchestration for evidence freshness, gate dependency graph, operator action queue, change-window safety, candidate promotion, evidence completeness, secret-safe summaries, fleet readiness aggregation, acceptance snapshot versioning/ETag and a versioned release contract.

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
- BATCH-200: B200-001..100 historical accounting COMPLETE; **current-main reconciliation PR #156 is active for the previously unmerged B200-051..060 and B200-071..090 code**.
- BATCH-300: B300-001..100 COMPLETE; PR #102 merged as `385c2ee7a4d592c1e32e6e00a5c533c8790963b6`; reconciled CI `31465013971`, 395/395.
- BATCH-400: B400-001..110 COMPLETE.
- BATCH-500: B500-001..100 COMPLETE.
- BATCH-600: B600-001..100 COMPLETE.
- Total completed batch task IDs across B100+B200+B300+B400+B500+B600 remains **610**; PR #156 is baseline reconciliation, not new task accounting.

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

**Overall:** 🟢 verified foundation · 🟢 P0.1–P0.4 COMPLETE · 🟢 P0.5 core repository cutover/evidence/session/finalization workflow COMPLETE · 🟡 #154 tagged-release parity hardening IN PROGRESS · 🟡 external IIS/HTTPS 15-gate acceptance pending · 🟡 BATCH-200 current-main reconciliation PR #156 active · 🔴 production acceptance not yet granted
