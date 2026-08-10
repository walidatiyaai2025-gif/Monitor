# Roadmap

## M0 — Visual Foundation — VERIFIED

Delivered `0.0.1-ui-preview`: authentication, premium shell, Command Center, core screens, mock snapshot, centralized live visual language and CI.

## M1 — First Real SQL Vertical Slice — VERIFIED

Register one SQL Server -> test connection -> collect lightweight identity/availability -> create health snapshot -> display real data -> backend-controlled refresh. SignalR delivery was evaluated and intentionally deferred until snapshots are published independently.

## M2 — Health Modules — VERIFIED

Memory, database health, backups, SQL Agent jobs, storage, blocking and bounded baseline performance are represented through immutable cached snapshot facts and cache-backed read surfaces.

## M3 — Incident & Recommendation Engine — VERIFIED THROUGH M3-016

Rules, deduplication, incident lifecycle, filters/details, protected operator transitions and deterministic human-reviewed recommendations are implemented. No recommendation executes production SQL.

## M4 — AI Advisor Boundary & Hardening — VERIFIED THROUGH M4-013

Normalized advisory context, backend provider abstraction, guarded explicit request path, single-flight, bounded cache/timeout/circuit behavior and redacted audit are implemented. The configured provider remains disabled by default; no provider output can execute SQL or autonomously mutate monitored systems.

## M5 — History & Enterprise Hardening — VERIFIED THROUGH M5-026

Bounded history/trends, optional scheduled collection infrastructure, audit trail, RBAC, browser security, login limiting and state-aware incident transition audit are CI verified. Scheduled collection remains disabled unless explicitly configured.

## M6 — Real SQL Server User Journey — VERIFIED THROUGH M6-050

The first complete operator journey is CI verified: login -> Connections -> register -> Test Connection -> first cached snapshot -> observer -> real Servers/Dashboard/Health. Real registrations replace demo data as an estate, unavailable targets remain visible, and runtime SQL credentials never enter registration JSON or audit.

## M7 — Production Persistence & Deployment Readiness — ACTIVE

M7-001 adds durable local server-registration metadata persistence behind the existing repository contract. The store is outside `wwwroot`, uses atomic writes, persists only safe registration metadata plus opaque secret references, and fails closed on corrupt data. Runtime SQL credential values remain process-memory only and are intentionally not persisted.

Next production-readiness priorities after M7-001: enterprise secret-provider integration, durable/shared operational stores for audit/history/incidents, HA/shared-state strategy, deployment configuration validation, backup/restore of Monitor-owned state, and production observability.
