# Project Status

## SingleNode core operational file cross-process safety — #451 / PR #452

**Base:** `main@e3e8942ee624426bc1481de4908e4022a09caac8`.  
**Gap:** SingleNode `FileAuditStore`, `FileSnapshotHistoryStore` and `FileHealthIncidentRepository` previously cached persisted state per object/process and serialized read-modify-write only with instance-local locks. `AtomicJsonFile.Save` kept each JSON replacement physically atomic but did not serialize the complete transaction across overlapping IIS workers. That allowed lost audit/history/incident updates, long-lived stale reads, and stale incident expected-status comparisons. The file-native bounded `FileHealthIncidentRepository.Read(...)` specialization also projected directly from cached `_items`.  
**Implementation:** PR #452 introduces one bounded reusable `CrossProcessFileLease` based on stable `FileShare.None` sidecars. Audit, snapshot history and health incidents load authoritative disk state while holding their per-store `<state-file>.lock`; mutations reload before applying existing bounds/retention/reconciliation policy and reads refresh under the same lease. `TrySetStatus` now evaluates its compare-and-set expectation only after the fresh reload. The file-native bounded incident `Read(...)` refreshes under the same lease. `FileIncidentNoteRequestStateStore` reuses the same primitive while preserving its existing `incident-note-requests.lock`, 64-shard/512-entry bounds and monotonic `Armed -> Applied` behavior. Lease contention is bounded to five seconds with 25 ms retries and fails closed.  
**Regression contract:** independent store instances must preserve peer audit appends, preserve peer history points, preserve peer incident findings, refresh bounded incident reads, reject a stale incident status CAS, and block a mutation while the stable sidecar lease is held.  
**Pre-canonical-doc validation:** core code/test head `f746022c177a7d36d377d7ff57e6fbeb2e0e518d` passed normal CI `32187827746` with Release build **0 warnings / 0 errors**, **1447/1447** Linux tests and all repository safety runtimes; Windows production-candidate `32187827803` passed the full suite, PowerShell/toolkit/session/recorder/finalizer checks, win-x64 publish, secret-free validation, HTTPS/auth smoke before and after restart, clean package validation and artifact upload; protected-P0 metadata `32187827734` and commit `32187827799` guards were Green. Real SQL was not selected because this closure changes no monitored-target SQL query, collector or permission path. Later shared-lease refactoring, bounded-read coverage and canonical-doc commits must pass the same selected gates on the final PR head before merge.  
**Merge gate:** exact docs-inclusive PR #452 head must remain current with `main`, have zero unresolved review threads, and pass Linux CI, Windows production-candidate, both protected-P0 guards, plus Real SQL if selected/required before Ready/squash merge and #451 completion.  
**Boundary:** no monitored-target SQL query/permission expansion, credential behavior change, MultiNode SharedState contract change, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external P0 acceptance or branch-protection mutation. Manual/external order remains `#162 -> #116 -> #111`; #353 remains repository-admin only.

## Incident-note file-state cross-process safety — #449 / PR #450 COMPLETE / MERGED

**Repository state:** COMPLETE; PR #450 squash-merged to `main` as `e3e8942ee624426bc1481de4908e4022a09caac8` and #449 is closed completed.  
**Exact implementation head:** `03f410de8b1ea5e0fbe00e28fd8576ebfb3ed477`.  
**Closed gap:** SingleNode `FileIncidentNoteRequestStateStore` no longer relies on stale per-process shard caches. One stable `incident-note-requests.lock` covers authoritative disk reload, schema/bounds validation, mutation and atomic persist, so overlapping workers cannot double-claim an already armed request, lose peer entries or downgrade durable `Applied` to stale `Armed`.  
**Validation:** exact PR #450 head passed Linux CI, SQL Server 2022 Real SQL, Windows production-candidate and both protected-P0 guards before squash merge.  
**Follow-on:** #451 / PR #452 only consolidates the already-proven incident-note lease implementation into the reusable SingleNode file-lease primitive; its lock-file identity and idempotency policy remain unchanged.

## Incident-note durable idempotency + replay authority — #445/#446 and #447/#448 COMPLETE / MERGED

**#445 / PR #446:** COMPLETE / MERGED as `b4365058bd9809f080070f2d440f358083938262`; incident-note request identity survives rolling-audit eviction/restart through bounded durable SingleNode/SharedState stores, retained legacy receipts are materialized on startup, `Applied` wins over `Armed`, and saturation remains fail-closed. Exact PR #446 head `367d1b929b64abcba28596289ac7a16cf2be72a1` passed normal CI, SQL Server 2022 Real SQL, Windows production-candidate and both protected-P0 guards before merge.  
**#447 / PR #448:** COMPLETE / MERGED as `c0d6472b522548a8cf4ad4d2d6271ad722dd0f86`; coordinated incident-note request state is authoritative over legacy rolling-audit preflight, so a request durably advanced to `Applied` before a failing final audit append replays as an idempotent no-op rather than false ambiguity; durable `Armed` stays fail-closed and plain non-coordinated `IAuditStore` behavior stays unchanged. Exact PR #448 head `115848b9a14c720281b6faf17706064b007aba09` passed Linux CI, SQL Server 2022 Real SQL, Windows production-candidate and both protected-P0 guards before squash merge.  
**Tracking reconciliation:** PR #450 reconciled the stale post-merge `#447/#448 CLOSURE CANDIDATE` wording; #451/PR #452 preserves those completed semantics while sharing the bounded file-lease primitive.

## Programming closure — atomic shared-state execution guard — #423 / PR #424

**Base:** `main@3b5e60fef2fa41c6e627468850cf3cf8532b0524`.  
**Repository programming state:** implementation + source contract + SQL Server 2022 acceptance are present on `agent/423-atomic-shared-state-execution`; PR #424 remains Draft until the exact docs-inclusive head passes all repository gates and has zero unresolved review threads.  
**Gap closed:** #421 left the canonical SharedState schema-v1 readiness fingerprint and subsequent document Read/CAS on separate backend calls/connections. The production SQL backend now repeats the canonical fingerprint after held schema-row + document key/range locks inside the same explicit `SERIALIZABLE` transaction as the Read/CAS, eliminating that documented TOCTOU window without duplicating or weakening the fingerprint.  
**Validation on pre-canonical-doc head `ef9abdec6c5e203c0395c03c937320792f9f2ed2`:** normal CI `32156983549` Green; SQL Server 2022 Real SQL `32156983565` Green, including the production execution-lock regression that blocks concurrent schema-version mutation and document-table DDL until rollback; protected-P0 metadata `32156983671` and commit `32156983621` guards Green; Windows production-candidate was still running when canonical tracking was reconciled and must be Green again on the final docs-inclusive head before merge.  
**Boundary:** no schema v2/migration/auto-repair, no runtime DDL by Monitor, no monitored-target permission/query expansion, no secret disclosure, no autonomous remediation, no RC.61 publication, no production IIS/SQL mutation, no external P0 acceptance and no branch-protection mutation. External/manual order remains `#162 -> #116 -> #111`; #353 remains repository-admin only.

## Post-closure security/control hardening — PR #369

**Scope:** issues #368, #370, #371, #372 and #373.  
**Repository programming state:** implementation and regression coverage complete on the hardening branch; `FEATURE_CATALOG.md` and this status are reconciled in the same PR; exact final closure still requires the docs-inclusive head to pass the repository-selected workflows.  
**Pre-doc Green evidence:** code/test head `6a8e55eb89f8bf39c868768d53a274379abe3d35`; normal CI `32121424138` passed Release build, 1341 tests and release/P0 safety runtimes; Windows production-candidate `32121424133` passed end-to-end; protected P0 metadata/commit guards `32121424301` / `32121424216` passed. Real SQL was not selected because this tranche changes no monitored-SQL query, collector or SQL-permission path.  
**Closed programming gaps in this tranche:** credential policy defaults deny when configuration is absent; unsupported credential deletion cannot masquerade as successful cleanup; login cannot authenticate when lockout/audit controls are missing; login-attempt state is hard-bounded with expiry reclamation and fail-closed saturation; operator mutations require attributable actor identity before state change; manual refresh and incident transitions require audit availability before collection/mutation; Advisor requests no longer fall back to an `unknown` actor.  
**Boundary:** no monitored-SQL permission expansion, secret disclosure, autonomous remediation, RC.61 publication/supersession, production IIS/SQL mutation, external acceptance PASS or branch-protection mutation. Remaining external/manual work is still #162 durable RC.61 publication + independent verification, then #116 real trusted-IIS 15/15 acceptance, then #111 closure; #353 remains repository-admin branch-protection apply/readback.

## Programming closure hardening — PR #363 — COMPLETE / MERGED

**Scope:** issues #362, #364, #365, #366 and #367.  
**Repository programming state:** COMPLETE; PR #363 squash-merged to `main` as `c8515f310091bcb62af488d9132c4f330c182bf8`, and the five programming issues are closed completed.  
**Exact-head validation:** `4fe2118f088219fbd7781a04ca77feebf352184b`; normal CI `32118070289`, Real SQL `32118070315`, Windows production-candidate `32118070230`, protected P0 commit guard `32118070235` and metadata guard `32118070299` all passed.  
**Closed programming gaps:** missing/partial estate evidence no longer becomes healthy zero; Global Refresh cannot treat auth redirects or non-JSON HTML as success; retained stale fallback is explicit and not counted as a fresh refresh; application credential readiness is topology-aware without turning operational backup into an undocumented readiness gate; Dashboard/Servers no longer invent numeric health scores, reachability, connectivity or collector-live claims from cache-only evidence.  
**Known remaining work after this programming tranche:** external/manual only — #162 durable RC.61 publication + independent verification, then #116 real trusted-IIS 15/15 acceptance, then #111 closure; #353 remains repository-admin branch-protection apply/readback. No external gate, release/tag, production IIS/SQL state or acceptance result was changed by PR #363.

## CURRENT P0 — Real SQL Production MVP

**Updated:** 2026-08-18  
**Umbrella:** #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Production acceptance guide:** `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`  
**Active external release gate:** #116 / P0.5 First Production SingleNode  
**Repository cutover/evidence/finalization/session/release workflow:** COMPLETE through selected-product-hash hardening PR #257, locked-session sidecar binding PR #259, exact Acceptance Control Toolkit provenance PR #262, cutover-readiness bridge PR #337, explicit acknowledged RC.61 operator helper #338/#339, canonical handoff reconciliation #340/#341 and production acceptance guide reconciliation #342/#343. Exact cutover toolkit source remains `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`; selected RC.61 durable publication remains pending explicit operator action under #162. PR #343 squash-merged as `3cd711b608e4ceaf8872eb22a25541bbbfe2729a` after exact-head CI #2996 / `32097392971` passed 1287/1287 and Windows production-candidate #562 / `32097392991` passed end-to-end. These repository changes do not publish RC.61 or create a real production PASS.  
**Latest RC.61 read-only state check:** source run `31667721306` remains successful; artifact `9168574442` remains present/unexpired with exact outer digest `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382` and expiry `2026-09-12T04:41:34Z`; fresh 2026-08-18 checks still found tag `v0.1.0-rc.61` absent and no operator-supplied promotion/verification evidence recorded on #162. The connected agent surface has no workflow-dispatch action, so the explicitly acknowledged promotion remains an external operator action.  
**Remaining dependency:** `#162 durable RC.61 publication + independent verification -> #116 real trusted-IIS 15/15 acceptance -> #111 closure`; canonical short form `#162 -> #116 -> #111`; **no #116 production mutation while #162 is OPEN**.  
**Production target:** actual Windows/IIS trusted-HTTPS SingleNode acceptance.

### Current #162 operator contract

Preferred sequence:

`Invoke-Rc61DurablePromotion.ps1 preview -> explicit -AcknowledgePromotion -> exact captured promotion run -> separately execute returned IndependentVerificationCommand -> Test-Rc61CutoverReadiness.ps1 with explicit run IDs`

From a trusted authenticated operator checkout, preview first:

```powershell
.\scripts\Invoke-Rc61DurablePromotion.ps1
```

Require `Status = READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT`, `WorkflowDispatchPerformed = False`, `IndependentVerificationDispatched = False`, `ProductionMutationPerformed = False`, and `MutatedGitHubState = False`.

After review, explicit acknowledgement is required:

```powershell
.\scripts\Invoke-Rc61DurablePromotion.ps1 -AcknowledgePromotion
```

The helper dispatches only the locked promotion workflow, captures/binds one exact promotion run and monitors only that run. Ambiguous discovery, timeout or failure is **do not redispatch**. It never auto-dispatches the independent verifier. After the exact promotion run is Green require `Status = PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED`, retain its run ID/URL, then separately execute the returned `IndependentVerificationCommand`.

After the separate verifier is Green, bind both exact run IDs:

```powershell
.\scripts\Test-Rc61CutoverReadiness.ps1 `
  -PromotionRunId <PROMOTION_RUN_ID> `
  -VerificationRunId <VERIFICATION_RUN_ID>
```

Require `Status = READY_FOR_P0_5_PRE_CUTOVER_PREPARATION`, `DurableReleasePrerequisiteSatisfied = True`, `ExternalGatesPassed = 0`, `ProductionMutationPerformed = False`, and `MutatedGitHubState = False`. This state remains **0/15** external gates and performs **no production mutation**. The lower-level `Test-Rc61DurablePromotionPreflight.ps1` remains a diagnostic/audit guard, not the preferred primary operator path.

### P0 release chain

| Release | State | Evidence |
|---|---|---|
| P0.1 / #112 | COMPLETE | PR #119; final CI `31476747212`; 501/501 |
| P0.2 / #113 | COMPLETE | PR #121; final CI `31478470867`; 505/505 |
| P0.3 / #114 | COMPLETE | PR #122 merged `245bb0770d7ec6e7a334f7763d3560cef80324fe`; final CI `31479311552`; 507/507 |
| P0.4 / #115 | COMPLETE | PR #124 merged `f4c08292734c293a6d0b865cc2a005b8c42b02a6`; normal `31481874425` 518/518; Real SQL `31481874501` 8/8 |
| P0.5 / #116 | ACTIVE / BLOCKED BEFORE MUTATION BY #162 | repository deployment/evidence/session/finalization/release-package/durable-promotion/workflow-supply-chain/native-Node-24/durable-release, selected-product-hash binding, locked-session sidecar binding #258/#259, Acceptance Control Toolkit provenance #261/#262, explicit operator helper #338/#339, handoff reconciliation #340/#341 and production-guide reconciliation #342/#343 are complete; selected RC.61 durable publication #162 must complete before external IIS/HTTPS acceptance begins |

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
- Issue #266 / PR #267 COMPLETE: lower-level read-only `Test-Rc61DurablePromotionPreflight.ps1` pins the exact selected RC.61 repository/run/artifact/digest/hash/source head/tested-merge/tag, fails closed on provenance, expiry and ambiguous durable-state API failures, and emits the exact approved promotion plus separate verification commands. Exact head `cdaff693810534db52975976309b726a0a8d409c` passed CI #1843, Real SQL #121 and Windows production-candidate #203; PR #267 squash-merged as `43aaa6071fd0c577c792d427ad490717f28acbac`; post-merge main CI #1844 passed. It remains diagnostic/audit tooling and never dispatches workflows or mutates tag/release state.
- Issue #270 / PR #271 COMPLETE: historical Step 0 handoff documented `Status=READY_FOR_EXPLICIT_MANUAL_PROMOTION`, `MutatedGitHubState=False`, `TagExists=False` and `ReleaseExists=False`; stop/investigate is explicit for durable-state presence, artifact expiry, provenance/digest drift or ambiguous GitHub probing. PR #271 squash-merged as `479f9b557948b56fc5ec5692efb67fd6f1f4a921`; CI #1854 and Windows production-candidate #205 were Green and post-merge main CI #1855 passed. It is superseded as the primary operator path by #338/#339 and later reconciliation, while remaining valid historical evidence.
- Issue #338 / PR #339 COMPLETE: explicit acknowledged RC.61 operator helper. `Invoke-Rc61DurablePromotion.ps1` previews fail-closed, requires `-AcknowledgePromotion`, captures one exact promotion run, fails ambiguous/failing discovery closed with **do not redispatch**, and does not auto-dispatch the independent verifier. PR #339 squash-merged as `f129e63b8ae9e83dda4f89d49e40892f4f36af56`.
- Issue #340 / PR #341 COMPLETE: canonical durable-promotion handoffs reconciled to helper preview -> acknowledgement -> exact run -> separate `IndependentVerificationCommand` -> explicit run-ID readiness. PR #341 squash-merged as `dfabec7f8cde7953a3f9c1fb5142b56774949537` after CI #2989 / `32096484890` and Windows production-candidate #560 / `32096484902` Green.
- Issue #342 / PR #343 COMPLETE: `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md` and its regression contract now use the same helper sequence and retain raw workflow inputs only as audit/troubleshooting reference. Exact head `0a1f90b4c1f850426a5a3b0d491eb2f9d1f28905` passed CI #2996 / `32097392971` with 1287/1287 and Windows production-candidate #562 / `32097392991` Green before squash merge `3cd711b608e4ceaf8872eb22a25541bbbfe2729a`.
- Issue #168 / PR #171 COMPLETE: every active external `actions/*` workflow dependency is pinned to an approved exact 40-character upstream commit SHA; a dedicated fail-closed regression test rejects mutable/unapproved/drifted refs; the completed BATCH-100 one-shot merge workflow with write permissions is removed. PR #171 squash-merged as `c9084dd32b12a9a078f953f85f39b253793e2343`. Exact implementation head `052e969b5ab450526ab996a2e77459f4087846c8` passed normal CI `31881105832`, Real SQL `31881105877`, and Windows production-candidate `31881105818` end-to-end. This does not alter selected RC.61 or satisfy #162/#116/#111.
- Issue #173 / PR #174 COMPLETE: the immutable Action allowlist moved from older Node 20-based majors to official native Node 24 releases while retaining exact SHA pinning and readable version metadata. PR #174 squash-merged as `bc7cb2d275f423fb381b83d92c76f6516e404fe9`. Exact implementation head `8134720cf1260abc7e6c0609a5afa239f31bb5f7` passed normal CI `31881744429`, Real SQL `31881744413`, and Windows production-candidate `31881744437`; Windows passed **814/814**, Release **0 warnings / 0 errors**, HTTPS/auth before and after restart, clean package validation and native Node 24 artifact upload. Approved pins: checkout v7.0.1 `3d3c42e5aac5ba805825da76410c181273ba90b1`, setup-dotnet v6.0.0 `a98b56852c35b8e3190ac28c8c2271da59106c68`, upload-artifact v7.0.1 `043fb46d1a93c77aae656e7c1c64a875d1fc6a0a`, download-artifact v8.0.1 `3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c`. The prior Node 20 deprecation/forced-Node-24 warning is absent. RC.87 is CI evidence only and does not supersede selected RC.61.
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
| P0-049 versioned artifact/checksum/evidence/release workflow | **REPOSITORY HARDENED** — RC.61 + reusable release pipeline + durable tag publication tooling + immutable session + selected-product-hash binding #256 + locked-session sidecar binding #258/#259 + Acceptance Control Toolkit provenance #261/#262 + generator + recorder + finalizer + validator; explicit operator helper #338/#339 + handoff #340/#341 + production-guide #342/#343 COMPLETE; **actual RC.61 acknowledged promotion + separate read-only verification + explicit run-ID readiness pending operator #162** |
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

## BATCH-800 — Full functional operator wiring — COMPLETE

**Umbrella:** #287 — CLOSED / COMPLETED  
**Final closeout:** B800-100 / PR #335 squash-merged as `a6832d99f629cdbd3a93887199fe608a3ae474ec`  
**Task range:** B800-001..100  
**Ledger:** `docs/BATCH_800.md`