# Implementation Plan

This is the canonical current execution plan. The pre-#425 append-only plan is preserved byte-for-byte at [`docs/archive/IMPLEMENTATION_PLAN_PRE_425_2026-08-18.md`](archive/IMPLEMENTATION_PLAN_PRE_425_2026-08-18.md). Historical batch detail remains in the batch ledgers and `docs/work/` closure records.

## 1. Current programming closure — #425 / PR #426

**Goal:** make SharedState logical document identity independent of SQL Server database-collation equality without breaking the accepted key alphabet or distributed-lease resource contract.

**Implementation contract:**
1. Preserve mixed-case key support; do not normalize/lowercase callers.
2. Under the existing atomic execution transaction/lock from #423, capture the actual persisted `DocumentKey`.
3. Before Read/CAS execution, compare persisted/requested NVARCHAR bytes with `CONVERT(varbinary(256), ...)`; fail closed on any byte-different collation alias.
4. Return the actual persisted `DocumentKey` from Read and CAS results.
5. Re-check persisted/requested identity with `StringComparison.Ordinal` before commit so a future SQL/result-shape regression rolls back.
6. Preserve existing key/version/payload validation, CAS conflict semantics, cancellation, payload bounds and redacted `SharedStateStoreUnavailableException` behavior.
7. Prove the contract with unit/source tests and an isolated SQL Server 2022 database using explicit `Latin1_General_100_CI_AS`.
8. Reconcile `STATUS.md`, `FEATURE_CATALOG.md`, this plan and `docs/work/PROGRAMMING_CLOSURE_425.md` in the same PR.
9. Keep PR #426 Draft until the exact final docs-inclusive head is current with `main`, normal CI + Real SQL + Windows production-candidate + both protected-P0 guards are Green, and unresolved review threads are zero.

**Pre-canonical-doc evidence:** `a070c123183048dc418ecd19bcd6f98b1028d8f0` passed CI `32159604946`, Real SQL `32159604935`, Windows production-candidate `32159604932`, protected-P0 commits `32159604926` and metadata `32159605011`.

**Safety boundary:** no key normalization/migration, schema-v2, runtime DDL by Monitor, monitored-target query/permission expansion, secret disclosure, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external acceptance PASS or branch-protection mutation.

## 2. Completed SharedState execution hardening

- **#423 / PR #424 COMPLETE / MERGED:** schema fingerprint and document Read/CAS share one SERIALIZABLE transaction/held-lock boundary. Exact head `84c509dec867ff5e1b4e913b2b318b81fa927171`; CI `32158179450`, Real SQL `32158179479`, Windows production-candidate `32158179740`, protected-P0 commit `32158179498`, metadata `32158179675`; squash merge `3720c7aaa3e86ac3eb599685f39e98fa0a6ecb64`.
- **#421 / PR #422 COMPLETE / MERGED:** full schema-v1 readiness is a precondition for valid SharedState Read/CAS; squash merge `3b5e60fef2fa41c6e627468850cf3cf8532b0524`.
- Earlier SharedState v1 bounded JSON/CAS/readiness, shared repositories, distributed leases, shared DP key ring and HA safety contracts remain authoritative unless explicitly superseded by a reviewed closure.

## 3. Repository programming baseline

- PR #369 security/credential/operator-accountability hardening is COMPLETE/MERGED as `bbd8e5eb11ee8e4a7e34fbe91519e166fe087bc5`; exact head `e99678c32ae0af38f1d1529a63425325182d9266` passed all selected gates.
- PR #363 evidence/auth/refresh/readiness/operator-surface truthfulness hardening is COMPLETE/MERGED as `c8515f310091bcb62af488d9132c4f330c182bf8`.
- BATCH-800 / Issue #287 is COMPLETE (100/100) via PR #335; BATCH-100..800 repository hardening/UI task accounting remains 760 completed task IDs.
- No open application-programming issue may silently override the external/manual production sequence below.

## 4. CURRENT P0 — Real SQL Production MVP

**Umbrella:** #111.  
**Active production gate:** #116 / P0.5.  
**Selected candidate:** RC.61.  
**Mandatory external/manual dependency:** `#162 durable RC.61 publication + independent verification -> #116 real trusted-IIS 15/15 acceptance -> #111 closure`.  
**Hard stop:** no #116 production mutation while #162 is OPEN.  
**Repository-admin action:** #353 branch-protection apply/readback remains separate and cannot be fabricated by application code.

### Release chain

| Order | Gate | State |
|---|---|---|
| 1 | P0.1 / #112 real SQL registration | COMPLETE |
| 2 | P0.2 / #113 truthful first snapshot | COMPLETE |
| 3 | P0.3 / #114 Server Details source of truth | COMPLETE |
| 4 | P0.4 / #115 real SQL end-to-end acceptance | COMPLETE |
| 5 | P0.5 / #116 trusted-HTTPS IIS SingleNode acceptance | BLOCKED BEFORE MUTATION BY #162 |

### #162 approved operator sequence

1. From a trusted authenticated operator checkout, run `scripts/Invoke-Rc61DurablePromotion.ps1` in preview mode.
2. Require `READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT` with no workflow dispatch/GitHub mutation.
3. After human review, rerun with `-AcknowledgePromotion`.
4. Bind one exact Green promotion run. Ambiguous discovery, timeout or failure means **do not redispatch**.
5. Separately execute the helper-returned `IndependentVerificationCommand`; do not auto-chain it.
6. Bind the exact Green verifier run.
7. Run `scripts/Test-Rc61CutoverReadiness.ps1` with the exact promotion and verification run IDs.
8. Require `READY_FOR_P0_5_PRE_CUTOVER_PREPARATION`, `DurableReleasePrerequisiteSatisfied=True`, `ExternalGatesPassed=0`, `ProductionMutationPerformed=False` and `MutatedGitHubState=False`.
9. Only then may #116 real-host preparation/mutation begin.

### #116 real-environment execution after #162

1. Preserve selected RC.61 product SHA-256 and exact approved Acceptance Control Toolkit identity.
2. Create/verify the pre-cutover operational backup.
3. Export and independently verify the toolkit from its exact approved commit; preserve toolkit-manifest SHA-256 externally.
4. Create one fresh immutable acceptance session bound to selected product hash, tooling commit and toolkit manifest; preserve returned session-manifest SHA-256 externally.
5. Run packaged IIS preflight; review PLAN ONLY output; apply only after explicit operator review.
6. Prove trusted HTTPS health/authentication and least-privilege monitored SQL path.
7. Recycle IIS and prove registration, protected credential and operational-state durability.
8. Rehearse rollback/recovery and repeat health/auth/read checks.
9. Record each real gate with `Set-ProductionAcceptanceGate.ps1` using the externally preserved session-manifest anchor and same-session evidence.
10. At real 15/15, run `Complete-ProductionAcceptance.ps1` with explicit final acknowledgement and independently revalidate/human-review closure evidence.
11. Only then may #116 close; #111 closes only after #116.

## 5. Stable implementation guardrails

- Browser monitoring GETs are cache/control-plane only; they never start monitored-SQL collection.
- No browser-to-SQL direct connectivity.
- No autonomous remediation or AI-generated SQL execution.
- Credentials/full connection strings/current secret references/raw provider errors/arbitrary SQL text remain outside UI/audit/telemetry/exports/diagnostics/evidence.
- Mutations remain POST + antiforgery + named authorization.
- Missing/stale/truncated/permission-limited evidence remains explicit; no fake healthy/zero defaults.
- MultiNode remains fail-closed/deferred until separately accepted.
- Repository CI, synthetic acceptance evidence and local tooling cannot close external/manual gates.
- Programming closures must update this plan, `STATUS.md`, `FEATURE_CATALOG.md` and a `docs/work/PROGRAMMING_CLOSURE_<issue>.md` ledger in the same PR.

## Definition of done

The current programming closure is done only when its exact final head is current with `main`, all repository-selected gates are Green, review threads are resolved, canonical tracking is reconciled and the PR merges. The production plan is done only after #162 completes manually, #116 produces and human-accepts real trusted-IIS 15/15 evidence, and #111 closes afterward.