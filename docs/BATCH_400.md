# BATCH-400 — Production DBA Diagnostics & Decision Safety

Issue #108 is the canonical umbrella for this 100-code-task program. Every task is implemented with mapped acceptance coverage before CI. A task is closed only after Release build with `--warnaserror`, the complete test suite, PR CI and squash merge to `main`.

## Guardrails

- Diagnostics helpers are deterministic and side-effect free.
- No browser-to-SQL access.
- No autonomous remediation or AI-generated SQL execution.
- No plaintext credentials, connection strings, raw provider errors or SQL text in UI/audit/export/diagnostics.
- The B400 contract endpoint is read-only and protected by the named Read policy.

## 1 — Wait-stat intelligence

| Task | Code outcome | Status |
|---|---|---|
| B400-001 | Normalize bounded wait-type tokens | IMPLEMENTED — CI PENDING |
| B400-002 | Classify waits into DBA domains | IMPLEMENTED — CI PENDING |
| B400-003 | Exclude benign/background waits | IMPLEMENTED — CI PENDING |
| B400-004 | Compute interval-normalized wait rate | IMPLEMENTED — CI PENDING |
| B400-005 | Compute signal-wait percentage | IMPLEMENTED — CI PENDING |
| B400-006 | Compute actionable wait share | IMPLEMENTED — CI PENDING |
| B400-007 | Composite deterministic wait score | IMPLEMENTED — CI PENDING |
| B400-008 | Wait severity thresholds | IMPLEMENTED — CI PENDING |
| B400-009 | Opaque wait fingerprint | IMPLEMENTED — CI PENDING |
| B400-010 | Bounded ordered wait summary | IMPLEMENTED — CI PENDING |

## 2 — Query regression

| Task | Code outcome | Status |
|---|---|---|
| B400-011 | Bounded query-key normalization | IMPLEMENTED — CI PENDING |
| B400-012 | Defensive percent-delta primitive | IMPLEMENTED — CI PENDING |
| B400-013 | Duration regression calculation | IMPLEMENTED — CI PENDING |
| B400-014 | CPU regression calculation | IMPLEMENTED — CI PENDING |
| B400-015 | Logical-read regression calculation | IMPLEMENTED — CI PENDING |
| B400-016 | Plan-change detection | IMPLEMENTED — CI PENDING |
| B400-017 | Composite query-regression score | IMPLEMENTED — CI PENDING |
| B400-018 | Query-regression severity | IMPLEMENTED — CI PENDING |
| B400-019 | Regression-candidate predicate | IMPLEMENTED — CI PENDING |
| B400-020 | Bounded top-regression ranking | IMPLEMENTED — CI PENDING |

## 3 — TempDB pressure

| Task | Code outcome | Status |
|---|---|---|
| B400-021 | Normalize TempDB file samples | IMPLEMENTED — CI PENDING |
| B400-022 | TempDB aggregate used percentage | IMPLEMENTED — CI PENDING |
| B400-023 | File-size imbalance detection | IMPLEMENTED — CI PENDING |
| B400-024 | File-used imbalance detection | IMPLEMENTED — CI PENDING |
| B400-025 | Aggregate TempDB growth rate | IMPLEMENTED — CI PENDING |
| B400-026 | Average TempDB I/O latency | IMPLEMENTED — CI PENDING |
| B400-027 | Allocation-contention score | IMPLEMENTED — CI PENDING |
| B400-028 | Bounded file-count recommendation | IMPLEMENTED — CI PENDING |
| B400-029 | TempDB pressure severity | IMPLEMENTED — CI PENDING |
| B400-030 | Composite TempDB pressure summary | IMPLEMENTED — CI PENDING |

## 4 — Transaction-log health

| Task | Code outcome | Status |
|---|---|---|
| B400-031 | Transaction-log used percentage | IMPLEMENTED — CI PENDING |
| B400-032 | VLF-count risk bands | IMPLEMENTED — CI PENDING |
| B400-033 | Log-reuse wait normalization | IMPLEMENTED — CI PENDING |
| B400-034 | Active-transaction age bands | IMPLEMENTED — CI PENDING |
| B400-035 | Recovery-aware log-backup overdue check | IMPLEMENTED — CI PENDING |
| B400-036 | Log-growth risk bands | IMPLEMENTED — CI PENDING |
| B400-037 | Composite log-risk score | IMPLEMENTED — CI PENDING |
| B400-038 | Log-risk severity | IMPLEMENTED — CI PENDING |
| B400-039 | Truncation-blocked predicate | IMPLEMENTED — CI PENDING |
| B400-040 | Transaction-log summary with safe reason | IMPLEMENTED — CI PENDING |

## 5 — I/O latency intelligence

| Task | Code outcome | Status |
|---|---|---|
| B400-041 | Normalize bounded file keys | IMPLEMENTED — CI PENDING |
| B400-042 | Clamp invalid/extreme latency values | IMPLEMENTED — CI PENDING |
| B400-043 | Aggregate read/write throughput | IMPLEMENTED — CI PENDING |
| B400-044 | Operation-weighted latency | IMPLEMENTED — CI PENDING |
| B400-045 | Write-workload share | IMPLEMENTED — CI PENDING |
| B400-046 | I/O latency risk bands | IMPLEMENTED — CI PENDING |
| B400-047 | Composite I/O score | IMPLEMENTED — CI PENDING |
| B400-048 | I/O severity thresholds | IMPLEMENTED — CI PENDING |
| B400-049 | Opaque file fingerprint | IMPLEMENTED — CI PENDING |
| B400-050 | Bounded top-I/O hotspot ranking | IMPLEMENTED — CI PENDING |

## 6 — SQL Agent reliability

| Task | Code outcome | Status |
|---|---|---|
| B400-051 | Normalize job-owner labels | IMPLEMENTED — CI PENDING |
| B400-052 | Job success-rate calculation | IMPLEMENTED — CI PENDING |
| B400-053 | Consecutive-failure streak | IMPLEMENTED — CI PENDING |
| B400-054 | Deterministic p95 duration | IMPLEMENTED — CI PENDING |
| B400-055 | Bounded schedule lateness | IMPLEMENTED — CI PENDING |
| B400-056 | Job-duration regression percentage | IMPLEMENTED — CI PENDING |
| B400-057 | Composite reliability risk score | IMPLEMENTED — CI PENDING |
| B400-058 | Agent reliability severity | IMPLEMENTED — CI PENDING |
| B400-059 | Alert-worthy job predicate | IMPLEMENTED — CI PENDING |
| B400-060 | Bounded job-reliability summary | IMPLEMENTED — CI PENDING |

## 7 — HA readiness

| Task | Code outcome | Status |
|---|---|---|
| B400-061 | Replica synchronization-state parser | IMPLEMENTED — CI PENDING |
| B400-062 | Replica-lag bands | IMPLEMENTED — CI PENDING |
| B400-063 | Send/redo queue score | IMPLEMENTED — CI PENDING |
| B400-064 | Synchronization health score | IMPLEMENTED — CI PENDING |
| B400-065 | Failover-readiness predicate | IMPLEMENTED — CI PENDING |
| B400-066 | Configurable RPO compliance | IMPLEMENTED — CI PENDING |
| B400-067 | RTO readiness predicate | IMPLEMENTED — CI PENDING |
| B400-068 | Quorum-majority risk detection | IMPLEMENTED — CI PENDING |
| B400-069 | HA severity thresholds | IMPLEMENTED — CI PENDING |
| B400-070 | Composite HA readiness summary | IMPLEMENTED — CI PENDING |

## 8 — Maintenance decision safety

| Task | Code outcome | Status |
|---|---|---|
| B400-071 | Strict maintenance-operation normalization | IMPLEMENTED — CI PENDING |
| B400-072 | Environment-aware base-risk classification | IMPLEMENTED — CI PENDING |
| B400-073 | Approval requirement policy | IMPLEMENTED — CI PENDING |
| B400-074 | Rollback-plan requirement policy | IMPLEMENTED — CI PENDING |
| B400-075 | Approved-window requirement policy | IMPLEMENTED — CI PENDING |
| B400-076 | Deterministic blocker enumeration | IMPLEMENTED — CI PENDING |
| B400-077 | Fail-closed allowed predicate | IMPLEMENTED — CI PENDING |
| B400-078 | Bounded maintenance risk score | IMPLEMENTED — CI PENDING |
| B400-079 | Opaque maintenance fingerprint | IMPLEMENTED — CI PENDING |
| B400-080 | Composite maintenance decision | IMPLEMENTED — CI PENDING |

## 9 — Fleet signal correlation

| Task | Code outcome | Status |
|---|---|---|
| B400-081 | Normalize server correlation keys | IMPLEMENTED — CI PENDING |
| B400-082 | Normalize environment keys | IMPLEMENTED — CI PENDING |
| B400-083 | Bounded correlation window | IMPLEMENTED — CI PENDING |
| B400-084 | Deterministic time bucketing | IMPLEMENTED — CI PENDING |
| B400-085 | Opaque correlation key | IMPLEMENTED — CI PENDING |
| B400-086 | Severity weighting | IMPLEMENTED — CI PENDING |
| B400-087 | Blast-radius calculation | IMPLEMENTED — CI PENDING |
| B400-088 | Dominant-rule detection | IMPLEMENTED — CI PENDING |
| B400-089 | Distinct affected-environment projection | IMPLEMENTED — CI PENDING |
| B400-090 | Bounded ordered cluster summary | IMPLEMENTED — CI PENDING |

## 10 — Release contract

| Task | Code outcome | Status |
|---|---|---|
| B400-091 | Canonical B400 task-ID formatter | IMPLEMENTED — CI PENDING |
| B400-092 | Strict B400 task-ID parser | IMPLEMENTED — CI PENDING |
| B400-093 | 100-task completeness verifier | IMPLEMENTED — CI PENDING |
| B400-094 | Versioned B400 contract schema | IMPLEMENTED — CI PENDING |
| B400-095 | Deterministic feature-group manifest | IMPLEMENTED — CI PENDING |
| B400-096 | Explicit safety-guardrail manifest | IMPLEMENTED — CI PENDING |
| B400-097 | Versioned contract manifest | IMPLEMENTED — CI PENDING |
| B400-098 | SHA-256 contract hash | IMPLEMENTED — CI PENDING |
| B400-099 | Fail-closed release evaluation | IMPLEMENTED — CI PENDING |
| B400-100 | Read-policy `/intelligence/v2/contract` endpoint | IMPLEMENTED — CI PENDING |

## Delivery rule

`Implement -> mapped acceptance coverage -> Release build --warnaserror -> complete test suite -> canonical evidence -> PR CI -> squash merge -> close issue`.
