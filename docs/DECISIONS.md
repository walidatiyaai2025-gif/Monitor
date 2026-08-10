# Decisions

## ADR-001 — Visual product before deep collectors
M0 delivered a working product surface before broad collector depth.

## ADR-002 — Snapshot-first architecture
Monitoring values are centrally collected into reusable snapshots; widgets do not independently query SQL Servers.

## ADR-003 — One central live area
The SQL Command Center owns the live visual language; detail pages consume snapshot state.

## ADR-004 — Motion is not collection
Client heartbeat/clocks/transitions never increase SQL collection frequency.

## ADR-005 — Development authentication
Development Admin uses cookie auth and PBKDF2 hash/salt; plaintext password is not committed.

## ADR-006 — AI remains advisory
Advisor output explains/suggests and cannot autonomously execute production SQL.

## ADR-007 — Registration metadata is separate from connection secrets
Registration stores endpoint/auth metadata plus opaque references, never plaintext credential values/full connection strings.

## ADR-008 — Test Connection is bounded and redacted
Connection tests are authorized backend actions with bounded timeouts and fixed safe result categories.

## ADR-009/010 — Collector output is reusable and cache/single-flight controlled
One backend collection result feeds multiple consumers; cache freshness and single-flight prevent fan-out.

## ADR-011/012 — Real data is labeled and refresh is explicit
Unavailable real targets remain visible. Manual refresh is authorized, throttled and backend-controlled.

## ADR-013 — SignalR is delivery-only and deferred
SignalR may later deliver independently produced snapshots; it must not trigger collection.

## ADR-014..016 — Health modules are bounded cached facts
Memory/database/backup/Agent/storage/blocking/performance are bounded snapshot facts consumed through shared reads.

## ADR-017/018 — Incidents are deterministic; recommendations never execute
Only fresh evidence resolves incidents. Operator transitions are explicit protected POST actions. Recommendations remain human-review-only.

## ADR-019/023 — Advisor is a guarded backend boundary
Advisor context is bounded, explicitly requested, timeout/cache/circuit controlled and metadata-audited.

## ADR-020/021 — History and scheduler are bounded
History is aggregate/retention-bounded. Scheduler infrastructure is disabled by default and failure-isolated.

## ADR-022 — Authorization uses named policies
Viewer/Operator/Administrator capabilities are explicit and unsafe actions remain POST + antiforgery.

## ADR-024/025 — Commissioning is deliberate and real estate replaces demo as a whole
Register -> Test -> first collection -> observer -> real estate is backend-controlled; unavailable real targets remain visible.

## ADR-026 — Incident audit enrichment must not break workflow contracts
Canonical incident transitions retain their interface while bounded before/after state enriches audit when available.

## ADR-027 — Persist registration metadata, never credential values
Registration file state is versioned/atomic/outside `wwwroot` and stores opaque references only.

## ADR-028 — A provider-owned secret reference never downgrades
`env:` references resolve from process environment; missing provider-owned values do not fall through to appsettings.

## ADR-029 — Durable operational state is split by state machine and committed before publication
Audit/history/incidents use independent versioned state and candidate-state atomic commit; invalid state fails closed.

## ADR-030 — Multi-node intent fails closed until shared state and coordination are real
`SingleNode` is supported. `MultiNode` startup is rejected while persistence/coordination remains node-local. Local/network files are not distributed coordination substitutes.

## ADR-031 — Monitoring GETs are zero-SQL reads
Dashboard, Servers, Server Details, health modules and incident navigation consume cache/Peek state only. Collection requires an explicit authorized backend action. Successful explicit refresh observation occurs exactly once.

## ADR-032 — UI-entered SQL Login credentials use protected local persistence
Server-generated `local:v1` references identify Data Protection ciphertext stored atomically outside `wwwroot`. A persistent reference-scoped key ring enables restart resolution; lost keys/tampering fail closed. The store remains node-local.

## ADR-033 — Shared state uses a dedicated Monitor-owned SQL Server control-plane provider
M7-017 introduces `ISharedStateDocumentStore` and a real SQL Server backend that is configured separately from monitored targets. Connection-string values are read only from a named process environment variable. Runtime code does not execute DDL; schema v1 is deployed explicitly.

Shared writes use bounded valid JSON documents and optimistic compare/exchange under SQL `SERIALIZABLE` + `UPDLOCK/HOLDLOCK`. Stale writers conflict rather than overwrite. Provider errors are redacted. Readiness exposes provider/schema status only.

A READY shared-state provider is **not** permission to enable MultiNode. Required repositories, coordination, security state and cache/delivery prerequisites must all pass the deployment readiness gate.

## ADR-034 — Shared key rings are encrypted before control-plane persistence and credential rotation is test-before-commit
B100-011..020 separates Data Protection key-ring location from SQL credential values. `LocalFile` remains the backward-compatible single-node default. Explicit `SharedState` key-ring mode writes only AES-256-GCM ciphertext to the dedicated Monitor control-plane provider; a 256-bit key-encryption key is read directly from a named process environment variable and is never stored in Monitor state, source, UI or audit. Missing, invalid or wrong key material fails closed and SharedState mode never falls back to local key files.

HA credential readiness additionally requires new Monitor-owned `local:v1` credential creation to be disabled and no registration to retain a local-owned reference. Existing external secret providers remain the credential-value boundary.

Credential reference replacement is an explicit Administrator command. The candidate external reference is resolved and passes the existing bounded Test Connection **before** registration metadata changes. Failure preserves the old registration and owned secret. Success commits the opaque reference first, then removes the previous Monitor-owned secret if it became orphaned. Cleanup is ownership-scoped and never mutates external provider secrets. Audit stores actor/action/registration/outcome metadata only and excludes old/new references, usernames, passwords, provider errors and connection strings.

## ADR-035 — Operational backup uses a canonical checksummed bundle and restore is staged/rollback-capable
B100-021..030 defines one versioned backup contract over the safe Monitor-owned operational domain instead of copying implementation files blindly. The bundle contains registration metadata plus opaque secret references, bounded incident lifecycle state, bounded 24-hour aggregate history and bounded audit metadata. Protected SQL credential ciphertext, Data Protection keys/KEKs, provider connection material, raw provider errors and monitored SQL text are not backup sections.

Each section is serialized deterministically and covered by a SHA-256 manifest checksum. Dry-run validation verifies bundle/file identity, format versions, hashes, section limits, domain bounds and cross-section registration references before any mutation. Backup IDs are strict tokens rather than paths, and backup files are written under a configured root outside `wwwroot` using write-through same-directory staging plus atomic replacement and bounded retention.

Restore targets the persistence mode already selected by deployment configuration. File-backed sections are written atomically and require an application restart before Monitor resumes operations because existing singleton repositories are not mutated in-place. Shared-state sections use optimistic compare/exchange. Every applied section captures its prior durable payload; if any later section fails, applied sections are rolled back in reverse order. A concurrent shared-state change is a conflict, never an overwrite. InMemory persistence remains exportable for diagnostics but is explicitly not a restart-safe restore target.

## ADR-036 — Health and observability are control-plane-only, bounded and redacted
B100-031..040 defines production health and runtime telemetry without adding a new monitored-SQL query path. `/health/live` is process-only liveness. `/health/ready` evaluates deployment configuration plus Monitor-owned control-plane dependencies and probes the dedicated shared-state provider only when configured. `/health` exposes a bounded aggregate status. None of these endpoints invokes the monitored SQL collector, cache refresh or scheduler.

Telemetry is intentionally aggregate-only: collector/cache/scheduler/incident/authentication counters, timestamps and finite status categories. Collector failure categories use a strict allowlist derived from `SnapshotCollectionFailure` plus `Unexpected`; arbitrary free-form input is stored only as `Unknown`. Incident evidence, SQL text, usernames, passwords, IP addresses, secret references, connection strings, provider endpoints, request bodies and raw provider exceptions are excluded from telemetry state.

`X-Correlation-ID` accepts only a bounded alphanumeric/`.`/`_`/`-` token; unsafe or missing values are replaced with a server-generated identifier. Structured request completion logging uses the safe correlation scope and records only HTTP method, response status and elapsed time. The Administrator observability page is a read-only aggregate view and does not trigger collection.

## ADR-037 — Scale is governed by deterministic budgets, not timing-dependent microbenchmarks
B100-041..050 makes estate growth explicit through configuration-backed hard bounds: snapshot-cache capacity, history/audit/incident page sizes, server-estate paging, global manual-refresh concurrency, targets per scheduler cycle and monitored-SQL pool size/lifetime. Capacity tests assert deterministic counts and concurrency limits rather than machine-specific elapsed-time thresholds.

Cache eviction removes the oldest retained snapshot first, with registration ID as a deterministic tie-breaker. Server-estate paging evaluates registration metadata and Peeks only the requested page; moving between pages remains a zero-monitored-SQL GET. History is constrained to the existing 24-hour aggregate retention domain, while audit and incident reads retain strict maximum output sizes.

Manual refresh uses an application-wide non-blocking concurrency permit in addition to registration throttling and distributed refresh single-flight. Scheduler cycles add bounded deterministic jitter and select round-robin target batches so large estates neither synchronize collection bursts nor permanently starve later registrations.

Only monitored background snapshot collection opts into bounded SQL connection pooling. Test Connection deliberately remains non-pooled so a credential rotation/test cannot appear valid because an old authenticated pooled session was reused.

During Batch 5 review, a Batch 4 integration gap was found: observability controllers/services existed, but their DI/decorator/middleware wiring had not reached `Program.cs`. Batch 5 treats runtime wiring as part of acceptance, registering telemetry/readiness, collector/cache/cycle/incident decorators, correlation middleware and authentication-outcome telemetry before marking the batch verified.

## ADR-038 — DBA operations UI uses one safe control-plane projection and recovery never exposes secrets
B100-051..060 introduces `IDbaOperationsSurfaceService` as the single Dashboard projection for deployment readiness, node label, shared-state health, operational backup state and scheduler runtime. Shared-state status/schema is carried in the existing application-readiness result so Dashboard widgets do not multiply control-plane probes. None of these cards invokes the monitored SQL collector.

The visible node label is an opaque SHA-256-derived `NODE-XXXXXXXX` token. The machine name, configured distributed node ID and lease owner are intentionally not rendered. Backup UI is limited to readiness/count/time plus opaque backup identifiers; scheduler UI is limited to enabled/running/passive state and aggregate counts.

A registered server with no usable cached snapshot is still an operational object, not a 404. Server Details therefore returns a recovery-oriented model using safe registration metadata only. Administrators can move to Connection Lab for bounded retest/reference replacement and Operators/Admins can request the existing bounded refresh. Current SQL usernames/passwords and secret references remain outside the recovery surface.

Refresh status/freshness is carried through PRG TempData only as bounded enum-derived classification and a safe service message. Incident filtering is bounded/paged. Accessibility and wallboard behavior are view/CSS concerns: skip/focus semantics, live-status regions, reduced-motion support and wide-screen layout never change polling, collection frequency or monitored-SQL traffic.

## ADR-039 — Browser trust, authenticated lifetime and SQL metadata are explicit fail-closed policies
B100-061..070 replaces scattered web-security literals with one validated `WebSecurityOptions` boundary plus dedicated middleware/events. Browser response headers are emitted from `SecurityHeadersMiddleware`; CSP does not allow `unsafe-inline` or `unsafe-eval`, denies frames/objects, constrains forms/resources and carries a cryptographically random per-request nonce. A regression test reflects over all controllers and fails if a mutating action is introduced without antiforgery protection.

Cookie authentication has two independent clocks. Sliding renewal is bounded by the configured idle lifetime, while the first successful sign-in receives an immutable session-start claim. `AbsoluteSessionCookieEvents` rejects principals after the configured absolute lifetime even if the cookie would otherwise renew. Missing, malformed or future session-start metadata fails closed.

Forwarded headers are not enabled merely because an `X-Forwarded-*` header is present. Monitor processes only `X-Forwarded-For` and `X-Forwarded-Proto`, only when deployment configuration contains an explicit trusted proxy or CIDR, with one-hop and header-symmetry limits. HSTS duration/subdomain/preload behavior is similarly explicit and startup-validated rather than relying on hidden framework defaults.

Login limiting does not store raw remote IP or username in its key: normalized values are SHA-256-derived into an opaque token. Audit input is bounded, control-character normalized and redacts secret-bearing connection/credential patterns. SQL host/instance registration metadata rejects delimiters/control characters that could alter target connection metadata; all connection-string composition remains through `SqlConnectionStringBuilder`, and acceptance tests prove application name, SQL username and password strings remain values rather than injected keys. These controls do not add any monitored-SQL GET path and do not weaken the existing advisory-only execution boundary.
