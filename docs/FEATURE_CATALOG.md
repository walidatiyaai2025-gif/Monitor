# Feature Catalog

This is the **current live feature catalog**. Exact GitHub state, `AGENTS.md`, and `docs/CURRENT_EXECUTION_PLAN.md` remain authoritative for work selection. The detailed catalog that preceded this reconciliation is preserved byte-for-byte at `docs/history/FEATURE_CATALOG_PRE_OWNER_CLOSURE_RECONCILIATION_2026-09-09.md`.

## Current product/repository features

| Feature | Scope | State | Current truth |
|---|---|---|---|
| Core Monitor platform | M0–M8 | VERIFIED | Authentication, registration, bounded SQL snapshot/cache, health evidence, deterministic incidents/recommendations, scheduler, audit/RBAC/security, durable state and cache-only navigation contracts are implemented and regression-covered |
| Enterprise operations baseline | BATCH-100..BATCH-600 | COMPLETE | HA/shared-state foundations, security, scale, production acceptance/recovery tooling and operator evidence orchestration are complete in repository scope |
| Visible portal/UI completion | BATCH-700 | COMPLETE 50/50 | Final PR #240 squash-merged as `fd33e79c6d19d7f9852417b9c35a11f91f21714c`; exact final head `0834db6b5d518fe5c52eec9b47c03e467929aa89` passed selected CI/Real-SQL/Windows gates |
| Functional operator wiring | BATCH-800 / #287 | COMPLETE 100/100 | PR #335 squash-merged as `a6832d99f629cdbd3a93887199fe608a3ae474ec`; exact head `4379dbc0e1b346cb51bebf8e7467823c58f2361c` passed CI `32093252549`, Real SQL `32093252670`, Windows `32093252563`; #287 closed completed |
| Programming truthfulness/security closure | PR #363 / PR #369 | COMPLETE / MERGED | Missing evidence stays explicit, refresh/auth failures fail closed, credential and mutation controls are hardened, and synthetic health/readiness claims are rejected |
| Shared-state atomic execution guard | #423 / PR #424 | COMPLETE / MERGED | Production SharedState read/CAS revalidates schema readiness inside the same serializable transaction; selected exact-head gates passed before merge |
| Incident-note durability/replay | #445–#450 | COMPLETE / MERGED | Durable request identity, Applied replay authority and SingleNode cross-process mutation safety are implemented and regression-covered |
| Core SingleNode file cross-process safety | #451 / PR #452 | COMPLETE / MERGED | Audit/history/incidents reload authoritative state under bounded stable file leases; stale CAS/lost-update regressions are covered |
| Operator metadata cross-process safety | #459 / PR #460 | COMPLETE / MERGED | Shared operator metadata envelope is serialized under a stable cross-process lease; peer state/notes survive overlapping workers and restart |
| Website Monitoring runtime | #463/#466 via #467 -> #464 | COMPLETE / MERGED | Bounded targets, fail-closed SSRF/private-destination policy, DNS/TCP/TLS/HTTP/content/latency/certificate evidence, durable scheduling/history/check state, incident reconciliation, bounded correlation, recipient groups, SMTP secret resolution, durable outbox, management/cached-live UI and SharedState/distributed ownership are implemented. Monitoring and notifications remain default-disabled |
| Optional Website Monitoring browser verification | PR #472 | CLOSED UNMERGED BY OWNER DIRECTION | Recovered exact head `fce84a2c6743061d6ff402d3e6ba1d9e66ac1db6` passed selected browser/CI gates and retained artifact `10117386057`, but owner explicitly directed close without merge. No #472 browser workflow/script/doc/test is part of `main`; it is not required for Website Monitoring or P0 closure and must not be duplicated/revived without new authoritative scope |
| Configurable Dashboard live database status | #476 / PR #477 | CURRENT INTEGRATION LINE | Adds an animated cached-evidence panel on `/dashboard` with per-server database state, online/total counts and freshness. Operator cadence is bounded to 1/2/5/10/15/30 minutes and stored client-side. Background refresh re-reads authenticated `/dashboard` only, rejects redirect/non-HTML/missing evidence, retains last display on failure, prevents overlapping refreshes, skips hidden-tab network refresh and honors reduced motion. Browser code must never call monitored SQL, a collector or `/refresh-snapshot`. Final state is governed by live PR #477 and exact-head selected gates |
| Real SQL production gates | P0.1–P0.4 | COMPLETE | Registration, truthful first snapshot, Server Details source-of-truth and real SQL end-to-end acceptance are complete with retained CI/Real-SQL evidence |
| Production SingleNode repository preparation | P0.5 | REPOSITORY COMPLETE | Secret-free production configuration, IIS deployment/bootstrap, immutable acceptance session, selected-product-hash binding, acceptance-control-toolkit provenance, fail-closed 15-gate record/finalize/validate tooling, backup/rollback and release packaging are implemented |
| Deterministic tagged release notes | PR #473 | COMPLETE / MERGED | Future normal tagged releases bind notes to exact version/tag/source/ZIP/product hash/checksum/candidate provenance and fail closed on notes/assets drift; PR #473 did not publish RC.61 |
| Selected RC.61 durable publication | #162 | OWNER_ONLY / OPEN / NOT PASS | Selected product remains `Monitor-0.1.0-rc.61-win-x64.zip`, SHA-256 `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`, source run `31667721306`, artifact `9168574442`. No release/tag exists; explicit promotion + separate verifier + independent tag/assets/hash/readiness evidence are required |
| First Production SingleNode acceptance | #116 | EXTERNAL_ENVIRONMENT / OPEN / NOT PASS | Blocked before production mutation by #162. After #162, real trusted HTTPS/IIS/least-privilege/recycle/durability/backup/rollback evidence must reach independently validated 15/15 |
| P0 umbrella completion | #111 | OPEN / NOT PASS | Closure-only after real #116 acceptance; no independent production gate/action |
| Main branch protection | #353 | OWNER_ONLY / REPOSITORY_ADMIN / OPEN / NOT PASS | Repository helper/tests/docs complete; live `main.protected=false`, rulesets empty, admin protection read inaccessible to integration. Exact provider-bound policy must be applied and independently read back by repository admin |

## Selected RC.61 identity

```text
Version                    0.1.0-rc.61
Tag                        v0.1.0-rc.61
ZIP                        Monitor-0.1.0-rc.61-win-x64.zip
Checksum                   Monitor-0.1.0-rc.61-win-x64.zip.sha256
Product SHA-256            d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
Source run                 31667721306
Actions artifact ID        9168574442
Outer artifact digest      sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382
Source commit              e28158da67b36dfc5dbf8f4c38b5c43d99c7c728
Tested merge               158148d8bfd05f724014541bc7a0b1eab5dae1b5
Acceptance toolkit source  b422eaaee53d931a62a43b3c36a53b68cd4f3e27
```

## Production dependency and truth boundary

```text
#162 -> #116 -> #111
```

#353 is independent repository governance. No repository CI, optional browser evidence, preview, candidate packaging, tag tooling or documentation can manufacture an OWNER_ONLY or EXTERNAL PASS.

Stable feature boundaries remain:

- browser/navigation GETs are cache/control-plane only and never initiate monitored-SQL collection;
- browser/UI code never connects directly to monitored SQL;
- missing/stale/truncated/uncollected evidence remains explicit;
- no autonomous remediation or AI-generated SQL execution;
- credentials, full connection strings, secret material, raw provider errors, arbitrary SQL text, client/table data and physical paths remain excluded according to existing contracts;
- mutations retain POST + antiforgery + named authorization boundaries;
- MultiNode remains fail-closed until its distributed prerequisites are genuinely satisfied.
