# Roadmap

## M0 — Visual Foundation — VERIFIED

Delivered `0.0.1-ui-preview`: authentication, premium shell, Command Center, core screens, mock snapshot, centralized live visual language and CI.

## M1 — First Real SQL Vertical Slice — VERIFIED

Register one SQL Server -> test connection -> collect lightweight identity/availability -> create health snapshot -> display real data -> backend-controlled refresh. SignalR delivery was evaluated and intentionally deferred until snapshots are published independently.

## M2 — Health Modules — VERIFIED

Memory, database health, backups, SQL Agent jobs, storage, blocking and bounded baseline performance are represented through immutable cached snapshot facts and cache-backed read surfaces.

## M3 — Incident & Recommendation Engine — VERIFIED THROUGH M3-016

Rules, deduplication, incident lifecycle, filters/details, protected operator transitions and deterministic human-reviewed recommendations are implemented. No recommendation executes production SQL.

## M4 — AI Advisor Boundary — VERIFIED THROUGH M4-006

Normalized advisory context and a backend provider abstraction are implemented. The only configured provider remains disabled by default; no external AI/network call or autonomous production execution is enabled.

## M5 — History, Reports & Enterprise Hardening — ACTIVE

Implemented through M5-007: bounded allowlisted history, 24-hour/288-point retention, observer integration, deterministic collection cycle and fixed-window trends. Background scheduling remains disabled.

Next hardening priorities: operator audit trail, reporting/audit review, RBAC expansion, enterprise secrets integration, HA and deployment hardening.
