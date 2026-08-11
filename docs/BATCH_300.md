# BATCH-300 — DBA Intelligence & Production Operations

Issue #101 is the canonical umbrella for 100 new code tasks after the BATCH-200 reconciliation. Every item below has production code and mapped acceptance coverage; status remains `IMPLEMENTED — CI PENDING` until the complete Release build/test gate is Green and the corresponding batch is merged to stable `main`.

## Guardrails

- Normal GET/read API/dashboard paths use retained cache, history or Monitor-owned control-plane state and do not initiate monitored-SQL collection.
- No browser connects directly to monitored SQL.
- No autonomous remediation or executable SQL is introduced.
- Credentials, connection secrets, SQL text and raw provider errors stay out of UI/API/audit/exports/diagnostics.
- Mutations require explicit bounded state transitions and existing authorization/audit controls.

## Batch 1 — DBA Risk Scoring
| Task | Description | Status |
|---|---|---|
| B300-001 | Weighted server risk score model | IMPLEMENTED — CI PENDING |
| B300-002 | Snapshot freshness risk contribution | IMPLEMENTED — CI PENDING |
| B300-003 | Database availability risk contribution | IMPLEMENTED — CI PENDING |
| B300-004 | Backup compliance risk contribution | IMPLEMENTED — CI PENDING |
| B300-005 | Memory pressure risk contribution | IMPLEMENTED — CI PENDING |
| B300-006 | Blocking risk contribution | IMPLEMENTED — CI PENDING |
| B300-007 | Runnable scheduler pressure contribution | IMPLEMENTED — CI PENDING |
| B300-008 | Active incident risk contribution | IMPLEMENTED — CI PENDING |
| B300-009 | Maintenance/suppression-aware risk projection | IMPLEMENTED — CI PENDING |
| B300-010 | Risk scoring acceptance suite | IMPLEMENTED — CI PENDING |

## Batch 2 — Trends & Baselines
| Task | Description | Status |
|---|---|---|
| B300-011 | Bounded numeric trend series | IMPLEMENTED — CI PENDING |
| B300-012 | Moving average baseline | IMPLEMENTED — CI PENDING |
| B300-013 | Trend slope classification | IMPLEMENTED — CI PENDING |
| B300-014 | Memory trend projection | IMPLEMENTED — CI PENDING |
| B300-015 | Blocking trend projection | IMPLEMENTED — CI PENDING |
| B300-016 | Runnable-task trend projection | IMPLEMENTED — CI PENDING |
| B300-017 | Database availability trend projection | IMPLEMENTED — CI PENDING |
| B300-018 | Backup trend projection | IMPLEMENTED — CI PENDING |
| B300-019 | Sparse/stale history confidence model | IMPLEMENTED — CI PENDING |
| B300-020 | Trend acceptance suite | IMPLEMENTED — CI PENDING |

## Batch 3 — Incident Prioritization
| Task | Description | Status |
|---|---|---|
| B300-021 | Deterministic priority score | IMPLEMENTED — CI PENDING |
| B300-022 | Severity weighting | IMPLEMENTED — CI PENDING |
| B300-023 | SLA-age weighting | IMPLEMENTED — CI PENDING |
| B300-024 | Occurrence-frequency weighting | IMPLEMENTED — CI PENDING |
| B300-025 | Suppression-aware actionability | IMPLEMENTED — CI PENDING |
| B300-026 | Assignee-aware queue projection | IMPLEMENTED — CI PENDING |
| B300-027 | Rule-family deterministic grouping | IMPLEMENTED — CI PENDING |
| B300-028 | Duplicate incident collapse projection | IMPLEMENTED — CI PENDING |
| B300-029 | Top-N bounded priority queue | IMPLEMENTED — CI PENDING |
| B300-030 | Prioritization acceptance suite | IMPLEMENTED — CI PENDING |

## Batch 4 — Notification Routing & Outbox
| Task | Description | Status |
|---|---|---|
| B300-031 | Notification event model | IMPLEMENTED — CI PENDING |
| B300-032 | Deterministic route policy | IMPLEMENTED — CI PENDING |
| B300-033 | Environment routing rules | IMPLEMENTED — CI PENDING |
| B300-034 | Severity routing rules | IMPLEMENTED — CI PENDING |
| B300-035 | Suppression blocks dispatch projection | IMPLEMENTED — CI PENDING |
| B300-036 | Maintenance notification semantics | IMPLEMENTED — CI PENDING |
| B300-037 | Durable bounded outbox | IMPLEMENTED — CI PENDING |
| B300-038 | Idempotent notification key | IMPLEMENTED — CI PENDING |
| B300-039 | Retry/dead-letter state machine | IMPLEMENTED — CI PENDING |
| B300-040 | Notification acceptance suite | IMPLEMENTED — CI PENDING |

## Batch 5 — Change & Maintenance Calendar
| Task | Description | Status |
|---|---|---|
| B300-041 | Change-window model | IMPLEMENTED — CI PENDING |
| B300-042 | UTC validation and duration bounds | IMPLEMENTED — CI PENDING |
| B300-043 | Server-group change windows | IMPLEMENTED — CI PENDING |
| B300-044 | Environment change windows | IMPLEMENTED — CI PENDING |
| B300-045 | Overlap detection | IMPLEMENTED — CI PENDING |
| B300-046 | Upcoming-window projection | IMPLEMENTED — CI PENDING |
| B300-047 | Active-window projection | IMPLEMENTED — CI PENDING |
| B300-048 | Change freeze policy | IMPLEMENTED — CI PENDING |
| B300-049 | Audited change-window mutations | IMPLEMENTED — CI PENDING |
| B300-050 | Change calendar acceptance suite | IMPLEMENTED — CI PENDING |

## Batch 6 — Capacity & Compliance
| Task | Description | Status |
|---|---|---|
| B300-051 | Storage utilization ratio model | IMPLEMENTED — CI PENDING |
| B300-052 | Capacity risk classification | IMPLEMENTED — CI PENDING |
| B300-053 | Backup compliance classification | IMPLEMENTED — CI PENDING |
| B300-054 | Backup-age bounds | IMPLEMENTED — CI PENDING |
| B300-055 | Database online ratio compliance | IMPLEMENTED — CI PENDING |
| B300-056 | Memory headroom compliance | IMPLEMENTED — CI PENDING |
| B300-057 | Fleet capacity rollup | IMPLEMENTED — CI PENDING |
| B300-058 | Environment compliance rollup | IMPLEMENTED — CI PENDING |
| B300-059 | Deterministic compliance score | IMPLEMENTED — CI PENDING |
| B300-060 | Capacity/compliance acceptance suite | IMPLEMENTED — CI PENDING |

## Batch 7 — Estate Inventory Lifecycle
| Task | Description | Status |
|---|---|---|
| B300-061 | SQL product version parser | IMPLEMENTED — CI PENDING |
| B300-062 | Major-version classification | IMPLEMENTED — CI PENDING |
| B300-063 | Edition normalization | IMPLEMENTED — CI PENDING |
| B300-064 | Instance topology classification | IMPLEMENTED — CI PENDING |
| B300-065 | Encryption posture projection | IMPLEMENTED — CI PENDING |
| B300-066 | Registration lifecycle state | IMPLEMENTED — CI PENDING |
| B300-067 | Disabled/stale inventory status | IMPLEMENTED — CI PENDING |
| B300-068 | Environment/version inventory matrix | IMPLEMENTED — CI PENDING |
| B300-069 | Upgrade-candidate projection | IMPLEMENTED — CI PENDING |
| B300-070 | Estate inventory acceptance suite | IMPLEMENTED — CI PENDING |

## Batch 8 — Read API Contracts
| Task | Description | Status |
|---|---|---|
| B300-071 | Versioned fleet summary DTO | IMPLEMENTED — CI PENDING |
| B300-072 | Versioned risk DTO | IMPLEMENTED — CI PENDING |
| B300-073 | Versioned incident priority DTO | IMPLEMENTED — CI PENDING |
| B300-074 | Strict pagination contract | IMPLEMENTED — CI PENDING |
| B300-075 | Safe filter normalization | IMPLEMENTED — CI PENDING |
| B300-076 | ETag generation from control-plane state | IMPLEMENTED — CI PENDING |
| B300-077 | Cache-control policy for read APIs | IMPLEMENTED — CI PENDING |
| B300-078 | API secret-field exclusion contract | IMPLEMENTED — CI PENDING |
| B300-079 | Read API authorization matrix | IMPLEMENTED — CI PENDING |
| B300-080 | Read API zero-monitored-SQL acceptance suite | IMPLEMENTED — CI PENDING |

## Batch 9 — SLO & Runtime Observability
| Task | Description | Status |
|---|---|---|
| B300-081 | Read-path latency histogram model | IMPLEMENTED — CI PENDING |
| B300-082 | Collection-cycle duration model | IMPLEMENTED — CI PENDING |
| B300-083 | Cache hit-ratio projection | IMPLEMENTED — CI PENDING |
| B300-084 | Stale-read ratio projection | IMPLEMENTED — CI PENDING |
| B300-085 | Incident transition success ratio | IMPLEMENTED — CI PENDING |
| B300-086 | Shared-state CAS conflict ratio | IMPLEMENTED — CI PENDING |
| B300-087 | SLO threshold options validation | IMPLEMENTED — CI PENDING |
| B300-088 | SLO health classification | IMPLEMENTED — CI PENDING |
| B300-089 | Bounded observability snapshot | IMPLEMENTED — CI PENDING |
| B300-090 | SLO acceptance suite | IMPLEMENTED — CI PENDING |

## Batch 10 — Operator UX & Release Candidate
| Task | Description | Status |
|---|---|---|
| B300-091 | DBA intelligence dashboard read model | IMPLEMENTED — CI PENDING |
| B300-092 | Risk-ranked fleet cards | IMPLEMENTED — CI PENDING |
| B300-093 | Trend summary cards | IMPLEMENTED — CI PENDING |
| B300-094 | Priority incident queue UI model | IMPLEMENTED — CI PENDING |
| B300-095 | Capacity/compliance UI model | IMPLEMENTED — CI PENDING |
| B300-096 | Estate lifecycle UI model | IMPLEMENTED — CI PENDING |
| B300-097 | Degraded/empty-state contracts | IMPLEMENTED — CI PENDING |
| B300-098 | BATCH-300 compatibility regression suite | IMPLEMENTED — CI PENDING |
| B300-099 | Release-candidate acceptance suite | IMPLEMENTED — CI PENDING |
| B300-100 | Canonical docs/status/release gate | IMPLEMENTED — CI PENDING |

## Delivery rule

`Code -> mapped acceptance -> Release build with --warnaserror -> full test suite -> canonical evidence -> final PR CI -> squash merge -> CLOSED`.
