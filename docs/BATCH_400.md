# BATCH-400 — Portal Completion + Production DBA Diagnostics

BATCH-400 contains the ten portal/typography tasks already merged by PR #107 plus **100 additional diagnostics and decision-safety tasks B400-011..110** tracked by issue #108. The continuation is additive and preserves the portal work. New diagnostics are deterministic/read-only helpers; they do not initiate monitored-SQL collection from browser GETs and never execute remediation or AI-generated SQL.

## Guardrails

- No browser-to-SQL access.
- No autonomous remediation or AI SQL execution.
- No plaintext credentials, connection strings, raw provider errors or SQL text in UI/audit/export/diagnostics.
- Intelligence helpers are bounded, deterministic and side-effect free.
- `/intelligence/v2/contract` is read-only and protected by the named Read policy.
- B400-011..110 close only after Release `--warnaserror`, full tests, PR CI and squash merge.

## Existing portal work — PR #107

| Task | Code outcome | Status |
|---|---|---|
| B400-001 | Dedicated Performance Health portal | MERGED — PR #107 |
| B400-002 | Estate Recommendations portal | MERGED — PR #107 |
| B400-003 | Reports & Diagnostics portal | MERGED — PR #107 |
| B400-004 | Role-aware information architecture | MERGED — PR #107 |
| B400-005 | Fleet/help/readiness/audit/history discoverability | MERGED — PR #107 |
| B400-006 | Self-hosted Inter Variable typography | MERGED — PR #107 |
| B400-007 | Self-hosted Noto Sans Arabic typography | MERGED — PR #107 |
| B400-008 | Self-only font CSP and asset coverage | MERGED — PR #107 |
| B400-009 | Responsive sidebar overflow polish | MERGED — PR #107 |
| B400-010 | Portal release/browser acceptance gate | MERGED — PR #107 |

## Wait-stat intelligence — B400-011..020

| Task | Code outcome | Status |
|---|---|---|
| B400-011 | Normalize bounded wait-type tokens | IMPLEMENTED — CI PENDING |
| B400-012 | Classify waits into DBA domains | IMPLEMENTED — CI PENDING |
| B400-013 | Exclude benign/background waits | IMPLEMENTED — CI PENDING |
| B400-014 | Compute interval-normalized wait rate | IMPLEMENTED — CI PENDING |
| B400-015 | Compute signal-wait percentage | IMPLEMENTED — CI PENDING |
| B400-016 | Compute actionable wait share | IMPLEMENTED — CI PENDING |
| B400-017 | Composite deterministic wait score | IMPLEMENTED — CI PENDING |
| B400-018 | Wait severity thresholds | IMPLEMENTED — CI PENDING |
| B400-019 | Opaque wait fingerprint | IMPLEMENTED — CI PENDING |
| B400-020 | Bounded ordered wait summary | IMPLEMENTED — CI PENDING |

## Query regression — B400-021..030

| Task | Code outcome | Status |
|---|---|---|
| B400-021 | Bounded query-key normalization | IMPLEMENTED — CI PENDING |
| B400-022 | Defensive percent-delta primitive | IMPLEMENTED — CI PENDING |
| B400-023 | Duration regression calculation | IMPLEMENTED — CI PENDING |
| B400-024 | CPU regression calculation | IMPLEMENTED — CI PENDING |
| B400-025 | Logical-read regression calculation | IMPLEMENTED — CI PENDING |
| B400-026 | Plan-change detection | IMPLEMENTED — CI PENDING |
| B400-027 | Composite query-regression score | IMPLEMENTED — CI PENDING |
| B400-028 | Query-regression severity | IMPLEMENTED — CI PENDING |
| B400-029 | Regression-candidate predicate | IMPLEMENTED — CI PENDING |
| B400-030 | Bounded top-regression ranking | IMPLEMENTED — CI PENDING |

## TempDB pressure — B400-031..040

| Task | Code outcome | Status |
|---|---|---|
| B400-031 | Normalize TempDB file samples | IMPLEMENTED — CI PENDING |
| B400-032 | TempDB aggregate used percentage | IMPLEMENTED — CI PENDING |
| B400-033 | File-size imbalance detection | IMPLEMENTED — CI PENDING |
| B400-034 | File-used imbalance detection | IMPLEMENTED — CI PENDING |
| B400-035 | Aggregate TempDB growth rate | IMPLEMENTED — CI PENDING |
| B400-036 | Average TempDB I/O latency | IMPLEMENTED — CI PENDING |
| B400-037 | Allocation-contention score | IMPLEMENTED — CI PENDING |
| B400-038 | Bounded file-count recommendation | IMPLEMENTED — CI PENDING |
| B400-039 | TempDB pressure severity | IMPLEMENTED — CI PENDING |
| B400-040 | Composite TempDB pressure summary | IMPLEMENTED — CI PENDING |

## Transaction-log health — B400-041..050

| Task | Code outcome | Status |
|---|---|---|
| B400-041 | Transaction-log used percentage | IMPLEMENTED — CI PENDING |
| B400-042 | VLF-count risk bands | IMPLEMENTED — CI PENDING |
| B400-043 | Log-reuse wait normalization | IMPLEMENTED — CI PENDING |
| B400-044 | Active-transaction age bands | IMPLEMENTED — CI PENDING |
| B400-045 | Recovery-aware log-backup overdue check | IMPLEMENTED — CI PENDING |
| B400-046 | Log-growth risk bands | IMPLEMENTED — CI PENDING |
| B400-047 | Composite log-risk score | IMPLEMENTED — CI PENDING |
| B400-048 | Log-risk severity | IMPLEMENTED — CI PENDING |
| B400-049 | Truncation-blocked predicate | IMPLEMENTED — CI PENDING |
| B400-050 | Transaction-log summary with safe reason | IMPLEMENTED — CI PENDING |

## I/O latency — B400-051..060

| Task | Code outcome | Status |
|---|---|---|
| B400-051 | Normalize bounded file keys | IMPLEMENTED — CI PENDING |
| B400-052 | Clamp invalid/extreme latency values | IMPLEMENTED — CI PENDING |
| B400-053 | Aggregate read/write throughput | IMPLEMENTED — CI PENDING |
| B400-054 | Operation-weighted latency | IMPLEMENTED — CI PENDING |
| B400-055 | Write-workload share | IMPLEMENTED — CI PENDING |
| B400-056 | I/O latency risk bands | IMPLEMENTED — CI PENDING |
| B400-057 | Composite I/O score | IMPLEMENTED — CI PENDING |
| B400-058 | I/O severity thresholds | IMPLEMENTED — CI PENDING |
| B400-059 | Opaque file fingerprint | IMPLEMENTED — CI PENDING |
| B400-060 | Bounded top-I/O hotspot ranking | IMPLEMENTED — CI PENDING |

## SQL Agent reliability — B400-061..070

| Task | Code outcome | Status |
|---|---|---|
| B400-061 | Normalize job-owner labels | IMPLEMENTED — CI PENDING |
| B400-062 | Job success-rate calculation | IMPLEMENTED — CI PENDING |
| B400-063 | Consecutive-failure streak | IMPLEMENTED — CI PENDING |
| B400-064 | Deterministic p95 duration | IMPLEMENTED — CI PENDING |
| B400-065 | Bounded schedule lateness | IMPLEMENTED — CI PENDING |
| B400-066 | Job-duration regression percentage | IMPLEMENTED — CI PENDING |
| B400-067 | Composite reliability risk score | IMPLEMENTED — CI PENDING |
| B400-068 | Agent reliability severity | IMPLEMENTED — CI PENDING |
| B400-069 | Alert-worthy job predicate | IMPLEMENTED — CI PENDING |
| B400-070 | Bounded job-reliability summary | IMPLEMENTED — CI PENDING |

## HA readiness — B400-071..080

| Task | Code outcome | Status |
|---|---|---|
| B400-071 | Replica synchronization-state parser | IMPLEMENTED — CI PENDING |
| B400-072 | Replica-lag bands | IMPLEMENTED — CI PENDING |
| B400-073 | Send/redo queue score | IMPLEMENTED — CI PENDING |
| B400-074 | Synchronization health score | IMPLEMENTED — CI PENDING |
| B400-075 | Failover-readiness predicate | IMPLEMENTED — CI PENDING |
| B400-076 | Configurable RPO compliance | IMPLEMENTED — CI PENDING |
| B400-077 | RTO readiness predicate | IMPLEMENTED — CI PENDING |
| B400-078 | Quorum-majority risk detection | IMPLEMENTED — CI PENDING |
| B400-079 | HA severity thresholds | IMPLEMENTED — CI PENDING |
| B400-080 | Composite HA readiness summary | IMPLEMENTED — CI PENDING |

## Maintenance decision safety — B400-081..090

| Task | Code outcome | Status |
|---|---|---|
| B400-081 | Strict maintenance-operation normalization | IMPLEMENTED — CI PENDING |
| B400-082 | Environment-aware base-risk classification | IMPLEMENTED — CI PENDING |
| B400-083 | Approval requirement policy | IMPLEMENTED — CI PENDING |
| B400-084 | Rollback-plan requirement policy | IMPLEMENTED — CI PENDING |
| B400-085 | Approved-window requirement policy | IMPLEMENTED — CI PENDING |
| B400-086 | Deterministic blocker enumeration | IMPLEMENTED — CI PENDING |
| B400-087 | Fail-closed allowed predicate | IMPLEMENTED — CI PENDING |
| B400-088 | Bounded maintenance risk score | IMPLEMENTED — CI PENDING |
| B400-089 | Opaque maintenance fingerprint | IMPLEMENTED — CI PENDING |
| B400-090 | Composite maintenance decision | IMPLEMENTED — CI PENDING |

## Fleet signal correlation — B400-091..100

| Task | Code outcome | Status |
|---|---|---|
| B400-091 | Normalize server correlation keys | IMPLEMENTED — CI PENDING |
| B400-092 | Normalize environment keys | IMPLEMENTED — CI PENDING |
| B400-093 | Bounded correlation window | IMPLEMENTED — CI PENDING |
| B400-094 | Deterministic time bucketing | IMPLEMENTED — CI PENDING |
| B400-095 | Opaque correlation key | IMPLEMENTED — CI PENDING |
| B400-096 | Severity weighting | IMPLEMENTED — CI PENDING |
| B400-097 | Blast-radius calculation | IMPLEMENTED — CI PENDING |
| B400-098 | Dominant-rule detection | IMPLEMENTED — CI PENDING |
| B400-099 | Distinct affected-environment projection | IMPLEMENTED — CI PENDING |
| B400-100 | Correlation preserves critical severity | IMPLEMENTED — CI PENDING |

## Release contract — B400-101..110

| Task | Code outcome | Status |
|---|---|---|
| B400-101 | Continuation-aware B400 task-ID formatter | IMPLEMENTED — CI PENDING |
| B400-102 | Strict B400 task-ID parser | IMPLEMENTED — CI PENDING |
| B400-103 | B400-011..110 completeness verifier | IMPLEMENTED — CI PENDING |
| B400-104 | Versioned diagnostics contract schema | IMPLEMENTED — CI PENDING |
| B400-105 | Deterministic feature-group manifest | IMPLEMENTED — CI PENDING |
| B400-106 | Explicit safety-guardrail manifest | IMPLEMENTED — CI PENDING |
| B400-107 | 100-task continuation contract manifest | IMPLEMENTED — CI PENDING |
| B400-108 | SHA-256 contract hash | IMPLEMENTED — CI PENDING |
| B400-109 | Fail-closed release evaluation | IMPLEMENTED — CI PENDING |
| B400-110 | Read-policy `/intelligence/v2/contract` endpoint | IMPLEMENTED — CI PENDING |

## Delivery rule

`Preserve merged portal work -> implement B400-011..110 -> 100 mapped acceptance tests -> Release build/tests -> canonical evidence -> PR CI -> squash merge -> close #108`.
