# Project Status

## CURRENT P0 — Real SQL Production MVP

**Updated:** 2026-08-17  
**Umbrella:** #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Production acceptance guide:** `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`  
**Active external release gate:** #116 / P0.5 First Production SingleNode  
**Repository cutover/evidence/finalization/session/release workflow:** COMPLETE through selected-product-hash hardening PR #257, locked-session sidecar binding PR #259 and exact Acceptance Control Toolkit provenance PR #262; selected RC.61 durable publication remains pending manual dispatch under #162. Exact cutover toolkit source is `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`. Read-only operator preflight #266 / PR #267 is COMPLETE, squash-merged as `43aaa6071fd0c577c792d427ad490717f28acbac`; exact head `cdaff693810534db52975976309b726a0a8d409c` passed CI #1843, Real SQL #121 and Windows production-candidate #203, with post-merge main CI #1844 Green. Final Step 0 operator handoff #270 / PR #271 is also COMPLETE, squash-merged as `479f9b557948b56fc5ec5692efb67fd6f1f4a921` after CI #1854 and Windows production-candidate #205 Green; post-merge main CI #1855 Green. These changes do not dispatch or publish RC.61.  
**Remaining dependency:** `#162 durable RC.61 publication + independent verification -> #116 real trusted-IIS 15/15 acceptance -> #111 closure`; **no #116 production mutation while #162 is OPEN**.  
**Production target:** actual Windows/IIS trusted-HTTPS SingleNode acceptance.

### P0 release chain

| Release | State | Evidence |
|---|---|---|
| P0.1 / #112 | COMPLETE | PR #119; final CI `31476747212`; 501/501 |
| P0.2 / #113 | COMPLETE | PR #121; final CI `31478470867`; 505/505 |
| P0.3 / #114 | COMPLETE | PR #122 merged `245bb0770d7ec6e7a334f7763d3560cef80324fe`; final CI `31479311552`; 507/507 |
| P0.4 / #115 | COMPLETE | PR #124 merged `f4c08292734c293a6d0b865cc2a005b8c42b02a6`; normal `31481874425` 518/518; Real SQL `31481874501` 8/8 |
| P0.5 / #116 | ACTIVE / BLOCKED BEFORE MUTATION BY #162 | repository deployment/evidence/session/finalization/release-package/durable-promotion/workflow-supply-chain/native-Node-24/durable-release, selected-product-hash binding, locked-session sidecar binding #258/#259, Acceptance Control Toolkit provenance #261/#262 and Step 0 preflight/operator handoff #266/#267/#270/#271 are complete; selected RC.61 durable publication #162 must complete before external IIS/HTTPS acceptance begins |

## P0.5 repository preparation — COMPLETE · #162 MANUAL RETENTION BLOCKS EXTERNAL IIS MUTATION

- Acceptance tooling PR #127 merged as `9bdd96940454f2586c0e81ff0c25a524d7f1281c`.
- Production-candidate PR #126 merged as `d512ee156f07db566898a817f3c76dd3f46c1091`.
- Candidate/docs reconciliation PR #128 merged as `564f7655a1001da98addd793a000a15d069a243a`.
- Safe IIS SingleNode deployment automation merged as `7cb47945b47aab6558f7132dcfa818b9f02d2b20`.
- BATCH-500 added fail-closed production acceptance/recovery safety; BATCH-600 added live operator readiness/evidence orchestration without changing the external-acceptance boundary.
- Issue #141 / PR #142 COMPLETE: machine-verifiable exact 15-gate external acceptance pack + fail-closed closure validator, merged `5ee5431cce26e875d80a4cfb623553f762c8a161`.
- Issue #144 / PR #145 COMPLETE: explicit one-gate recorder `Set-ProductionAcceptanceGate.ps1`, merged `8a548c984c62b904a184e54415ea7bf491dc78fb`.
- Issue #147 / PR #148 COMPLETE: `Complete-ProductionAcceptance.ps1` removes manual final `acceptedBy` / `acceptedAtUtc` edits and adds explicit final acknowledgement, prospective 15/15 validation, concurrent-pack mutation detection, atomic final metadata commit, authoritative revalidation/closure summary and fail-closed rollback. PR #148 squash-merged as `e15a9654fbe744e426c95d5965a5faba60868e14`.
- Issue #150 / PR #151 COMPLETE: `New-ProductionAcceptanceSession.ps1` creates one fresh immutable candidate-bound workspace with verified candidate/checksum bytes, a SHA-locked non-secret manifest, the canonical fail-closed 15-gate pack at 0/15 and deterministic operator next steps. PR #151 squash-merged as `9a76abe61422502c4889b04ce8b6a59f18ac04f4`.
- Issue #256 / PR #257 COMPLETE: selected-product-hash acceptance-session hardening requires an independent 64-hex `ExpectedProductSha256`, rejects a mutually consistent but substituted ZIP + checksum pair, rechecks the selected hash after candidate copy, binds manifest/evidence to that selected hash and leaves every external gate at 0/15. PR #257 squash-merged as `41410491df19699be6329e26e99a9328965782bc`; exact final head `70d1a8fb6814de1ec23dcff6b9942b945333c052` passed CI #1696, Real SQL #94 and production-candidate #151 Green. This does not publish RC.61 or satisfy #116/#111.
- Issue #258 / PR #259 COMPLETE: locked-session gate/finalization chain-of-custody hardening binds every later gate/finalizer/reviewer action to the externally preserved session-manifest SHA-256 and exact six-file Acceptance Control Toolkit sidecar while leaving RC.61 product/deployment bytes unchanged. Exact source `8d79361cccf98acfc0a1753d16de943458887389` passed CI #1751, Real SQL #112 and Windows production-candidate #170 Green; PR #259 squash-merged as `c22c4e5e4f59576cbb41b8fc46886474f8749ebb`. This cannot manufacture any real production PASS.
- Issue #261 / PR #262 COMPLETE: Acceptance Control Toolkit provenance is exported only from a clean checkout of exact tooling commit `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`, writes a deterministic manifest + canonical SHA-256 lock, is independently re-verified, and binds toolkit-manifest SHA plus the exact six sidecar file hashes into each session. CI #1786 / `31992503009` passed 984/984 and Windows production-candidate #186 / `31992502977` passed end-to-end; PR #262 squash-merged as `a448eb715af9b3a2fcfe89ce92807b71fc7e1127`. Moving refs such as `main` or `latest` are not cutover toolkit identity.
- Issue #154 / PR #155 COMPLETE: direct RC.53 artifact audit exposed release-package drift and ambiguous manifest evidence naming. PR #155 squash-merged as `8d8ae2c5f35e8a1d774c5a9480f582e432e5dc03`; tagged/manual releases now delegate to the same verified Windows production-candidate workflow, candidate versions are validated, and release manifest schema 2 records fixed P0.4 run IDs as `prerequisiteEvidence.p04` rather than candidate-specific acceptance evidence.
- Issue #159 / PR #160 COMPLETE: real pushed version tags publish only the already-verified same-run ZIP + `.sha256` as durable GitHub Release assets after checksum re-verification, with no rebuild/repackage/clobber path and no production-acceptance implication. PR #160 merged as `a14110181932bcd6e14b99e5b6984974a5b477f8`.
- Issue #162 implementation COMPLETE via PR #163 merged `43d8a193205495f155bb8866532a4e99ed93b655`; the manual `promote-existing-candidate` workflow validates and preserves exact existing RC.61 bytes without rebuild/repackage. Handoff docs PR #164 merged `930c057f431a36ab2b603d3dc39e70e8c31c744e` after normal CI `31726008394` and Windows production-candidate `31726008464` Green. **Actual durable RC.61 publication remains PENDING MANUAL DISPATCH; #162 stays OPEN until promotion + separate read-only verification + tag/assets/hash checks are complete.**
- Issue #266 / PR #267 COMPLETE: read-only `Test-Rc61DurablePromotionPreflight.ps1` pins the exact selected RC.61 repository/run/artifact/digest/hash/source/tested-merge/tag, fails closed on provenance, expiry and ambiguous durable-state API failures, and emits the exact approved promotion plus separate verification commands. Exact head `cdaff693810534db52975976309b726a0a8d409c` passed CI #1843, Real SQL #121 and Windows production-candidate #203; PR #267 squash-merged as `43aaa6071fd0c577c792d427ad490717f28acbac`; post-merge main CI #1844 passed. It never dispatches workflows or mutates tag/release state and therefore does not satisfy #162/#116/#111.
- Issue #270 / PR #271 COMPLETE: both RC.61 operator handoffs now require Step 0 before the first publication attempt and require `Status=READY_FOR_EXPLICIT_MANUAL_PROMOTION`, `MutatedGitHubState=False`, `TagExists=False` and `ReleaseExists=False`; stop/investigate is explicit for durable-state presence, artifact expiry, provenance/digest drift or ambiguous GitHub probing. PR #271 squash-merged as `479f9b557948b56fc5ec5692efb67fd6f1f4a921`; CI #1854 and Windows production-candidate #205 were Green and post-merge main CI #1855 passed. This is documentation/handoff only and does not publish RC.61.
- Issue #168 / PR #171 COMPLETE: every active external `actions/*` workflow dependency is pinned to an approved exact 40-character upstream commit SHA; a dedicated fail-closed regression test rejects mutable/unapproved/drifted refs; the completed BATCH-100 one-shot merge workflow with write permissions is removed. PR #171 squash-merged as `c9084dd32b12a9a078f953f85f39b253793e2343`. Exact implementation head `052e969b5ab450526ab996a2e77459f4087846c8` passed normal CI `31881105832`, Real SQL `31881105877`, and Windows production-candidate `31881105818` end-to-end. This does not alter selected RC.61 or satisfy #162/#116/#111.
- Issue #173 / PR #174 COMPLETE: the immutable Action allowlist moved from older Node 20-based majors to official native Node 24 releases while retaining exact SHA pinning and readable version metadata. PR #174 squash-merged as `bc7cb2d275f423fb381b83d92c76f6516e404fe9`. Exact implementation head `8134720cf1260abc7e6c0609a5afa239f31bb5f7` passed normal CI `31881744429`, Real SQL `31881744413`, and Windows production-candidate `31881744437`; Windows passed **814/814**, Release **0 warnings / 0 errors**, HTTPS/auth before and after restart, clean package validation and native Node 24 artifact upload. Approved pins: checkout v7.0.1 `3d3c42e5aac5ba805825da76410c181273ba90b1`, setup-dotnet v6.0.0 `a98b56852c35b8e3190ac28c8c2271da59106c68`, upload-artifact v7.0.1 `043fb46d1a93c77aae656e7c1c64a875d1fc6a0a`, download-artifact v8.0.1 `3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c`. The prior Node 20 deprecation/forced-Node-24 warning is absent. RC.87 is implementation CI evidence only and does not supersede selected RC.61.
- PRs #177–#219 further hardened reproducible SDK/SQL image selection, pinned Linux/Windows runners, NuGet/package-source policy, checkout/token permissions, write-capable workflow allowlists, release mutation serialization, main-ref promotion gating, durable release exact-asset metadata/digest/provenance/TOCTOU/workspace/atomic-publication behavior, independent read-only verification and toolchain capability preflight. PR #219 is the latest merged durable-release hardening batch; none of these changes dispatches RC.61 promotion or alters the external IIS acceptance boundary.

### Final selected PR #155 / RC.61 repository evidence

- source head `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`;
- exact tested merge ref `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- merged main commit `8d8ae2c5f35e8a1d774c5a9480f582e432e5dc03`;
- normal CI `31667721350`: **Green**, Release **0 warnings / 0 errors**, **770/770 tests passed**;
- Real SQL `31667721353`: **Green**, SQL Server 2022 + Agent + non-sysadmin least privilege, **8/8**;
- Windows production-candidate `31667721306`: **Green end-to-end**, Release **0 warnings / 0 errors**, **770/770 tests passed**;
- explicit candidate-version validation, PowerShell parser, immutable session initializer, one-gate recorder and finalizer runtime all Green;
- synthetic exact 15 gates were recorded through the recorder, followed by prospective and authoritative 15/15 validation, final metadata commit and independent validator recheck;
- HTTPS health + Administrator authentication passed before and after process restart;
- package validation proved `SingleNode=True`, `DevelopmentCredentialPublished=False`, `PersistedStatePublished=False`, `PackageValidated=True`;
- selected cutover candidate `Monitor-0.1.0-rc.61-win-x64.zip`;
- product SHA-256 `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- Actions artifact ID `9168574442`;
- outer Actions artifact digest `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`.

Independent artifact inspection after CI re-hashed the RC.61 product ZIP to the same SHA-256, matched the companion checksum, confirmed 95 package files and all 19 expected `_operations` entries, and verified `_operations/release-manifest.json` schema 2 with `prerequisiteEvidence.p04`, `candidateVerification.sourceOfTruth=#116`, `embeddedWorkflowRunIds=false`, and no legacy `realSqlAcceptance` field.

RC.61 supersedes RC.53 unless a later equivalently verified candidate is explicitly selected on #116.

**No external IIS gate is implied by repository CI. #116 production mutation must not begin while #162 is OPEN. After #162 completes, #116 and #111 remain OPEN until the real trusted-certificate Windows/IIS SingleNode target produces a valid real 15/15 evidence pack, explicit approved operator finalization and reviewed closure summary.**

### P0.5 task status

| Task | State |
|---|---|
| P0-041 SingleNode scope freeze | REPOSITORY/CI COMPLETE |
| P0-042 secret-free production configuration | REPOSITORY/CI COMPLETE |
| P0-043 actual IIS + trusted HTTPS deployment | **BLOCKED BY #162; then PENDING EXTERNAL** |
| P0-044 Data Protection / protected credentials after restart | CI process-restart VERIFIED; **IIS recycle blocked by #162 then pending external** |
| P0-045 durable registration/audit/history/incidents after IIS recycle | **BLOCKED BY #162; then PENDING EXTERNAL** |
| P0-046 deployment health smoke | CI HTTPS VERIFIED; tooling READY; **real IIS endpoint blocked by #162 then pending external** |
| P0-047 least-privilege monitored target | P0.4 prerequisite VERIFIED; **deployed IIS identity/target blocked by #162 then pending external** |
| P0-048 backup + rollback/recovery | code/unit/tooling VERIFIED; **production rehearsal blocked by #162 then pending external** |
| P0-049 versioned artifact/checksum/evidence/release workflow | **REPOSITORY HARDENED** — RC.61 + reusable release pipeline + durable tag publication tooling + immutable session + selected-product-hash binding #256 + locked-session sidecar binding #258/#259 + Acceptance Control Toolkit provenance #261/#262 + generator + recorder + finalizer + validator; exact existing-candidate promotion implementation merged; read-only operator preflight #266/#267 COMPLETE; final Step 0 handoff #270/#271 COMPLETE; **actual RC.61 durable publication + separate read-only verification pending manual #162**; supply-chain pinning/removal hardening #168/#171 COMPLETE; native Node 24 Action migration #173/#174 COMPLETE; additional durable-release hardening through PR #219 COMPLETE |
| P0-050 final production acceptance | **BLOCKED BY #162; then PENDING EXTERNAL #116** |

## BATCH-200 baseline reconciliation — COMPLETE

A current-main audit found a real historical mismatch: the historical BATCH-200 completion marker existed while B200-051..060 and B200-071..090 implementation files were absent from `main` and `docs/BATCH_200.md` still marked those task ranges as PLANNED.

Issue #99 is **CLOSED / COMPLETED**. PR #156 selectively restored retention governance, enterprise security hardening and bounded scale primitives plus mapped B200-051..090 regression tests and an audit-pagination regression, while preserving `IServerTargetLifecycleService`, later BATCH-300/P0 behavior and merged RC.61 release-parity work. PR #156 squash-merged as `221e44a9f13ed02e994311addff94b0e7996e444`.

Final exact-head `98d8cc54b2483fb7bad641680fd1f90e3802a9c4` verification:
- normal CI `31669072593`: **Green**;
- Real SQL `31669072572`: **Green** against SQL Server 2022 + Agent + non-sysadmin least privilege;
- Windows production-candidate `31669072625`: **Green end-to-end**, including Release build/full suite, PowerShell parser, immutable session, recorder, finalizer, win-x64 publish, secret-free validation, HTTPS/auth smoke before and after restart, clean package revalidation, ZIP/SHA-256 and artifact upload.

Legacy issues #87/#91/#93 are closed completed. Historical PRs #88/#92/#94/#104 are closed unmerged as superseded so branch-era lifecycle deltas cannot be accidentally merged over current main.

This baseline correction is historical reconciliation rather than new task accounting. It does not change #116, does not claim production acceptance and does not replace RC.61.

## BATCH-700 — Full visible portal/UI completion — COMPLETE

**Parent:** #220 — CLOSED / COMPLETED  
**Children:** #221–#225 — CLOSED / COMPLETED  
**Task range:** UI700-001..050  
**State:** **50/50 COMPLETE and merged to `main`.**

BATCH-700 closes the gap between feature/backend completion and purposeful operator pages. It adds safe error surfaces, reusable UI states, mobile/keyboard shell behavior, dedicated Health pages, complete Audit/History workflows, bounded recommendation filtering, report metadata/discoverability, task-oriented Enterprise/Admin workflows and an executable visible-route contract smoke.

Merged child evidence:
- #221 / PR #236 merged `59a931cc031e19f162edfadc278dc8b9c6c842e3`; final head `32dbcd56b14a58ebb193ef81c8fa9c715c31feb8`; CI #1571, Real SQL #88 and production-candidate #133 Green.
- #222 / PR #237 merged `308a2f31a42500ce7354b1af2c2369d59be57455`; head `fa0353431bc02abbc7cf520fec04adf5418ecfc6`; CI #1590 and production-candidate #134 Green.
- #223 / PR #238 merged `3864b4f8acc14d6e0bd259bfb1ab52d9fec07be1`; synchronized head `473944f21ce4cabb0b96f6040edf5992605930b5`; CI #1617 and production-candidate #135 Green.
- #224 / PR #239 merged `cab4b9492eb65a6ec7340add016dd12bb99eb13f`; synchronized head `8f5733b4235609a083e0535486342663a80b3b2b`; CI #1623 and production-candidate #137 Green.
- #225 / PR #240 squash-merged `fd33e79c6d19d7f9852417b9c35a11f91f21714c`; exact final head `0834db6b5d518fe5c52eec9b47c03e467929aa89`; CI #1637, Real SQL #91 and production-candidate #142 Green. The Windows gate passed Release build/full suite, production tooling validation, publish, HTTPS/auth smoke before and after restart, clean package validation and ZIP/SHA-256 artifact creation.

The repository has no browser/Playwright screenshot harness; UI700-049 therefore records responsive/accessibility source contracts and CI regression rather than claiming a browser visual run. BATCH-700 never changes the external acceptance boundary: RC.61 publication #162 and real IIS/HTTPS acceptance #116/#111 remain independent and open until their own evidence is complete.

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
- BATCH-200: B200-001..100 COMPLETE; current-main reconciliation COMPLETE via Issue #99 / PR #156, merged `221e44a9f13ed02e994311addff94b0e7996e444`.
- BATCH-300: B300-001..100 COMPLETE; PR #102 merged as `385c2ee7a4d592c1e32e6e00a5c533c8790963b6`; reconciled CI `31465013971`, 395/395.
- BATCH-400: B400-001..110 COMPLETE.
- BATCH-500: B500-001..100 COMPLETE.
- BATCH-600: B600-001..100 COMPLETE.
- BATCH-700: UI700-001..050 COMPLETE; PR #240 squash-merged as `fd33e79c6d19d7f9852417b9c35a11f91f21714c` after exact final head `0834db6b5d518fe5c52eec9b47c03e467929aa89` passed CI #1637, Real SQL #91 and production-candidate #142.
- Total completed hardening/UI task IDs B100+B200+B300+B400+B500+B600+B700: **660**. PR #156 remains baseline reconciliation, not new task accounting.

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
- Remaining production order is fail-closed: #162 first, then #116, then #111; no production mutation for #116 while #162 is OPEN.

**Overall:** 🟢 verified foundation · 🟢 P0.1–P0.4 COMPLETE · 🟢 P0.5 repository cutover/evidence/session/finalization/release/promotion/workflow-supply-chain/native-Node-24/durable-release hardening through PR #219 · 🟢 #256 selected-product-hash session hardening COMPLETE via PR #257 · 🟢 #258/#259 locked-session sidecar binding COMPLETE · 🟢 #261/#262 Acceptance Control Toolkit provenance COMPLETE with exact toolkit source `b422eaaee53d931a62a43b3c36a53b68cd4f3e27` · 🟢 #266/#267 RC.61 read-only promotion preflight COMPLETE · 🟢 #270/#271 Step 0 operator handoff COMPLETE · 🟢 BATCH-200 current-main reconciliation COMPLETE · 🟢 BATCH-700 50/50 COMPLETE and merged via PR #240 · 🟡 selected RC.61 durable publication + separate verification pending manual #162 · ⛔ #116 production mutation blocked while #162 is OPEN · 🟡 external IIS/HTTPS 15-gate acceptance pending after #162 · 🔴 production acceptance not yet granted
