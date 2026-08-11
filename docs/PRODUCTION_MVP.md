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
9. CI simulation/synthetic evidence is not a substitute for external production acceptance. IIS binding, trusted HTTPS, application-pool identity, real recycle behavior, least-privilege SQL, backup and rollback require actual environment evidence.

## Priority chain

| Priority | Release | Issue | Outcome | State |
|---|---|---|---|---|
| 1 | P0.1 | #112 | Real SQL registration is production-safe and restart durable | COMPLETE — PR #119 / final CI `31476747212` |
| 2 | P0.2 | #113 | First snapshot is mapped truthfully into production read models | COMPLETE — PR #121 / final CI `31478470867` |
| 3 | P0.3 | #114 | Server Details v0.1 is the trusted operator source of truth | COMPLETE — PR #122 / final CI `31479311552` |
| 4 | P0.4 | #115 | Full journey passes against a real SQL Server | COMPLETE — PR #124 / normal `31481874425` / Real SQL `31481874501` |
| 5 | P0.5 | #116 | First IIS/HTTPS SingleNode production release is accepted | **ACTIVE — core repository workflow complete; #150 session hardening in progress; external IIS acceptance pending** |

---

## P0.1 — Real SQL Registration

**Issue:** #112 — CLOSED / COMPLETED  
**Implementation PR:** #119 — squash-merged to `main` as `57ab5cae6b5bdd3a04adb5069008aae80a1f84e0`  
**Final code+docs CI:** `31476747212` — Release build 0 warnings / 0 errors; 501/501 tests passed.  
**Release gate:** COMPLETE.

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

### P0.1 exit evidence

Failed/cancelled initial tests do not commit a normal enabled target or orphan a Monitor-owned candidate secret. External secret references are not mutated. Successful registration and protected credentials survive restart boundaries without rendering credential values/references.

---

## P0.2 — First Real Snapshot & Truthful Mapping

**Issue:** #113 — CLOSED / COMPLETED  
**Implementation PR:** #121 — squash-merged to `main` as `a294c6530d60f17e7c60e3a1ac070ce562af7b18`  
**Final code+docs CI:** `31478470867` — Release build 0 warnings / 0 errors; 505/505 tests passed.  
**Release gate:** COMPLETE.

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

### P0.2 exit evidence

Every production-visible health dimension is supported by actual cached snapshot evidence or explicitly marked unavailable/not collected. Missing evidence is not aggregated as numeric zero.

---

## P0.3 — Server Details v0.1 Source of Truth

**Issue:** #114 — CLOSED / COMPLETED  
**Implementation PR:** #122 — squash-merged to `main` as `245bb0770d7ec6e7a334f7763d3560cef80324fe`  
**Final code+docs CI:** `31479311552` — Release build 0 warnings / 0 errors; 507/507 tests passed.  
**Release gate:** COMPLETE.

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

### P0.3 exit evidence

Server Details is the first DBA evidence surface: observed cached facts or explicit absence, no synthetic numeric Health Score, and normal GET navigation remains cache-only.

---

## P0.4 — Real SQL End-to-End Acceptance

**Issue:** #115 — CLOSED / COMPLETED  
**Foundation PR:** #123 — squash-merged as `83540afe15f5d52ee528ff7de46430682444594d`  
**Full-journey PR:** #124 — squash-merged as `f4c08292734c293a6d0b865cc2a005b8c42b02a6`  
**Final normal CI:** `31481874425` — Release build 0 warnings / 0 errors; 518/518 passed.  
**Final Real SQL:** `31481874501` — Release build 0 warnings / 0 errors; 8/8 RealSql passed.  
**Durable evidence:** `docs/REAL_SQL_ACCEPTANCE.md`  
**Release gate:** COMPLETE.

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

### P0.4 exit evidence

The complete `Add -> Test -> Register -> Collect -> View -> Refresh -> Restart -> View` path passed against SQL Server 2022 under success and controlled authentication/network/timeout/TLS/server-permission/msdb-permission failures. The exact least-privilege SQL contract was proven with a non-sysadmin login. No acceptance secret was committed or persisted in plaintext.

---

## P0.5 — First Production SingleNode Release

**Issue:** #116 — OPEN / ACTIVE  
**Dependency:** P0.4 COMPLETE  
**Live selected candidate/evidence:** #116  
**Repository cutover/evidence/finalization workflow:** COMPLETE through #147 / PR #148  
**Active repository hardening:** #150 / immutable production acceptance session initializer  
**Release gate:** CORE REPOSITORY WORKFLOW COMPLETE / SESSION HARDENING ACTIVE / EXTERNAL IIS ACCEPTANCE PENDING.

### Stable repository milestones

- PR #127 merged `9bdd96940454f2586c0e81ff0c25a524d7f1281c`: HTTPS acceptance harness + runbook.
- PR #126 merged `d512ee156f07db566898a817f3c76dd3f46c1091`: Windows production-candidate pipeline.
- PR #129 merged `7cb47945b47aab6558f7132dcfa818b9f02d2b20`: safe IIS preflight/deploy automation, stable external `App_Data`, versioned releases and automatic immediate cutover rollback.
- BATCH-500 and BATCH-600: production acceptance/recovery safety plus live readiness/evidence orchestration; complete.
- PR #142 / #141 COMPLETE: exact 15-gate fail-closed evidence pack and `Test-ProductionAcceptanceEvidence.ps1` closure validator.
- PR #145 / #144 COMPLETE: `Set-ProductionAcceptanceGate.ps1` records one real gate at a time only after explicit `-AcknowledgePass`; no manual gate hash/timestamp editing.
- PR #148 / #147 COMPLETE: `Complete-ProductionAcceptance.ps1` removes manual final acceptance metadata editing; merged `e15a9654fbe744e426c95d5965a5faba60868e14`.
- Issue #150 / PR #151 **IN PROGRESS**: immutable candidate-bound session initialization before any real cutover mutation; this is additive P0-049 hardening and cannot create external acceptance.

### Selected repository-verified candidate — RC.43

- package `Monitor-0.1.0-rc.43-win-x64.zip`;
- product SHA-256 `95d6d545cfa53fb514814fb22c82cfafc2c14cf28c1e07c15177852b677234aa`;
- Actions artifact `9119560465`;
- source head `d05bea3ea1372a6566eb9c237bb06e84de681014`;
- exact tested merge ref `0445ac9c8bbeafb075a506a06231dd87c4b1b27b`;
- normal CI `31537914600` Green;
- Real SQL `31537914667` Green, 8/8;
- Windows production-candidate `31537914596` Green, Release 0 warnings/errors, 761/761;
- recorder + finalizer runtime Green;
- synthetic prospective and authoritative exact 15/15 validation plus independent validator recheck Green;
- negative premature/no-ack/path/operator/re-finalization/false-gate/tampered-hash/secret-bearing cases rejected;
- HTTPS health/authentication before and after process restart Green;
- SingleNode clean package validation Green.

#116 remains the live source of truth if a later equivalently verified candidate supersedes RC.43.

### Deterministic evidence/finalization workflow — COMPLETE

The packaged operator workflow is:

`candidate/checksum -> fail-closed 15-gate pack -> perform real operation -> record one explicit PASS with SHA-bound evidence -> repeat 15 gates -> explicit final operator acknowledgement -> prospective validator -> atomic final metadata commit -> authoritative validator -> closure summary -> human review`

`Complete-ProductionAcceptance.ps1`:

- requires explicit `-AcknowledgeFinalAcceptance` and a bounded non-secret `AcceptedBy` identity;
- never changes a gate from FAIL to PASS and never infers evidence;
- restricts closure summary output to a relative path under the evidence-pack root;
- validates a prospective finalized copy against all exact 15 SHA-bound gates before touching the authoritative pack;
- re-hashes the authoritative pack to detect concurrent mutation;
- atomically commits only `acceptedBy` / `acceptedAtUtc`;
- performs authoritative second validation and closure-summary creation;
- restores the original unaccepted pack if final validation unexpectedly fails;
- refuses existing acceptance metadata, existing summary, unsafe paths and re-finalization;
- has no IIS deployment/recycle, SQL execution, GitHub API call or issue-closing authority.

### Immutable acceptance session hardening — #150 ACTIVE

`New-ProductionAcceptanceSession.ps1` makes pre-cutover setup candidate-bound and fail-closed:

- fresh absolute Windows session root only; reuse, drive/share roots and traversal-bearing roots are rejected;
- exact `Monitor-<version>-win-x64.zip` + `.sha256` names, checksum content, actual SHA-256 and readable non-empty ZIP are verified before session creation;
- source/tested-merge SHA and environment metadata are validated through the existing production evidence contract and secret-like/provider-error/connection-string/SQL text is rejected;
- session construction happens under a temporary sibling directory and moves atomically into the final fresh root;
- exact artifact/checksum bytes are copied under `candidate/` and rehashed after copy;
- the canonical pack generator creates exactly 15 gates and the initializer verifies all remain false with no `acceptedBy` / `acceptedAtUtc`;
- `session-manifest.json`, `session-manifest.sha256`, `evidence/proof/` and deterministic `OPERATOR-NEXT-STEPS.txt` are created;
- session creation reports 0/15 and `ProductionAccepted=false` and has no IIS/SQL/gate-PASS/finalizer/GitHub side effects.

| Task | Description | State |
|---|---|---|
| P0-041 | Freeze first production scope to SingleNode | COMPLETE — repository/CI |
| P0-042 | Validate secret-free production configuration and environment values | COMPLETE — repository/CI |
| P0-043 | Deploy IIS + trusted HTTPS using the production guide | **PENDING EXTERNAL** |
| P0-044 | Validate persistent Data Protection/protected credential behavior after IIS recycle/restart | CI process restart VERIFIED; **IIS recycle pending external** |
| P0-045 | Validate durable registration/audit/history/incident state after recycle/restart | **PENDING EXTERNAL** |
| P0-046 | Run `/health/live`, `/health/ready`, `/health` deployment smoke | CI HTTPS VERIFIED + tooling READY; **actual IIS endpoint pending external** |
| P0-047 | Validate monitored target remains read-only/least-privilege from deployed application identity | P0.4 prerequisite VERIFIED; **deployed IIS identity/target pending external** |
| P0-048 | Validate operational backup and rollback/recovery path | code/unit/tooling VERIFIED; **production rollback rehearsal pending external** |
| P0-049 | Versioned candidate/checksum + deterministic external evidence/finalization workflow | **COMPLETE — repository/CI; RC.43 verified; #150 additional session hardening ACTIVE** |
| P0-050 | Final production acceptance; close #111 only after real gates are Green | **PENDING EXTERNAL** |

### P0.5 external acceptance checklist

P0.5 stays OPEN until the actual intended Windows/IIS environment produces real evidence for all of the following:

1. Preserve the selected candidate filename, source/tested merge SHA and product SHA-256 from #116 and validate the pre-cutover operational backup.
2. Create one fresh immutable acceptance session with `New-ProductionAcceptanceSession.ps1`; verify `session-manifest.sha256`, `PreparedFailClosed` and 0/15 before any production mutation.
3. Configure/verify the intended application-pool identity and trusted machine certificate/HTTPS binding.
4. Run packaged `Test-IisProductionPrerequisites.ps1` and retain bounded proof in the same session.
5. Review packaged `Deploy-ProductionSingleNode.ps1` in PLAN ONLY mode with the real host/config/validated backup ID, then use explicit `-Apply`.
6. Authenticate through the actual trusted HTTPS endpoint and run bounded health acceptance using the session-bound candidate/checksum.
7. Register/Test/Refresh the approved least-privilege production SQL target and prove no monitored-target DML/write privilege is required.
8. Recycle IIS and prove health/auth, registration, protected credential and operational-state durability.
9. Execute the rollback rehearsal and repeat health/auth/read checks.
10. Record each of the exact 15 gates with `Set-ProductionAcceptanceGate.ps1` and relative SHA-bound non-secret evidence from the same session.
11. After real 15/15, use `Complete-ProductionAcceptance.ps1` with the approved operator identity and explicit final acknowledgement; retain the validated closure summary.
12. Human-review the real closure evidence. Only then close #116; umbrella #111 closes only after #116.

### Candidate/CI evidence is not production acceptance

A Green Windows candidate, Real SQL CI, synthetic session/15-gate pack or successful finalizer test only proves tooling behavior. It does not claim a GitHub-hosted runner is the intended IIS host, a loopback certificate is the trusted production certificate, or synthetic evidence represents actual deployment/recycle/rollback operations. #116 remains OPEN until the real external evidence is complete.

---

## Deferred until after P0

The existing BATCH-300/BATCH-400/BATCH-500/BATCH-600 capabilities remain valuable, but additional feature breadth must not displace the remaining P0.5 external acceptance. MultiNode production activation stays deferred until after the first stable SingleNode production release.

## Definition of Done for Issue #111

Issue #111 is complete only when P0-001..050 are reconciled, all five child release gates are accepted in order, the real SQL journey has passed, the selected SingleNode candidate has actual trusted-HTTPS IIS/recycle/least-privilege/backup/rollback evidence, the real exact 15-gate pack is explicitly operator-finalized and validates, all secret and zero-SQL-GET guardrails remain intact, and final required CI/acceptance gates are Green.
