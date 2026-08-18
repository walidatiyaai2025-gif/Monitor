# Implementation Plan

This is the canonical execution plan. Update it in the same PR as material implementation changes.

## Programming closure — SingleNode core operational file cross-process state — #451 / PR #452

**Base:** `main@e3e8942ee624426bc1481de4908e4022a09caac8`.  
**Programming gap:** `FileAuditStore`, `FileSnapshotHistoryStore` and `FileHealthIncidentRepository` protected file-backed state only with per-instance caches/locks. `AtomicJsonFile.Save` made each replacement atomic but did not serialize the authoritative read -> policy mutation -> write transaction across overlapping IIS workers sharing a SingleNode operational root. The result could be lost audit/history/incident updates, stale long-lived reads and stale incident compare-and-set decisions; the file-native bounded `FileHealthIncidentRepository.Read(...)` specialization also projected directly from cached `_items`.  
**Implementation:** reusable `CrossProcessFileLease` acquires stable `FileShare.None` sidecars with a five-second bounded acquisition window and 25 ms retry. Audit/history/incidents use `<state-file>.lock`; constructors load under the lease, each mutation reloads authoritative disk state inside the lease before preserving existing bounds/retention/reconciliation logic, and reads refresh inside the same lease. Incident `TrySetStatus` evaluates the expected status only after fresh reload, and the file-native bounded `Read(...)` follows the same refresh boundary. `FileIncidentNoteRequestStateStore` reuses this primitive while retaining its existing `incident-note-requests.lock`, 64 shards, 512-entry/shard bound and monotonic `Armed -> Applied` policy.  
**Regression contract:** independent instances preserve peer audit appends, preserve peer history points, preserve peer incident findings through the bounded repository `Read(...)`, reject stale incident status CAS, and wait while the stable file lease is held. Existing corruption/path/restart tests remain.  
**Pre-canonical-doc evidence:** core code/test head `f746022c177a7d36d377d7ff57e6fbeb2e0e518d`; normal CI `32187827746` Green with Release build **0 warnings / 0 errors**, **1447/1447** Linux tests and repository release/P0 safety runtimes; Windows production-candidate `32187827803` Green end-to-end through full suite, parser/toolkit/session/recorder/finalizer, win-x64 publish, secret-free validation, HTTPS/auth smoke before and after restart, clean package validation and artifact upload; protected-P0 metadata `32187827734` and commit `32187827799` guards Green. Real SQL was not selected because no monitored-target SQL query, collector or permission path changed. The later reusable-lease refactor, bounded `Read(...)` correction/regression and canonical-doc commits must pass the same selected gates on the final PR head before merge.  
**Definition of done for #451:** exact docs-inclusive PR #452 head current with `main`; zero unresolved review threads; Linux CI, Windows production-candidate, both protected-P0 guards, and SQL Server 2022 Real SQL when selected/required all Green; then Ready, squash-merge and close #451 completed.  
**Safety boundary:** no monitored-target SQL query/permission expansion, credential behavior change, MultiNode SharedState contract change, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external P0 acceptance or branch-protection mutation. External/manual dependency remains `#162 -> #116 -> #111`; #353 remains repository-admin only.

## Completed incident-note cross-process closure — #449 / PR #450

**Repository state:** COMPLETE / MERGED as `e3e8942ee624426bc1481de4908e4022a09caac8`; #449 is closed completed.  
**Exact PR head:** `03f410de8b1ea5e0fbe00e28fd8576ebfb3ed477`.  
**Closed gap:** SingleNode file-backed incident-note request state uses the stable `incident-note-requests.lock` around authoritative disk reload, schema/bounds validation, policy mutation and atomic persist, eliminating stale per-process shard double-claim/lost-entry/Applied-downgrade races while preserving existing idempotency bounds and SharedState behavior.  
**Validation:** exact PR #450 head passed Linux CI, SQL Server 2022 Real SQL, Windows production-candidate and both protected-P0 guards before squash merge. #451/PR #452 only consolidates that lease implementation into the shared SingleNode primitive; lock-file identity and request-state policy remain unchanged.

## Completed incident-note durability closures — #445/#446 and #447/#448

**#445 / PR #446:** COMPLETE / MERGED as `b4365058bd9809f080070f2d440f358083938262`; bounded durable `Armed` / `Applied` SingleNode and SharedState request state survives rolling-audit eviction/restart, materializes retained legacy receipts, keeps `Applied` dominant and fails closed at capacity. Exact PR #446 head `367d1b929b64abcba28596289ac7a16cf2be72a1` passed Linux CI, Real SQL, Windows production-candidate and both protected-P0 guards.  
**#447 / PR #448:** COMPLETE / MERGED as `c0d6472b522548a8cf4ad4d2d6271ad722dd0f86`; coordinated durable state is authoritative over stale legacy audit preflight, so a durably Applied request remains an idempotent no-op even when the final applied audit append previously failed. Exact PR #448 head `115848b9a14c720281b6faf17706064b007aba09` passed Linux CI, Real SQL, Windows production-candidate and both protected-P0 guards before squash merge. PR #450 reconciled the stale post-merge candidate wording left in the canonical documents.

## Programming closure — atomic SharedState schema/execution guard — #423 / PR #424

**Base:** `main@3b5e60fef2fa41c6e627468850cf3cf8532b0524`.  
**PR:** #424 `agent/423-atomic-shared-state-execution`.  
**Programming state:** implementation + source-contract regression + SQL Server 2022 acceptance are complete on the branch. Exact closure requires the final docs-inclusive head to remain current with `main`, pass normal CI, Real SQL, Windows production-candidate and both protected-P0 guards, and have zero unresolved review threads before merge.  
**Gap closed:** #421 made the canonical schema-v1/readiness fingerprint a document-execution precondition but left that preflight and Read/CAS on separate backend calls/connections. The production SQL backend now keeps the existing store-level preflight as defense-in-depth, begins one explicit `SERIALIZABLE` transaction, holds the canonical schema metadata row plus target document key/range, reruns the same schema-v1 fingerprint on that connection/transaction, performs the Read/CAS, materializes the bounded result, and commits. Administrative schema-version/DDL drift can no longer interleave between the execution guard and the document operation.  
**Pre-canonical-doc evidence:** code/test/ledger head `ef9abdec6c5e203c0395c03c937320792f9f2ed2`; normal CI `32156983549` Green; SQL Server 2022 Real SQL `32156983565` Green including the production execution-lock regression; protected-P0 metadata `32156983671` and commit `32156983621` guards Green. The final docs-inclusive head must rerun all required gates; earlier runs are implementation evidence only.  
**Boundary:** no schema-v2 design, migration/auto-repair or Monitor runtime DDL; no monitored-target SQL query/permission expansion; no secret disclosure, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external P0 acceptance or branch-protection mutation. Remaining external/manual dependency stays `#162 -> #116 -> #111`; #353 remains repository-admin only.

## Post-closure security/control hardening — #368 / #370 / #371 / #372 / #373 / #374

**PR:** #369 `agent/368-credential-policy-fail-closed`  
**Programming state:** implementation + regression coverage complete on the branch; `STATUS.md`, `FEATURE_CATALOG.md`, and this plan are reconciled in the same PR. Exact final closure requires the docs-inclusive head to pass repository-selected workflows before merge.  
**Pre-doc Green evidence:** code/test head `6a8e55eb89f8bf39c868768d53a274379abe3d35`; normal CI `32121424138` passed Release build, 1341 tests and release/P0 safety runtimes; Windows production-candidate `32121424133` passed end-to-end; protected P0 metadata/commit guards `32121424301` / `32121424216` passed. Real SQL was not selected because this tranche changes no monitored-SQL query, collector or SQL-permission path.  
**Scope closed:**
1. Missing credential policy defaults to deny instead of enabling Monitor-owned local SQL credential creation implicitly; current SingleNode behavior remains an explicit configuration opt-in.
2. Credential/secret-store deletion defaults fail closed instead of allowing a completed no-op to masquerade as successful compensation.
3. Login POST cannot verify credentials when lockout or audit controls are unavailable.
4. Login-attempt state is capacity-bounded, expired state is reclaimed with bounded-frequency pruning, and unseen keys fail closed at saturation while existing five-failure/five-minute semantics remain.
5. Incident, enterprise, manual-refresh, transition and Advisor operator actions require attributable actor identity; refresh/transitions also require audit availability before collection/mutation.
6. Canonical BATCH-800 closeout state is locked to the already-merged B800-100 result: Issue #287 is CLOSED/COMPLETED; PR #335 squash-merged as `a6832d99f629cdbd3a93887199fe608a3ae474ec` from exact head `4379dbc0e1b346cb51bebf8e7467823c58f2361c`, with CI `32093252549`, Real SQL `32093252670`, and Windows production-candidate `32093252563` Green.

**Boundary:** no monitored-SQL permission expansion, secret disclosure, autonomous remediation, RC.61 publication/supersession, production IIS/SQL mutation, external acceptance PASS or branch-protection mutation. Remaining external/manual work remains `#162 -> #116 -> #111`, plus repository-admin branch-protection apply/readback #353.

## Programming closure tranche — #362 / #364 / #365 / #366 / #367 — COMPLETE / MERGED

**PR:** #363; squash-merged to `main` as `c8515f310091bcb62af488d9132c4f330c182bf8`.  
**Exact implementation head:** `4fe2118f088219fbd7781a04ca77feebf352184b`.  
**Validation:** normal CI `32118070289`, Real SQL `32118070315`, Windows production-candidate `32118070230`, protected P0 commit guard `32118070235` and metadata guard `32118070299` all passed.  
**Scope closed:**
1. Estate Intelligence no longer converts absent/partial domain evidence into healthy zero; genuine collected zero stays zero.
2. Global Refresh uses explicit XHR auth semantics and rejects followed redirects/non-JSON success.
3. Retained stale fallback is explicit `RetainedStale`/JSON 503 and is never counted as a fresh refresh.
4. Application credential readiness is topology-aware: valid SingleNode is not falsely negative; MultiNode remains fail-closed; backup remains informational under the existing control-plane-only readiness contract.
5. Dashboard/Servers remove synthetic numeric health scores and unsupported reachability/connectivity/collector-live claims and use cache/evidence semantics instead.

The five issues #362/#364/#365/#366/#367 are closed completed. This tranche did not publish/tag RC.61, mutate production IIS/SQL, manufacture any external gate PASS or alter `#162 -> #116 -> #111` / #353.

## CURRENT P0 — Real SQL Production MVP

**Umbrella:** Issue #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Production acceptance guide:** `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`  
**Active release gate:** Issue #116 / P0.5 First Production SingleNode  
**Repository cutover/evidence/session/finalization/release/durable-tag/workflow-supply-chain/native-Node-24/durable-release tooling:** COMPLETE through selected-product-hash acceptance-session hardening PR #257, locked-session sidecar binding PR #259, clean exact-commit Acceptance Control Toolkit provenance PR #262, read-only cutover-readiness PR #337, explicit acknowledged RC.61 operator helper #338/#339, canonical handoff reconciliation #340/#341 and production acceptance guide reconciliation #342/#343. Exact cutover toolkit source is `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`; selected RC.61 durable publication remains pending explicit operator #162. PR #343 squash-merged as `3cd711b608e4ceaf8872eb22a25541bbbfe2729a` after exact-head CI #2996 / `32097392971` passed 1287/1287 and Windows production-candidate #562 / `32097392991` passed end-to-end. None of this dispatches or publishes RC.61.  
**Latest RC.61 read-only state check:** source run `31667721306` successful; artifact `9168574442` present/unexpired with exact outer digest `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382` and expiry `2026-09-12T04:41:34Z`; fresh 2026-08-18 checks found tag `v0.1.0-rc.61` absent and no operator-supplied promotion/verification evidence recorded on #162. The connected agent surface has no workflow-dispatch action; explicit acknowledgement remains an external operator action.  
**Required remaining dependency:** `#162 durable RC.61 publication + independent verification -> #116 real trusted-IIS 15/15 acceptance -> #111 closure`; canonical short form `#162 -> #116 -> #111`. **Do not begin #116 production mutation while #162 is OPEN.**  
**Live selected candidate/evidence ledger:** Issue #116 — RC.61  
**Project rule:** until P0.5 is accepted on the real environment, production-slice blockers outrank unrelated feature expansion. Repository/product work may proceed only inside the documented non-production safety boundary and cannot manufacture P0 acceptance.

### Current #162 operator contract

Preferred sequence:

`Invoke-Rc61DurablePromotion.ps1 preview -> explicit -AcknowledgePromotion -> exact captured promotion run -> separately execute returned IndependentVerificationCommand -> Test-Rc61CutoverReadiness.ps1 with explicit run IDs`

Preview from a trusted authenticated operator checkout:

```powershell
.\scripts\Invoke-Rc61DurablePromotion.ps1
```

Require `Status = READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT`, `WorkflowDispatchPerformed = False`, `IndependentVerificationDispatched = False`, `ProductionMutationPerformed = False`, and `MutatedGitHubState = False`.

After reviewing the preview, explicitly acknowledge:

```powershell
.\scripts\Invoke-Rc61DurablePromotion.ps1 -AcknowledgePromotion
```

The helper reruns the locked preflight, dispatches only the approved promotion workflow, captures/binds one exact promotion run and monitors only that run. Ambiguous discovery, timeout or failure is **do not redispatch**. It never auto-dispatches the independent verifier. After the exact promotion run is Green require `Status = PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED`, retain the run ID/URL, then separately execute the returned `IndependentVerificationCommand`.

After the separate verifier is Green:

```powershell
.\scripts\Test-Rc61CutoverReadiness.ps1 `
  -PromotionRunId <PROMOTION_RUN_ID> `
  -VerificationRunId <VERIFICATION_RUN_ID>
```

Require `Status = READY_FOR_P0_5_PRE_CUTOVER_PREPARATION`, `DurableReleasePrerequisiteSatisfied = True`, `ExternalGatesPassed = 0`, `ProductionMutationPerformed = False`, and `MutatedGitHubState = False`. This is read-only **0/15** readiness with **no production mutation**. `Test-Rc61DurablePromotionPreflight.ps1` remains the lower-level diagnostic/audit guard rather than the preferred primary operator path.

The production outcome remains one trustworthy vertical slice:

`Login -> Add SQL Server -> Test -> Save -> Collect -> View Server Details -> Refresh -> Restart/Recycle -> View trustworthy persisted target again`

Production-visible values must come from collected evidence. Missing, stale, permission-limited or uncollected dimensions must be explicit; default numeric values must never masquerade as measurements.

### P0 release chain

| Order | Release gate | Issue | Outcome | State |
|---|---|---|---|---|
| 1 | P0.1 | #112 | Real SQL registration: safe, testable and restart durable | COMPLETE — PR #119 / final CI `31476747212` |
| 2 | P0.2 | #113 | First real snapshot + truthful read-model mapping | COMPLETE — PR #121 / final CI `31478470867` |
| 3 | P0.3 | #114 | Server Details v0.1 trusted evidence surface | COMPLETE — PR #122 / final CI `31479311552` |
| 4 | P0.4 | #115 | Real SQL end-to-end acceptance under success/failure cases | COMPLETE — PR #124; normal `31481874425`; Real SQL `31481874501` |
| 5 | P0.5 | #116 | First trusted-HTTPS IIS SingleNode production release | **ACTIVE / BLOCKED BEFORE MUTATION BY #162 — repository workflow, selected-product-hash binding, locked-session sidecar binding #258/#259, Acceptance Control Toolkit provenance #261/#262, explicit operator helper #338/#339, handoff reconciliation #340/#341 and production-guide #342/#343 are complete; RC.61 publication #162 must complete before external IIS acceptance begins** |

### Resolved production gates

- **P0.1 COMPLETE:** candidate Test Connection precedes durable registration commit; failed/cancelled Monitor-owned candidate credentials are compensated safely.
- **P0.2 COMPLETE:** absent evidence stays absent; uncollected CPU/Memory/Agent dimensions are not rendered as fake numeric zero.
- **P0.3 COMPLETE:** Server Details is evidence-first, synthetic Health Score is removed, and monitored GET remains cache-only.
- **P0.4 COMPLETE:** SQL Server 2022 proves Add/Test/Register/Collect/View/Refresh/Restart/View with a non-sysadmin least-privilege login and controlled auth/network/timeout/TLS/server/msdb permission failures. Final normal CI `31481874425` — 518/518; Real SQL `31481874501` — 8/8.

## P0.5 repository preparation — COMPLETE / #162 RETENTION BLOCKS EXTERNAL ACCEPTANCE MUTATION

The repository contains the complete operator cutover, evidence and release workflow while intentionally leaving production acceptance external:

- PR #127 — HTTPS-only acceptance harness and production acceptance guide; merged `9bdd96940454f2586c0e81ff0c25a524d7f1281c`.
- PR #126 — Windows production-candidate pipeline; merged `d512ee156f07db566898a817f3c76dd3f46c1091`.
- PR #129 — safe IIS preflight + plan-first/apply-gated deployment + stable external `App_Data` + automatic physicalPath rollback; merged `7cb47945b47aab6558f7132dcfa818b9f02d2b20`.
- BATCH-500 / BATCH-600 — production safety and live operator-readiness orchestration; complete without changing the external-acceptance boundary.
- PR #142 / Issue #141 — exact 15-gate fail-closed evidence pack and closure validator; complete.
- PR #145 / Issue #144 — explicit one-gate-at-a-time recorder `Set-ProductionAcceptanceGate.ps1`; complete.
- PR #148 / Issue #147 — explicit fail-closed final operator acceptance finalizer `Complete-ProductionAcceptance.ps1`; complete and merged `e15a9654fbe744e426c95d5965a5faba60868e14`.
- PR #151 / Issue #150 — immutable candidate-bound acceptance-session initializer `New-ProductionAcceptanceSession.ps1`; complete and merged `9a76abe61422502c4889b04ce8b6a59f18ac04f4`.
- PR #257 / Issue #256 — selected-product-hash acceptance-session hardening **COMPLETE**, squash-merged `41410491df19699be6329e26e99a9328965782bc`. `New-ProductionAcceptanceSession.ps1` requires an independently selected 64-hex product SHA-256, rejects a mutually consistent but substituted ZIP + checksum pair before workspace creation, rechecks the selected hash after copy and binds manifest/evidence to it. Exact final head `70d1a8fb6814de1ec23dcff6b9942b945333c052` passed CI #1696, Real SQL #94 and Windows production-candidate #151 Green. This cannot publish RC.61 or satisfy any external gate.
- Issue #258 / PR #259 — locked-session gate/finalization chain-of-custody hardening **COMPLETE**: preserve the initializer-returned session-manifest SHA-256 outside mutable session files; authenticate the manifest + lock, canonical paths, actual candidate ZIP/checksum and evidence-pack candidate/environment identity before any gate PASS; bind exactly six acceptance-control sidecar files while leaving RC.61 product/deployment bytes unchanged; require the same anchor through finalization and independent production review; exercise the real Session → Recorder → Finalizer → Validator chain on Windows with pack/manifest/candidate drift negatives. Exact source `8d79361cccf98acfc0a1753d16de943458887389` passed CI #1751, Real SQL #112 and Windows production-candidate #170 Green; PR #259 squash-merged as `c22c4e5e4f59576cbb41b8fc46886474f8749ebb`. This cannot manufacture any external production PASS.
- Issue #261 / PR #262 — Acceptance Control Toolkit provenance **COMPLETE**: export requires a clean checkout of independently supplied exact commit `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`; deterministic manifest/lock, independent verification and session binding are fail-closed; `main`/`latest` are not toolkit identity.
- PR #155 / Issue #154 — tagged/manual release-package parity; **COMPLETE**, squash-merged `8d8ae2c5f35e8a1d774c5a9480f582e432e5dc03`. `production-candidate.yml` is reusable through `workflow_call`; `release.yml` delegates packaging to that exact Windows workflow; explicit candidate versions are syntax-bounded; manifest schema 2 records fixed P0.4 run IDs under `prerequisiteEvidence.p04` and leaves candidate-specific CI authoritative on #116.
- PR #160 / Issue #159 — durable tagged GitHub Release assets; **COMPLETE**, squash-merged `a14110181932bcd6e14b99e5b6984974a5b477f8`. Real pushed version tags publish only the already-verified same-run ZIP + `.sha256` after checksum verification; no rebuild/repackage/clobber path and no production-acceptance implication.
- PR #163 / Issue #162 — exact existing-candidate durable-promotion implementation **COMPLETE** and merged `43d8a193205495f155bb8866532a4e99ed93b655`; handoff PR #164 merged `930c057f431a36ab2b603d3dc39e70e8c31c744e`. Actual RC.61 publication remains pending manual dispatch and independent asset/hash verification on #162.
- PR #267 / Issue #266 — lower-level read-only RC.61 durable-promotion operator preflight **COMPLETE**: exact selected repository/version/source run/artifact/outer digest/product hash/source head/tested merge/tag are pinned; source-run/artifact provenance, expiry and ambiguous durable-state API failures fail closed; exact approved promotion and separate verifier commands are emitted. Exact head `cdaff693810534db52975976309b726a0a8d409c` passed CI #1843, Real SQL #121 and Windows production-candidate #203; PR #267 squash-merged as `43aaa6071fd0c577c792d427ad490717f28acbac`; post-merge main CI #1844 passed. The preflight remains diagnostic/audit tooling and never dispatches, creates/mutates a release or tag, rebuilds/repackages RC.61, touches IIS/SQL or satisfies #162/#116/#111.
- PR #271 / Issue #270 — historical Step 0 operator handoff **COMPLETE**: both RC.61 durable-promotion guides required the read-only preflight before the first publication attempt and required `Status=READY_FOR_EXPLICIT_MANUAL_PROMOTION`, `MutatedGitHubState=False`, `TagExists=False` and `ReleaseExists=False`; existing durable state, artifact expiry, provenance/digest drift or ambiguous GitHub probing was an explicit stop condition. PR #271 squash-merged `479f9b557948b56fc5ec5692efb67fd6f1f4a921`; CI #1854 and Windows production-candidate #205 were Green; post-merge main CI #1855 Green. It remains historical evidence but is superseded as the primary operator path by #338/#339 and later reconciliation.
- PR #339 / Issue #338 — explicit acknowledged RC.61 operator helper **COMPLETE**. `Invoke-Rc61DurablePromotion.ps1` previews fail-closed, requires `-AcknowledgePromotion`, captures and binds one exact promotion run, monitors only that run, treats ambiguity/failure as **do not redispatch**, and never auto-dispatches the independent verifier. PR #339 squash-merged as `f129e63b8ae9e83dda4f89d49e40892f4f36af56`.
- PR #341 / Issue #340 — canonical durable-promotion handoffs reconciled to helper preview -> acknowledgement -> exact promotion run -> separate `IndependentVerificationCommand` -> explicit run-ID readiness. PR #341 squash-merged as `dfabec7f8cde7953a3f9c1fb5142b56774949537`; exact head passed CI #2989 / `32096484890` and Windows production-candidate #560 / `32096484902` Green.
- PR #343 / Issue #342 — canonical production acceptance guide reconciled to the same operator helper sequence and raw workflow inputs retained only as audit/troubleshooting reference. Exact head `0a1f90b4c1f850426a5a3b0d491eb2f9d1f28905` passed CI #2996 / `32097392971` with 1287/1287 and Windows production-candidate #562 / `32097392991` Green end-to-end before squash merge `3cd711b608e4ceaf8872eb22a25541bbbfe2729a`.
- PR #171 / Issue #168 — GitHub Actions supply-chain hardening **COMPLETE**, merged `c9084dd32b12a9a078f953f85f39b253793e2343`; mutable external Action refs removed in favor of approved exact SHAs and obsolete write-capable completed workflow removed.
- PR #174 / Issue #173 — native Node 24 Action migration **COMPLETE**, merged `bc7cb2d275f423fb381b83d92c76f6516e404fe9`; CI `31881744429`, Real SQL `31881744413` and Windows `31881744437` Green. RC.87 is implementation evidence only and does not supersede selected RC.61.
- PRs #177–#219 — additional repository-only hardening **COMPLETE**: reproducible SDK and exact SQL image, pinned Linux/Windows runners, NuGet source and direct-package guards, checkout/token scope, write-capable workflow allowlists, release-tag mutation serialization, main-ref promotion preflight, exact-two assets, metadata/digest/provenance/TOCTOU/workspace/directory-atomic durable verification, independent read-only verification and toolchain capability preflight. None dispatches promotion, changes RC.61 selection or satisfies external IIS acceptance.

### Selected repository candidate evidence — RC.61

Issue #116 is the live source of truth. Current selected candidate:

- package `Monitor-0.1.0-rc.61-win-x64.zip`;
- product SHA-256 `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- Actions artifact `9168574442`;
- outer artifact digest `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`;
- source head `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`;
- tested merge ref `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- merged main commit `8d8ae2c5f35e8a1d774c5a9480f582e432e5dc03`;
- normal CI `31667721350` Green, Release 0 warnings/errors, 770/770;
- Real SQL `31667721353` Green, 8/8;
- Windows production-candidate `31667721306` Green, Release 0 warnings/errors, 770/770;
- candidate-version validation, immutable session initializer, recorder, finalizer and exact synthetic 15/15 closure validation Green;
- HTTPS health/authentication before and after process restart Green;
- package is secret-free SingleNode with persisted runtime state excluded.

Independent artifact inspection on 2026-08-13 recomputed the product SHA-256, matched the companion checksum, confirmed 95 package files and all 19 expected `_operations` entries, and verified manifest schema 2 with `prerequisiteEvidence.p04`, `candidateVerification.sourceOfTruth=#116`, `embeddedWorkflowRunIds=false`, and no legacy `realSqlAcceptance` field.

RC.61 supersedes RC.53 unless a later equivalently verified candidate is explicitly selected on #116. The RC.75 artifact produced while verifying PR #160 is implementation evidence only and is not automatically promoted.

Repository candidate evidence is **not** production acceptance. It does not replace actual IIS, a trusted machine certificate, intended app-pool identity, real recycle durability, deployed least-privilege SQL behavior, operational backup, rollback rehearsal, or human review of the real evidence.

### Finalizer contract — COMPLETE

`Complete-ProductionAcceptance.ps1` closes the last manual JSON mutation in the external evidence workflow:

1. requires explicit `-AcknowledgeFinalAcceptance`, a bounded non-secret operator identity and the externally preserved expected session-manifest SHA-256;
2. requires locked-session binding before prospective work and again immediately before authoritative commit;
3. never changes a gate from FAIL to PASS;
4. restricts closure summary output to a relative path under the evidence-pack root;
5. creates and validates a prospective finalized copy against all exact 15 SHA-bound gates before authoritative mutation;
6. re-hashes the authoritative pack to detect concurrent mutation;
7. atomically commits only `acceptedBy` / `acceptedAtUtc`;
8. revalidates the authoritative finalized pack with locked-session binding and writes the closure summary including session/product anchors;
9. restores the original unaccepted pack if final authoritative validation unexpectedly fails and refuses existing acceptance metadata, unsafe paths or re-finalization;
10. has no IIS deployment/recycle, SQL execution, GitHub API call or issue-closing authority.

### Immutable acceptance session contract — COMPLETE

`New-ProductionAcceptanceSession.ps1` makes the last pre-cutover setup deterministic without manufacturing production evidence:

1. requires a fresh absolute Windows session root and rejects drive/share roots, leading/trailing whitespace, relative roots, explicit `.` / `..` traversal segments and reuse;
2. verifies exact candidate filename/version, requires the checksum and candidate bytes to match an independently supplied selected product SHA-256, and validates a readable non-empty ZIP before creating the session;
3. validates non-secret production metadata through the existing evidence-pack contract;
4. atomically creates a candidate-bound workspace, copies the artifact/checksum into `candidate/`, re-hashes the copy and requires it to remain equal to the selected product SHA-256;
5. invokes the canonical 15-gate generator and verifies all 15 gates remain false with no final acceptance metadata;
6. writes bounded non-secret `session-manifest.json`, including the selected product hash, `session-manifest.sha256` and deterministic `OPERATOR-NEXT-STEPS.txt`;
7. creates `evidence/proof/` as the bounded authoritative proof root;
8. returns `ExternalGateCount=15`, `ExternalGatesPassed=0`, `ProductionAccepted=false`, the selected product hash and the manifest SHA-256 that must be preserved externally for the rest of the cutover;
9. never deploys/recycles IIS, executes SQL, records a gate PASS, finalizes acceptance, calls GitHub or closes #116/#111;
10. is parsed, executed and packaged by the Windows production-candidate gate with positive and negative runtime cases, including rejection of a valid substituted ZIP + matching checksum pair whose bytes differ from the independently selected hash.

### Locked-session evidence chain contract — #258 / PR #259 COMPLETE

`Test-ProductionAcceptanceSessionBinding.ps1` and the session-bound recorder/finalizer close the post-initialization chain-of-custody gap without creating production evidence:

1. the operator preserves the initializer-returned session-manifest SHA-256 outside the mutable session files; later code never silently derives a replacement expected value from the current manifest;
2. the binding verifier requires the actual manifest hash and canonical `session-manifest.sha256` lock to equal that externally preserved value;
3. manifest schema/status/SingleNode/0-of-15 anchor and candidate/evidence relative paths must remain exact and session-confined;
4. the copied candidate ZIP is re-hashed and its companion checksum must still equal `selectedProductSha256`;
5. evidence-pack candidate version/source/tested-merge/artifact/hash and environment host/site/app-pool/identity/certificate/backup/paths must exactly match the locked manifest;
6. `Set-ProductionAcceptanceGate.ps1` requires this binding before every explicit one-gate-at-a-time PASS mutation;
7. `Complete-ProductionAcceptance.ps1` requires binding before prospective validation, rechecks it immediately before authoritative commit, and invokes authoritative session-bound validation after commit;
8. `Test-ProductionAcceptanceEvidence.ps1` retains low-level standalone schema/evidence validation, but production finalization/review supplies the expected manifest SHA and emits `sessionManifestSha256` + `selectedProductSha256` in closure summaries;
9. Windows production-candidate runtime exercises Session → Recorder → Finalizer → Validator and rejects wrong manifest anchors, pack candidate drift, manifest drift, candidate-byte drift, unsafe evidence and invalid finalization;
10. this chain is repository safety only: it never proves trusted IIS, real SQL operations, recycle durability, backup/rollback or any of the 15 external gates.

### Acceptance Control Toolkit provenance contract — #261 / PR #262 COMPLETE

The post-RC.61 acceptance-control sidecar is immutable and independently attributable without rebuilding or repackaging RC.61:

1. `Export-ProductionAcceptanceToolkit.ps1` requires an independently supplied exact 40-hex tooling commit and verifies Git `HEAD` equals it;
2. tracked checkout state must be clean, all six approved scripts must be tracked/present, and export goes only to a fresh directory outside the source checkout;
3. deterministic `toolkit-manifest.json` records the exact tooling commit, exact six filenames and each SHA-256, with canonical `toolkit-manifest.sha256`;
4. `Test-ProductionAcceptanceToolkit.ps1` independently requires both expected tooling commit and expected toolkit-manifest SHA-256, validates the exact root set and rejects missing/extra/modified/commit-drift cases;
5. `New-ProductionAcceptanceSession.ps1` verifies the exported toolkit and binds `operatorToolingCommit`, `operatorToolkitManifestSha256` and all six file hashes into the immutable session manifest;
6. `Test-ProductionAcceptanceSessionBinding.ps1` re-verifies toolkit manifest hash/lock, exact commit/file-set entries and all six current file hashes before Gate/Finalizer/Reviewer operations;
7. Windows provenance runtime covers clean export/verify plus wrong commit, dirty tracked checkout, manifest tamper, extra file, modified file and missing file negatives;
8. exact approved cutover tooling identity is `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`; `main`, `latest` or any moving branch ref is not a substitute;
9. PR #262 exact head passed CI #1786 / `31992503009` with 984/984 and Windows production-candidate #186 / `31992502977` end-to-end before squash merge `a448eb715af9b3a2fcfe89ce92807b71fc7e1127`;
10. toolkit provenance is repository safety only and cannot create a release, deploy IIS, execute SQL, mark an external gate PASS or close #162/#116/#111.

### Release-package parity contract — COMPLETE

The release artifact no longer has a weaker construction path than the selected production candidate:

1. `production-candidate.yml` is the single reusable Windows package workflow for PR candidates and release callers;
2. an explicit reusable `candidate_version` is syntax-bounded before it can reach artifact paths/version metadata;
3. `release.yml` resolves/validates the tag/manual version and delegates packaging to the reusable production-candidate workflow rather than running independent publish/zip/upload steps;
4. tagged/manual releases inherit the same Release build warnings-as-errors, full tests, production PowerShell parser, immutable-session runtime, session-bound recorder/finalizer runtime, RID-specific win-x64 publish, secret-free baseline validation, HTTPS/auth smoke before/after restart, runtime-state removal, `_operations` staging, clean-package validation and SHA-256 artifact upload;
5. release manifest schema 2 records fixed P0.4 run IDs as `prerequisiteEvidence.p04`, while candidate-specific run evidence remains authoritative on #116;
6. regression tests fail if independent `dotnet publish`, packaging, `upload-artifact` in `release.yml`, or ambiguous `realSqlAcceptance` manifest fields return;
7. this is repository release-integrity evidence only and cannot satisfy a real IIS gate.

### Durable tagged release asset contract — #159 / PR #160 COMPLETE

A real version tag remains recoverable after GitHub Actions artifact retention expires without introducing a second build path:

1. only a pushed version tag may enter the durable publication job; `workflow_dispatch` and PR candidate runs remain Actions-artifact-only;
2. publication depends on the successful reusable Windows production-candidate job and downloads that exact same-run artifact by deterministic name;
3. the downloaded ZIP must match its strict companion SHA-256 record before any GitHub Release mutation;
4. `contents: write` is scoped only to the tag-publication job; default workflow and candidate permissions remain `contents: read`;
5. a new release is created only for the exact existing pushed tag with `--verify-tag`, and only the verified ZIP plus `.sha256` are attached;
6. non-plain semantic versions are marked prerelease;
7. reruns do not upload or clobber assets: if a release exists, both assets are downloaded and must exactly match the expected product hash/checksum/filename; missing or mismatched assets fail closed;
8. regression tests prohibit independent `dotnet publish`, packaging, `upload-artifact` in `release.yml`, `gh release upload`, and `--clobber`;
9. #159 completion proves release durability only; it does not satisfy any external IIS gate or change the selected cutover candidate automatically.