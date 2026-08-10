# Feature Catalog

| Feature | Milestone | Status | Notes |
|---|---|---|---|
| Development Admin authentication | M0 | Verified | Cookie auth; PBKDF2 hash only |
| SQL Command Center | M0 | Verified | Central live visual area + estate topology |
| Command Center visual telemetry layer | M0 | Verified | Client-only radar, freshness, scan phases; zero data calls |
| Server estate operations view | M0 | Verified | Estate summary, local filter/search, health score, resource pressure |
| Server operational overview | M0 | Verified | DBA command header, attention assessment, resource envelope, DBA focus |
| Snapshot freshness presentation | M0 | Verified | Browser-only age progression; no collection trigger |
| Database Health | M0 | Verified | Shared snapshot data |
| Memory Health | M0 | Verified | Shared snapshot data |
| Alerts / Incidents | M0 | Verified | Preview queue |
| Settings | M0 | Verified | Read-only preview |
| UI design system | M0 | Verified | CSS tokens/components |
| Controlled motion | M0 | Verified | Client only; no data calls |
| Server registration model | M1 | CI verified | Validated endpoint/auth metadata; in-memory repository |
| Connection secret boundary | M1 | CI verified | Opaque reference; values only from User Secrets/environment |
| Test Connection workflow | M1 | CI verified | Admin-only ID endpoint; bounded timeout and redacted outcomes |
| Lightweight SQL collector | M1 | CI verified | One query: identity, uptime and database availability counts |
| Server health snapshot cache | M1 | CI verified | 30s fresh, 5m stale fallback, per-server single-flight |
| Real snapshot UI card | M1 | CI verified | First configured server replaces one demo card; mixed data labeled |
| Throttled snapshot refresh | M1 | CI verified | Admin POST, 15s per-server throttle, shared cache flight |
| SignalR snapshot delivery | M1 | Evaluated / deferred | Revisit after scheduled backend publication exists |
| Memory snapshot projection | M2 | CI verified | System/process memory from the existing single collector query |
| Real memory health UI | M2 | CI verified | Cached SQL process utilization; CI 31372312362 |
| Database state detail projection | M2 | CI verified | Validated state counts; CI 31372957383 |
| Backup health summary | M2 | CI verified | Full-backup coverage and latest full backup; CI 31372957383 |
| SQL Agent jobs summary | M2 | CI verified | Total, enabled and failed-last-run counts; CI 31372957383 |
| Storage allocation summary | M2 | CI verified | Total, data and log allocated bytes; CI 31372957383 |
| Blocking summary | M2 | CI verified | Blocked request count and maximum wait; CI 31372957383 |
| Cached health module pages | M2 | CI verified | Database, backup, Agent, storage and blocking views share cache reads; CI 31373849952 |
| Baseline performance facts | M2 | CI verified | Active requests, runnable tasks and pending I/O counts; CI 31373849952 |
| Deterministic findings | M3 | CI verified | Allowlisted server-side rules with bounded evidence; CI 31373849952 |
| Incident lifecycle | M3 | CI verified | Stable dedupe; fresh healthy evidence resolves incidents; CI 31373849952 |
| Real incident center | M3 | CI verified | Cached snapshots feed the authorized Alerts UI; CI 31373849952 |
| Incident operator workflow | M3 | CI verified | Acknowledge, resolve and reopen with antiforgery protection; CI 31375034604 |
| Deterministic recommendations | M3 | CI verified | Rule-owned advisory steps; no execution; CI 31375034604 |
| AI Advisor boundary | M4 | CI verified | Normalized backend context; provider disabled by default; CI 31375034604 |
| Snapshot history | M5 | CI verified | Allowlisted 24-hour in-memory aggregate retention; CI 31375034604 |
| Collection cycle | M5 | CI verified | Backend-only deterministic refresh cycle; scheduler remains disabled; CI 31375034604 |
| Snapshot trends | M5 | CI verified | Fixed 1h/6h/24h read-only windows; CI 31375034604 |
| Operator audit trail | M5 | Planned | Immutable bounded audit evidence for protected incident transitions |
