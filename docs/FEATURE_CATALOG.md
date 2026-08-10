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
| Real snapshot UI card | M1 | Local verified | First configured server replaces one demo card; mixed data labeled |
| Real SQL connection | M1 | Planned | First vertical slice |
| Backups | M2 | Planned | Coming soon in UI |
| Jobs | M2 | Planned | Coming soon in UI |
| Storage | M2 | Planned | Coming soon in UI |
| Blocking | M2 | Planned | Command Center target |
| Recommendation engine | M3 | Planned | Detailed remediation/query suggestions |
| AI Advisor | M4 | Planned | Advisory boundary only |
| Reports/history | M5 | Planned | Trends and reporting |
