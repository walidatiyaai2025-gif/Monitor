# Project Status

**Updated:** 2026-08-10 15:03 +03:00  
**Branch:** `agent/m7-004-ha-topology-guard-v3`  
**Target:** reconcile HA guard with M8 + protected local SQL credentials  
**Issue:** #47  
**PR:** #53  
**Overall:** 🟢 M0–M6 VERIFIED · M7-001..M7-016 CI VERIFIED · M8-001..M8-015 CI VERIFIED

## M7-004 — HA / multi-node topology guard — CI VERIFIED ON CURRENT BASELINE

- Rebuilt from current `main` after PR #51 protected SQL credentials merged.
- `Deployment:Mode` is explicit; `SingleNode` is supported.
- Selecting `MultiNode` fails startup before persistence/services activate until real shared registration/operational state and distributed coordination exist.
- Protected local SQL credential file + Data Protection key ring are explicitly node-local.
- Administrator Settings exposes bounded topology/readiness information with no mutation control.
- Existing M8 zero-SQL GET behavior and explicit refresh POST are preserved.
- CI `31385935255`: SUCCESS — Release build 0 warnings / 0 errors; **99/99 tests passed**.

## M7-005..M7-016 — Protected local SQL credentials — CI VERIFIED

- SQL Login credentials use server-generated `local:v1` references.
- Credential payloads are protected by ASP.NET Data Protection with reference-scoped purposes.
- Encrypted secret file + Data Protection key ring persist outside `wwwroot`.
- Candidate file writes are atomically replaced; persisted JSON contains ciphertext, not plaintext credential canaries.
- Missing/different key rings and tampered ciphertext fail closed.
- Existing `env:` and legacy external references remain compatible.
- CI `31384727247`: SUCCESS — Release build 0 warnings / 0 errors; **94/94 tests passed**.

## M8 — Zero-SQL reads & operator refresh — CI VERIFIED

- Monitoring GETs use cache-only Peek and never initiate monitored SQL collection.
- Incident navigation is read-only; observations happen after explicit successful collection/refresh.
- Operator/Admin Server Details refresh is POST + antiforgery with PRG feedback.
- Successful manual refresh observation occurs once; failed/throttled refresh does not publish observation.
- CI `31383991126`: SUCCESS — **91/91 tests passed**, 0 warnings / 0 errors.

## Earlier M7 foundation

- M7-001 durable registration metadata: final CI `31381074579`.
- M7-002 external `env:` SQL secret provider: final CI `31382052980`.
- M7-003 durable audit/history/incident state: final CI `31383226721`.

## Stable architecture guardrails

- Browser/UI components never connect directly to monitored SQL Servers.
- Monitoring navigation/GETs never trigger monitored SQL collection.
- Explicit refresh/collection remains authorized and backend-controlled.
- Snapshot cache remains the shared evidence/read boundary.
- Recommendations/Advisor remain advisory-only with no SQL execution path.
- Registration, operational and protected-local-secret files are single-node implementations.
- MultiNode remains fail-closed until real shared state and distributed coordination are present.

## Merge gate

Run CI on this final documentation head. Re-check `main` for overlap, then merge PR #53 only if Release build with warnings-as-errors, Razor compilation and all tests remain Green.

## Next action

After PR #53 merge, close superseded PR #50 and execute **M7-017 / Issue #52 — Shared-state capability + dedicated SQL Server provider**. Do not enable MultiNode in M7-017.
