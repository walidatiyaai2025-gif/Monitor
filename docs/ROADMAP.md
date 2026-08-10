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

## M7 — Production Persistence & Deployment Readiness — ACTIVE

- **M7-001 — VERIFIED:** durable registration metadata outside `wwwroot`; opaque references only.
- **M7-002 — VERIFIED:** fail-closed external secret-provider routing; `env:<alias>` reads direct process environment.
- **M7-003 — VERIFIED:** durable independent audit/history/incident state with atomic candidate commit.
- **M7-004 — CI VERIFIED:** explicit `SingleNode` topology guard; `MultiNode` startup rejected until shared state + distributed coordination exist. Post-protected-credential CI `31385935255`.
- **M7-005..M7-016 — CI VERIFIED:** protected local SQL Login credential store, persisted Data Protection key ring, `local:v1` references, versioned encrypted envelope, reference-scoped protection, atomic ciphertext replacement, restart resolution, fail-closed key/ciphertext behavior, compatibility, validation and deletion. CI `31384727247`.
- **M7-017 — PLANNED / Issue #52:** generic shared-state document capability plus first real dedicated Monitor SQL Server provider with optimistic compare/exchange. This does not enable MultiNode.
- **M7-018 — PLANNED:** migrate required repositories/coordination and add distributed scheduler ownership/cross-node single-flight before MultiNode can be enabled.

## M8 — Zero-SQL Reads & Operator Refresh — CI VERIFIED
Monitoring GET/navigation surfaces are cache/Peek-only. Explicit Operator/Admin refresh remains POST + antiforgery; successful refresh observation occurs once. CI `31383991126` (91/91 tests).
