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
| Durable registration metadata | M7 | CI verified | Atomic file store outside `wwwroot`; opaque refs only; CI 31380699808 |
| Environment SQL secret provider | M7 | CI verified | `env:<alias>` direct environment; no fallback; CI 31381465706 |
| Durable operational state | M7 | CI verified | Independent audit/history/incident files; CI 31382770932 |
| Protected local SQL credential store | M7 | CI verified | `local:v1`, Data Protection, encrypted atomic file, persisted key ring; CI 31384727247 |
| HA topology safety guard | M7 | CI verified | SingleNode supported; MultiNode fail-closed; post-credential CI 31385935255 |
| Shared-state provider capability | M7 | Planned | M7-017 / Issue #52; dedicated Monitor-owned SQL provider |
| Distributed coordination | M7 | Planned | M7-018; required before MultiNode enablement |
| Zero-SQL monitoring GETs | M8 | CI verified | Cache/Peek-only browser reads; CI 31383991126 |
| Explicit observed manual refresh | M8 | CI verified | Operator/Admin POST; successful refresh observed once |
