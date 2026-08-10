# BATCH-100 — Production / Enterprise Execution Program

This file is the repository execution ledger for Issue #55. The program is delivered as ten bounded batches of ten tasks. Every batch requires merge-result GitHub Actions verification before merge to `main`.

## Global guardrails

- Browser monitoring GETs remain cache-only and never trigger monitored-SQL collection.
- Dedicated Monitor shared-state SQL is control-plane state only; it is never inferred from a monitored target.
- No plaintext credentials, complete connection strings, raw provider errors or arbitrary SQL text enter UI, audit or Monitor-owned operational state.
- Recommendations and AI remain human-review only with no autonomous SQL execution path.
- `main` stays stable; every batch uses a feature branch/PR and updates the canonical plan/status/decisions.

## Batch 1 — Shared state & HA foundation

| Task | Description | Status |
|---|---|---|
| B100-001 | Shared server-registration repository adapter | CI VERIFIED — RUN 31389275376 |
| B100-002 | Deterministic local-to-shared registration migration/import | CI VERIFIED — RUN 31389275376 |
| B100-003 | Shared audit store | CI VERIFIED — RUN 31389275376 |
| B100-004 | Shared incident lifecycle repository | CI VERIFIED — RUN 31389275376 |
| B100-005 | Shared snapshot-history store | CI VERIFIED — RUN 31389275376 |
| B100-006 | Distributed lease primitive | CI VERIFIED — RUN 31389275376 |
| B100-007 | Scheduler leader/ownership lease | CI VERIFIED — RUN 31389275376 |
| B100-008 | Cross-node snapshot-refresh single-flight | CI VERIFIED — RUN 31389275376 |
| B100-009 | Shared scheduler runtime status | CI VERIFIED — RUN 31389275376 |
| B100-010 | MultiNode readiness/activation gate | CI VERIFIED — RUN 31389275376 |

Batch 1 deliberately keeps `Deployment:MultiNode` fail-closed after evaluating the real cross-field state. Protected local SQL credentials/Data Protection key ring, login security state and node-local snapshot cache still block MultiNode activation and are handled by later batches.

## Batch 2 — HA secret & key management

| Task | Description | Status |
|---|---|---|
| B100-011 | Shared Data Protection key-ring boundary | CI VERIFIED — RUN 31391446513 |
| B100-012 | External/shared key-ring provider selection | CI VERIFIED — RUN 31391446513 |
| B100-013 | Prohibit node-local `local:v1` credentials in MultiNode | CI VERIFIED — RUN 31391446513 |
| B100-014 | Credential migration command from local ownership to external reference | CI VERIFIED — RUN 31391446513 |
| B100-015 | Safe secret-reference replacement workflow | CI VERIFIED — RUN 31391446513 |
| B100-016 | Orphaned owned-secret cleanup | CI VERIFIED — RUN 31391446513 |
| B100-017 | Credential readiness/health projection | CI VERIFIED — RUN 31391446513 |
| B100-018 | Secret-rotation metadata and audit | CI VERIFIED — RUN 31391446513 |
| B100-019 | Automatic bounded connection re-test after credential replacement | CI VERIFIED — RUN 31391446513 |
| B100-020 | HA credential security acceptance suite | CI VERIFIED — RUN 31391446513 |

Batch 2 adds an optional shared encrypted ASP.NET Data Protection key ring over the dedicated Monitor state provider. A 256-bit key-encryption key comes from process environment and is never persisted in Monitor state. Credential migration uses Resolve → Test Connection → metadata commit → owned-secret cleanup, and current secret references are never rendered. `Deployment:MultiNode` remains fail-closed until the later login-security and snapshot-cache strategy tasks are complete.

## Batch 3 — Backup, export & restore

| Task | Description | Status |
|---|---|---|
| B100-021 | Operational export contract | PLANNED |
| B100-022 | Registration export | PLANNED |
| B100-023 | Incident export | PLANNED |
| B100-024 | History export | PLANNED |
| B100-025 | Audit export | PLANNED |
| B100-026 | Versioned manifest and checksums | PLANNED |
| B100-027 | Import validation / dry-run | PLANNED |
| B100-028 | Atomic restore workflow | PLANNED |
| B100-029 | Backup retention/pruning | PLANNED |
| B100-030 | Backup/restore readiness UI | PLANNED |

## Batch 4 — Production observability

| Task | Description | Status |
|---|---|---|
| B100-031 | Application health endpoint | PLANNED |
| B100-032 | Liveness probe | PLANNED |
| B100-033 | Readiness probe | PLANNED |
| B100-034 | Shared-state dependency readiness | PLANNED |
| B100-035 | Collector telemetry | PLANNED |
| B100-036 | Scheduler telemetry | PLANNED |
| B100-037 | Snapshot-cache telemetry | PLANNED |
| B100-038 | Incident telemetry | PLANNED |
| B100-039 | Security/auth telemetry | PLANNED |
| B100-040 | Correlation IDs + structured redacted logging | PLANNED |

## Batch 5 — Performance & scale governance

| Task | Description | Status |
|---|---|---|
| B100-041 | Snapshot-cache size bounds | PLANNED |
| B100-042 | History read paging/window cost bounds | PLANNED |
| B100-043 | Audit paging cost bounds | PLANNED |
| B100-044 | Incident query indexing/read limits | PLANNED |
| B100-045 | Server estate paging | PLANNED |
| B100-046 | Bounded concurrent manual refresh | PLANNED |
| B100-047 | Scheduler jitter | PLANNED |
| B100-048 | Collection batch limits | PLANNED |
| B100-049 | SQL connection-pool governance | PLANNED |
| B100-050 | Automated performance-budget tests | PLANNED |

## Batch 6 — DBA UX & operations surfaces

| Task | Description | Status |
|---|---|---|
| B100-051 | Dashboard HA/readiness banner | PLANNED |
| B100-052 | Node identity/status surface | PLANNED |
| B100-053 | Shared-state provider health card | PLANNED |
| B100-054 | Backup readiness card | PLANNED |
| B100-055 | Scheduler leader card | PLANNED |
| B100-056 | Manual refresh progress/feedback hardening | PLANNED |
| B100-057 | Connection recovery actions | PLANNED |
| B100-058 | Incident filtering/navigation polish | PLANNED |
| B100-059 | Keyboard/focus/accessibility pass | PLANNED |
| B100-060 | Responsive DBA wallboard mode | PLANNED |

## Batch 7 — Web/application security hardening

| Task | Description | Status |
|---|---|---|
| B100-061 | CSP nonce migration / reduce inline allowance | PLANNED |
| B100-062 | Antiforgery coverage test for all mutating routes | PLANNED |
| B100-063 | Session idle + absolute expiry policy | PLANNED |
| B100-064 | Account lockout/audit hardening | PLANNED |
| B100-065 | Trusted proxy/forwarded-header policy | PLANNED |
| B100-066 | Production HSTS validation | PLANNED |
| B100-067 | Security-header regression suite | PLANNED |
| B100-068 | Input-normalization fuzz/property tests | PLANNED |
| B100-069 | SQL connection metadata injection tests | PLANNED |
| B100-070 | Repository-wide secret/log canary suite | PLANNED |

## Batch 8 — Reliability & concurrency verification

| Task | Description | Status |
|---|---|---|
| B100-071 | Shared-state fault injection harness | PLANNED |
| B100-072 | Lease-loss/re-election test | PLANNED |
| B100-073 | Dedicated state DB outage/recovery test | PLANNED |
| B100-074 | Partial migration interruption/restart test | PLANNED |
| B100-075 | Concurrent incident transition test | PLANNED |
| B100-076 | Concurrent audit append test | PLANNED |
| B100-077 | Cross-node history dedupe test | PLANNED |
| B100-078 | Cross-node registration conflict test | PLANNED |
| B100-079 | Distributed refresh single-flight acceptance test | PLANNED |
| B100-080 | Multi-node soak simulation harness | PLANNED |

## Batch 9 — Deployment & operations documentation/tooling

| Task | Description | Status |
|---|---|---|
| B100-081 | Production configuration template | PLANNED |
| B100-082 | IIS deployment guide | PLANNED |
| B100-083 | Windows Service deployment guide | PLANNED |
| B100-084 | Reverse-proxy deployment guide | PLANNED |
| B100-085 | Dedicated Monitor state DB least-privilege SQL script | PLANNED |
| B100-086 | Monitored SQL least-privilege permissions script | PLANNED |
| B100-087 | Upgrade/migration checklist | PLANNED |
| B100-088 | Release versioning automation | PLANNED |
| B100-089 | Deployment smoke-test script | PLANNED |
| B100-090 | Rollback/recovery runbook | PLANNED |

## Batch 10 — Enterprise operator features & RC acceptance

| Task | Description | Status |
|---|---|---|
| B100-091 | Maintenance-window model | PLANNED |
| B100-092 | Server tags/groups | PLANNED |
| B100-093 | Environment classification | PLANNED |
| B100-094 | Alert suppression windows | PLANNED |
| B100-095 | Incident ownership/assignee metadata | PLANNED |
| B100-096 | Bounded incident operator notes | PLANNED |
| B100-097 | Recommendation acknowledgment state | PLANNED |
| B100-098 | Safe CSV report export | PLANNED |
| B100-099 | Redacted diagnostics package | PLANNED |
| B100-100 | Release-candidate acceptance suite | PLANNED |

## Delivery rule

`Plan -> Design -> Implement -> Merge-result CI -> Docs/Status -> Final CI -> Merge -> Next batch`.
