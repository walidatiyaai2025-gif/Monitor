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

Test Connection accepts a server registration ID only and runs exclusively in the authorized backend. It uses `Microsoft.Data.SqlClient`, explicit connection/overall timeouts, cancellation, no pooling, and fixed safe result messages. Raw provider exceptions, credentials and connection strings are never returned to the browser.

## ADR-009 — The first collector uses one reusable query result

The lightweight collector issues one bounded command for SQL identity, uptime and database counts. One row feeds the whole identity snapshot; it does not fan out into widget or per-database queries. Invalid or partial rows fail safely instead of inventing healthy values.

## ADR-010 — Snapshot cache is fresh/stale and single-flight

Cached server snapshots are fresh for 30 seconds and retained as an explicitly stale fallback for five minutes. Concurrent refresh requests for the same registration await one shared collection task. Refresh failure never overwrites the last good value, and older collection timestamps never replace newer snapshots.

## ADR-011 — Mixed real and demo data is labeled per card

The first configured server may replace one demo estate card from the backend snapshot cache. Every card carries an explicit Demo, LiveFresh or LiveStale source. Missing real dimensions are presented as not collected, never filled with preview numbers. If no configuration or usable snapshot exists, the unchanged estate remains explicitly development data.

## ADR-012 — Manual refresh is backend-controlled and throttled

Snapshot refresh is an administrator POST accepting only a registration ID. A 15-second atomic per-server throttle rejects repeated requests before collection, while the cache single-flight coalesces concurrent accepted work. The browser cannot provide SQL text, endpoints, credentials or collection frequency.

## ADR-013 — SignalR delivery is deferred until snapshots are published independently

SignalR is not added in M1 because snapshots are currently produced on request and there is no backend scheduler or snapshot-published event. A hub would add transport complexity without new information. Revisit when multiple consumers need independently produced updates or measured polling load justifies push. SignalR, if adopted, is delivery-only and must never invoke collectors or alter refresh frequency.

## ADR-014 — Memory health extends the existing collector row

M2 memory data is projected from `sys.dm_os_sys_memory` and `sys.dm_os_process_memory` inside the existing bounded collector command. It does not add a widget query or second connection. The snapshot stores raw validated facts; thresholds, alerts, UI and history remain later tasks.

## ADR-015 — Health modules remain bounded snapshot facts

Database states, backup coverage, SQL Agent, storage allocation and blocking are immutable optional modules on the canonical snapshot. One fixed backend command collects aggregate facts under the existing timeout/cache boundary. Browsers cannot supply SQL or trigger per-widget queries. Negative, inconsistent or overflowing values fail through the redacted collector boundary. Full-detail lists, history and UI policy remain separate slices.

## ADR-016 — Module pages consume one shared cached projection

Database, backup, SQL Agent, storage and blocking pages consume immutable module facts through `IMonitorReadService`. Null means not collected, stale remains explicit, and storage allocation is never presented as disk capacity. Controllers and browser code cannot call collectors or provide SQL.

## ADR-017 — Findings are deterministic and incidents resolve only from fresh evidence

The rule evaluator emits allowlisted IDs, severities and compact evidence from snapshots only. Incident identity is registration plus rule ID. Repeated evidence updates one record; older observations are ignored. Missing, stale or failed collection cannot resolve an incident. Only a newer fresh evaluation without the finding may resolve it. The initial repository is intentionally in-memory and executes no remediation.

## ADR-018 — Recommendations are deterministic, advisory and non-executable

M3-005 maps only known allowlisted incident rule IDs to application-owned recommendation templates. Each recommendation explains the problem, preserves bounded evidence, provides ordered remediation guidance and may include a fixed read-only diagnostic SQL statement. Incident evidence, browser input and AI output are never interpolated into diagnostic SQL. Unsupported rule IDs fail closed. Monitor provides no recommendation execution endpoint: ALTER/RESTORE/BACKUP/KILL/DBCC/job execution or any other production mutation remains a deliberate DBA/change-management action outside this recommendation service.
