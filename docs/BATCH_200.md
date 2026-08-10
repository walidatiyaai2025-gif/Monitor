# BATCH-200 — Enterprise Operations Expansion

Issue #76 is the canonical umbrella for this second 100-task program. Delivery uses ten bounded PR batches of ten tasks. Every batch requires Release build with `--warnaserror`, the complete test suite, canonical documentation synchronization and squash merge to stable `main` before the next batch is considered verified.

## Global guardrails

- Monitoring, navigation, reporting and diagnostics GETs never initiate collection against monitored SQL targets.
- No browser/widget connects directly to monitored SQL.
- No autonomous remediation or AI SQL execution.
- Plaintext credentials, full connection strings, raw provider errors and SQL text never enter UI, audit, telemetry, exports, diagnostics or operator metadata.
- Alert suppression does not delete or rewrite incident evidence.
- Maintenance affects explicit backend scheduling policy only; user navigation remains cache/control-plane-only.
- MultiNode stays fail-closed when required shared/security/readiness dependencies are unavailable.
- Every mutation is POST + antiforgery + a named authorization policy.

## Batch 1 — Enterprise UX integration

| Task | Description | Status |
|---|---|---|
| B200-001 | Add Enterprise Operations primary navigation entry | IMPLEMENTED — CI PENDING |
| B200-002 | Active navigation / role-aware enterprise shell state | IMPLEMENTED — CI PENDING |
| B200-003 | Surface environment/group/tags on server details | IMPLEMENTED — CI PENDING |
| B200-004 | Surface active maintenance/suppression state on server details | IMPLEMENTED — CI PENDING |
| B200-005 | Surface assignee/notes/recommendation acknowledgment on incident details | IMPLEMENTED — CI PENDING |
| B200-006 | Server estate environment/group/tag filters | IMPLEMENTED — CI PENDING |
| B200-007 | Incident assignee/suppression filters | IMPLEMENTED — CI PENDING |
| B200-008 | Validation-error UX with safe bounded messages | IMPLEMENTED — CI PENDING |
| B200-009 | Audit coverage for all enterprise metadata mutations | IMPLEMENTED — CI PENDING |
| B200-010 | Enterprise UX/accessibility acceptance suite | IMPLEMENTED — CI PENDING |

## Batch 2 — Maintenance & suppression policy semantics

| Task | Description | Status |
|---|---|---|
| B200-011 | Scheduler skips targets in active maintenance windows | PLANNED |
| B200-012 | Manual refresh during maintenance remains explicit/audited | PLANNED |
| B200-013 | Fleet/server projections expose maintenance-active state | PLANNED |
| B200-014 | Alert suppression projection without evidence mutation | PLANNED |
| B200-015 | Actionable-vs-suppressed incident counts | PLANNED |
| B200-016 | Automatic suppression expiry semantics | PLANNED |
| B200-017 | Start-inclusive/end-exclusive boundary tests | PLANNED |
| B200-018 | Cross-node policy consistency | PLANNED |
| B200-019 | Corrupt policy metadata fails closed | PLANNED |
| B200-020 | Maintenance/suppression acceptance suite | PLANNED |

## Batch 3 — Incident collaboration workflow

| Task | Description | Status |
|---|---|---|
| B200-021 | Assignee-aware incident query/filter | PLANNED |
| B200-022 | Owner-change audit timeline | PLANNED |
| B200-023 | Bounded note paging | PLANNED |
| B200-024 | Immutable operator-note identity validation | PLANNED |
| B200-025 | Note replay/idempotency protection | PLANNED |
| B200-026 | Incident age/SLA bucket projection | PLANNED |
| B200-027 | Severity escalation marker/history | PLANNED |
| B200-028 | Bounded reopen reason | PLANNED |
| B200-029 | Bounded resolution note | PLANNED |
| B200-030 | Collaboration workflow acceptance suite | PLANNED |

## Batch 4 — Reporting & diagnostics expansion

| Task | Description | Status |
|---|---|---|
| B200-031 | Filtered server CSV export | PLANNED |
| B200-032 | Formula-safe incident CSV export | PLANNED |
| B200-033 | Bounded history CSV export | PLANNED |
| B200-034 | Administrator audit CSV export | PLANNED |
| B200-035 | Versioned deterministic export schemas | PLANNED |
| B200-036 | Explicit export row/size caps | PLANNED |
| B200-037 | UTF-8/BOM compatibility tests | PLANNED |
| B200-038 | Spreadsheet-formula injection matrix | PLANNED |
| B200-039 | Diagnostics manifest build/revision metadata | PLANNED |
| B200-040 | Export/diagnostics acceptance suite | PLANNED |

## Batch 5 — Fleet intelligence

| Task | Description | Status |
|---|---|---|
| B200-041 | Health summary by environment | PLANNED |
| B200-042 | Health summary by server group | PLANNED |
| B200-043 | Health summary by tag | PLANNED |
| B200-044 | Stale/unavailable snapshot counts | PLANNED |
| B200-045 | Active maintenance counts | PLANNED |
| B200-046 | Active suppression counts | PLANNED |
| B200-047 | Incident hot-spots by deterministic rule | PLANNED |
| B200-048 | Backup-risk fleet summary | PLANNED |
| B200-049 | Memory/blocking/performance risk summary | PLANNED |
| B200-050 | Fleet intelligence zero-monitored-SQL acceptance suite | PLANNED |

## Batch 6 — Retention & governance

| Task | Description | Status |
|---|---|---|
| B200-051 | Orphaned server operator-metadata detection | PLANNED |
| B200-052 | Resolved/orphaned incident metadata pruning policy | PLANNED |
| B200-053 | Operator-note retention policy | PLANNED |
| B200-054 | Configurable bounded audit retention | PLANNED |
| B200-055 | Backup-retention configuration validation | PLANNED |
| B200-056 | History-retention configuration validation | PLANNED |
| B200-057 | Cleanup dry-run report | PLANNED |
| B200-058 | Administrator cleanup POST + antiforgery | PLANNED |
| B200-059 | Cleanup audit trail | PLANNED |
| B200-060 | Retention/governance acceptance suite | PLANNED |

## Batch 7 — HA & disaster recovery for operator state

| Task | Description | Status |
|---|---|---|
| B200-061 | Shared operator-state fault injection | PLANNED |
| B200-062 | Concurrent report consistency under metadata writes | PLANNED |
| B200-063 | Redacted diagnostics during shared-state degradation | PLANNED |
| B200-064 | Include operator metadata in operational backup contract | PLANNED |
| B200-065 | Operator metadata restore dry-run validation | PLANNED |
| B200-066 | Atomic operator metadata restore/rollback | PLANNED |
| B200-067 | Cross-node recommendation-ack convergence | PLANNED |
| B200-068 | Cross-node note concurrency verification | PLANNED |
| B200-069 | Cross-node maintenance/scheduler policy verification | PLANNED |
| B200-070 | HA/operator-state acceptance suite | PLANNED |

## Batch 8 — Enterprise security hardening II

| Task | Description | Status |
|---|---|---|
| B200-071 | Secure download/content-disposition policy | PLANNED |
| B200-072 | Safe export filename generation | PLANNED |
| B200-073 | Fixed/allowlisted diagnostics ZIP entry names | PLANNED |
| B200-074 | Operator-note HTML/XSS rendering regression suite | PLANNED |
| B200-075 | Formula-safe group/assignee/tag exports | PLANNED |
| B200-076 | Request-size limits for enterprise text inputs | PLANNED |
| B200-077 | Strict incident/registration route-ID normalization | PLANNED |
| B200-078 | Enterprise endpoint authorization matrix | PLANNED |
| B200-079 | Enterprise audit/diagnostic secret-canary suite | PLANNED |
| B200-080 | Security acceptance suite | PLANNED |

## Batch 9 — Performance & scale II

| Task | Description | Status |
|---|---|---|
| B200-081 | Operator-metadata lookup/indexing budget | PLANNED |
| B200-082 | Enterprise server pagination | PLANNED |
| B200-083 | Enterprise incident pagination | PLANNED |
| B200-084 | Bounded/lazy note rendering | PLANNED |
| B200-085 | Streaming/bounded CSV generation | PLANNED |
| B200-086 | Diagnostics timeout/cancellation bounds | PLANNED |
| B200-087 | Shared CAS retry telemetry | PLANNED |
| B200-088 | Metadata write-contention test | PLANNED |
| B200-089 | Fleet-summary O(N) deterministic budget test | PLANNED |
| B200-090 | Performance/scale acceptance suite | PLANNED |

## Batch 10 — Operator polish & release acceptance

| Task | Description | Status |
|---|---|---|
| B200-091 | Enterprise operator help/navigation copy | PLANNED |
| B200-092 | Responsive enterprise operations CSS pass | PLANNED |
| B200-093 | Empty/degraded/error-state polish | PLANNED |
| B200-094 | Enterprise persistence/readiness status card | PLANNED |
| B200-095 | Maintenance/suppression operator runbook | PLANNED |
| B200-096 | Incident collaboration operator runbook | PLANNED |
| B200-097 | BATCH-100 -> BATCH-200 upgrade compatibility check | PLANNED |
| B200-098 | Deployment smoke/readiness contract update | PLANNED |
| B200-099 | BATCH-200 release-candidate acceptance suite | PLANNED |
| B200-100 | Canonical docs/ADR/status/release gate | PLANNED |

## Delivery rule

`Audit current behavior -> design bounded change -> implement -> Release build/tests -> canonical docs/status -> final PR CI -> squash merge -> next batch`.
