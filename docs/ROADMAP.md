# Roadmap

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
- **M7-018 — VERIFIED via BATCH-100 Batch 1:** shared registrations/audit/incidents/history plus distributed scheduler ownership and cross-node refresh single-flight. CI `31389275376`.

## M8 — Zero-SQL Reads & Operator Refresh — CI VERIFIED
Monitoring GET/navigation surfaces are cache/Peek-only for monitored SQL. Explicit Operator/Admin refresh remains POST + antiforgery; successful refresh observation occurs once. CI `31383991126`.

## BATCH-100 — Production / Enterprise Program — 50/100 CI VERIFIED

- **Batch 1 / B100-001..010:** shared state & HA foundation — VERIFIED.
- **Batch 2 / B100-011..020:** HA secret & key management — VERIFIED.
- **Batch 3 / B100-021..030:** backup/export/restore — VERIFIED.
- **Batch 4 / B100-031..040:** production health, observability, correlation and redacted telemetry — VERIFIED.
- **Batch 5 / B100-041..050:** performance & scale governance — CI VERIFIED, final merge gate in progress.
- **Batch 6 / B100-051..060:** DBA UX & operations surfaces — NEXT.
- **Batches 7–10 / B100-061..100:** security, reliability, deployment tooling and RC/operator features — PLANNED.

The canonical task-level ledger is `docs/BATCH_100.md`.
