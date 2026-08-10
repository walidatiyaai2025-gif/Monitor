# Decisions

## ADR-001 — Visual product before deep collectors

M0 delivers a working UI preview before broad SQL monitoring capability. This allows product direction to be validated early.

## ADR-002 — Snapshot-first architecture

Monitoring values are centrally collected into reusable snapshots. UI widgets do not independently query SQL Servers.

## ADR-003 — One central live area

The SQL Command Center owns the main live visual language. Detailed pages consume the latest snapshot and show snapshot age.

## ADR-004 — Motion is not collection

Heartbeat, clocks and transitions are client-side presentation. They must never increase SQL collection frequency.

## ADR-005 — Development authentication

M0 uses a single Administrator and ASP.NET Core cookie auth. The agreed development password is represented only by a PBKDF2-SHA256 hash/salt in source control.

## ADR-006 — AI remains advisory

AI integration receives normalized evidence and proposes explanations/remediation/query suggestions. It does not autonomously execute production SQL.

## ADR-007 — Registration metadata is separate from connection secrets

Server registrations may contain endpoint and authentication-mode metadata, but never passwords or full connection strings. Secret values are resolved through a backend-only boundary.

## ADR-008 — Test Connection is bounded and redacted

Test Connection accepts a server registration ID only and runs exclusively in the authorized backend with bounded timeouts and fixed safe result categories. Raw provider exceptions, credentials and connection strings never return to the browser.

## ADR-009 — Collector results are reusable snapshots

Collector work is backend-owned and bounded. One collected snapshot feeds multiple UI surfaces instead of widget/per-database SQL fan-out.

## ADR-010 — Snapshot cache is fresh/stale and single-flight

Cached server snapshots have bounded freshness/stale fallback and concurrent refresh consumers share one collection task. Refresh failure never overwrites a newer good snapshot.

## ADR-011 — Real and demo data are explicitly labeled

Missing real dimensions are presented as not collected rather than invented from demo values. Once a real estate exists, unavailable real targets remain visible instead of being silently replaced by demo cards.

## ADR-012 — Manual refresh is backend-controlled and throttled

Snapshot refresh is an administrator POST by registration ID, with atomic per-server throttling and cache single-flight. The browser cannot provide SQL text, credentials or collection frequency.

## ADR-013 — SignalR is delivery-only and deferred until independent publication exists

SignalR is not introduced merely to make request-driven collection look live. It may be added only as downstream delivery for snapshots produced independently of a UI request.

## ADR-014 — Memory and later health modules extend the shared snapshot

Memory, database states, backup, Agent, storage, blocking and baseline performance are immutable bounded facts on the canonical snapshot and shared cache boundary.

## ADR-015 — Health modules remain bounded snapshot facts

Aggregate health facts must not expose SQL text, job command, physical path, credentials or raw provider messages. Invalid/inconsistent facts fail through the redacted collector boundary.

## ADR-016 — Module pages consume one shared cached projection

Dedicated health pages are read projections over `IMonitorReadService`; they are not collection triggers.

## ADR-017 — Findings are deterministic and incidents resolve only from fresh evidence

Incident identity is registration plus rule ID. Older observations are ignored and missing/stale/failed collection cannot resolve incidents. Only newer fresh evidence may resolve them.

## ADR-018 — Incident commands are explicit and recommendations never execute

Operator transitions are authorized antiforgery-protected POST actions. Recommendation content is server-owned human-review guidance with no SQL execution path.

## ADR-019 — AI integration starts as a disabled backend boundary

Advisor context is normalized and bounded. Provider integration is backend-only, disabled by default and disconnected from SQL execution/autonomous remediation.

## ADR-020 — History is bounded aggregate evidence

History keeps allowlisted aggregate facts, deduplicated by registration/time, with fixed retention and no collection side effect from reads.

## ADR-021 — Scheduled collection is disabled by default and failure-isolated

The hosted scheduler has no immediate startup collection, validates policy, prevents overlapping host cycles, bounds server concurrency and isolates/backoffs failures.

## ADR-022 — Monitoring authorization uses named policies

Viewer, Operator and Administrator roles map to explicit read, incident-operation, connection-management and Advisor policies. Unsafe actions remain POST plus antiforgery.

## ADR-023 — Advisor requests are explicit, bounded and audited

Advisor requests are explicit authorized actions with single-flight, evidence-version caching, timeout, circuit behavior and metadata-only audit.

## ADR-024 — First-run commissioning is one deliberate backend workflow

An administrator with no enabled registration is routed to Connections. Save -> Test -> first collection -> observer -> Servers is one backend-controlled journey. Runtime SQL Login values are process-memory only and never rendered/persisted.

## ADR-025 — Real registrations replace the demo estate as a whole

When real registrations exist, all enabled registrations are returned in deterministic order and an unavailable target remains `RegisteredUnavailable`; demo cards are not mixed in.

## ADR-026 — Incident audit enrichment must not break the workflow contract

M5-026 keeps the boolean incident workflow API and reads canonical incident repository state around the atomic transition to produce bounded state-aware audit metadata. Missing actor identity fails closed before mutation.

## ADR-027 — Persist registration metadata, never runtime credential values

M7-001 uses a versioned Monitor-owned file outside `wwwroot`, persisting only registration metadata and opaque secret references. Writes are atomic and corrupt state fails startup. Runtime credential values remain process-memory only.

## ADR-028 — A provider-owned secret reference never downgrades to another source

M7-002 gives external providers ownership over recognized references. A provider-owned null result is final for that attempt. `env:` references resolve directly from strict process environment variables and never from appsettings fallback.

## ADR-029 — Durable operational state is split by state machine and committed before publication

M7-003 keeps audit, history and incident lifecycle in independent versioned files under one Monitor-owned root. Candidate state is durably committed before live state changes. Corrupt/domain-invalid state fails closed and bounded persistence excludes SQL credentials/text/endpoints/provider errors/job commands/arbitrary payloads.

## ADR-030 — Multi-node intent fails closed until shared state and coordination are real

M7-004 adds explicit deployment topology. `SingleNode` is supported. Selecting `MultiNode` is rejected during startup while registration/operational storage, runtime credentials, login limiting, snapshot cache/single-flight and scheduler coordination still contain node-local state.

This is an intentional safety invariant: local files, process memory or a network-share path must not be treated as a distributed transaction/coordination system. A future shared provider may enable multi-node only after persistence and coordination capabilities are externally implemented and validated. The Settings readiness view is informational and cannot override the startup guard.
