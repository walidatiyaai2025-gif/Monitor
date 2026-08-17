# Implementation Plan

This is the canonical execution plan. Update it in the same PR as material implementation changes.

## CURRENT P0 — Real SQL Production MVP

**Umbrella:** Issue #111  
**Execution ledger:** `docs/PRODUCTION_MVP.md`  
**Real SQL evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Production acceptance guide:** `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md`  
**Active release gate:** Issue #116 / P0.5 First Production SingleNode  
**Repository cutover/evidence/session/finalization/release/durable-tag/workflow-supply-chain/native-Node-24/durable-release tooling:** COMPLETE through selected-product-hash acceptance-session hardening PR #257, locked-session sidecar binding PR #259 and clean exact-commit Acceptance Control Toolkit provenance PR #262. Exact cutover toolkit source is `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`; selected RC.61 durable publication remains pending manual #162. Read-only operator preflight #266 / PR #267 is COMPLETE, squash-merged as `43aaa6071fd0c577c792d427ad490717f28acbac`; exact head `cdaff693810534db52975976309b726a0a8d409c` passed CI #1843, Real SQL #121 and Windows production-candidate #203, and post-merge main CI #1844 passed. Final Step 0 operator handoff #270 / PR #271 is COMPLETE, squash-merged as `479f9b557948b56fc5ec5692efb67fd6f1f4a921` after CI #1854 and Windows production-candidate #205 Green; post-merge main CI #1855 Green. None of this dispatches or publishes RC.61.  
**Latest RC.61 read-only state check:** source run `31667721306` successful; artifact `9168574442` present/unexpired with exact outer digest `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382` and expiry `2026-09-12T04:41:34Z`; promotion/verifier still have zero runs and `v0.1.0-rc.61` tag/release remain absent. The connected agent surface has no workflow-dispatch action; Step 1 remains an explicit external operator action.  
**Required remaining dependency:** `#162 durable RC.61 publication + independent verification -> #116 real trusted-IIS 15/15 acceptance -> #111 closure`. **Do not begin #116 production mutation while #162 is OPEN.**  
**Live selected candidate/evidence ledger:** Issue #116 — RC.61  
**Project rule:** until P0.5 is accepted on the real environment, production-slice blockers outrank unrelated feature expansion. Repository/product work such as BATCH-800 may proceed only inside the documented non-production safety boundary and cannot manufacture P0 acceptance.

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
| 5 | P0.5 | #116 | First trusted-HTTPS IIS SingleNode production release | **ACTIVE / BLOCKED BEFORE MUTATION BY #162 — repository workflow, selected-product-hash binding, locked-session sidecar binding #258/#259, Acceptance Control Toolkit provenance #261/#262 and Step 0 preflight/operator handoff #266/#267/#270/#271 are complete; RC.61 publication #162 must complete before external IIS acceptance begins** |

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
- Issue #261 / PR #262 — Acceptance Control Toolkit provenance **COMPLETE**: export requires a clean checkout of independently supplied exact commit `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`; deterministic `toolkit-manifest.json` + canonical SHA-256 lock bind the exact six files; an independent verifier re-checks commit/manifest/lock/file set and file hashes; each production acceptance session binds the toolkit-manifest SHA plus six current file hashes before Gate/Finalizer/Reviewer operations. CI #1786 / `31992503009` passed 984/984 and Windows production-candidate #186 / `31992502977` passed end-to-end; PR #262 squash-merged as `a448eb715af9b3a2fcfe89ce92807b71fc7e1127`. Moving refs such as `main` or `latest` are not accepted as cutover toolkit identity.
- PR #155 / Issue #154 — tagged/manual release-package parity; **COMPLETE**, squash-merged `8d8ae2c5f35e8a1d774c5a9480f582e432e5dc03`. `production-candidate.yml` is reusable through `workflow_call`; `release.yml` delegates to that exact Windows workflow; explicit candidate versions are syntax-bounded; manifest schema 2 records fixed P0.4 run IDs under `prerequisiteEvidence.p04` and leaves candidate-specific CI authoritative on #116.
- PR #160 / Issue #159 — durable tagged GitHub Release assets; **COMPLETE**, squash-merged `a14110181932bcd6e14b99e5b6984974a5b477f8`. Real pushed version tags publish only the already-verified same-run ZIP + `.sha256` after checksum re-verification; package construction remains solely in `production-candidate.yml`; publication is tag-only, has job-scoped `contents: write`, never rebuilds/repackages, never clobbers an existing release, and accepts a rerun only when existing release assets exactly match the verified product/checksum. Final exact-head normal CI `31677055397` was Green 809/809, Real SQL `31677055241` Green 8/8, and Windows production-candidate `31677055305` Green 809/809.
- PR #163 / Issue #162 — exact existing-candidate durable-promotion implementation **COMPLETE** and merged `43d8a193205495f155bb8866532a4e99ed93b655`; handoff PR #164 merged `930c057f431a36ab2b603d3dc39e70e8c31c744e`. Actual RC.61 publication remains pending manual dispatch and independent asset/hash verification on #162.
- PR #267 / Issue #266 — read-only RC.61 durable-promotion operator preflight **COMPLETE**: exact selected repository/version/source run/artifact/outer digest/product hash/source head/tested merge/tag are pinned; source-run/artifact provenance, expiry and ambiguous durable-state API failures fail closed; exact approved promotion and separate verifier commands are emitted. Exact head `cdaff693810534db52975976309b726a0a8d409c` passed CI #1843, Real SQL #121 and Windows production-candidate #203; PR #267 squash-merged as `43aaa6071fd0c577c792d427ad490717f28acbac`; post-merge main CI #1844 passed. The helper never dispatches, creates/mutates a release or tag, rebuilds/repackages RC.61, touches IIS/SQL or satisfies #162/#116/#111.
- PR #271 / Issue #270 — final Step 0 operator handoff **COMPLETE**: both RC.61 durable-promotion guides require the read-only preflight before the first publication attempt and require `Status=READY_FOR_EXPLICIT_MANUAL_PROMOTION`, `MutatedGitHubState=False`, `TagExists=False` and `ReleaseExists=False`; existing durable state, artifact expiry, provenance/digest drift or ambiguous GitHub probing is an explicit stop condition. PR #271 squash-merged `479f9b557948b56fc5ec5692efb67fd6f1f4a921`; CI #1854 and Windows production-candidate #205 were Green; post-merge main CI #1855 Green. Documentation/handoff only; RC.61 remains unpublished.
- PR #171 / Issue #168 — GitHub Actions supply-chain hardening **COMPLETE**, merged `c9084dd32b12a9a078f953f85f39b253793e2343`; mutable external Action refs removed in favor of approved exact SHAs and obsolete write-capable completed workflow removed.
- PR #174 / Issue #173 — native Node 24 Action migration **COMPLETE**, merged `bc7cb2d275f423fb381b83d92c76f6516e404fe9`; CI `31881744429`, Real SQL `31881744413` and Windows `31881744437` Green. RC.87 is implementation evidence only and does not supersede selected RC.61.
- PRs #177–#219 — additional repository-only hardening **COMPLETE**: reproducible SDK and exact SQL image, pinned Linux/Windows runners, NuGet source and direct-package guards, checkout/token scope, write-capable workflow allowlists, release-tag mutation serialization, main-ref promotion preflight, exact-two assets, metadata/digest/provenance/TOCTOU/workspace/directory-atomic durable verification, independent read-only verification and toolchain capability preflight. PR #219 is the latest merged durable-release hardening batch. None dispatches promotion, changes RC.61 selection or satisfies external IIS acceptance.

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

1. the operator preserves the initializer-returned manifest SHA-256 outside the mutable session files; later code never silently derives a replacement expected value from the current manifest;
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
6. regression tests fail if independent release packaging or the ambiguous `realSqlAcceptance` manifest field returns;
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

### P0.5 execution order

| Task | Required result | State |
|---|---|---|
| P0-041 | Freeze production scope to SingleNode | COMPLETE — repository/CI |
| P0-042 | Secret-free Production baseline; runtime-only credentials | COMPLETE — repository/CI |
| P0-043 | Deploy to actual IIS with trusted HTTPS | **BLOCKED BY #162; then PENDING EXTERNAL** |
| P0-044 | Prove Data Protection/protected credentials through restart/recycle | CI process-restart VERIFIED; **IIS recycle blocked by #162 then pending external** |
| P0-045 | Prove registration/audit/history/incidents through real recycle | **BLOCKED BY #162; then PENDING EXTERNAL** |
| P0-046 | Run health smoke on deployed HTTPS endpoint | CI HTTPS VERIFIED; acceptance tooling READY; **IIS endpoint blocked by #162 then pending external** |
| P0-047 | Prove target remains read-only/least-privilege from deployed application identity | P0.4 prerequisite VERIFIED; **blocked by #162 then external deployment evidence pending** |
| P0-048 | Create/validate backup and rehearse rollback/recovery | code/unit/tooling VERIFIED; **production rehearsal blocked by #162 then pending external** |
| P0-049 | Versioned artifact/checksum + deterministic session/evidence/finalization/release workflow | **REPOSITORY HARDENED — RC.61 selected; durable tagged GitHub Release tooling and exact-candidate promotion implementation/hardening verified through PR #219; selected-product-hash session binding COMPLETE via #256/#257; locked-session sidecar binding COMPLETE via #258/#259; Acceptance Control Toolkit provenance COMPLETE via #261/#262 with exact toolkit source `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`; read-only operator preflight #266/#267 COMPLETE; Step 0 handoff #270/#271 COMPLETE; actual RC.61 publication + separate verification pending manual #162** |
| P0-050 | Final real-environment 15/15 acceptance and #111 closure | **BLOCKED BY #162; then PENDING EXTERNAL #116** |

### Immediate next actions

1. Preserve RC.61 and product SHA-256 from #116 unless #116 explicitly selects another equivalently verified candidate.
2. Complete #162 in strict order: run the merged read-only Step 0 preflight from a trusted authenticated operator checkout and require `Status=READY_FOR_EXPLICIT_MANUAL_PROMOTION`, `MutatedGitHubState=False`, `TagExists=False`, `ReleaseExists=False`; then run the exact manual existing-candidate promotion from `main`; then run the separate read-only durable-release verification and independently verify tag/assets/product hash. **No #116 production mutation before #162 is complete.**
3. After #162 completes, on the intended Windows/IIS host create/validate the pre-cutover operational backup.
4. Export and independently verify the Acceptance Control Toolkit from clean exact source `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`; preserve its toolkit-manifest SHA-256 independently. Then create one fresh immutable candidate-bound acceptance session with the independently selected RC.61 product SHA-256, exact tooling commit and expected toolkit-manifest SHA; verify the copied candidate and all six sidecar files still match their locked identities, preserve the initializer-returned session manifest SHA-256 outside mutable session files, verify `session-manifest.sha256`, `PreparedFailClosed` and 0/15 before any production mutation.
5. Run packaged IIS preflight, review PLAN ONLY deploy output, then cut over with explicit `-Apply`.
6. Prove trusted HTTPS health/authentication and the approved least-privilege monitored SQL path.
7. Recycle IIS and prove registration, protected credential and operational-state durability.
8. Rehearse rollback/recovery and repeat health/auth/read checks.
9. Record each real gate with `Set-ProductionAcceptanceGate.ps1`, the externally preserved expected session-manifest SHA-256 and SHA-bound non-secret evidence from the same session.
10. After real 15/15, run `Complete-ProductionAcceptance.ps1` with the same manifest anchor, approved operator identity and explicit final acknowledgement; independently re-run session-bound validation and human-review the real closure evidence. Only then may #116 close; #111 closes only after #116.

## BATCH-700 — Full visible portal/UI completion — COMPLETE

**Parent:** Issue #220 — CLOSED / COMPLETED  
**Children:** #221–#225 — CLOSED / COMPLETED  
**Task range:** UI700-001..050  
**Repository state:** **50/50 COMPLETE and merged to `main`.**

BATCH-700 was added after a current-main UI audit showed that backend/CI completion did not mean every visible operator route was product-complete. The batch deliberately changes presentation and control-plane workflows without changing monitored-SQL collection semantics.

Completed sequence:

1. **#221 / PR #236 — Foundation:** safe 403/404/500 surfaces, production error wiring, reusable page/state components, boundary-aware navigation and keyboard/mobile shell. Merged `59a931cc031e19f162edfadc278dc8b9c6c842e3`; exact-head CI/Real-SQL/Windows Green.
2. **#222 / PR #237 — Health:** dedicated Database, Backup, SQL Agent, Storage, Blocking and Performance pages, consistent source states and server drill-down. Merged `308a2f31a42500ce7354b1af2c2369d59be57455`; exact-head CI/Windows Green.
3. **#223 / PR #238 — Audit/History:** bounded filters/paging, semantic outcomes, history window/page controls, evidence-only summaries and missing-evidence states. Merged `3864b4f8acc14d6e0bd259bfb1ab52d9fec07be1`; synchronized exact-head CI/Windows Green.
4. **#224 / PR #239 — Recommendations/Reports:** bounded recommendation filters, ordered risk guidance, evidence links, report format/version/access metadata, Administrator diagnostics separation and contextual history export. Merged `cab4b9492eb65a6ec7340add016dd12bb99eb13f`; synchronized exact-head CI/Windows Green.
5. **#225 / PR #240 — Enterprise/Admin/final:** actionable Readiness, task-oriented Help, Governance dry-run/apply/receipt workflow, Observability source/readiness hierarchy, grouped Settings, reinforced Connection Lab states, Fleet drill-down, role regression, explicit 390px/reduced-motion/focus contracts and CI visible-route smoke. PR #240 squash-merged as `fd33e79c6d19d7f9852417b9c35a11f91f21714c`. Exact final head `0834db6b5d518fe5c52eec9b47c03e467929aa89` passed `ci` #1637, `real-sql-acceptance` #91 and `production-candidate` #142 before merge.

BATCH-700 has no browser/Playwright screenshot harness, so responsive/visual acceptance is represented honestly by source contracts, route/view smoke and the existing build/test/Windows gates rather than by a claimed browser screenshot run.

BATCH-700 does **not** change production priority or acceptance truth: monitored GETs remain cache/control-plane only; no autonomous remediation or SQL execution is added; #162 still governs durable RC.61 publication; #116/#111 still govern real IIS/HTTPS 15-gate production acceptance.

## BATCH-800 — Full functional operator wiring — IN PROGRESS

**Umbrella:** Issue #287 — OPEN  
**Current PR:** #288 — DRAFT / partial evidence-backed slice  
**Task range:** B800-001..100  
**Execution ledger:** `docs/BATCH_800.md`

BATCH-800 closes the gap between a visible route and a functionally wired operator workflow. The completion contract is:

`UI control / route -> controller endpoint -> authorization + antiforgery boundary -> service/read model -> persisted or cached evidence -> explicit success/error/unavailable state -> regression evidence`

The current #288 slice keeps browser navigation cache/control-plane-only and implements only evidence that can be bounded and represented truthfully:

- B800-001..019 inventory, cached Server Details intelligence, cross-page consistency/drill-down/freshness contracts, role integration coverage and explicit non-browser acceptance boundary;
- B800-021 assembly-wide protected POST workflow matrix and B800-022 Razor POST-form-to-real-endpoint wiring regression contract;
- B800-031..038 bounded optional Memory diagnostics and deterministic decision support with no configuration writes;
- B800-041..047 bounded top-12 cumulative non-benign wait evidence and cached Performance projection;
- B800-051..061 bounded top-12 logical-file I/O evidence, SQL Agent max-50 summary run-history evidence and cached reliability projections, with physical paths/commands/recurrence definitions/proxies/credentials excluded;
- B800-063 bounded max-50 current SQL Agent activity rows from the latest `sysjobactivity` session, carrying logical job name, server-local next scheduled run (`DateTimeKind.Unspecified`) and current running state only. This is evidence for current activity, **not** lateness: no UTC conversion, Late/On-time label, server time-zone identity, recurrence definition or expected-run policy is invented;
- B800-069 bounded max-50 user-database logical name/state evidence with non-online states prioritized, allowing exact B300 classification/actionable/worst-observed projection without reconstructing detail from aggregate counts.

Safety/truth boundaries remain mandatory: monitored GETs never collect SQL; missing evidence is never converted to zero/healthy; wait/I/O counters are cumulative since SQL Server start rather than interval history; `AgentReliabilityProjection` keeps `ScheduleLatenessEvaluated=false` until canonical time-zone + recurrence/expected-run semantics exist; backup RPO compliance is not claimed without policy; TempDB, transaction-log, HA readiness and privacy-safe query regression remain pending; no SQL text/query plans/client identity/table data/physical paths are collected; no autonomous remediation or AI-generated SQL execution is introduced.

Least privilege remains read-only: SQL Server 2022+ uses `VIEW SERVER PERFORMANCE STATE` (older supported versions `VIEW SERVER STATE`) plus `VIEW ANY DEFINITION` and existing narrow metadata grants; Agent history/activity adds only read-only `SELECT` on `msdb.dbo.sysjobhistory` and `msdb.dbo.sysjobactivity`, with no SQLAgent execution/operator role.

Pre-canonical chronology includes CI #2029 + Real SQL #161 Green for waits; CI #2046 + Real SQL #169 + Windows #265 Green for Storage/I/O; database-state source head `895297809d5dcb656cb3e6bc064aba96d02e58b1` passed CI #2120 + Real SQL #205. These are not final merge evidence. B800-063 and workflow-matrix diagnostics moved the branch after earlier validation. PR #288 becomes eligible for Ready/merge only after `BATCH_800`, `FEATURE_CATALOG`, `STATUS` and this plan are reconciled on one exact head, normal CI + applicable Real SQL + Windows production-candidate are all Green, review threads are resolved and the branch is current with `main`. Merging #288 closes only this partial slice; #287 remains OPEN for remaining tasks.

BATCH-800 does not publish/supersede selected RC.61, mutate real production IIS/SQL, satisfy #162/#116/#111, or change the strict production dependency.

## Verified foundation

| Milestone | Scope | State |
|---|---|---|
| M0 | Visual foundation, secure development auth, shell, Command Center and CI/visual acceptance | VERIFIED |
| M1 | Registration/secret boundary, Test Connection, collector, snapshot/cache, real UI, throttled refresh | VERIFIED |
| M2 | Memory/database/backup/Agent/storage/blocking/performance health modules | VERIFIED |
| M3 | Deterministic findings, incidents, recommendations and operator workflow | VERIFIED |
| M4 | AI Advisor advisory-only boundary, guarded request/cache/timeout/circuit/audit | VERIFIED |
| M5 | History, collection cycle, trends, scheduler, audit, RBAC, browser security | VERIFIED |
| M6 | Real multi-server onboarding and estate UI | VERIFIED |
| M7 | Durable registration/operational state/protected credentials/shared-state readiness/deployment safety | VERIFIED |
| M8 | Zero-SQL monitored GETs and explicit protected refresh | VERIFIED |

## Historical hardening batches

- `docs/BATCH_100.md` — B100-001..100 COMPLETE.
- `docs/BATCH_200.md` — B200-001..100 COMPLETE; current-main reconciliation **COMPLETE** through Issue #99 / PR #156, squash-merged `221e44a9f13ed02e994311addff94b0e7996e444`. Final exact-head normal CI `31669072593`, Real SQL `31669072572`, and Windows production-candidate `31669072625` are Green.
- BATCH-300 — B300-001..100 COMPLETE; final reconciled CI `31465013971`.
- `docs/BATCH_400.md` — B400-001..110 COMPLETE.
- BATCH-500 — B500-001..100 COMPLETE.
- BATCH-600 — B600-001..100 COMPLETE.
- `docs/BATCH_700.md` — UI700-001..050 COMPLETE; PR #240 squash-merged as `fd33e79c6d19d7f9852417b9c35a11f91f21714c` after exact final head `0834db6b5d518fe5c52eec9b47c03e467929aa89` passed CI #1637, Real SQL #91 and production-candidate #142.
- `docs/BATCH_800.md` — B800-001..100 IN PROGRESS under #287; PR #288 is a partial evidence-backed slice and is not counted as full-batch completion.

The BATCH-200 reconciliation selectively restored retention governance, enterprise security hardening and bounded scale primitives plus mapped B200-051..090 regression coverage and an additional audit-pagination regression on RC.61-era current main. Legacy issues #87/#91/#93 are closed completed, while stale PRs #88/#92/#94/#104 are closed unmerged as superseded. This was baseline correction rather than feature expansion or new task accounting; it preserves `IServerTargetLifecycleService`, BATCH-300 and all P0 production/release boundaries and does not change #116 or selected RC.61.

Historical feature breadth remains available, but it does not outrank the remaining P0.5 production acceptance gate.

## Stable guardrails

- Browser monitoring GETs remain cache/control-plane only and never initiate monitored SQL collection.
- No browser connects directly to monitored SQL.
- No autonomous remediation or AI-generated SQL execution.
- Credentials/full connection strings/current secret references/raw provider errors/arbitrary SQL text remain outside UI, audit, telemetry, exports, diagnostics and production evidence.
- Mutations require POST + antiforgery + named authorization.
- Suppression does not rewrite incident evidence.
- Maintenance changes scheduled collection behavior only; manual refresh remains explicit/audited.
- MultiNode remains fail-closed and deferred until after stable SingleNode production acceptance.
- Repository CI/synthetic evidence/session/finalizer/release-package/UI validation cannot close #116.
- Release dependencies remain fail-closed: #162 must complete before #116 production mutation; #111 cannot close before #116 is accepted.
- BATCH-800 may extend bounded snapshot evidence only when the UI cannot be wired truthfully from existing cached state; unsupported diagnostics must remain explicit rather than receiving placeholder values.

## Definition of done

The production plan is complete only when P0-001..050 are reconciled, P0.1..P0.5 are accepted in order, #162 Step 0/manual promotion/separate durable verification and tag/assets/product-hash checks are complete before #116 production mutation, the selected SingleNode release has actual trusted-HTTPS IIS/recycle/least-privilege/backup/rollback evidence, the real 15/15 evidence pack remains bound to the externally preserved session-manifest SHA-256 through recording/finalization/review, and the final required CI/acceptance gates are Green. BATCH-700 repository/UI completion and BATCH-800 functional-wiring work are independent of that external production acceptance and cannot satisfy it.

## Issue #276 / PR #279 — Idempotent IIS bootstrap installer and deploy entrypoint — COMPLETE

- Repository implementation landed on `main` in `ce498e1beeb7acf9b9950917132cda313be9778f`, `94e44caf3872c40710fc4ec04adb37fea2a62244` and `e7621fcb5dd94d0cc3a7baa91603c3beda11c1c8`.
- PR #279 completed production-candidate parser/package integration, operator documentation and canonical tracking and squash-merged as `d784d0d62b9db6cec2a94d07102e5330ded7783a` after CI #1921 / `31999646008`, Real SQL #131 / `31999646007` and Windows production-candidate #218 / `31999645994` Green.
- The bootstrap remains PlanOnly-by-default and preserves the existing authoritative preflight/deploy, immutable release, external `App_Data`, package SHA-256, acceptance and physical-path rollback semantics.
- This work did not rebuild/repackage the selected RC.61 candidate, dispatch/publish #162, mutate a real IIS/SQL target or manufacture #116 evidence.

## Issue #281 / PR #283 — Fresh-host IIS bootstrap and PowerShell 7 prerequisite — COMPLETE

- PR #283 ported only the unique fresh-host/idempotency improvements from superseded PR #280 onto current main and squash-merged as `75f4a3e9a8f84ac2c088b2ba77e4d9b18a80eb15`; duplicate #279 workflow/docs changes were not reintroduced.
- Before any IIS/application mutation, `Install-ProductionSingleNode.ps1` detects/prepares PowerShell 7 and requires relaunch under `pwsh` if the operator starts in Windows PowerShell 5.1.
- Online PowerShell mode is pinned to official v7.4.16 `PowerShell-7.4.16-win-x64.msi` with SHA-256 `2c0c2036b0032375ad4f7809a92d0b6fa4a8e4ee89a75211514c4cf55ae22495`; Offline mode accepts an operator-supplied MSI. Both paths require SHA-256 and Microsoft Corporation Authenticode verification.
- Prerequisite installer exit `3010` and Windows-feature reboot requirements stop before IIS/application cutover; after installation the operator reruns from elevated PowerShell 7.
- Bootstrap hardening includes robust .NET/ANCM discovery, exact approved Microsoft Hosting Bundle Online hosts, optional Hosting Bundle SHA-256, shared-IIS restart gating behind explicit `-AllowIisServiceRestart`, PFX reuse, binding drift fail-closed behavior and conditional ACL handling.
- Dedicated regression tests plus `docs/IIS_FRESH_HOST_BOOTSTRAP.md` and `docs/work/P0-053.md` cover these boundaries.
- Exact-head CI #1957, Real SQL #138 and Windows production-candidate #228 passed Green; post-merge main CI #1958 passed. This work did not satisfy or bypass #162/#116/#111.

## Issue #285 / PR #286 — Clean IIS start without implicit demo data — COMPLETE

- The sample DA-SQL01..04 estate is explicit configuration instead of an unavoidable fallback: base and Production default `DemoData:Enabled=false`; Development explicitly opts in.
- A fresh/empty persistent store renders a truthful zero-registration dashboard and directs the operator to Connection Lab until a real SQL Server is added/tested/saved; registered real targets and cached snapshot mapping remain unchanged.
- POST failure handling remains truthful under IIS/ASP.NET Core status-code re-execution because `/error` and `/error/status/{statusCode}` are verb-agnostic; targeted regression coverage prevents HTTP 405 from masking the intended error surface.
- `docs/IIS_CLEAN_STAGING.md` documents the disposable staging reset: removing `App_Data` deletes local registrations, protected SQL secrets, Data Protection keys, audit/history/incidents and local operational backups and is not a production migration procedure.
- Final PR head `ff14f16006b1d5c953ba4c507f196a3393660e42` passed CI #2085, Real SQL #188 and Windows production-candidate #284 Green. PR #286 squash-merged as `74b804e8b681a77b9e619490610af556a4b1ae3e`; post-merge main CI #2095 passed Green and Issue #285 closed completed.
- Safety boundary remained repository/staging only: no selected RC.61 rebuild/repackage/publication, real production IIS/SQL mutation, #116 acceptance manufacture or bypass of `#162 -> #116 -> #111`.
