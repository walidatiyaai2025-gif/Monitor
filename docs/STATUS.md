# Project Status

**Updated:** 2026-08-11 09:55 +03:00  
**Branch:** `agent/b400`  
**Target:** BATCH-400 — Production DBA Diagnostics & Decision Safety  
**Issue:** #108  
**Overall:** 🟢 M0–M8 VERIFIED · 🟢 BATCH-100 100/100 COMPLETE · 🟢 BATCH-200 100/100 COMPLETE · 🟢 BATCH-300 100/100 COMPLETE · 🟡 BATCH-400 IMPLEMENTED / CI PENDING

## BATCH-400 implementation

- B400-001..100 production code is implemented on `agent/b400`.
- 100 mapped acceptance tests are included, one for every B400 task.
- New deterministic domains: wait statistics, query regression, TempDB pressure, transaction-log health, I/O latency, SQL Agent reliability, HA readiness, maintenance decision safety and fleet correlation.
- Versioned read-only contract endpoint: `/intelligence/v2/contract` under the named Read policy.
- Tasks remain **open** until Release build with `--warnaserror`, full tests, PR CI and squash merge are all Green.

## Stable guardrails

- Read/navigation/reporting/diagnostic/intelligence GETs do not initiate monitored-SQL collection.
- No browser connects directly to monitored SQL.
- No autonomous remediation or AI SQL execution.
- Credentials/full connection strings/raw provider errors/SQL text remain outside UI, audit, exports and diagnostics.
- Suppression does not rewrite incident evidence.
- Maintenance affects scheduled collection only; manual refresh is explicit and audited.
- Mutations remain POST + antiforgery + named authorization policy.
- MultiNode remains fail-closed behind shared-state/security/credential readiness.
- Concurrent team lifecycle/reconnect work remains preserved.
