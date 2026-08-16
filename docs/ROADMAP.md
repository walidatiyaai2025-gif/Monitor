# Roadmap

## Current priority — P0.5 First Production SingleNode

P0.1 through P0.4 are COMPLETE. Repository-side P0.5 deployment/evidence/release/durable-retention hardening is complete through PR #219, with the operator acceptance runbook reconciled through PR #253.

The remaining production path is intentionally external/manual:

1. **#162 — RC.61 durable retention:** manually promote the exact selected existing RC.61 from `main`, then run the separate read-only `verify-durable-release` workflow and independently verify tag provenance, exact-two assets and product SHA-256.
2. **#116 — real Windows/IIS acceptance:** deploy the exact selected candidate to the intended trusted-certificate SingleNode IIS host and complete the real 15/15 evidence pack, recycle/durability, least-privilege SQL, backup/rollback and explicit operator finalization.
3. **#111 — umbrella closure:** close only after #116 is accepted.

Repository CI, candidate packaging or durable publication cannot substitute for those real acceptance gates.

## M0 — Visual Foundation — VERIFIED
Premium ASP.NET Core command-center UI, authentication, core screens, controlled motion and visual acceptance.

## M1 — First Real SQL Vertical Slice — VERIFIED
Registration, bounded Test Connection, lightweight collector, reusable snapshot/cache and controlled refresh. SignalR was evaluated and deferred until independently produced snapshots exist.

## M2 — Health Modules — VERIFIED
Memory, database, backup, SQL Agent, storage, blocking and baseline performance are bounded cached snapshot facts.

## M3 — Incidents & Recommendations — VERIFIED THROUGH M3-016
Deterministic findings, incident lifecycle/operator transitions and deterministic human-reviewed recommendations.

## M4 — AI Advisor Boundary — VERIFIED THROUGH M4-013
Explicit guarded advisory requests with timeout/cache/circuit/audit boundaries and no SQL execution path.

## M5 — History & Enterprise Hardening — VERIFIED THROUGH M5-026
Bounded trends/history, scheduler infrastructure, audit, RBAC, browser security, login limiting and transition audit enrichment.

## M6 — Real SQL Server User Journey — VERIFIED THROUGH M6-050
Login -> Connections -> Register -> Test -> Collect -> Observe -> real multi-server estate/Dashboard/Health.

## M7 — Production Persistence & Deployment Readiness — VERIFIED THROUGH M7-018

- **M7-001..M7-004 — VERIFIED:** durable registration metadata, external secret-provider routing, durable operational state and explicit topology guard.
- **M7-005..M7-016 — VERIFIED:** protected local SQL Login credential persistence with Data Protection, persistent key ring, `local:v1` references and fail-closed tamper/key behavior. CI `31384727247`.
- **M7-017 — VERIFIED:** generic shared-state versioned-document capability plus dedicated Monitor SQL Server provider. Environment-only connection material, schema v1 deployment script, bounded JSON payloads and transactional optimistic compare/exchange. CI `31386867949`.
- **M7-018 — VERIFIED via BATCH-100:** shared registrations/audit/incidents/history plus distributed scheduler ownership and cross-node refresh single-flight. CI `31389275376`.

## M8 — Zero-SQL Reads & Operator Refresh — VERIFIED
Monitoring GET/navigation surfaces are cache/Peek-only for monitored SQL. Explicit Operator/Admin refresh remains POST + antiforgery; successful refresh observation occurs once. CI `31383991126`.

## Completed hardening and product batches

| Batch | Task range | State | Scope |
|---|---:|---|---|
| BATCH-100 | B100-001..100 | **100/100 COMPLETE** | shared state/HA, credentials/key management, backup/restore, observability, scale, control-plane UX, security, reliability, deployment and enterprise release features |
| BATCH-200 | B200-001..100 | **100/100 COMPLETE** | historical enterprise operations expansion; current-main reconciliation completed via Issue #99 / PR #156 without new task accounting |
| BATCH-300 | B300-001..100 | **100/100 COMPLETE** | production DBA diagnostics and deterministic operational intelligence |
| BATCH-400 | B400-001..110 | **110/110 COMPLETE** | production diagnostics plus original portal completion/typography |
| BATCH-500 | B500-001..100 | **100/100 COMPLETE** | production acceptance and recovery safety |
| BATCH-600 | B600-001..100 | **100/100 COMPLETE** | live operator readiness and evidence orchestration |
| BATCH-700 | UI700-001..050 | **50/50 COMPLETE** | full visible portal/UI completion, specialized health pages, audit/history, reports/recommendations and enterprise/admin acceptance |

Total completed hardening/UI task IDs across BATCH-100..700: **660**.

Canonical ledgers:

- `docs/BATCH_100.md`
- `docs/BATCH_200.md`
- `docs/BATCH_400.md`
- `docs/BATCH_700.md`
- `docs/STATUS.md`
- `docs/IMPLEMENTATION_PLAN.md`
- `docs/PRODUCTION_MVP.md`

## Stable roadmap guardrails

- Browser/UI components never connect directly to monitored SQL Servers.
- Monitoring/navigation GETs remain cache/control-plane only and never initiate monitored SQL collection.
- Manual monitored-SQL refresh remains explicit, authorized, backend-controlled and concurrency-bounded.
- No autonomous remediation or AI-generated SQL execution.
- Credentials, full connection strings, current secret references, raw provider errors and arbitrary SQL text remain outside UI/audit/telemetry/export/diagnostics/evidence.
- Recommendations/Advisor output remain advisory-only and human-reviewed.
- First production activation remains **SingleNode**.
- `MultiNode` remains fail-closed/deferred until after a stable accepted SingleNode production release.
- RC.61 remains the selected cutover candidate unless #116 explicitly selects another equivalently verified candidate.
- Durable publication/verification is retention evidence only; it cannot mark a real #116 gate PASS.
