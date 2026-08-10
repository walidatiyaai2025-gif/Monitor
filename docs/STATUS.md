# Project Status

**Updated:** 2026-08-10 11:36 +03:00  
**Stable branch:** `main`  
**Stable head at latest reconciliation:** `81e7a1076663cd606078647908969126b22e27fe`  
**Active branch:** `agent/m1-002a-connection-lab-ui`  
**Issue:** #10  
**PR:** #11  
**Overall:** 🟡 M1-002A RECONCILED THROUGH M1-004 — FINAL CI / VISUAL REVIEW GATES

## Team progression preserved

- M1-001 registration / secret boundary: MERGED and VERIFIED.
- M1-002 backend Test Connection: MERGED and VERIFIED.
- M1-003 lightweight SQL collector: MERGED and VERIFIED.
- M1-004 ServerHealthSnapshot contract + cache: MERGED and VERIFIED.
- M1-002A is a visual follow-up only; it has been reconciled twice as `main` advanced and does not replace the team's collector/cache architecture.

## M1-002A — SQL Connection Lab UI

- Administrator-only `/servers/connections` workflow.
- Safe SQL target registration using the existing `ServerRegistration` domain/repository.
- SQL Login UI contains no password field; it accepts only an opaque external secret-reference name.
- Registration cards expose only `HasSecretReference`; raw secret-reference values are not included in target summaries.
- Windows Integrated Security requires no SQL login secret.
- Manual Test Connection calls the existing M1-002 `IServerConnectionTester`.
- Sanitized `ConnectionTestResult` fields only are rendered back to the browser.
- Servers screen exposes the Connection Lab visibly.
- Premium responsive UI uses the existing Monitor design language.
- Local auth-mode switching only; no fetch, polling, collector timer or background SQL activity.

## Connection hardening

- SqlClient error `-2` maps to `TimedOut` rather than network failure.
- Shared connection-string factory uses `ConnectRetryCount=0`.
- Existing five-second connect timeout, seven-second overall budget, non-pooled probe and external secret boundary remain unchanged.
- M1-003 collector and M1-004 cache are preserved after reconciliation.

## Verification evidence

- First reconciliation with M1-003: `602f904f30e335297838ad8f384270e1129dc57c`.
- M1-003-compatible implementation CI run `31370363183`: SUCCESS — 0 warnings / 0 errors / 23 tests passed.
- Second reconciliation with merged M1-004: `df1fea7df35d38f35677f28726edfc6df21044f6`.
- Full post-M1-004 branch CI: ⏳ pending latest tracking-doc head.
- Visual browser review of Servers -> SQL Connection Lab: ⏳ PENDING.

## Current M1 progression

- M1-001: ✅ COMPLETE.
- M1-002: ✅ COMPLETE.
- M1-002A: 🟡 CODE VERIFIED / FINAL RECONCILIATION CI + VISUAL REVIEW PENDING.
- M1-003: ✅ COMPLETE.
- M1-004: ✅ COMPLETE.
- M1-005: NEXT after Connection Lab merge gate.

## Merge gate

Do not merge PR #11 until the latest post-M1-004 commit passes Restore + Release Build + all tests and the project owner visually checks the SQL Connection Lab.

## Next action

Verify the latest CI. Then open Servers -> SQL Connection Lab in Visual Studio and review Integrated Security / SQL Login switching, safe registration cards and Test Connection result states. After acceptance, merge PR #11 and continue M1-005.
