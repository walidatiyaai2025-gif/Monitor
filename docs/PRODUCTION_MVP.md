# P0 — Real SQL Production MVP

This is the execution ledger for Issue #111 and the highest-priority delivery program until the first SingleNode production release is accepted.

## Product outcome

Deliver one trustworthy vertical slice:

`Login -> Add SQL Server -> Test -> Save -> Collect -> View Server Details -> Refresh -> Restart/Recycle -> View trustworthy persisted target again`

Production-visible values must be backed by collected evidence. If a dimension is not collected, unavailable, stale, or permission-limited, the UI must say so explicitly. A default numeric zero must never masquerade as observed production data.

## Execution rules

1. Work in release-gate order. A later gate may prepare non-conflicting work, but cannot be declared complete before its dependencies.
2. Issue #111 is the umbrella. Child issues #112..#116 are the release gates.
3. Until P0.5 is accepted, unrelated feature expansion is secondary to production-slice blockers.
4. Preserve the zero-monitored-SQL GET boundary. Browser navigation reads cached/control-plane state only.
5. No autonomous remediation or AI SQL execution.
6. No plaintext credentials, full connection strings, current secret references, raw provider exceptions, or arbitrary SQL text in UI/audit/telemetry/export/diagnostics/evidence.
7. First production activation is SingleNode. MultiNode remains outside P0.
8. Every repository gate requires Release build with warnings-as-errors, applicable tests, canonical docs synchronization, and PR CI before merge.
9. CI simulation, candidate packaging, durable release publication and synthetic evidence are not substitutes for external production acceptance. IIS binding, trusted HTTPS, application-pool identity, real recycle behavior, least-privilege SQL, backup and rollback require actual environment evidence.
10. Remaining P0 order is strict: `#162 durable RC.61 publication + independent verification -> #116 real trusted-IIS 15/15 acceptance -> #111 closure`. Do not begin production mutation for #116 while #162 is OPEN. Canonical short form: `#162 -> #116 -> #111`.

## Priority chain

| Priority | Release | Issue | Outcome | State |
|---|---|---|---|---|
| 1 | P0.1 | #112 | Real SQL registration is production-safe and restart durable | COMPLETE — PR #119 / final CI `31476747212` |
| 2 | P0.2 | #113 | First snapshot is mapped truthfully into production read models | COMPLETE — PR #121 / final CI `31478470867` |
| 3 | P0.3 | #114 | Server Details v0.1 is the trusted operator source of truth | COMPLETE — PR #122 / final CI `31479311552` |
| 4 | P0.4 | #115 | Full journey passes against a real SQL Server | COMPLETE — PR #124 / normal `31481874425` / Real SQL `31481874501` |
| 5 | P0.5 | #116 | First IIS/HTTPS SingleNode production release is accepted | **ACTIVE — repository implementation/hardening complete; #162 manual RC.61 retention is the blocking prerequisite before any #116 production mutation; external IIS acceptance follows only after #162** |

---

## P0.1 — Real SQL Registration — COMPLETE

**Issue:** #112 — CLOSED / COMPLETED  
**Implementation PR:** #119 — squash-merged to `main` as `57ab5cae6b5bdd3a04adb5069008aae80a1f84e0`  
**Final CI:** `31476747212` — Release build 0 warnings / 0 errors; 501/501 tests passed.

| Task | Description | State |
|---|---|---|
| P0-001 | Reconcile the current Connection Lab registration path against production acceptance criteria | COMPLETE |
| P0-002 | Test candidate before durable registration commit | COMPLETE |
| P0-003 | Compensate failed/cancelled Monitor-owned candidate credentials | COMPLETE |
| P0-004 | Verify Integrated Security and safe connection-string construction | COMPLETE |
| P0-005 | Verify successful registration persistence across restart | COMPLETE |
| P0-006 | Make failed Test Connection state explicit without publishing a live snapshot | COMPLETE |
| P0-007 | Verify deterministic duplicate/repeated registration semantics | COMPLETE |
| P0-008 | Verify protected credential reconnect preserves identity/history | COMPLETE |
| P0-009 | Desktop/mobile Connection Lab acceptance | COMPLETE |
| P0-010 | P0.1 Release build/tests/docs/PR CI gate | COMPLETE |

Exit evidence: failed/cancelled initial tests do not commit a normal enabled target or orphan a Monitor-owned candidate secret; successful registration and protected credentials survive restart boundaries without rendering secret material.

---

## P0.2 — First Real Snapshot & Truthful Mapping — COMPLETE

**Issue:** #113 — CLOSED / COMPLETED  
**Implementation PR:** #121 — squash-merged as `a294c6530d60f17e7c60e3a1ac070ce562af7b18`  
**Final CI:** `31478470867` — Release build 0 warnings / 0 errors; 505/505 tests passed.

| Task | Description | State |
|---|---|---|
| P0-011 | Define the v0.1 production snapshot contract | COMPLETE |
| P0-012 | Verify real identity/version/edition/instance/uptime projection | COMPLETE |
| P0-013 | Verify database total/online/problem-state projection | COMPLETE |
| P0-014 | Verify memory evidence mapping and unavailable semantics | COMPLETE |
| P0-015 | Reconcile SQL Agent total/enabled/failed evidence | COMPLETE |
| P0-016 | Reconcile CPU as explicit Not collected rather than fake 0% | COMPLETE |
| P0-017 | Verify backup/storage/blocking/runtime-pressure mapping | COMPLETE |
| P0-018 | Add stale/unavailable/permission-limited evidence semantics | COMPLETE |
| P0-019 | Add truthful-projection regression tests | COMPLETE |
| P0-020 | P0.2 Release build/tests/docs/PR CI gate | COMPLETE |

Exit evidence: every production-visible health dimension is supported by actual cached snapshot evidence or explicitly marked unavailable/not collected; missing evidence is never aggregated as numeric zero.

---

## P0.3 — Server Details v0.1 Source of Truth — COMPLETE

**Issue:** #114 — CLOSED / COMPLETED  
**Implementation PR:** #122 — squash-merged as `245bb0770d7ec6e7a334f7763d3560cef80324fe`  
**Final CI:** `31479311552` — Release build 0 warnings / 0 errors; 507/507 tests passed.

| Task | Description | State |
|---|---|---|
| P0-021 | Make connection/collection/freshness status first-class | COMPLETE |
| P0-022 | Show instance/version/edition/uptime from cached real evidence | COMPLETE |
| P0-023 | Show database availability and problem-state evidence | COMPLETE |
| P0-024 | Show memory evidence with explicit unavailable state | COMPLETE |
| P0-025 | Show backup evidence and last-full-backup context | COMPLETE |
| P0-026 | Show SQL Agent total/enabled/failed evidence | COMPLETE |
| P0-027 | Show storage, blocking and runtime-pressure evidence | COMPLETE |
| P0-028 | Make collected timestamp/snapshot age/stale state visible | COMPLETE |
| P0-029 | Remove invented health-score dependencies and accept desktop/mobile UI | COMPLETE |
| P0-030 | Zero-SQL-GET + Release build/tests/docs/PR CI gate | COMPLETE |

Exit evidence: Server Details is evidence-first — observed cached facts or explicit absence, no synthetic numeric Health Score, and normal GET navigation remains cache-only.

---

## P0.4 — Real SQL End-to-End Acceptance — COMPLETE

**Issue:** #115 — CLOSED / COMPLETED  
**Foundation PR:** #123 — squash-merged as `83540afe15f5d52ee528ff7de46430682444594d`  
**Full-journey PR:** #124 — squash-merged as `f4c08292734c293a6d0b865cc2a005b8c42b02a6`  
**Final normal CI:** `31481874425` — Release build 0 warnings / 0 errors; 518/518 passed.  
**Final Real SQL:** `31481874501` — Release build 0 warnings / 0 errors; 8/8 RealSql passed.  
**Durable evidence:** `docs/REAL_SQL_ACCEPTANCE.md`.

| Task | Description | State |
|---|---|---|
| P0-031 | Prepare SQL Server 2022 target and least-privilege non-sysadmin monitor login | COMPLETE |
| P0-032 | Execute Add -> Test -> Register -> Collect -> View against real SQL | COMPLETE |
| P0-033 | Execute manual Refresh with bounded collection/publication | COMPLETE |
| P0-034 | Reconstruct services/persistence/key ring and recover after restart | COMPLETE |
| P0-035 | Verify bad-password classification and bounded safe message | COMPLETE |
| P0-036 | Verify network-unavailable and timeout classifications | COMPLETE |
| P0-037 | Verify TLS/certificate rejection classification | COMPLETE |
| P0-038 | Verify missing server-state/msdb/Agent permissions and least-privilege completeness | COMPLETE |
| P0-039 | Record redaction canaries and real-server evidence | COMPLETE |
| P0-040 | P0.4 final Release + real-server acceptance gate | COMPLETE |

Exit evidence: the full journey passed against SQL Server 2022 under success and controlled authentication/network/timeout/TLS/server-permission/msdb-permission failures with a non-sysadmin least-privilege login. No acceptance secret was committed or persisted in plaintext.

---

## P0.5 — First Production SingleNode Release — ACTIVE

**Issue:** #116 — OPEN / ACTIVE  
**Dependency:** P0.4 COMPLETE  
**Selected candidate:** #116 — **RC.61**  
**Durable publication gate:** #162 — **PENDING EXPLICIT OPERATOR PROMOTION + SEPARATE READ-ONLY VERIFICATION**  
**Repository implementation/hardening:** **COMPLETE through durable-release hardening PR #219, selected-product/session/toolkit provenance hardening, explicit RC.61 operator helper #338/#339, canonical handoff reconciliation #340/#341 and production acceptance guide reconciliation #342/#343**  
**Current operator handoff:** `Invoke-Rc61DurablePromotion.ps1` preview -> explicit `-AcknowledgePromotion` -> exact captured promotion run -> separately execute returned `IndependentVerificationCommand` -> `Test-Rc61CutoverReadiness.ps1` with explicit promotion/verification run IDs.  
**Latest handoff merge:** PR #343 squash-merged as `3cd711b608e4ceaf8872eb22a25541bbbfe2729a`; this is docs/tests-only repository evidence and does not publish RC.61.  
**Release gate:** **#116 production mutation BLOCKED while #162 is OPEN; external IIS/HTTPS ACCEPTANCE follows only after #162 is complete**.

### Repository preparation — COMPLETE

The repository-side deployment, evidence, release-retention and hardening work is complete. Key milestones:

- PR #126 / #127 / #129 — Windows production-candidate pipeline, HTTPS acceptance harness/runbook and safe IIS preflight/deploy automation with stable external `App_Data` and immediate rollback.
- BATCH-500 / BATCH-600 — production acceptance/recovery safety and live operator-readiness/evidence orchestration.
- #141 / PR #142 — exact 15-gate fail-closed evidence pack + closure validator.
- #144 / PR #145 — explicit one-gate recorder `Set-ProductionAcceptanceGate.ps1`.
- #147 / PR #148 — explicit finalizer `Complete-ProductionAcceptance.ps1`, merged `e15a9654fbe744e426c95d5965a5faba60868e14`.
- #150 / PR #151 — immutable candidate-bound session initializer `New-ProductionAcceptanceSession.ps1`, merged `9a76abe61422502c4889b04ce8b6a59f18ac04f4`.
- #154 / PR #155 — tagged/manual package parity through the exact reusable Windows candidate workflow; manifest schema 2; merged `8d8ae2c5f35e8a1d774c5a9480f582e432e5dc03`.
- #159 / PR #160 — durable pushed-tag GitHub Release publication from already-verified same-run bytes; no rebuild/repackage/clobber path.
- #162 / PR #163 — exact existing-candidate promotion implementation complete; actual RC.61 publication intentionally remains manual.
- #168 / PR #171 — immutable GitHub Actions supply-chain pins and obsolete privileged workflow removal.
- #173 / PR #174 — native Node 24 pinned Actions.
- PRs #177–#199 — reproducible SDK/SQL image, explicit Ubuntu/Windows runners, NuGet/package guards, non-persisting checkout, workflow privilege/write-surface guards, release mutation serialization, main-ref promotion preflight and step-scoped GitHub CLI token exposure.
- PRs #201–#219 — exact-two assets, source-run/artifact/provenance/ZIP safety, REST asset metadata/digest binding, TOCTOU-safe exact-ID verification, private/atomic verifier workspaces, immutable tag provenance, separate read-only durable-release verification and fail-fast toolchain capability preflight.
- PR #219 exact head `ca1e40acfac635650df32cd0bc60ed63df224380`: normal CI `31935989980` Green, Windows production-candidate `31935989954` Green, 919/919 tests.
- PR #245 — short RC.61 operator handoff synchronized with the hardened promotion + independent verification workflows; merged `75661cfc730f60667d1786a9bcd6ca9427ef2faa` after CI #1656 and Windows #146 Green.
- PR #247 — canonical P0.5 tracking delta reconciled through PR #219; merged `3f046143c4dd4e86059d9eb33c55cd2514073fc3` after CI #1661 Green.
- Issue #266 / PR #267 — lower-level read-only fail-closed RC.61 preflight COMPLETE; exact head `cdaff693810534db52975976309b726a0a8d409c` passed CI #1843, Real SQL #121 and Windows production-candidate #203 before squash merge `43aaa6071fd0c577c792d427ad490717f28acbac`; post-merge main CI #1844 Green. It remains available for diagnosis/audit beneath the current operator helper.
- Issue #270 / PR #271 — historical Step 0 operator handoff aligned with exact READY/no-mutation/no-existing-tag/no-existing-release requirements; squash-merged `479f9b557948b56fc5ec5692efb67fd6f1f4a921` after CI #1854 and Windows production-candidate #205 Green; post-merge main CI #1855 Green. Documentation/handoff only.
- Issue #338 / PR #339 — explicit acknowledged RC.61 promotion operator helper COMPLETE. `Invoke-Rc61DurablePromotion.ps1` previews without mutation, requires explicit acknowledgement before dispatch, captures/binds one exact promotion run, monitors only that run, treats ambiguity/failure as **do not redispatch**, and never auto-dispatches the independent verifier. PR #339 squash-merged as `f129e63b8ae9e83dda4f89d49e40892f4f36af56`.
- Issue #340 / PR #341 — canonical RC.61 promotion handoffs reconciled to the helper sequence; PR #341 squash-merged as `dfabec7f8cde7953a3f9c1fb5142b56774949537` after exact-head CI #2989 / `32096484890` and Windows production-candidate #560 / `32096484902` Green.
- Issue #342 / PR #343 — canonical production acceptance guide reconciled to the same helper -> separate verifier -> explicit run-ID readiness sequence; exact head `0a1f90b4c1f850426a5a3b0d491eb2f9d1f28905` passed CI #2996 / `32097392971` with 1287/1287 and Windows production-candidate #562 / `32097392991` Green end-to-end before squash merge `3cd711b608e4ceaf8872eb22a25541bbbfe2729a`.

Later CI-generated candidates are repository verification evidence only. They do **not** supersede RC.61 unless #116 explicitly selects another equivalently verified candidate.

### Selected repository-verified candidate — RC.61

- version `0.1.0-rc.61`;
- package `Monitor-0.1.0-rc.61-win-x64.zip`;
- source production-candidate run `31667721306`;
- Actions artifact ID `9168574442`;
- product SHA-256 `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- outer Actions artifact digest `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`;
- source head `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`;
- tested merge `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- implementation merge `8d8ae2c5f35e8a1d774c5a9480f582e432e5dc03`;
- normal CI `31667721350` Green, 770/770;
- Real SQL `31667721353` Green, 8/8;
- Windows production-candidate `31667721306` Green, 770/770;
- HTTPS health/authentication before and after process restart Green;
- clean SingleNode package validation Green.

Independent package inspection confirmed the product hash/checksum, 95 package files, all expected `_operations`, and release-manifest schema 2.

Fresh GitHub verification on 2026-08-18 still found source artifact `9168574442` present/unexpired with its exact locked digest and tag `v0.1.0-rc.61` absent. No durable publication is inferred from repository readiness or handoff merges.

### RC.61 durable retention gate — #162 PENDING EXPLICIT OPERATOR ACTION

Before production cutover, preserve the selected verified RC.61 as durable release assets without rebuilding or repackaging it. **#162 is a hard prerequisite: no #116 production mutation may begin while #162 is OPEN.**

The preferred operator contract is deliberately deterministic:

`Invoke-Rc61DurablePromotion.ps1 preview -> explicit -AcknowledgePromotion -> exact captured promotion run -> separately execute returned IndependentVerificationCommand -> Test-Rc61CutoverReadiness.ps1 with explicit run IDs`

**Step 0 — helper preview:** from a trusted authenticated operator checkout run:

```powershell
.\scripts\Invoke-Rc61DurablePromotion.ps1
```

Without acknowledgement require:

- `Status = READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT`
- `WorkflowDispatchPerformed = False`
- `IndependentVerificationDispatched = False`
- `ProductionMutationPerformed = False`
- `MutatedGitHubState = False`

The lower-level `Test-Rc61DurablePromotionPreflight.ps1` remains available for diagnosis/audit and still must fail closed on existing durable state, artifact expiry, provenance/digest drift or ambiguous GitHub probing.

**Step 1 — explicit acknowledged promotion:** only after reviewing the clean preview run:

```powershell
.\scripts\Invoke-Rc61DurablePromotion.ps1 -AcknowledgePromotion
```

The helper reruns the locked preflight, dispatches only `.github/workflows/promote-existing-candidate.yml` from `main`, captures and binds the exact promotion run, and monitors only that run. Ambiguous discovery, timeout or failure is a **do not redispatch** condition. The helper never dispatches the independent verifier automatically.

After the exact promotion run is Green require:

- `Status = PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED`
- retained `PromotionRunId` / `PromotionRunUrl`
- returned `IndependentVerificationCommand`

**Step 2 — separate independent verification:** separately execute the exact `IndependentVerificationCommand` returned by the helper. It dispatches `.github/workflows/verify-durable-release.yml` from `main`. This workflow is read-only (`contents: read`) and is independent closure evidence; promotion's own post-publication checks are not a substitute.

**Step 3 — bind exact Green runs before #116 preparation:** after both exact runs are Green run:

```powershell
.\scripts\Test-Rc61CutoverReadiness.ps1 `
  -PromotionRunId <PROMOTION_RUN_ID> `
  -VerificationRunId <VERIFICATION_RUN_ID>
```

Require:

- `Status = READY_FOR_P0_5_PRE_CUTOVER_PREPARATION`
- `DurableReleasePrerequisiteSatisfied = True`
- `ExternalGatesPassed = 0`
- `ProductionMutationPerformed = False`
- `MutatedGitHubState = False`

This read-only readiness state is still **0/15** external gates and performs **no production mutation**. It cannot satisfy #116.

#162 remains OPEN until both runs are Green and all of the following are independently true:

- tag `v0.1.0-rc.61` resolves to tested merge `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- release contains exactly `Monitor-0.1.0-rc.61-win-x64.zip` and `Monitor-0.1.0-rc.61-win-x64.zip.sha256`;
- durable ZIP SHA-256 equals `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- companion checksum is canonical and matches the exact filename/hash;
- `Test-Rc61CutoverReadiness.ps1` binds the exact Green promotion and verifier runs successfully.

Durable publication is retention/recoverability only. It does **not** satisfy any real IIS gate.

### Deterministic external acceptance workflow — REPOSITORY COMPLETE

The packaged operator path is:

`validated RC.61 -> immutable fail-closed session at 0/15 -> IIS preflight -> PLAN ONLY deploy -> explicit Apply -> real operation evidence -> one explicit SHA-bound PASS at a time -> 15/15 -> explicit final operator acknowledgement -> prospective validator -> atomic final metadata commit -> authoritative validator -> closure summary -> human review`

The session initializer, gate recorder, finalizer and validator never manufacture production evidence. Repository CI proves their fail-closed mechanics only.

### P0.5 task state

| Task | Description | State |
|---|---|---|
| P0-041 | Freeze first production scope to SingleNode | COMPLETE — repository/CI |
| P0-042 | Validate secret-free production configuration and environment values | COMPLETE — repository/CI |
| P0-043 | Deploy IIS + trusted HTTPS using the production guide | **BLOCKED BY #162; then PENDING EXTERNAL** |
| P0-044 | Validate persistent Data Protection/protected credential behavior after IIS recycle/restart | CI process restart VERIFIED; **IIS recycle blocked by #162 then pending external** |
| P0-045 | Validate durable registration/audit/history/incident state after recycle/restart | **BLOCKED BY #162; then PENDING EXTERNAL** |
| P0-046 | Run `/health/live`, `/health/ready`, `/health` deployment smoke | CI HTTPS VERIFIED + tooling READY; **actual IIS endpoint blocked by #162 then pending external** |
| P0-047 | Validate monitored target remains read-only/least-privilege from deployed application identity | P0.4 prerequisite VERIFIED; **deployed IIS identity/target blocked by #162 then pending external** |
| P0-048 | Validate operational backup and rollback/recovery path | code/unit/tooling VERIFIED; **production rollback rehearsal blocked by #162 then pending external** |
| P0-049 | Versioned candidate/checksum + deterministic session/evidence/finalization/release workflow | **REPOSITORY IMPLEMENTATION COMPLETE; explicit operator helper #338/#339 + handoff reconciliation #340/#341 + production acceptance guide reconciliation #342/#343 COMPLETE; actual RC.61 acknowledged promotion + separate read-only verification + explicit run-ID readiness PENDING OPERATOR #162** |
| P0-050 | Final production acceptance; close #111 only after real gates are Green | **BLOCKED BY #162 then PENDING EXTERNAL #116** |

### Immediate execution order

1. Preserve RC.61 identity and do not substitute another candidate unless #116 explicitly selects an equivalently verified replacement.
2. Complete #162 in order: preview `Invoke-Rc61DurablePromotion.ps1`; after exact `READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT`, explicitly run `Invoke-Rc61DurablePromotion.ps1 -AcknowledgePromotion`; retain the one exact Green promotion run; separately execute the returned `IndependentVerificationCommand`; retain the exact Green verification run; then run `Test-Rc61CutoverReadiness.ps1` with both run IDs and require `ExternalGatesPassed = 0`. Ambiguity/failure means **do not redispatch**. **Do not begin #116 production mutation before #162 is complete.**
3. After #162 completes, validate the real pre-cutover operational backup and record the approved backup ID.
4. Create one fresh immutable acceptance session; verify `session-manifest.sha256`, `PreparedFailClosed` and **0/15** before production mutation.
5. Configure/verify intended IIS application-pool identity and trusted machine certificate/HTTPS binding.
6. Run packaged IIS prerequisite preflight and retain bounded proof in the same session.
7. Review deployment in PLAN ONLY mode with the actual host/config/backup ID, then use explicit `-Apply`.
8. Authenticate over actual trusted HTTPS; register/Test/Refresh the approved least-privilege SQL target and prove no target DML/write privilege is required.
9. Recycle IIS and prove health/authentication, same registration, protected credential and registration/audit/history/incident state durability.
10. Execute rollback/recovery rehearsal and repeat health/auth/read checks.
11. Record each exact real gate with `Set-ProductionAcceptanceGate.ps1` using same-session relative SHA-bound non-secret evidence.
12. After real 15/15, run `Complete-ProductionAcceptance.ps1` with approved operator identity and explicit final acknowledgement; retain and human-review the closure summary.
13. Close #116 only after the actual external evidence is valid; close umbrella #111 only after #116.

### External acceptance checklist — no item is implied by CI or publication

P0.5 remains OPEN until #162 is complete and the actual intended Windows/IIS environment proves all required facts, including trusted HTTPS, intended app-pool identity, actual application authentication, real least-privilege SQL Test/Refresh, IIS recycle durability, backup/rollback rehearsal, 15 SHA-bound real PASS records, final operator acknowledgement and independent human review.

A Green candidate pipeline, successful durable publication, successful helper preview, successful promotion, successful independent release verification, successful read-only run-ID readiness, synthetic 15/15 pack, or successful finalizer test cannot claim the GitHub-hosted runner is the intended IIS host and cannot close #116/#111.

---

## Deferred until after P0

Historical BATCH-300 through BATCH-700 capabilities remain available, but additional feature breadth must not displace the remaining P0.5 retention/external acceptance work. MultiNode production activation remains deferred until after the first stable SingleNode production release.

## Definition of Done for Issue #111

Issue #111 is complete only when P0-001..050 are reconciled, all five child release gates are accepted in order, the real SQL journey has passed, #162 explicit acknowledged promotion/separate verification/run-ID readiness and durable asset checks are complete before #116 production mutation, the selected SingleNode release has actual trusted-HTTPS IIS/recycle/least-privilege/backup/rollback evidence, the real exact 15-gate pack is explicitly operator-finalized and validates, all secret and zero-SQL-GET guardrails remain intact, and final required CI/acceptance gates are Green.
