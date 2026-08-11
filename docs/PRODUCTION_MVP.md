# P0 — Real SQL Production MVP

This is the execution ledger for Issue #111. It is referenced by the canonical `docs/IMPLEMENTATION_PLAN.md` and is the highest-priority delivery program until the first SingleNode production release is accepted.

## Product outcome

Deliver one trustworthy vertical slice:

`Login -> Add SQL Server -> Test -> Save -> Collect -> View Server Details -> Refresh -> Restart Monitor -> View trustworthy persisted target again`

A production-visible value must be backed by collected evidence. If a dimension is not collected, unavailable, stale, or permission-limited, the UI must say so explicitly. A default numeric zero must never masquerade as observed production data.

## Execution rules

1. Work in the release-gate order below. A later gate may prepare non-conflicting work, but it cannot be declared complete before its dependency gate is complete.
2. Issue #111 is the umbrella. Child issues #112..#116 are the release gates.
3. Until P0.5 is accepted, unrelated feature expansion is secondary to production-slice blockers.
4. Preserve the existing zero-monitored-SQL GET boundary. Browser navigation reads cached/control-plane state only.
5. No autonomous remediation or AI SQL execution.
6. No plaintext credentials, full connection strings, current secret references, raw provider exceptions, or arbitrary SQL text in UI/audit/telemetry/export/diagnostics.
7. First production activation is SingleNode. MultiNode remains outside the P0 release.
8. Every gate requires Release build with `--warnaserror`, applicable tests, affected-screen review, canonical docs synchronization, and PR CI before merge.

## Priority chain

| Priority | Release | Issue | Outcome | State |
|---|---|---|---|---|
| 1 | P0.1 | #112 | Real SQL registration is production-safe and restart durable | ACTIVE / NEXT |
| 2 | P0.2 | #113 | First snapshot is mapped truthfully into production read models | BLOCKED BY P0.1 |
| 3 | P0.3 | #114 | Server Details v0.1 is the trusted operator source of truth | BLOCKED BY P0.2 |
| 4 | P0.4 | #115 | Full journey passes against a real SQL Server | BLOCKED BY P0.3 |
| 5 | P0.5 | #116 | First IIS/HTTPS SingleNode production release is accepted | BLOCKED BY P0.4 |

---

## P0.1 — Real SQL Registration

**Issue:** #112  
**Release gate:** registration can be used on a real target without secret leakage or false readiness.  
**Audit finding:** `ConnectionLabController.Register` currently persists the enabled registration before Test Connection finishes. The current regression test also expects a failed Test Connection to leave a registration behind. P0.1 changes this to candidate-test-before-durable-commit semantics unless a future explicit “save unreachable target” command is designed separately.

| Task | Description | State |
|---|---|---|
| P0-001 | Reconcile the current Connection Lab registration path against production acceptance criteria | AUDIT COMPLETE — BLOCKER RECORDED IN #112 |
| P0-002 | Test the candidate before durable registration commit; failed/cancelled Test must not silently persist a normal enabled target | READY / NEXT |
| P0-003 | Compensate/delete a candidate Monitor-owned credential when initial registration test fails or is cancelled; keep password write-only | PLANNED |
| P0-004 | Verify Integrated Security registration path and safe connection-string construction | PLANNED |
| P0-005 | Verify successful registration persists across Monitor process restart | PLANNED |
| P0-006 | Make failed Test Connection state explicit without publishing a live snapshot | PLANNED |
| P0-007 | Verify duplicate/repeated registration semantics are deterministic and do not leak credentials | PLANNED |
| P0-008 | Verify existing protected credential reconnect preserves registration identity/history | PLANNED |
| P0-009 | Desktop/mobile Connection Lab acceptance for success/failure/recovery states | PLANNED |
| P0-010 | P0.1 Release build/tests/docs/PR CI gate | PLANNED |

### P0.1 exit criteria

A DBA can add a real target, safely test it, save it, restart Monitor and still see the durable registration. A failed initial test does not silently commit a normal enabled target or orphan a Monitor-owned candidate secret. No credential value/reference is rendered.

---

## P0.2 — First Real Snapshot & Truthful Mapping

**Issue:** #113  
**Dependency:** P0.1 complete.  
**Known blocker:** the current read projection assigns `CpuPercent = 0` and does not project the collected SQL Agent snapshot into `ServerCard`, while Server Details contains UI that can visually interpret those fields as observed values.

| Task | Description | State |
|---|---|---|
| P0-011 | Define the v0.1 production snapshot contract from `ServerHealthSnapshot` | PLANNED |
| P0-012 | Verify real identity/version/edition/instance/uptime projection | PLANNED |
| P0-013 | Verify real database total/online/problem-state projection | PLANNED |
| P0-014 | Verify memory evidence mapping and unavailable semantics | PLANNED |
| P0-015 | Reconcile SQL Agent mapping: total/enabled/failed evidence without inventing a “healthy” count | PLANNED |
| P0-016 | Reconcile CPU: collect from a defined real source or render explicitly `Not collected`; never default to observed 0% | PLANNED |
| P0-017 | Verify backup/storage/blocking/runtime-pressure mapping from the same snapshot | PLANNED |
| P0-018 | Add explicit stale/unavailable/permission-limited evidence semantics | PLANNED |
| P0-019 | Add truthful-projection regression tests, including no fake numeric zero cases | PLANNED |
| P0-020 | P0.2 Release build/tests/docs/PR CI gate | PLANNED |

### P0.2 exit criteria

Every production-visible health dimension is either supported by actual snapshot evidence or explicitly marked unavailable/not collected. No placeholder number can be confused with a measurement.

---

## P0.3 — Server Details v0.1 Source of Truth

**Issue:** #114  
**Dependency:** P0.2 complete.

| Task | Description | State |
|---|---|---|
| P0-021 | Make connection/collection/freshness status first-class on Server Details | PLANNED |
| P0-022 | Show instance/version/edition/uptime from cached real evidence | PLANNED |
| P0-023 | Show database availability and problem-state evidence | PLANNED |
| P0-024 | Show memory evidence with explicit unavailable state | PLANNED |
| P0-025 | Show backup evidence and last-full-backup context | PLANNED |
| P0-026 | Show SQL Agent total/enabled/failed evidence with unambiguous labels | PLANNED |
| P0-027 | Show storage, blocking and runtime-pressure evidence | PLANNED |
| P0-028 | Make collected timestamp/snapshot age/stale state visible and non-misleading | PLANNED |
| P0-029 | Remove health-score/severity dependencies on invented or absent metrics; perform desktop/mobile visual acceptance | PLANNED |
| P0-030 | P0.3 zero-SQL-GET + Release build/tests/docs/PR CI gate | PLANNED |

### P0.3 exit criteria

Server Details is sufficient for a first DBA production check without requiring hidden routes. The page contains no value that claims to be live/observed when the collector did not produce it.

---

## P0.4 — Real SQL End-to-End Acceptance

**Issue:** #115  
**Dependency:** P0.3 complete.  
**Important:** deterministic/fake-based CI remains required, but it is not sufficient for this release gate.

| Task | Description | State |
|---|---|---|
| P0-031 | Prepare a production-like SQL Server acceptance target and least-privilege monitor login | PLANNED |
| P0-032 | Execute Add -> Test -> Register -> Collect -> View against the real target | PLANNED |
| P0-033 | Execute manual Refresh and verify one bounded collection/publication | PLANNED |
| P0-034 | Restart Monitor and prove registration/credential resolution/read path recovery | PLANNED |
| P0-035 | Verify bad-password authentication failure classification and safe operator message | PLANNED |
| P0-036 | Verify network-unavailable and timeout classifications | PLANNED |
| P0-037 | Verify TLS/certificate rejection classification | PLANNED |
| P0-038 | Verify missing server-state/msdb/SQL Agent permission behavior and least-privilege script completeness | PLANNED |
| P0-039 | Record redaction canaries and real-server acceptance evidence/runbook result | PLANNED |
| P0-040 | P0.4 full Release build/test + real-server acceptance gate | PLANNED |

### P0.4 exit criteria

The full user journey is proven against a real SQL Server under success and controlled failure conditions. The exact least-privilege SQL permissions needed by the collector are known and documented.

---

## P0.5 — First Production SingleNode Release

**Issue:** #116  
**Dependency:** P0.4 complete.

| Task | Description | State |
|---|---|---|
| P0-041 | Freeze first production scope to SingleNode | PLANNED |
| P0-042 | Validate secret-free production configuration and environment values | PLANNED |
| P0-043 | Deploy IIS + HTTPS using the existing production guide | PLANNED |
| P0-044 | Validate persistent Data Protection/protected credential behavior after application recycle/restart | PLANNED |
| P0-045 | Validate durable registration/audit/history/incident state after recycle/restart | PLANNED |
| P0-046 | Run `/health/live`, `/health/ready`, `/health` deployment smoke | PLANNED |
| P0-047 | Validate monitored target remains read-only/least-privilege from the application identity | PLANNED |
| P0-048 | Validate operational backup and rollback/recovery path | PLANNED |
| P0-049 | Produce versioned production candidate artifact/checksum and record acceptance evidence | PLANNED |
| P0-050 | P0.5 final production acceptance; close #111 only after all gates are Green | PLANNED |

### P0.5 exit criteria

A versioned SingleNode release can be deployed on IIS/HTTPS, survive process restart, reconnect safely to the registered SQL target, serve trustworthy cached data, pass health smoke checks and follow a tested rollback path.

---

## Deferred until after P0

The existing BATCH-300/BATCH-400 intelligence remains valuable, but additional diagnostics must not be promoted as production-visible truth until each one has a defined live evidence source, snapshot/cache projection, read-model/UI mapping and real-target acceptance. MultiNode production activation is also deferred until after the first SingleNode release is stable.

## Definition of Done for Issue #111

Issue #111 is complete only when P0-001..050 are reconciled, all five child release gates are accepted in order, a real SQL Server journey has passed, the SingleNode production candidate is deployable/recoverable, all secret/zero-SQL-GET guardrails remain intact, and final Release build + complete tests are Green.
