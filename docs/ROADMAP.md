# Roadmap

## M0 — Visual Foundation — COMPLETE

`0.0.1-ui-preview` delivered and merged: authentication, premium shell, Command Center, core screens, mock snapshot, centralized live visual language and CI verification.

## M1 — First Real SQL Vertical Slice — ACTIVE

Register one SQL Server -> test connection -> collect lightweight identity/availability -> create health snapshot -> display real data -> update dashboard.

Current state:

- M1-001 Server registration + external secret boundary: COMPLETE.
- M1-002 Test Connection + SQL Connection Lab: CODE/CI VERIFIED, visual review pending.
- M1-003 Lightweight identity collector: NEXT.

## M2 — Health Modules

Memory, database health, backups, SQL Agent jobs, storage, blocking, baseline performance.

## M3 — Incident & Recommendation Engine

Rules, deduplication, incident lifecycle, evidence bundles, detailed remediation suggestions and proposed read-only/diagnostic SQL where appropriate.

## M4 — AI Advisor

Send normalized problem evidence to an AI advisory boundary; return ranked explanations and remediation options. No autonomous production execution.

## M5 — History, Reports & Enterprise Hardening

Historical store, trend analysis, reporting, auditing, RBAC expansion, secrets integration, HA and deployment hardening.
