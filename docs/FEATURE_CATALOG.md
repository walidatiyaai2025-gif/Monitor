# Feature Catalog

| Feature | Milestone | Status | Notes |
|---|---|---|---|
| Real SQL production registration gate | P0.1 | Complete | Issue #112 / PR #119; candidate Test precedes durable commit; failed/cancelled owned-secret compensation; final CI 31476747212, 501/501 |
| Truthful first-snapshot projection | P0.2 | Complete | Issue #113 / PR #121; nullable CPU/Memory evidence, actual Agent total/enabled/failed-last-run facts, safe Server Details evidence envelope; final CI 31478470867, 505/505 |
| Server Details production source of truth | P0.3 | Complete | Issue #114 / PR #122; evidence-first page, synthetic Health Score removed, cache-only GET; final CI 31479311552, 507/507 |
| Real SQL end-to-end acceptance | P0.4 | Complete | Issue #115 / PR #123 + #124; PR #124 merged f4c08292734c293a6d0b865cc2a005b8c42b02a6; final normal CI 31481874425 518/518 + Real SQL 31481874501 8/8 |
| First Production SingleNode release | P0.5 | Repository verified / external pending | Issue #116; IIS deployment + immutable session + exact 15-gate evidence/record/finalization + release-package parity tooling verified; selected RC.61 on #116; actual trusted-HTTPS IIS/recycle/least-privilege/backup/rollback evidence still required |
| Fail-closed final operator acceptance | P0.5 | Complete | Issue #147 / PR #148 merged e15a9654fbe744e426c95d5965a5faba60868e14; explicit final acknowledgement, prospective 15/15 validation, concurrent-pack guard, atomic metadata commit, authoritative revalidation and rollback-on-failure; Windows candidate 31537914596 761/761; never closes #116/#111 automatically |
| Immutable production acceptance session | P0.5 | Complete | Issue #150 / PR #151 merged 9a76abe61422502c4889b04ce8b6a59f18ac04f4; exact candidate ZIP/checksum validation, fresh traversal-safe candidate-bound workspace, SHA-locked manifest + canonical 15-gate pack at 0/15, no IIS/SQL/PASS/finalization side effects; final Windows candidate 31540968010 769/769 |
| Verified tagged/manual release package parity | P0.5 | Complete | Issue #154 / PR #155 merged 8d8ae2c5f35e8a1d774c5a9480f582e432e5dc03; release.yml delegates to reusable Windows production-candidate pipeline; manifest schema 2 separates P0.4 prerequisite evidence from candidate evidence; RC.61 Windows 31667721306 770/770, normal 31667721350, Real SQL 31667721353 8/8 |
| Durable tagged GitHub Release assets | P0.5 | Complete | Issue #159 / PR #160 merged a14110181932bcd6e14b99e5b6984974a5b477f8; real pushed version tags publish only the already-verified same-run ZIP + .sha256 after checksum verification; no rebuild/repackage/clobber path and no production-acceptance implication |
| Selected existing candidate durable promotion | P0.5 | Implementation complete / publication pending | Issue #162; PR #163 merged 43d8a193205495f155bb8866532a4e99ed93b655 and handoff PR #164 merged 930c057f431a36ab2b603d3dc39e70e8c31c744e; manual promote-existing-candidate workflow preserves exact RC.61 bytes without rebuild/repackage; publication remains pending until the manual run creates and independently verifies v0.1.0-rc.61 assets |
| GitHub Actions supply-chain hardening | P0.5 | Complete | Issue #168 / PR #171 merged c9084dd32b12a9a078f953f85f39b253793e2343; exact implementation head 052e969b5ab450526ab996a2e77459f4087846c8 passed normal CI 31881105832, Real SQL 31881105877 and Windows production-candidate 31881105818; active external Actions pinned to approved immutable 40-character SHAs, dedicated fail-closed pin allowlist regression added, obsolete completed BATCH-100 write-capable one-shot merge workflow removed; no RC.61 or external acceptance change |
| Native Node 24 GitHub Actions | P0.5 | Complete | Issue #173 / PR #174 merged bc7cb2d275f423fb381b83d92c76f6516e404fe9; exact head 8134720cf1260abc7e6c0609a5afa239f31bb5f7 passed CI 31881744429, Real SQL 31881744413 and Windows 31881744437; 814/814 tests, Release 0 warnings/errors, HTTPS/auth restart smoke, clean package and artifact upload Green; immutable allowlist now uses official native Node 24 checkout v7.0.1, setup-dotnet v6.0.0, upload-artifact v7.0.1 and download-artifact v8.0.1; old Node 20 deprecation warning absent; RC.87 is CI evidence only and RC.61 remains selected |
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
| Retention governance reconciliation | B200 | Complete | Issue #99 / PR #156 merged 221e44a9f13ed02e994311addff94b0e7996e444; dry-run + audit-backed prune receipts, bounded retention validation and Administrator POST; final normal 31669072593, Real SQL 31669072572, Windows 31669072625 Green |
| Enterprise security hardening II reconciliation | B200 | Complete | Issue #99 / PR #156 merged 221e44a9f13ed02e994311addff94b0e7996e444; secure download headers/filenames, aggregate input budgets, strict incident route IDs and endpoint policy coverage; final gates 31669072593 / 31669072572 / 31669072625 Green |
| Enterprise scale II reconciliation | B200 | Complete | Issue #99 / PR #156 merged 221e44a9f13ed02e994311addff94b0e7996e444; metadata index, bounded paging, streaming CSV, diagnostics timeout and CAS telemetry; final gates 31669072593 / 31669072572 / 31669072625 Green |
| Zero-SQL monitored GETs | M8 | CI verified | Cache/Peek-only browser monitoring reads; CI 31383991126 |
| Explicit observed manual refresh | M8 | CI verified | Operator/Admin POST; successful refresh observed once |
| Performance Health portal | BATCH-400 | Superseded by BATCH-700 | Original cache-only page from B400; upgraded in #222 to a dedicated summary + per-server performance dashboard while retaining zero-SQL GET behavior |
| Estate Recommendations portal | BATCH-400 | Superseded by BATCH-700 | Original deterministic recommendation cards from B400; upgraded in #224 with bounded summary, filters, semantic steps and evidence drill-down |
| Reports & Diagnostics center | BATCH-400 | Superseded by BATCH-700 | Original export cards from B400; upgraded in #224 with format/version/access/scope metadata, safe permission messaging and complete discoverability |
| Google typography system | BATCH-400 | CI verified | Self-hosted Inter Variable + Noto Sans Arabic Variable under strict CSP |
| Role-aware portal navigation | BATCH-400 | Superseded by BATCH-700 | Original connected navigation retained; B700 adds boundary-aware active states, mobile keyboard behavior, role-safe Admin routes and route-smoke coverage |
| Safe portal error + shared UI state system | BATCH-700 | CI verified | #221 / PR #236; safe 403/404/500, exception/status wiring, reusable page/state components, mobile shell and responsive contracts |
| Purpose-built Health operator surfaces | BATCH-700 | CI verified | #222 / PR #237; Database, Backup, SQL Agent, Storage, Blocking and Performance surfaces use cached evidence, explicit missing states and server drill-down |
| Audit + snapshot-history operator UX | BATCH-700 | CI verified | #223 / PR #238; bounded Audit filters/paging and stored History windows/paging/summary/context; no monitored-SQL GET collection |
| Bounded Recommendations + report center | BATCH-700 | CI verified | #224 / PR #239; top-100 deterministic guidance filters, ordered risk steps, evidence links, role-aware exports/diagnostics and contextual history CSV |
| Enterprise/Admin workflow completion | BATCH-700 | Complete / exact-head verified | #225 / PR #240 squash-merged as fd33e79c6d19d7f9852417b9c35a11f91f21714c; final head 0834db6b5d518fe5c52eec9b47c03e467929aa89 passed CI #1637, Real SQL #91 and production-candidate #142; actionable Readiness, runbook Help, Governance workflow, Observability context, grouped Settings, Connection Lab state coverage and Fleet drill-down |
| Accessible responsive final portal contract | BATCH-700 | Complete / exact-head verified | #225 / PR #240 squash-merged as fd33e79c6d19d7f9852417b9c35a11f91f21714c; final head 0834db6b5d518fe5c52eec9b47c03e467929aa89; keyboard focus, reduced motion, desktop/tablet/mobile + explicit 390px source contracts and CI visible-route smoke; no browser screenshot harness claim |

## BATCH-200 Enterprise Operations Expansion

Status: **100/100 COMPLETE and current-main reconciliation COMPLETE**. Issue #99 is closed completed. PR #156 selectively restored B200-051..060 and B200-071..090 on RC.61-era main and merged as `221e44a9f13ed02e994311addff94b0e7996e444`. Exact-head normal CI `31669072593`, Real SQL `31669072572`, and Windows production-candidate `31669072625` all completed Green. This remains historical baseline reconciliation, not new batch-task accounting or P0 production acceptance.