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
| Server registration model | M1 | Verified | Validated endpoint/auth metadata; in-memory repository |
| Connection secret boundary | M1 | Verified | Opaque reference; values only from User Secrets/environment |
| Test Connection backend | M1 | Verified | Admin-only bounded probe with sanitized outcomes |
| SQL Connection Lab UI | M1 | CI verified / visual review pending | Safe metadata registration + manual Test Connection workflow |
| Connection Lab secret-safe summary | M1 | CI verified | Boolean secret-presence only; raw reference is not in target summaries |
| Provider timeout semantics | M1 | CI verified | SqlClient `-2` maps to TimedOut; shared profiles use zero connection retries |
| Lightweight SQL collector | M1 | Verified | One query: identity, uptime and database availability counts |
| Real SQL connection | M1 | Planned | First vertical slice |
| Backups | M2 | Planned | Coming soon in UI |
| Jobs | M2 | Planned | Coming soon in UI |
| Storage | M2 | Planned | Coming soon in UI |
| Blocking | M2 | Planned | Command Center target |
| Recommendation engine | M3 | Planned | Detailed remediation/query suggestions |
| AI Advisor | M4 | Planned | Advisory boundary only |
| Reports/history | M5 | Planned | Trends and reporting |
