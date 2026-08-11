# BATCH-600 — Live Operator Readiness & Evidence Orchestration

Issue: #134  
Scope: exactly 100 repository tasks `B600-001..100`.  
State: **100/100 IMPLEMENTED + IMPLEMENTATION CI VERIFIED; final synchronized gates pending**.

BATCH-600 adds deterministic, side-effect-free orchestration for operator readiness while preserving the P0 boundary: repository CI does **not** prove external Windows/IIS production acceptance. #116 and #111 remain open until the real target is accepted.

## Implementation verification evidence

- Source head: `1c31897a3652fd45bc3ff0c45bac91f991eaf6b9`.
- Exact PR merge ref against `main` `020f4f1d0576d42af74db88537ca0690ea3b8f47`: `f60d01676bade59eef0b1cbbe90eb200c71223a6`.
- Normal CI `31500259339`: **Green**.
- Release build: **0 warnings / 0 errors**.
- Full suite: **738/738 passed**, 0 failed, 0 skipped.
- Windows production-candidate `31500260363`: **Green end-to-end**.
- Windows gate passed Release build, 738-test full suite, production PowerShell syntax checks, `win-x64` publish, secret-free SingleNode validation, HTTPS health/authentication before and after restart, package validation and artifact upload.
- Windows candidate artifact: `Monitor-0.1.0-rc.32-win-x64`, Actions artifact ID `9104795076`.
- Exactly 100 mapped xUnit methods exist: `B600_001..B600_100`.
- Read-policy contract endpoint: `GET /production/v2/readiness-contract`.

## Guardrails

- Read-policy GET surfaces only; no GET-triggered monitored-SQL collection.
- No browser-to-SQL access.
- No autonomous remediation.
- No AI-generated SQL execution.
- No plaintext credentials, full connection strings, raw provider errors, or arbitrary SQL text in output contracts.
- Missing or contradictory evidence fails closed.
- SingleNode remains first production topology.
- External IIS acceptance is never inferred from repository CI.

## Task ledger

| Range | Capability | Verification | State |
|---|---|---|---|
| B600-001..010 | Evidence freshness, source normalization, completeness and fingerprints | `B600_001..B600_010` | CI VERIFIED |
| B600-011..020 | Gate dependency graph, depth, missing prerequisites and readiness | `B600_011..B600_020` | CI VERIFIED |
| B600-021..030 | Operator action queue, ownership, priority, acknowledgement/completion gates | `B600_021..B600_030` | CI VERIFIED |
| B600-031..040 | Change-window validation, freeze conflict, approvals, backup and rollback ownership | `B600_031..B600_040` | CI VERIFIED |
| B600-041..050 | Candidate selection, version/hash/commit validation and promotion safety | `B600_041..B600_050` | CI VERIFIED |
| B600-051..060 | Evidence completeness, readiness score, contradiction detection and fail-closed summary | `B600_051..B600_060` | CI VERIFIED |
| B600-061..070 | Secret-safe operator summary normalization, redaction, allowlist and export checks | `B600_061..B600_070` | CI VERIFIED |
| B600-071..080 | Fleet readiness aggregation, severity, blast radius and deterministic fingerprint | `B600_071..B600_080` | CI VERIFIED |
| B600-081..090 | Acceptance snapshot versioning, monotonic sequence and deterministic ETag/cache contract | `B600_081..B600_090` | CI VERIFIED |
| B600-091..100 | Versioned B600 release contract, task completeness and Read-policy endpoint | `B600_091..B600_100` | CI VERIFIED |

## Acceptance mapping

Exactly 100 xUnit test methods exist and are named `B600_001` through `B600_100`, one for every task ID. Production implementation lives in `src/Monitor.Web/Services/Batch600LiveReadiness.cs`. The read-only contract endpoint is `GET /production/v2/readiness-contract` and is protected by `MonitorPolicies.Read`.

## Closure rule

Mark B600 complete only after exact-head PR merge-ref gates are Green: warnings-as-errors Release build, full suite, Real SQL acceptance and Windows production-candidate. Completing B600 must not close #116 or #111.
