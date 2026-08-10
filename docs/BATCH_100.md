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

Batch 2 adds an optional shared encrypted ASP.NET Data Protection key ring over the dedicated Monitor state provider. A 256-bit key-encryption key comes from process environment and is never persisted in Monitor state. Credential migration uses Resolve → Test Connection → metadata commit → owned-secret cleanup, and current secret references are never rendered. `Deployment:MultiNode` remains fail-closed until later login-security and snapshot-cache strategy tasks are complete.

## Batch 3 — Backup, export & restore

| Task | Description | Status |
|---|---|---|
| B100-021 | Operational export contract | CI VERIFIED — RUN 31393040135 |
| B100-022 | Registration export | CI VERIFIED — RUN 31393040135 |
| B100-023 | Incident export | CI VERIFIED — RUN 31393040135 |
| B100-024 | History export | CI VERIFIED — RUN 31393040135 |
| B100-025 | Audit export | CI VERIFIED — RUN 31393040135 |
| B100-026 | Versioned manifest and checksums | CI VERIFIED — RUN 31393040135 |
| B100-027 | Import validation / dry-run | CI VERIFIED — RUN 31393040135 |
| B100-028 | Atomic restore workflow | CI VERIFIED — RUN 31393040135 |
| B100-029 | Backup retention/pruning | CI VERIFIED — RUN 31393040135 |
| B100-030 | Backup/restore readiness UI | CI VERIFIED — RUN 31393040135 |

Batch 3 exports a versioned canonical operational bundle containing safe registration metadata plus opaque secret references, bounded incidents, 24-hour aggregate history and bounded audit metadata. Each section has a SHA-256 checksum. Validation is mutation-free and rejects format, checksum, bound, referential-integrity and prohibited secret-bearing-property violations. Restore supports the selected File/Shared persistence backend, stages each write, and rolls previously applied sections back if a later section fails. File-backed restore reports restart-required instead of pretending already-loaded singleton state changed live. Protected credential ciphertext, Data Protection keys, provider connection material and monitored SQL text are excluded from the bundle contract.

## Batch 4 — Production observability

| Task | Description | Status |
|---|---|---|
| B100-031 | Application health endpoint | CI VERIFIED — RUN 31396619576 |
| B100-032 | Liveness probe | CI VERIFIED — RUN 31396619576 |
| B100-033 | Readiness probe | CI VERIFIED — RUN 31396619576 |
| B100-034 | Shared-state dependency readiness | CI VERIFIED — RUN 31396619576 |
| B100-035 | Collector telemetry | CI VERIFIED — RUN 31396619576 |
| B100-036 | Scheduler telemetry | CI VERIFIED — RUN 31396619576 |
| B100-037 | Snapshot-cache telemetry | CI VERIFIED — RUN 31396619576 |
| B100-038 | Incident telemetry | CI VERIFIED — RUN 31396619576 |
| B100-039 | Security/auth telemetry | CI VERIFIED — RUN 31396619576 |
| B100-040 | Correlation IDs + structured redacted logging | CI VERIFIED — RUN 31396619576 |

Batch 4 adds `/health/live`, `/health/ready`, `/health` and an Administrator observability surface without creating a monitored-SQL read path. Liveness has no external dependencies. Readiness evaluates configuration and Monitor-owned control-plane dependencies only; the dedicated shared-state provider is probed only when enabled. Runtime telemetry stores bounded aggregate counters/timestamps and allowlisted collector failure categories only. Unknown free-form failure text is reduced to `Unknown`, preventing provider/secret fragments from entering telemetry. Correlation IDs are strict bounded tokens or server-generated values, and request completion logging records method/status/elapsed time without query/body/credential data.

## Batch 5 — Performance & scale governance

| Task | Description | Status |
|---|---|---|
| B100-041 | Snapshot-cache size bounds | CI VERIFIED — RUN 31399632281 |
| B100-042 | History read paging/window cost bounds | CI VERIFIED — RUN 31399632281 |
| B100-043 | Audit paging cost bounds | CI VERIFIED — RUN 31399632281 |
| B100-044 | Incident query indexing/read limits | CI VERIFIED — RUN 31399632281 |
| B100-045 | Server estate paging | CI VERIFIED — RUN 31399632281 |
| B100-046 | Bounded concurrent manual refresh | CI VERIFIED — RUN 31399632281 |
| B100-047 | Scheduler jitter | CI VERIFIED — RUN 31399632281 |
| B100-048 | Collection batch limits | CI VERIFIED — RUN 31399632281 |
| B100-049 | SQL connection-pool governance | CI VERIFIED — RUN 31399632281 |
| B100-050 | Automated performance-budget tests | CI VERIFIED — RUN 31399632281 |

Batch 5 introduces explicit deterministic operating budgets instead of brittle microbenchmarks. Snapshot cache size is capped with deterministic oldest-entry eviction; history/audit/incidents and the server estate use bounded reads/pages; estate navigation Peeks cached snapshots only and never starts collection. Manual refresh adds an application-wide concurrency permit on top of existing per-registration/distributed single-flight. Scheduler cycles use bounded deterministic jitter and round-robin target batches to prevent synchronized bursts and starvation. The monitored snapshot collector opts into explicitly capped SQL pooling, while Test Connection remains non-pooled so credential tests cannot succeed from an old pooled session. CI includes executable budget tests for capacity, page size, cache-read count, concurrency, round-robin batching and pool bounds.

Batch 5 also corrected a production wiring gap discovered after Batch 4: the health/observability services and middleware existed but had not been wired into `Program.cs`. Runtime DI now registers telemetry/readiness and collector/cache/cycle/incident decorators plus correlation/auth-outcome middleware, so `/health*` and `/observability` are runtime-resolvable rather than unit-test-only.

## Batch 6 — DBA UX & operations surfaces

| Task | Description | Status |
|---|---|---|
| B100-051 | Dashboard HA/readiness banner | CI VERIFIED — RUN 31402491011 |
| B100-052 | Node identity/status surface | CI VERIFIED — RUN 31402491011 |
| B100-053 | Shared-state provider health card | CI VERIFIED — RUN 31402491011 |
| B100-054 | Backup readiness card | CI VERIFIED — RUN 31402491011 |
| B100-055 | Scheduler leader card | CI VERIFIED — RUN 31402491011 |
| B100-056 | Manual refresh progress/feedback hardening | CI VERIFIED — RUN 31402491011 |
| B100-057 | Connection recovery actions | CI VERIFIED — RUN 31402491011 |
| B100-058 | Incident filtering/navigation polish | CI VERIFIED — RUN 31402491011 |
| B100-059 | Keyboard/focus/accessibility pass | CI VERIFIED — RUN 31402491011 |
| B100-060 | Responsive DBA wallboard mode | CI VERIFIED — RUN 31402491011 |

Batch 6 adds a single `IDbaOperationsSurfaceService` that reuses one centralized readiness snapshot and combines only safe backup/scheduler metadata. Dashboard surfaces control-plane readiness, an opaque `NODE-XXXXXXXX` label, shared-state status/schema, operational-backup status and scheduler activity without rendering host names, provider endpoints, credentials or lease-owner IDs. Registered servers without a usable snapshot now open a recovery-aware details page instead of returning 404; recovery routes Administrators to Connection Lab and Operators/Admins to the existing bounded refresh path without showing secret references. Refresh PRG feedback carries status/freshness classification, the incident center has bounded filter/pager controls, the shell adds skip/focus/accessibility semantics, reduced-motion is respected, and large-screen wallboard behavior is CSS-only with no polling or SQL collection change.

## Batch 7 — Web/application security hardening

| Task | Description | Status |
|---|---|---|
| B100-061 | CSP nonce migration / reduce inline allowance | IMPLEMENTED — AWAITING FINAL CI |
| B100-062 | Antiforgery coverage test for all mutating routes | IMPLEMENTED — AWAITING FINAL CI |
| B100-063 | Session idle + absolute expiry policy | IMPLEMENTED — AWAITING FINAL CI |
| B100-064 | Account lockout/audit hardening | IMPLEMENTED — AWAITING FINAL CI |
| B100-065 | Trusted proxy/forwarded-header policy | IMPLEMENTED — AWAITING FINAL CI |
| B100-066 | Production HSTS validation | IMPLEMENTED — AWAITING FINAL CI |
| B100-067 | Security-header regression suite | IMPLEMENTED — AWAITING FINAL CI |
| B100-068 | Input-normalization fuzz/property tests | IMPLEMENTED — AWAITING FINAL CI |
| B100-069 | SQL connection metadata injection tests | IMPLEMENTED — AWAITING FINAL CI |
| B100-070 | Repository-wide secret/log canary suite | IMPLEMENTED — AWAITING FINAL CI |

Batch 7 centralizes the browser security boundary in `WebSecurityOptions`, `AbsoluteSessionCookieEvents`, `TrustedForwarderPolicy` and `SecurityHeadersMiddleware`. CSP removes `unsafe-inline`, emits a fresh per-request nonce and explicitly denies frames/objects while constraining form, image, script, style and connection sources. Mutating MVC/API actions are protected by an assembly-wide reflection gate. Cookie authentication retains bounded sliding idle renewal but carries an immutable session-start claim that enforces an absolute lifetime. Login limiter keys are SHA-256-derived from normalized IP/username material so raw IPs/usernames are not retained, and lockout outcomes are audited without credential material. Forwarded headers stay disabled unless trusted proxy/network configuration exists; configured forwarding has a one-hop limit and symmetry requirement. HSTS duration/subdomain policy is explicit and startup-validated. Registration/rule metadata rejects control characters, unsafe token syntax and connection-string delimiters where they could alter SQL target metadata. `SqlConnectionStringBuilder` acceptance tests prove application-name/user/password payloads remain values rather than injected keys, while secret canaries verify audit, telemetry and limiter keys do not echo sensitive input.

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
