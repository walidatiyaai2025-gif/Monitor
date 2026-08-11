# P0 — Real SQL Production MVP

This is the execution ledger for Issue #111 and the highest-priority delivery program until the first SingleNode production release is accepted.

## Product outcome

Deliver one trustworthy vertical slice:

`Login -> Add SQL Server -> Test -> Save -> Collect -> View Server Details -> Refresh -> Restart Monitor -> View trustworthy persisted target again`

Production-visible values must be backed by collected evidence. If a dimension is not collected, unavailable, stale, or permission-limited, the UI must say so explicitly. A default numeric zero must never masquerade as observed production data.

## Execution rules

1. Work in release-gate order. A later gate may prepare non-conflicting work, but cannot be declared complete before its dependencies.
2. Issue #111 is the umbrella. Child issues #112..#116 are the release gates.
3. Until P0.5 is accepted, unrelated feature expansion is secondary to production-slice blockers.
4. Preserve the zero-monitored-SQL GET boundary. Browser navigation reads cached/control-plane state only.
5. No autonomous remediation or AI SQL execution.
6. No plaintext credentials, full connection strings, current secret references, raw provider exceptions, or arbitrary SQL text in UI/audit/telemetry/export/diagnostics.
7. First production activation is SingleNode. MultiNode remains outside P0.
8. Every gate requires Release build with warnings-as-errors, applicable tests, affected-screen review, canonical docs synchronization, and PR CI before merge.
9. CI simulation is not a substitute for external production acceptance. IIS binding, trusted HTTPS, application-pool identity, real recycle behavior and rollback rehearsal require environment evidence.

## Priority chain

| Priority | Release | Issue | Outcome | State |
|---|---|---|---|---|
| 1 | P0.1 | #112 | Real SQL registration is production-safe and restart durable | COMPLETE — PR #119 / final CI `31476747212` |
| 2 | P0.2 | #113 | First snapshot is mapped truthfully into production read models | COMPLETE — PR #121 / final CI `31478470867` |
| 3 | P0.3 | #114 | Server Details v0.1 is the trusted operator source of truth | COMPLETE — PR #122 / final CI `31479311552` |
| 4 | P0.4 | #115 | Full journey passes against a real SQL Server | COMPLETE — PR #124 merged `f4c08292734c293a6d0b865cc2a005b8c42b02a6`; normal `31481874425`; Real SQL `31481874501` |
| 5 | P0.5 | #116 | First IIS/HTTPS SingleNode production release is accepted | ACTIVE — repository candidate merged; external IIS acceptance pending |

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
**Dependency:** P0.4 COMPLETE.  
**Acceptance tooling:** PR #127 — squash-merged as `9bdd96940454f2586c0e81ff0c25a524d7f1281c`; CI `31485290730`, 522/522.  
**Production-candidate PR:** #126 — squash-merged as `d512ee156f07db566898a817f3c76dd3f46c1091`.  
**Tested source head:** `da69636f2ba3525426c43aaca17d17617244d9d4`.  
**Tested merge ref including PR #127:** `f65ed48cea537b3ee7524f244a75a62fb442bd55`.  
**Final normal CI:** `31485712373` — Green.  
**Final Real SQL:** `31485712392` — Release 0 warnings / 0 errors; 8/8 RealSql passed.  
**Final Windows production-candidate:** `31485712370` — Green on Windows Server 2025; Release 0 warnings / 0 errors; 531/531 tests passed.  
**Candidate package:** `Monitor-0.1.0-rc.20-win-x64.zip`  
**Candidate SHA-256:** `27743b8f3f162a43f8c1bb6b7b9d1977dc5d1c55e8a4664f0c84294b094150bc`  
**GitHub Actions artifact:** ID `9099041392`, uploaded artifact size 4,770,438 bytes.  
**Release gate:** REPOSITORY PREPARATION COMPLETE / EXTERNAL IIS ACCEPTANCE PENDING.

The repository now contains both the deployable candidate and fail-closed production acceptance tooling:

- Release build with warnings-as-errors and the full Windows test suite.
- RID-specific `win-x64` restore and publish.
- Secret-free SingleNode package validation; Development credentials and persisted `App_Data` are excluded from the clean package.
- Production process startup on HTTPS with an ephemeral loopback certificate and masked runtime-only administrator credentials.
- `/health/live`, `/health/ready`, and `/health` return the expected states over HTTPS before and after process restart.
- A real Administrator login is exercised with antiforgery protection and authenticated `/servers/connections` access before and after restart.
- The local Data Protection key-ring directory is verified after restart.
- Runtime Production config/state is deleted before packaging; final package input is revalidated.
- ZIP + SHA-256 + tested source/merge provenance are recorded.
- `scripts/Accept-ProductionSingleNode.ps1` validates the selected artifact checksum and actual HTTPS endpoint and writes machine-readable evidence.
- `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md` defines the IIS cutover, recycle, least-privilege, backup and rollback evidence requirements.

| Task | Description | State |
|---|---|---|
| P0-041 | Freeze first production scope to SingleNode | COMPLETE — repository/CI |
| P0-042 | Validate secret-free production configuration and environment values | COMPLETE — repository/CI |
| P0-043 | Deploy IIS + HTTPS using the production guide | **PENDING EXTERNAL** — actual IIS binding/trusted certificate required |
| P0-044 | Validate persistent Data Protection/protected credential behavior after application recycle/restart | CI process restart VERIFIED; **IIS recycle pending external** |
| P0-045 | Validate durable registration/audit/history/incident state after recycle/restart | **PENDING EXTERNAL** |
| P0-046 | Run `/health/live`, `/health/ready`, `/health` deployment smoke | CI HTTPS VERIFIED + acceptance script READY; **actual IIS endpoint pending external** |
| P0-047 | Validate monitored target remains read-only/least-privilege from the application identity | P0.4 prerequisite VERIFIED; **deployed IIS identity/target pending external** |
| P0-048 | Validate operational backup and rollback/recovery path | code/unit/tooling VERIFIED; **production rollback rehearsal pending external** |
| P0-049 | Produce versioned production candidate artifact/checksum and record acceptance evidence | COMPLETE — RC.20 ZIP/SHA/artifact/provenance recorded |
| P0-050 | P0.5 final production acceptance; close #111 only after all gates are Green | **PENDING EXTERNAL** |

### P0.5 external acceptance checklist

P0.5 remains open until an actual SingleNode Windows/IIS environment proves all of the following using RC.20 or a later final-head-equivalent artifact:

1. Install/activate the .NET 8 IIS hosting prerequisites and deploy the selected artifact under the intended application-pool identity.
2. Bind the production hostname to a trusted HTTPS certificate; HTTP handling follows the approved deployment policy.
3. Supply production administrator credential material through approved environment/secret configuration, never checked-in JSON.
4. Run `scripts/Accept-ProductionSingleNode.ps1` against the actual HTTPS endpoint and retain its evidence JSON.
5. Register/test/collect a least-privilege SQL target from the deployed application identity and confirm no write/DML privilege is required.
6. Recycle the IIS application pool and prove protected credential resolution, target registration and trustworthy read paths recover.
7. Confirm audit/history/incident durable state survives the real recycle/restart boundary.
8. Create and validate an operational backup, perform the approved rollback/recovery rehearsal, and re-run health/auth/read checks.
9. Record deployed version, source SHA, package SHA-256, host/environment/recycle evidence and rollback result in `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md` or an approved environment evidence record.
10. Only then mark the remaining P0.5 external tasks complete, close #116 and finally close umbrella #111.

### P0.5 candidate evidence is not production acceptance

The Green Windows workflow and merged RC.20 are release-candidate evidence. They intentionally do not claim that GitHub-hosted Kestrel HTTPS is an IIS deployment, that a self-signed loopback certificate is a production certificate, or that a process restart in CI is identical to the final application-pool recycle. #116 remains open until the external operator acceptance is completed.

---

## Deferred until after P0

The existing BATCH-300/BATCH-400 intelligence remains valuable, but additional diagnostics must not be promoted as production-visible truth until each one has a defined live evidence source, snapshot/cache projection, read-model/UI mapping and real-target acceptance. MultiNode production activation is deferred until after the first stable SingleNode production release.

## Definition of Done for Issue #111

Issue #111 is complete only when P0-001..050 are reconciled, all five child release gates are accepted in order, the real SQL Server journey has passed, the SingleNode candidate has passed actual IIS/HTTPS deployment/recycle/least-privilege/rollback acceptance, all secret and zero-SQL-GET guardrails remain intact, and the final Release build plus required acceptance gates are Green.
