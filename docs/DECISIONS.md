# Decisions

## ADR-001 — Visual product before deep collectors

M0 delivers a working UI preview before broad SQL monitoring capability. This allows product direction to be validated early.

## ADR-002 — Snapshot-first architecture

Monitoring values will be centrally collected into reusable snapshots. UI widgets do not independently query SQL Servers.

## ADR-003 — One central live area

The SQL Command Center owns the main live visual language. Detailed pages consume the latest snapshot and show snapshot age.

## ADR-004 — Motion is not collection

Heartbeat, clocks and transitions are client-side presentation. They must never increase SQL collection frequency.

## ADR-005 — Development authentication

M0 uses a single Administrator and ASP.NET Core cookie auth. The agreed development password is represented only by a PBKDF2-SHA256 hash/salt in source control.

## ADR-006 — AI remains advisory

Future AI integration will receive normalized evidence and propose explanations/remediation/query suggestions. It will not autonomously execute production SQL.

## ADR-007 — Registration metadata is separate from connection secrets

Server registrations may contain endpoint and authentication-mode metadata, but never passwords or full connection strings. Secret values are resolved through a backend-only boundary from external secret configuration. Registration JSON omits even the opaque secret reference, and missing secrets fail closed.

## ADR-008 — Test Connection is bounded and redacted

Test Connection accepts a server registration ID only and runs exclusively in the authorized backend. It uses `Microsoft.Data.SqlClient`, explicit connection/overall timeouts, cancellation, no pooling, and fixed safe result messages. Raw provider exceptions, credentials and connection strings never cross the service boundary.

## ADR-009 — The first collector uses one reusable query result

The lightweight collector issues one bounded command for SQL identity, uptime and database counts. One row feeds the whole identity snapshot; it does not fan out into widget or per-database queries. Invalid or partial rows fail safely instead of inventing healthy values.

## ADR-010 — Snapshot cache is fresh/stale and single-flight

Cached server snapshots are fresh for 30 seconds and retained as an explicitly stale fallback for five minutes. Concurrent refresh requests for the same registration await one shared collection task. Refresh failure never overwrites the last good value, and older collection timestamps never replace newer snapshots.

## ADR-011 — Mixed real and demo data is labeled per card

The first configured server may replace one demo estate card from the backend snapshot cache. Every card carries an explicit Demo, LiveFresh or LiveStale source. Missing real dimensions are presented as not collected, never filled with preview numbers. If no configuration or usable snapshot exists, the unchanged estate remains explicitly development data.

## ADR-012 — Manual refresh is backend-controlled and throttled

Snapshot refresh is an administrator POST accepting only a registration ID. A 15-second atomic per-server throttle rejects repeated requests before collection, while the cache single-flight coalesces concurrent accepted work. The browser cannot provide SQL text, endpoints, credentials or collection frequency.

## ADR-013 — SignalR delivery is deferred until snapshots are published independently

SignalR is not added in M1 because snapshots are currently produced on request and there is no scheduled publisher, so a hub would add reconnect/authentication/state complexity without carrying independently produced updates. Revisit when multiple consumers need independently produced updates or measured polling load justifies push. SignalR, if adopted, is delivery-only and must never invoke collectors or alter refresh frequency.

## ADR-014 — Memory health extends the existing collector row

M2 memory data is projected from `sys.dm_os_sys_memory` and `sys.dm_os_process_memory` inside the existing bounded collector command. It does not add a widget query or second connection. The snapshot stores raw validated facts; thresholds, alerts, UI and history remain later tasks.

## ADR-015 — Health modules remain bounded snapshot facts

Database states, backup coverage, SQL Agent, storage allocation and blocking are immutable optional modules on the canonical snapshot. One fixed backend command collects aggregate facts under the existing timeout/cache boundary. Browsers cannot supply SQL or trigger per-widget queries. Negative, inconsistent or overflowing values fail through the redacted collector boundary. Full-detail lists, history and UI policy remain separate slices.

## ADR-016 — Module pages consume one shared cached projection

Database, backup, SQL Agent, storage and blocking pages consume immutable module facts through `IMonitorReadService`. Null means not collected, stale remains explicit, and storage allocation is never presented as disk capacity. Controllers and browser code cannot call collectors or provide SQL.

## ADR-017 — Findings are deterministic and incidents resolve only from fresh evidence

The rule evaluator emits allowlisted IDs, severities and compact evidence from snapshots only. Incident identity is registration plus rule ID. Repeated evidence updates one record; older observations are ignored. Missing, stale or failed collection cannot resolve an incident. Only a newer fresh evaluation without the finding may resolve it. The initial repository is intentionally in-memory and executes no remediation.

## ADR-018 — Incident commands are explicit and advisory recommendations never execute

Operator transitions are authorized POST actions protected by antiforgery and accept only an incident ID. Recommendation text is selected from a server-owned rule catalog. It is rendered as human-review guidance and has no path to the SQL client, collector or connection lab.

## ADR-019 — AI integration starts as a disabled backend boundary

Advisor context contains only normalized rule metadata, bounded evidence and deterministic recommendation text. The provider is backend-only and disabled by default. The UI displays provider status and cannot execute output, SQL or remediation. External model integration requires a later explicit configuration and security review.

## ADR-020 — History is bounded aggregate evidence

History stores only allowlisted snapshot aggregates, deduplicated by registration and collection time. The in-memory phase retains at most 288 points per server for 24 hours. Fixed-window reads never trigger collection. The schedule policy validates safe bounds and remains disabled by default; no hosted timer is activated in this slice.

## ADR-021 — Scheduled collection is disabled by default and failure-isolated

The hosted scheduler validates interval and concurrency at startup, performs no immediate collection, and exits without cycles unless explicitly enabled. Cycles are sequential at the host level and bounded-parallel per server. Each successful snapshot is observed once; categorized server failures are isolated and receive capped exponential backoff. Runtime status exposes counts and timestamps only.

## ADR-022 — Monitoring authorization uses named policies

Viewer, Operator and Administrator roles map to explicit read, incident operation, connection management and advisor request policies. Unsafe operator and advisor actions remain POST plus antiforgery. Cookies are HttpOnly, strict SameSite and always secure outside Development. Responses receive a baseline CSP, frame denial, nosniff and no-referrer headers.

## ADR-023 — Advisor requests are explicit, bounded and audited

Incident detail reads do not grant execution capability. An Operator or Administrator explicitly POSTs an incident ID to request advice. The backend builds context, coalesces duplicate requests, caches only the matching evidence version for five minutes, applies a ten-second timeout and opens a short circuit after repeated failures. Audit stores metadata/status only, never raw prompts, credentials or provider exceptions.

## ADR-024 — First-run commissioning is one deliberate backend workflow

After login, an administrator with no enabled registration is routed to Connections. Saving a target performs a bounded Test Connection and, only on success, one first snapshot collection through the shared cache and observer before redirecting to Servers. SQL Login credentials entered in this preview are stored only in process memory under a server-generated opaque reference; they never enter registration metadata, JSON, HTML, logs or audit, and disappear on restart. Production deployments should use the external configuration reference boundary.

## ADR-025 — Real registrations replace the demo estate as a whole

When no registration exists, the visual demo remains explicitly labeled. Once real registrations exist, estate reads return every enabled registration in deterministic order. A target without a usable snapshot remains visible as `RegisteredUnavailable`; it is never silently replaced by demo data. Dashboard and server pages share this projection.

## ADR-026 — Incident audit enrichment must not break the workflow contract

M5-026 keeps the existing boolean `IIncidentWorkflowService` transition API unchanged. The authorized controller reads the canonical `IHealthIncidentRepository` immediately before and after the existing atomic status transition and records only bounded state metadata in the existing `IAuditStore`. The canonical audit action remains `incident.transition`. If repository state cannot be observed, the audit outcome falls back to the established `applied` / `conflict` values instead of inventing state. Missing authenticated actor identity fails closed before mutation. Incident evidence, SQL text, credentials, endpoints, provider errors and arbitrary request payloads are excluded from transition audit metadata.

## ADR-027 — Persist registration metadata, never runtime credential values

M7-001 makes the existing `IServerRegistrationRepository` durable without broadening the secret boundary. The default implementation writes a versioned Monitor-owned metadata file outside `wwwroot`; the application may still select the in-memory implementation through configuration. The persisted contract includes endpoint/authentication metadata and, for SQL Login registrations, the opaque `ConnectionSecretReference` only. It never contains SQL usernames, passwords or full connection strings.

File mutations are serialized, flushed to a same-directory temporary file and atomically moved into place. A failed write restores the prior in-memory state. Malformed, unsupported or domain-invalid persisted data fails application startup rather than silently producing an empty estate. Runtime credentials created by the M6 commissioning UI remain process-memory only and therefore intentionally cannot survive restart; their persisted opaque references become unresolved until an operator supplies a new credential or migrates the registration to an external enterprise secret reference. A future shared/HA repository may replace the file implementation behind the unchanged interface.
