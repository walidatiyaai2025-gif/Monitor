# Feature Catalog

| Feature | Milestone | Status | Notes |
|---|---|---|---|
| Development Admin authentication | M0 | Verified | Cookie auth; PBKDF2 hash only |
| SQL Command Center / DBA estate UI | M0 | Verified | Central live visual language; controlled client motion |
| Server registration + Test Connection | M1 | CI verified | Bounded backend-only connection workflow |
| SQL snapshot collector/cache | M1 | CI verified | Reusable snapshot, fresh/stale cache, single-flight |
| Health modules | M2 | CI verified | Memory/database/backup/Agent/storage/blocking/performance |
| Deterministic incident engine | M3 | CI verified | Stable findings/lifecycle/operator workflow |
| Deterministic recommendations | M3 | CI verified | Human-reviewed only; no execution |
| AI Advisor boundary | M4 | CI verified | Guarded explicit advisory request; provider disabled by default |
| Snapshot history/trends | M5 | CI verified | Bounded aggregates; durable in M7-003 |
| Scheduler infrastructure | M5 | CI verified | Disabled by default; bounded/backoff/status |
| Audit + RBAC + web security | M5 | CI verified | Durable audit, named policies, browser baseline |
| Real SQL onboarding journey | M6 | CI verified | Register/Test/Collect/Observe real estate; CI 31378848889 |
| Durable registration metadata | M7 | CI verified | Atomic file store outside `wwwroot`; opaque refs only |
| Environment SQL secret provider | M7 | CI verified | `env:<alias>` direct environment; no fallback |
| Durable operational state | M7 | CI verified | Independent audit/history/incident files |
| Protected local SQL credential store | M7 | CI verified | `local:v1`, Data Protection, encrypted atomic file, persisted key ring; CI 31384727247 |
| HA topology safety guard | M7 | CI verified | Cross-field readiness; false MultiNode readiness is blocked |
| Shared-state document contract | M7 | CI verified | Bounded JSON, versioned read/compare-exchange; CI 31386867949 |
| Dedicated Monitor SQL shared-state provider | M7 | CI verified | Environment-only connection, schema v1, SERIALIZABLE compare/exchange; CI 31386867949 |
| Shared-state readiness | M7 | CI verified | Provider/schema status only; no endpoint/credential disclosure |
| Shared registration repository | B100 | CI verified | Same application interface, optimistic CAS, deterministic import-if-empty; CI 31389275376 |
| Shared audit/history/incident repositories | B100 | CI verified | Existing bounded semantics on dedicated control-plane state; CI 31389275376 |
| Distributed scheduler/refresh coordination | B100 | CI verified | Expiring versioned leases, leader renewal, cross-node refresh single-flight; CI 31389275376 |
| Shared encrypted Data Protection key ring | B100 | CI verified | AES-256-GCM shared XML; environment-only 256-bit KEK; CI 31391446513 |
| HA credential creation policy | B100 | CI verified | New Monitor-owned local credentials can be prohibited explicitly; CI 31391446513 |
| Credential reference migration/rotation | B100 | CI verified | Resolve -> Test -> commit -> owned cleanup; metadata-only audit; CI 31391446513 |
| Credential HA readiness | B100 | CI verified | Aggregate counts/key-ring mode only; current references never rendered; CI 31391446513 |
| Versioned operational backup bundle | B100 | CI verified | Safe registrations/incidents/history/audit + SHA-256 manifest; CI 31393040135 |
| Backup dry-run validation | B100 | CI verified | Format/hash/bounds/referential-integrity validation before mutation; CI 31393040135 |
| Rollback-capable operational restore | B100 | CI verified | File/Shared persistence, staged apply + reverse rollback on failure; CI 31393040135 |
| Backup retention/readiness UI | B100 | CI verified | Atomic files outside `wwwroot`, bounded retention, Admin POST controls; CI 31393040135 |
| Application health/liveness/readiness | B100 | CI verified | Process liveness + control-plane-only readiness; zero monitored-SQL collection; CI 31396619576 |
| Bounded runtime telemetry | B100 | CI verified | Collector/cache/scheduler/incident/auth aggregate counters only; runtime-wired in Batch 5 |
| Correlation + structured redacted logging | B100 | CI verified | Strict bounded correlation IDs; method/status/elapsed only; runtime-wired in Batch 5 |
| Administrator observability surface | B100 | CI verified | Read-only aggregate operational view; runtime-resolvable; no monitored-SQL collection |
| Snapshot cache capacity governance | B100 | CI verified | Configurable cap + deterministic oldest-entry eviction; CI 31399632281 |
| Bounded operational paging | B100 | CI verified | History/audit/incidents/server estate have explicit output/read bounds; CI 31399632281 |
| Server estate paging UI | B100 | CI verified | Total/page range + Previous/Next; page navigation Peeks cache only; CI 31399632281 |
| Manual refresh concurrency gate | B100 | CI verified | App-wide permit + registration throttle + distributed single-flight; CI 31399632281 |
| Scheduler jitter + round-robin batches | B100 | CI verified | Bounded deterministic jitter and max targets/cycle; CI 31399632281 |
| Governed monitored-SQL pooling | B100 | CI verified | Bounded collector pool; Test Connection remains non-pooled; CI 31399632281 |
| Deterministic performance-budget suite | B100 | CI verified | Capacity/concurrency/query-count/output-size budgets; CI 31399632281 |
| Central DBA operations projection | B100 | CI verified | One readiness snapshot + safe backup/scheduler metadata; CI 31402491011 |
| Dashboard control-plane cards | B100 | CI verified | Opaque node, shared schema/status, backup and scheduler state; CI 31402491011 |
| Registered-server recovery surface | B100 | CI verified | Unavailable cached snapshot returns recovery details, never 404/secret readback; CI 31402491011 |
| Classified manual-refresh feedback | B100 | CI verified | PRG-safe status/freshness classification with aria-live; CI 31402491011 |
| Incident filter/pager UX | B100 | CI verified | Bounded status/severity/rule/page navigation; CI 31402491011 |
| Accessibility + reduced-motion shell | B100 | CI verified | Skip link, focus-visible, live status, reduced motion; CI 31402491011 |
| Responsive DBA wallboard | B100 | CI verified | CSS-only large-display layout; no polling/collection change; CI 31402491011 |
| Central web security policy | B100 | Implemented / CI pending | `WebSecurityOptions`, nonce CSP, HSTS and trusted-forwarder configuration; B100-061/065/066/067 |
| Absolute authenticated-session lifetime | B100 | Implemented / CI pending | 30-minute idle renewal plus immutable 8-hour absolute session-start cap; B100-063 |
| Opaque login lockout + audit redaction | B100 | Implemented / CI pending | SHA-256 limiter keys, bounded five-failure window, redacted audit fields; B100-064/070 |
| Security acceptance regression suite | B100 | Implemented / CI pending | Antiforgery reflection gate, input fuzzing, security-header/HSTS/proxy tests and secret canaries; B100-062/067/068/070 |
| SQL connection metadata injection defense | B100 | Implemented / CI pending | Strict host/instance metadata plus `SqlConnectionStringBuilder` value-injection tests; B100-069 |
| Zero-SQL monitored GETs | M8 | CI verified | Cache/Peek-only browser monitoring reads; CI 31383991126 |
| Explicit observed manual refresh | M8 | CI verified | Operator/Admin POST; successful refresh observed once |
