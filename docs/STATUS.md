# Project Status

**Updated:** 2026-08-11 02:03 +03:00  
**Branch:** `agent/b100-9`  
**Target:** BATCH-100 / Batch 9 — Deployment & operations tooling  
**Issues:** #55 umbrella · #72 Batch 9  
**PR:** #73 — BATCH-100/9: add production deployment tooling  
**Overall:** 🟢 M0–M6 VERIFIED · M7-001..M7-018 CI VERIFIED · M8 CI VERIFIED · B100-001..090 CI VERIFIED · 🟡 FINAL PR CI PENDING BEFORE MERGE

## BATCH-100 / Batch 9 — CI VERIFIED

B100-081..090 are implemented on `agent/b100-9`. Branch CI run `31440573683` is Green and the verified program count is now **90/100**.

### CI evidence

- PR: #73.
- Branch CI: `31440573683`.
- Release build: **0 warnings / 0 errors** with `--warnaserror`.
- Tests: **219 passed / 0 failed / 0 skipped**.
- Deployment acceptance tests parse the production JSON, inspect runtime/service wiring, reject high-privilege SQL grants, require build/test before release packaging, enforce HTTPS/control-plane-only smoke probes and scan all Batch 9 artifacts for a secret canary.
- Final PR merge-result CI is required on this canonical code + docs head before merge.

### B100-081..090 delivered

- `deploy/appsettings.Production.example.json` provides a safe production baseline with environment-variable names only for shared-state/key secrets.
- IIS deployment guide covers Hosting Bundle, dedicated low-privilege identity, HTTPS, filesystem ACLs, readiness and rollback.
- Monitor now opts into the official .NET Windows Service lifetime and includes a Windows Service deployment guide; no wrapper process is required.
- Reverse-proxy guide documents explicit trusted proxy/CIDR configuration and rejects trust-all forwarding.
- Dedicated Monitor state DB runtime role receives only SELECT on schema metadata and SELECT/INSERT/UPDATE on shared documents.
- Monitored SQL role grants the read/view permissions required by the current bounded snapshot collector, without DML/DDL/sysadmin rights.
- Upgrade checklist requires exact release identity, CI evidence, operational backup, configuration diff, versioned deployment and readiness acceptance.
- Release workflow validates version tags, builds/tests first, publishes Windows x64, produces a SHA-256 checksum and uploads a read-only artifact.
- `scripts/Smoke-Monitor.ps1` requires HTTPS except explicit loopback and probes only `/health/live`, `/health/ready` and `/health`.
- Rollback runbook separates application rollback, operational-state restore, state-DB recovery and key/credential recovery without destructive shortcuts.

## Stable guardrails

- Production examples contain no passwords, connection strings, Admin hashes/salts or key material.
- Service identities remain least-privilege; deployment documentation explicitly rejects LocalSystem/Domain Admin/SQL sysadmin operation.
- Release packaging cannot run before Release build/tests in the release workflow.
- Smoke/readiness checks stay on Monitor control-plane endpoints and never connect directly to a monitored SQL target.
- Schema creation/migration remains an explicit administrative action; runtime role cannot ALTER/CONTROL the state DB.
- MultiNode remains fail-closed until the existing readiness prerequisites are satisfied.

## Merge gate

Require final PR #73 merge-result CI on this code + canonical docs head to pass Release `--warnaserror` build and all tests, confirm `main` has not moved into overlapping hosting/deployment code, then squash-merge.

## Next action

After Batch 9 merge, execute **B100-091..100 — enterprise operator features & release-candidate acceptance** from #55 / `docs/BATCH_100.md`.
