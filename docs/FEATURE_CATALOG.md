# Feature Catalog

| Feature | Milestone | Status | Notes |
|---|---|---|---|
| Real SQL production registration gate | P0.1 | Complete | Issue #112 / PR #119; candidate Test precedes durable commit; failed/cancelled owned-secret compensation; final CI 31476747212, 501/501 |
| Truthful first-snapshot projection | P0.2 | Complete | Issue #113 / PR #121; nullable CPU/Memory evidence, actual Agent total/enabled/failed-last-run facts, safe Server Details evidence envelope; final CI 31478470867, 505/505 |
| Server Details production source of truth | P0.3 | Complete | Issue #114 / PR #122; evidence-first page, synthetic Health Score removed, cache-only GET; final CI 31479311552, 507/507 |
| Real SQL end-to-end acceptance | P0.4 | Complete | Issue #115 / PR #123 + #124; PR #124 merged f4c08292734c293a6d0b865cc2a005b8c42b02a6; final normal CI 31481874425 518/518 + Real SQL 31481874501 8/8 |
| First Production SingleNode release | P0.5 | Repository verified / external pending | Issue #116; IIS deployment + immutable session + exact 15-gate evidence/record/finalization tooling verified; selected RC.53 on #116; actual trusted-HTTPS IIS/recycle/least-privilege/backup/rollback evidence still required |
| Fail-closed final operator acceptance | P0.5 | Complete | Issue #147 / PR #148 merged e15a9654fbe744e426c95d5965a5faba60868e14; explicit final acknowledgement, prospective 15/15 validation, concurrent-pack guard, atomic metadata commit, authoritative revalidation and rollback-on-failure; Windows candidate 31537914596 761/761; never closes #116/#111 automatically |
| Immutable production acceptance session | P0.5 | Complete | Issue #150 / PR #151 merged 9a76abe61422502c4889b04ce8b6a59f18ac04f4; exact candidate ZIP/checksum validation, fresh traversal-safe candidate-bound workspace, SHA-locked manifest + canonical 15-gate pack at 0/15, no IIS/SQL/PASS/finalization side effects; final Windows candidate 31540968010 769/769 |
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
| Dedicated Monitor SQL shared-state provider | M7 | CI verified | Environment-only connection, schema v1, SERIALIZABLE compare-exchange; CI 31386867949 |
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
| Central web security policy | B100 | CI verified | `WebSecurityOptions`, nonce CSP, HSTS and trusted-forwarder configuration; B100-061/065/066/067; CI 31439153733 |
| Absolute authenticated-session lifetime | B100 | CI verified | 30-minute idle renewal plus immutable 8-hour absolute session-start cap; B100-063; CI 31439153733 |
| Opaque login lockout + audit redaction | B100 | CI verified | SHA-256 limiter keys, bounded five-failure window, redacted audit fields; B100-064/070; CI 31439153733 |
| Security acceptance regression suite | B100 | CI verified | Antiforgery reflection gate, input fuzzing, security-header/HSTS/proxy tests and secret canaries; B100-062/067/068/070; CI 31439153733 |
| SQL connection metadata injection defense | B100 | CI verified | Strict host/instance metadata plus `SqlConnectionStringBuilder` value-injection tests; B100-069; CI 31439153733 |
| Deterministic shared-state fault harness | B100 | CI verified | Atomic failure/recovery, provider outage, restart-safe migration and lease takeover; B100-071..074; CI 31439886994 |
| Cross-node concurrency and soak acceptance | B100 | CI verified | Incident/audit/history/registration races, distributed refresh single-flight and 120-cycle three-node soak; B100-075..080; CI 31439886994 |
| Production deployment configuration & hosting | B100 | CI verified | Secret-free production template, IIS, Windows Service lifetime and reverse-proxy guidance; B100-081..084; CI 31440573683 |
| Least-privilege SQL deployment roles | B100 | CI verified | Dedicated state DB runtime role and monitored SQL read/view role; B100-085/086; CI 31440573683 plus SQL Server 2022 real-engine validation in P0.4 |
| Versioned release & recovery tooling | B100 | CI verified | Upgrade checklist, build/test-before-publish workflow, SHA-256 package, HTTPS smoke test and rollback runbook; B100-087..090; CI 31440573683 |
| Enterprise server governance metadata | B100 | CI verified | Environment, group/tags, bounded UTC maintenance and alert-suppression windows; B100-091..094; CI 31442930470 |
| Enterprise incident operator metadata | B100 | CI verified | Assignee, bounded notes and current-recommendation acknowledgment; B100-095..097; CI 31442930470 |
| Safe enterprise export & diagnostics | B100 | CI verified | Formula-safe cache-only CSV plus bounded Administrator redacted ZIP; B100-098/099; CI 31442930470 |
| Release-candidate enterprise acceptance | B100 | CI verified | One explicit regression test per B100-091..100 plus route authorization/antiforgery gate; B100-100; CI 31442930470 |
| Zero-SQL monitored GETs | M8 | CI verified | Cache/Peek-only browser monitoring reads; CI 31383991126 |
| Explicit observed manual refresh | M8 | CI verified | Operator/Admin POST; successful refresh observed once |
| Performance Health portal | BATCH-400 | Local verified | Dedicated cache-only performance page; monitored SQL is never contacted by GET |
| Estate Recommendations portal | BATCH-400 | Local verified | Deterministic active-incident recommendations; advisory only |
| Reports & Diagnostics center | BATCH-400 | Local verified | Discoverable bounded exports, diagnostics package and manifest |
| Google typography system | BATCH-400 | Local verified | Self-hosted Inter Variable + Noto Sans Arabic Variable under strict CSP |
| Role-aware portal navigation | BATCH-400 | Local verified | Connected fleet/help/readiness/audit/history surfaces and policy-aware management links |

## BATCH-200 Enterprise Operations Expansion

Status: **100/100 CI VERIFIED** by final gate `31446970475`. Includes enterprise metadata UX, maintenance/suppression policy, incident collaboration, versioned exports/diagnostics, fleet intelligence, retention governance, shared operator-state disaster recovery, enterprise security hardening, bounded scale controls, operator help/readiness and deployment/runbook compatibility.
