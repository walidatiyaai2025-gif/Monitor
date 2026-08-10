# Roadmap

## M0 — Visual Foundation — COMPLETE

`0.0.1-ui-preview` delivered and merged through PR #2: authentication, premium shell, Command Center, core screens, mock snapshot, centralized live visual language, Database/Memory command-view polish, and CI verification.

## M1 — First Real SQL Vertical Slice — ACTIVE

Register one SQL Server -> test connection -> collect lightweight identity/availability -> create health snapshot -> display real data -> update dashboard.

Active task: `M1-001 — Server registration and secure secret boundary` (Issue #3).

## M2 — Health Modules

Memory, database health, backups, SQL Agent jobs, storage, blocking, baseline performance.

## M3 — Incident & Recommendation Engine

Rules, deduplication, incident lifecycle, evidence bundles, detailed remediation suggestions and proposed read-only/diagnostic SQL where appropriate.

## M4 — AI Advisor

Send normalized problem evidence to an AI advisory boundary; return ranked explanations and remediation options. No autonomous production execution.

## M5 — History, Reports & Enterprise Hardening

Historical store, trend analysis, reporting, auditing, RBAC expansion, secrets integration, HA and deployment hardening.
