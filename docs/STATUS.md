# Project Status

**Updated:** 2026-08-10 16:12 +03:00  
**Branch:** `agent/b100-011-020-ha-secrets`  
**Target:** BATCH-100 / Batch 2 — HA credential and key management  
**Issues:** #55 umbrella · #58 Batch 2  
**PR:** #59  
**Overall:** 🟢 M0–M6 VERIFIED · M7-001..M7-017 CI VERIFIED · M8 CI VERIFIED · B100-001..020 CI VERIFIED

## BATCH-100 / Batch 2 — CI VERIFIED

Implementation CI `31391446513`: **SUCCESS — Release build 0 warnings / 0 errors; 148/148 tests passed; Razor compiled.**

The first implementation run `31390998220` failed the build on two nullable secret-reference access mistakes. Both were corrected without changing the design; the successful receipt above is the authoritative implementation gate.

### B100-011..020 delivered

- `DataProtectionKeyStore` selects the backward-compatible local file key ring or an explicit shared-state key ring.
- Shared mode persists only AES-256-GCM-encrypted Data Protection XML in the dedicated Monitor shared-state provider.
- A 256-bit key-encryption key is supplied directly from a named process environment variable and is never stored in source, appsettings, UI, audit or Monitor state.
- Missing, invalid or wrong KEK fails closed; SharedState mode never silently falls back to the local key ring.
- `CredentialPolicy:AllowLocalOwnedCredentials=false` prevents new `local:v1` writes for HA-oriented deployments.
- Administrator credential migration/reference replacement uses: resolve candidate → bounded Test Connection → commit registration metadata → delete the old Monitor-owned secret when safe.
- Failed/unavailable replacement never mutates registration metadata or deletes the current credential.
- Orphan cleanup can delete Monitor-owned local encrypted entries only; external provider secrets are never mutated.
- Rotation/cleanup audit contains actor, action, registration/aggregate target and bounded outcome only; secret references, usernames and passwords are excluded.
- Settings and Connection Lab expose aggregate credential readiness counts only and never display current secret references.
- MultiNode credential readiness requires a shared encrypted key ring, local-owned creation disabled and zero local-owned registration references.

## BATCH-100 progress

Issue #55 and `docs/BATCH_100.md` define 100 tasks as ten batches of ten. **20/100 tasks are CI verified.** Batch 3 is B100-021..030 — backup, export and restore.

## Stable guardrails

- Browser monitoring GETs never trigger monitored-SQL collection.
- Dedicated shared-state SQL is Monitor-owned control-plane state only.
- Recommendations and Advisor remain advisory-only; no autonomous SQL execution path exists.
- Registration/shared operational state excludes plaintext SQL credentials and full connection strings.
- Readiness/errors omit provider endpoint, connection value, secret reference and node identity.
- MultiNode remains fail-closed until all remaining login-security/cache prerequisites are externally coordinated or otherwise proven HA-safe.
- `main` remains stable; batch work merges only after final merge-result CI.

## Merge gate

Run GitHub Actions on the final docs head, verify `main` has not moved into overlapping credential/key-management code, then squash-merge PR #59 only if Release build with warnings-as-errors, Razor compilation and all tests remain Green.

## Next action

After Batch 2 merge, execute **B100-021..030 — backup, export and restore** from #55 / `docs/BATCH_100.md`.
