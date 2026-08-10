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

A READY M7-017 provider is **not** permission to enable MultiNode. M7-018 must migrate required repositories and distributed coordination first.
