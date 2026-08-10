# Roadmap

## M0 — Visual Foundation — COMPLETE

The accepted visual foundation is merged to stable `main`: authentication, premium shell, Command Center, core health screens, centralized live visual language and CI.

## M1 — First Real SQL Vertical Slice — ACTIVE

Register one SQL Server -> test connection -> collect lightweight identity/availability -> create health snapshot -> display real data -> update dashboard.

Current state:

- M1-001 Registration + external secret boundary: COMPLETE.
- M1-002 Backend Test Connection: COMPLETE.
- M1-002A SQL Connection Lab visual workflow: CODE/CI VERIFIED, visual review pending.
- M1-003 Lightweight SQL identity collector: COMPLETE and merged.
- M1-004 ServerHealthSnapshot contract + cache: NEXT.

## M2 — Health Modules

Memory, database health, backups, SQL Agent jobs, storage, blocking, baseline performance.

## M3 — Incident & Recommendation Engine

Rules, deduplication, incident lifecycle, evidence bundles, detailed remediation suggestions and proposed read-only/diagnostic SQL where appropriate.

## M4 — AI Advisor

Send normalized problem evidence to an AI advisory boundary; return ranked explanations and remediation options. No autonomous production execution.

## M5 — History, Reports & Enterprise Hardening

Historical store, trend analysis, reporting, auditing, RBAC expansion, secrets integration, HA and deployment hardening.
