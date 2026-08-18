# Project Status

## Programming closure — persistence and durable-state bounds — PR #380 / #385 / #389

**Merged baseline:** PR #380 squash-merged to `main` as `0610ad3f4603e411953f1862507ec9896d4394ae`; issues #378, #379, #381 and #382 are CLOSED / COMPLETED. Exact head `31412389bf93a080c1ad8caf20f5bed1a0fbb96c` passed normal CI `32129817542`, Real SQL `32129817657`, Windows production-candidate `32129817588`, protected-P0 commit guard `32129817596`, and protected-P0 metadata guards `32129817511` / `32129845549`.
**Protected credential-file bound:** Issue #383 is CLOSED / COMPLETED through PR #385, squash-merged as `38cf04a3fd4d73d926bde03bafb28feaa644541f`. Exact head `e2b38972b25b49b049ac89538b531ff9162fb40a` passed CI `32130351982`, Real SQL `32130351950`, Windows production-candidate `32130351969`, protected-P0 commits `32130351960` and metadata `32130351952`; unresolved review threads were zero. The local protected credential file is rejected before parsing above its 24 MiB raw-file ceiling, deserialized from a read-only stream, and checked against the same serialized-output ceiling before durable replacement.
**Current durable-registration closure:** Issue #386 / draft PR #389 on `agent/386-bound-registration-store-file`. The implementation rejects oversized `registrations.json` before JSON/domain parsing, enforces the same bound before atomic replacement and retains existing `Upsert`/`Remove` rollback semantics. Focused regression coverage proves oversized startup state fails closed and oversized persistence preserves the last-good durable file plus in-memory state. Restore/Build/Test have passed on code head `f238267976bc692c71b0092b48eb2bf0fa66eb26`; exact docs-inclusive head validation remains required before merge.
**Boundary:** these are repository-side reliability/data-integrity hardening changes only. They do not expand monitored-SQL permissions or collection, disclose secrets, add autonomous remediation, publish RC.61, mutate production IIS/SQL, manufacture external P0 acceptance, complete protected P0 issues, or mutate branch protection. Remaining external/manual work remains `#162 -> #116 -> #111`; #353 remains repository-admin branch-protection apply/readback.

## Post-closure security/control hardening — PR #369

**Scope:** issues #368, #370, #371, #372 and #373.  
**Repository programming state:** implementation and regression coverage complete on the hardening branch; `FEATURE_CATALOG.md` and this status are reconciled in the same PR; exact final closure still requires the docs-inclusive head to pass the repository-selected workflows.  
**Pre-doc Green evidence:** code/test head `6a8e55eb89f8bf39c868768d53a274379abe3d35`; normal CI `32121424138` passed Release build, 1341 tests and release/P0 safety runtimes; Windows production-candidate `32121424133` passed end-to-end; protected P0 metadata/commit guards `32121424301` / `32121424216` passed. Real SQL was not selected because this tranche changes no monitored-SQL query, collector or SQL-permission path.  
**Closed programming gaps in this tranche:** credential policy defaults deny when configuration is absent; unsupported credential deletion cannot masquerade as successful cleanup; login cannot authenticate when lockout/audit controls are missing; login-attempt state is hard-bounded with expiry reclamation and fail-closed saturation; operator mutations require attributable actor identity before state change; manual refresh and incident transitions require audit availability before collection/mutation; Advisor requests no longer fall back to an `unknown` actor.  
**Boundary:** no monitored-SQL permission expansion, secret disclosure, autonomous remediation, RC.61 publication/supersession, production IIS/SQL mutation, external acceptance PASS or branch-protection mutation. Remaining external/manual work is still #162 durable RC.61 publication + independent verification, then #116 real trusted-IIS 15/15 acceptance, then #111 closure; #353 remains repository-admin branch-protection apply/readback.

## Programming closure hardening — PR #363 — COMPLETE / MERGED

**Scope:** issues #362, #364, #365, #366 and #367.  
**Repository programming state:** COMPLETE; PR #363 squash-merged to `main` as `c8515f310091bcb62af488d9132c4f330c182bf8`, and the five programming issues are closed completed.  
**Exact-head validation:** `4fe2118f088219fbd7781a04ca77feebf352184b`; normal CI `32118070289`, Real SQL `32118070315`, Windows production-candidate `32118070230`, protected P0 commit guard `32118070235` and protected P0 metadata guard `32118070299` all passed.  
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
