# BATCH-400 — Portal Completion + Production DBA Diagnostics

BATCH-400 preserves the ten portal/typography tasks merged by PR #107 and adds **100 new code tasks B400-011..110** tracked by issue #108.

## Guardrails

- Browser GETs never connect to monitored SQL or trigger collection.
- No autonomous remediation or AI-generated SQL execution.
- No plaintext credentials, connection strings, raw provider errors or SQL text in UI/audit/export/diagnostics.
- Intelligence helpers are bounded, deterministic and side-effect free.
- `/intelligence/v2/contract` is read-only under the named Read policy.

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
| B400-011 | Normalize bounded wait-type tokens | CI VERIFIED — RUN 31467831498 |
| B400-012 | Classify waits into DBA domains | CI VERIFIED — RUN 31467831498 |
| B400-013 | Exclude benign/background waits | CI VERIFIED — RUN 31467831498 |
| B400-014 | Compute interval-normalized wait rate | CI VERIFIED — RUN 31467831498 |
| B400-015 | Compute signal-wait percentage | CI VERIFIED — RUN 31467831498 |
| B400-016 | Compute actionable wait share | CI VERIFIED — RUN 31467831498 |
| B400-017 | Composite deterministic wait score | CI VERIFIED — RUN 31467831498 |
| B400-018 | Wait severity thresholds | CI VERIFIED — RUN 31467831498 |
| B400-019 | Opaque wait fingerprint | CI VERIFIED — RUN 31467831498 |
| B400-020 | Bounded ordered wait summary | CI VERIFIED — RUN 31467831498 |

## Query regression — B400-021..030

| Task | Code outcome | Status |
|---|---|---|
| B400-021 | Bounded query-key normalization | CI VERIFIED — RUN 31467831498 |
| B400-022 | Defensive percent-delta primitive | CI VERIFIED — RUN 31467831498 |
| B400-023 | Duration regression calculation | CI VERIFIED — RUN 31467831498 |
| B400-024 | CPU regression calculation | CI VERIFIED — RUN 31467831498 |
| B400-025 | Logical-read regression calculation | CI VERIFIED — RUN 31467831498 |
| B400-026 | Plan-change detection | CI VERIFIED — RUN 31467831498 |
| B400-027 | Composite query-regression score | CI VERIFIED — RUN 31467831498 |
| B400-028 | Query-regression severity | CI VERIFIED — RUN 31467831498 |
| B400-029 | Regression-candidate predicate | CI VERIFIED — RUN 31467831498 |
| B400-030 | Bounded top-regression ranking | CI VERIFIED — RUN 31467831498 |

## TempDB pressure — B400-031..040

| Task | Code outcome | Status |
|---|---|---|
| B400-031 | Normalize TempDB file samples | CI VERIFIED — RUN 31467831498 |
| B400-032 | TempDB aggregate used percentage | CI VERIFIED — RUN 31467831498 |
| B400-033 | File-size imbalance detection | CI VERIFIED — RUN 31467831498 |
| B400-034 | File-used imbalance detection | CI VERIFIED — RUN 31467831498 |
| B400-035 | Aggregate TempDB growth rate | CI VERIFIED — RUN 31467831498 |
| B400-036 | Average TempDB I/O latency | CI VERIFIED — RUN 31467831498 |
| B400-037 | Allocation-contention score | CI VERIFIED — RUN 31467831498 |
| B400-038 | Bounded file-count recommendation | CI VERIFIED — RUN 31467831498 |
| B400-039 | TempDB pressure severity | CI VERIFIED — RUN 31467831498 |
| B400-040 | Composite TempDB pressure summary | CI VERIFIED — RUN 31467831498 |

## Transaction-log health — B400-041..050

| Task | Code outcome | Status |
|---|---|---|
| B400-041 | Transaction-log used percentage | CI VERIFIED — RUN 31467831498 |
| B400-042 | VLF-count risk bands | CI VERIFIED — RUN 31467831498 |
| B400-043 | Log-reuse wait normalization | CI VERIFIED — RUN 31467831498 |
| B400-044 | Active-transaction age bands | CI VERIFIED — RUN 31467831498 |
| B400-045 | Recovery-aware log-backup overdue check | CI VERIFIED — RUN 31467831498 |
| B400-046 | Log-growth risk bands | CI VERIFIED — RUN 31467831498 |
| B400-047 | Composite log-risk score | CI VERIFIED — RUN 31467831498 |
| B400-048 | Log-risk severity | CI VERIFIED — RUN 31467831498 |
| B400-049 | Truncation-blocked predicate | CI VERIFIED — RUN 31467831498 |
| B400-050 | Transaction-log summary with safe reason | CI VERIFIED — RUN 31467831498 |

## I/O latency — B400-051..060

| Task | Code outcome | Status |
|---|---|---|
| B400-051 | Normalize bounded file keys | CI VERIFIED — RUN 31467831498 |
| B400-052 | Clamp invalid/extreme latency values | CI VERIFIED — RUN 31467831498 |
| B400-053 | Aggregate read/write throughput | CI VERIFIED — RUN 31467831498 |
| B400-054 | Operation-weighted latency | CI VERIFIED — RUN 31467831498 |
| B400-055 | Write-workload share | CI VERIFIED — RUN 31467831498 |
| B400-056 | I/O latency risk bands | CI VERIFIED — RUN 31467831498 |
| B400-057 | Composite I/O score | CI VERIFIED — RUN 31467831498 |
| B400-058 | I/O severity thresholds | CI VERIFIED — RUN 31467831498 |
| B400-059 | Opaque file fingerprint | CI VERIFIED — RUN 31467831498 |
| B400-060 | Bounded top-I/O hotspot ranking | CI VERIFIED — RUN 31467831498 |

## SQL Agent reliability — B400-061..070

| Task | Code outcome | Status |
|---|---|---|
| B400-061 | Normalize job-owner labels | CI VERIFIED — RUN 31467831498 |
| B400-062 | Job success-rate calculation | CI VERIFIED — RUN 31467831498 |
| B400-063 | Consecutive-failure streak | CI VERIFIED — RUN 31467831498 |
| B400-064 | Deterministic p95 duration | CI VERIFIED — RUN 31467831498 |
| B400-065 | Bounded schedule lateness | CI VERIFIED — RUN 31467831498 |
| B400-066 | Job-duration regression percentage | CI VERIFIED — RUN 31467831498 |
| B400-067 | Composite reliability risk score | CI VERIFIED — RUN 31467831498 |
| B400-068 | Agent reliability severity | CI VERIFIED — RUN 31467831498 |
| B400-069 | Alert-worthy job predicate | CI VERIFIED — RUN 31467831498 |
| B400-070 | Bounded job-reliability summary | CI VERIFIED — RUN 31467831498 |

## HA readiness — B400-071..080

| Task | Code outcome | Status |
|---|---|---|
| B400-071 | Replica synchronization-state parser | CI VERIFIED — RUN 31467831498 |
| B400-072 | Replica-lag bands | CI VERIFIED — RUN 31467831498 |
| B400-073 | Send/redo queue score | CI VERIFIED — RUN 31467831498 |
| B400-074 | Synchronization health score | CI VERIFIED — RUN 31467831498 |
| B400-075 | Failover-readiness predicate | CI VERIFIED — RUN 31467831498 |
| B400-076 | Configurable RPO compliance | CI VERIFIED — RUN 31467831498 |
| B400-077 | RTO readiness predicate | CI VERIFIED — RUN 31467831498 |
| B400-078 | Quorum-majority risk detection | CI VERIFIED — RUN 31467831498 |
| B400-079 | HA severity thresholds | CI VERIFIED — RUN 31467831498 |
| B400-080 | Composite HA readiness summary | CI VERIFIED — RUN 31467831498 |

## Maintenance decision safety — B400-081..090

| Task | Code outcome | Status |
|---|---|---|
| B400-081 | Strict maintenance-operation normalization | CI VERIFIED — RUN 31467831498 |
| B400-082 | Environment-aware base-risk classification | CI VERIFIED — RUN 31467831498 |
| B400-083 | Approval requirement policy | CI VERIFIED — RUN 31467831498 |
| B400-084 | Rollback-plan requirement policy | CI VERIFIED — RUN 31467831498 |
| B400-085 | Approved-window requirement policy | CI VERIFIED — RUN 31467831498 |
| B400-086 | Deterministic blocker enumeration | CI VERIFIED — RUN 31467831498 |
| B400-087 | Fail-closed allowed predicate | CI VERIFIED — RUN 31467831498 |
| B400-088 | Bounded maintenance risk score | CI VERIFIED — RUN 31467831498 |
| B400-089 | Opaque maintenance fingerprint | CI VERIFIED — RUN 31467831498 |
| B400-090 | Composite maintenance decision | CI VERIFIED — RUN 31467831498 |

## Fleet signal correlation — B400-091..100

| Task | Code outcome | Status |
|---|---|---|
| B400-091 | Normalize server correlation keys | CI VERIFIED — RUN 31467831498 |
| B400-092 | Normalize environment keys | CI VERIFIED — RUN 31467831498 |
| B400-093 | Bounded correlation window | CI VERIFIED — RUN 31467831498 |
| B400-094 | Deterministic time bucketing | CI VERIFIED — RUN 31467831498 |
| B400-095 | Opaque correlation key | CI VERIFIED — RUN 31467831498 |
| B400-096 | Severity weighting | CI VERIFIED — RUN 31467831498 |
| B400-097 | Blast-radius calculation | CI VERIFIED — RUN 31467831498 |
| B400-098 | Dominant-rule detection | CI VERIFIED — RUN 31467831498 |
| B400-099 | Distinct affected-environment projection | CI VERIFIED — RUN 31467831498 |
| B400-100 | Correlation preserves critical severity | CI VERIFIED — RUN 31467831498 |

## Release contract — B400-101..110

| Task | Code outcome | Status |
|---|---|---|
| B400-101 | Continuation-aware B400 task-ID formatter | CI VERIFIED — RUN 31467831498 |
| B400-102 | Strict B400 task-ID parser | CI VERIFIED — RUN 31467831498 |
| B400-103 | B400-011..110 completeness verifier | CI VERIFIED — RUN 31467831498 |
| B400-104 | Versioned diagnostics contract schema | CI VERIFIED — RUN 31467831498 |
| B400-105 | Deterministic feature-group manifest | CI VERIFIED — RUN 31467831498 |
| B400-106 | Explicit safety-guardrail manifest | CI VERIFIED — RUN 31467831498 |
| B400-107 | 100-task continuation contract manifest | CI VERIFIED — RUN 31467831498 |
| B400-108 | SHA-256 contract hash | CI VERIFIED — RUN 31467831498 |
| B400-109 | Fail-closed release evaluation | CI VERIFIED — RUN 31467831498 |
| B400-110 | Read-policy `/intelligence/v2/contract` endpoint | CI VERIFIED — RUN 31467831498 |

## Verification

- Clean implementation CI on top of PR #107: `31467831498`.
- Release build: **0 warnings / 0 errors** with `--warnaserror`.
- Full suite: **498/498 passed; 0 failed**.
- New B400 continuation coverage: **100 mapped tests for B400-011..110**.
- Final closure still requires PR CI and squash merge to `main`.

## Delivery rule

`Preserve PR #107 -> implement B400-011..110 -> 100 mapped tests -> Release build/tests -> PR CI -> squash merge -> close #108`.
