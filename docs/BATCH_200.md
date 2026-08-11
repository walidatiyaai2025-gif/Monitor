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
| B200-001 | Add Enterprise Operations primary navigation entry | CI VERIFIED — RUN 31443481889 |
| B200-002 | Active navigation / role-aware enterprise shell state | CI VERIFIED — RUN 31443481889 |
| B200-003 | Surface environment/group/tags on server details | CI VERIFIED — RUN 31443481889 |
| B200-004 | Surface active maintenance/suppression state on server details | CI VERIFIED — RUN 31443481889 |
| B200-005 | Surface assignee/notes/recommendation acknowledgment on incident details | CI VERIFIED — RUN 31443481889 |
| B200-006 | Server estate environment/group/tag filters | CI VERIFIED — RUN 31443481889 |
| B200-007 | Incident assignee/suppression filters | CI VERIFIED — RUN 31443481889 |
| B200-008 | Validation-error UX with safe bounded messages | CI VERIFIED — RUN 31443481889 |
| B200-009 | Audit coverage for all enterprise metadata mutations | CI VERIFIED — RUN 31443481889 |
| B200-010 | Enterprise UX/accessibility acceptance suite | CI VERIFIED — RUN 31443481889 |

Batch 1 integrates the BATCH-100 enterprise governance state into the normal operator journey. Server and incident details project operator metadata without invoking collection, `/enterprise` has bounded metadata-only filters, rejected mutations use PRG with bounded messages and rejection audit metadata, and all enterprise mutations remain POST + antiforgery + named authorization policy.

## Batch 2 — Maintenance & suppression policy semantics

| Task | Description | Status |
|---|---|---|
| B200-011 | Scheduler skips targets in active maintenance windows | CI VERIFIED — RUN 31444314976 |
| B200-012 | Manual refresh during maintenance remains explicit/audited | CI VERIFIED — RUN 31444314976 |
| B200-013 | Fleet/server projections expose maintenance-active state | CI VERIFIED — RUN 31444314976 |
| B200-014 | Alert suppression projection without evidence mutation | CI VERIFIED — RUN 31444314976 |
| B200-015 | Actionable-vs-suppressed incident counts | CI VERIFIED — RUN 31444314976 |
| B200-016 | Automatic suppression expiry semantics | CI VERIFIED — RUN 31444314976 |
| B200-017 | Start-inclusive/end-exclusive boundary tests | CI VERIFIED — RUN 31444314976 |
| B200-018 | Cross-node policy consistency | CI VERIFIED — RUN 31444314976 |
| B200-019 | Corrupt policy metadata fails closed | CI VERIFIED — RUN 31444314976 |
| B200-020 | Maintenance/suppression acceptance suite | CI VERIFIED — RUN 31444314976 |

Batch 2 turns operator windows into explicit backend policy semantics. Scheduled collection skips active maintenance and fails closed when operator policy cannot be read; manual refresh remains an explicit operator override and is audited before/after execution. Alert suppression changes actionability projections only and leaves incident status/evidence untouched. Policy windows are start-inclusive/end-exclusive and shared-state readers converge across nodes.

## Batch 3 — Incident collaboration workflow

| Task | Description | Status |
|---|---|---|
| B200-021 | Assignee-aware incident query/filter | CI VERIFIED — RUN 31444920282 |
| B200-022 | Owner-change audit timeline | CI VERIFIED — RUN 31444920282 |
| B200-023 | Bounded note paging | CI VERIFIED — RUN 31444920282 |
| B200-024 | Immutable operator-note identity validation | CI VERIFIED — RUN 31444920282 |
| B200-025 | Note replay/idempotency protection | CI VERIFIED — RUN 31444920282 |
| B200-026 | Incident age/SLA bucket projection | CI VERIFIED — RUN 31444920282 |
| B200-027 | Severity escalation marker/history | CI VERIFIED — RUN 31444920282 |
| B200-028 | Bounded reopen reason | CI VERIFIED — RUN 31444920282 |
| B200-029 | Bounded resolution note | CI VERIFIED — RUN 31444920282 |
| B200-030 | Collaboration workflow acceptance suite | CI VERIFIED — RUN 31444920282 |

Batch 3 adds bounded incident collaboration over the existing durable operator metadata and audit stores. Assignee filtering and deterministic SLA buckets are service-level projections; owner changes create previous-to-next audit history; notes support bounded paging and durable audit receipts for replay protection; note identity remains immutable; Warning-to-Critical escalation has an explicit audit marker; reopen/resolution reasons are validated operator notes separate from incident evidence; and the primary incident UI exposes reason-aware protected transition paths.

## Batch 4 — Reporting & diagnostics expansion

| Task | Description | Status |
|---|---|---|
| B200-031 | Filtered server CSV export | CI VERIFIED — RUN 31445480775 |
| B200-032 | Formula-safe incident CSV export | CI VERIFIED — RUN 31445480775 |
| B200-033 | Bounded history CSV export | CI VERIFIED — RUN 31445480775 |
| B200-034 | Administrator audit CSV export | CI VERIFIED — RUN 31445480775 |
| B200-035 | Versioned deterministic export schemas | CI VERIFIED — RUN 31445480775 |
| B200-036 | Explicit export row/size caps | CI VERIFIED — RUN 31445480775 |
| B200-037 | UTF-8/BOM compatibility tests | CI VERIFIED — RUN 31445480775 |
| B200-038 | Spreadsheet-formula injection matrix | CI VERIFIED — RUN 31445480775 |
| B200-039 | Diagnostics manifest build/revision metadata | CI VERIFIED — RUN 31445480775 |
| B200-040 | Export/diagnostics acceptance suite | CI VERIFIED — RUN 31445480775 |

Batch 4 introduces the versioned `monitor-export-v2` contract with explicit row/byte/cell caps, UTF-8 BOM emission, deterministic LF line endings and spreadsheet-formula neutralization. Server, incident, history and audit exports remain Monitor-owned/cache-only; the server report proves `Peek`-only snapshot access and excludes monitored SQL endpoints/secret references. Administrator diagnostics exposes a bounded build/revision manifest without environment values. The BOM regression test caught and corrected the .NET encoding preamble assumption before merge.

## Batch 5 — Fleet intelligence

| Task | Description | Status |
|---|---|---|
| B200-041 | Health summary by environment | CI VERIFIED — RUN 31446020409 |
| B200-042 | Health summary by server group | CI VERIFIED — RUN 31446020409 |
| B200-043 | Health summary by tag | CI VERIFIED — RUN 31446020409 |
| B200-044 | Stale/unavailable snapshot counts | CI VERIFIED — RUN 31446020409 |
| B200-045 | Active maintenance counts | CI VERIFIED — RUN 31446020409 |
| B200-046 | Active suppression counts | CI VERIFIED — RUN 31446020409 |
| B200-047 | Incident hot-spots by deterministic rule | CI VERIFIED — RUN 31446020409 |
| B200-048 | Backup-risk fleet summary | CI VERIFIED — RUN 31446020409 |
| B200-049 | Memory/blocking/performance risk summary | CI VERIFIED — RUN 31446020409 |
| B200-050 | Fleet intelligence zero-monitored-SQL acceptance suite | CI VERIFIED — RUN 31446020409 |

Batch 5 provides cache-only fleet intelligence by environment, group and tag; freshness/unavailable, maintenance and suppression counts; deterministic incident rule hot-spots; and backup/memory/blocking/runnable risk summaries. The B200-050 acceptance gate uses a cache fake that rejects `GetAsync`/`RefreshAsync`, proving fleet reads use `Peek` only. Verification run `31446020409` passed Release warnings-as-errors and 281/281 tests (0 failed).

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
