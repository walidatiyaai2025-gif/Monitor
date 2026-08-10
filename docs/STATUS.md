# Project Status

**Updated:** 2026-08-10 16:33 +03:00  
**Branch:** `agent/b100-021-030-backup-restore`  
**Target:** BATCH-100 / Batch 3 — backup, export and restore  
**Issues:** #55 umbrella · #60 Batch 3  
**PR:** #61  
**Overall:** 🟢 M0–M6 VERIFIED · M7-001..M7-017 CI VERIFIED · M8 CI VERIFIED · B100-001..030 CI VERIFIED

## BATCH-100 / Batch 3 — CI VERIFIED

Implementation CI `31393040135`: **SUCCESS — Release build 0 warnings / 0 errors; 156/156 tests passed; Razor compiled.**

### B100-021..030 delivered

- A versioned canonical operational backup bundle exports safe registration metadata plus opaque references, deterministic incident state, bounded 24-hour history aggregates and bounded audit metadata.
- Each section is protected by a SHA-256 manifest checksum; backup version/identity, checksum, count bounds and referential integrity are validated before restore.
- Dry-run validation performs no mutation and rejects tampered, malformed, oversized or path-traversal backup requests.
- Secret-bearing properties are excluded by contract: protected credential ciphertext, Data Protection key material, provider connection strings, SQL usernames/passwords and monitored SQL text do not enter the operational bundle.
- Backup files live under a configured root outside `wwwroot`, use same-directory write-through temporary files plus atomic replacement, enforce a bundle-size cap and prune to bounded retention.
- Restore supports File and Shared persistence according to current deployment settings. Each section captures prior persisted state; any later failure rolls already-applied sections back in reverse order.
- Shared restore uses optimistic compare/exchange so concurrent control-plane changes cause a conflict rather than silent overwrite.
- Local file restore writes durable native envelopes and explicitly reports `RestartRequired=true`; Monitor does not pretend already-loaded singleton state changed live.
- InMemory deployments can export but cannot claim restart-safe restore readiness.
- Administrator-only create, dry-run and restore commands use POST + antiforgery; restore requires exact `RESTORE` confirmation.
- Settings exposes only backup count/latest/status/opaque backup identifier, never filesystem paths or secret material.

## BATCH-100 progress

Issue #55 and `docs/BATCH_100.md` define 100 tasks as ten batches of ten. **30/100 tasks are CI verified.** Batch 4 is B100-031..040 — production observability.

## Stable guardrails

- Browser monitoring GETs never trigger monitored-SQL collection.
- Dedicated shared-state SQL is Monitor-owned control-plane state only.
- Recommendations and Advisor remain advisory-only; no autonomous SQL execution path exists.
- Registration/shared operational state and operational backups exclude plaintext SQL credentials and full connection strings.
- Readiness/errors omit provider endpoint, connection value, secret reference and node identity.
- MultiNode remains fail-closed until all remaining login-security/cache prerequisites are externally coordinated or otherwise proven HA-safe.
- `main` remains stable; batch work merges only after final merge-result CI.

## Merge gate

Run GitHub Actions on the final docs head, verify `main` has not moved into overlapping backup/restore persistence code, then squash-merge PR #61 only if Release build with warnings-as-errors, Razor compilation and all tests remain Green.

## Next action

After Batch 3 merge, execute **B100-031..040 — production observability** from #55 / `docs/BATCH_100.md`.
