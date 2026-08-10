# Project Status

**Updated:** 2026-08-10 11:31 +03:00  
**Stable branch:** `main`  
**Stable head at reconciliation:** `af5bc9afc8382d2bd579b9bb2f1d1194267fae97`  
**Active branch:** `agent/m1-002a-connection-lab-ui`  
**Issue:** #10  
**PR:** #11  
**Overall:** 🟡 M1-002A CODE + CI VERIFIED — VISUAL REVIEW PENDING

## Team reconciliation

- M1-001 is merged and verified.
- M1-002 backend Test Connection was merged through PR #7.
- M1-003 lightweight SQL identity collector was merged while M1-002A was being developed.
- M1-002A was explicitly reconciled on top of M1-003 rather than reverting or replacing the team's shared `SqlConnectionStringFactory` / collector architecture.

## M1-002A — SQL Connection Lab UI

Implemented and CI verified:

- Administrator-only `/servers/connections` page.
- Safe SQL target registration using the existing `ServerRegistration` domain and repository.
- SQL Login UI contains no password field; only an opaque external secret-reference name is accepted.
- Registered-target summaries expose `HasSecretReference` only and never expose the raw reference value.
- Windows Integrated Security stores/resolves no SQL login secret.
- Manual Test Connection calls the existing verified `IServerConnectionTester` backend.
- Test results render only sanitized `ConnectionTestResult` fields.
- Premium responsive visual treatment consistent with the existing Monitor design system.
- Servers screen now exposes a visible `SQL Connection Lab` action.
- Authentication-mode switching is local browser behavior only; no fetch, polling or background SQL activity was added.

## M1-002 hardening included in M1-002A

- SqlClient provider error `-2` is classified as `TimedOut` instead of `NetworkUnavailable`.
- Shared SQL connection profiles explicitly use `ConnectRetryCount=0`.
- Existing overall timeout, non-pooled connection behavior, secret boundary and sanitized result architecture remain intact.
- M1-003 shared connection-string factory and collector are preserved.

## Verification evidence

- Reconciliation merge commit: `602f904f30e335297838ad8f384270e1129dc57c`.
- Final implementation head before docs: `f6020c6066a3b00f49126d3931a7e8214ae9ee15`.
- CI run `31370363183`: SUCCESS.
- `dotnet restore Monitor.sln`: ✅.
- Release build with `--warnaserror`: ✅ 0 warnings / 0 errors.
- `dotnet test Monitor.sln --configuration Release --no-build`: ✅ 23 passed / 0 failed / 0 skipped.
- Provider-timeout mapping test: ✅.
- Connection Lab summary raw-secret-reference exclusion test: ✅.
- Visual browser review of SQL Connection Lab: ⏳ PENDING.

## Current M1 progression

- M1-001 Registration / secret boundary: ✅ COMPLETE.
- M1-002 Backend Test Connection: ✅ COMPLETE.
- M1-002A Connection Lab UI: 🟡 CI VERIFIED / VISUAL REVIEW PENDING.
- M1-003 Lightweight SQL identity collector: ✅ COMPLETE / MERGED.
- M1-004 ServerHealthSnapshot contract + cache: NEXT after UI merge gate.

## Merge gate

Do not merge PR #11 until the project owner visually checks Servers -> SQL Connection Lab. Code/build/test gates are green and the branch is reconciled with merged M1-003.

## Next action

Open the SQL Connection Lab in Visual Studio, verify Integrated Security / SQL Login field switching, registration cards and safe Test Connection result states. After visual acceptance, merge PR #11 and begin M1-004.
