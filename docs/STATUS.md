# Project Status

**Updated:** 2026-08-10 11:18 +03:00  
**Stable branch:** `main`  
**Stable head at task start:** `4398c7cd91e38f37dc65b164cdcc381868a41205`  
**Active branch:** `agent/m1-002-test-connection`  
**Issue:** #6  
**PR:** #8  
**Overall:** 🟡 M1-002 CODE + CI VERIFIED — VISUAL REVIEW PENDING

## M1-001 — Server registration and secure secret boundary — MERGED / VERIFIED

- Merged through PR #4 into `main`.
- Validated SQL Server endpoint and authentication-mode domain model.
- Opaque `ConnectionSecretReference` excluded from normal JSON output.
- Backend-only configuration secret store using User Secrets/environment configuration.
- In-memory registration repository contains no passwords or connection strings.
- CI run `31368239695`: SUCCESS with 5 tests.

## M1-002 — Test Connection workflow — CODE + CI VERIFIED

Implemented on `agent/m1-002-test-connection`:

- Administrator-only `/servers/connections` SQL Connection Lab.
- Safe SQL target metadata registration on top of the M1-001 domain/repository.
- No password input exists in the browser UI.
- SQL Login registrations accept an opaque secret reference only; username/password values remain in external configuration.
- Integrated Security never resolves a SQL Login secret.
- Backend `SqlConnectionProfileFactory` builds the connection string immediately before the probe.
- `Microsoft.Data.SqlClient` is used only inside the backend connection boundary.
- Test Connection performs one deliberate connection attempt: `ConnectTimeout=5`, `ConnectRetryCount=0`, `Pooling=false`.
- Test Connection executes no collector query; M1-003 remains responsible for lightweight SQL identity collection.
- Success returns only safe operational metadata: DataSource, ServerVersion and elapsed milliseconds.
- Failures map to fixed categories/messages: authentication, timeout, network, TLS/certificate, invalid configuration, unexpected failure, registration missing/disabled and secret unavailable.
- Raw `SqlException` messages, connection strings, usernames, passwords and secret-reference values are never returned in browser-facing test results.
- Servers screen now exposes the SQL Connection Lab.
- Dedicated responsive Connection Lab styling and local auth-mode interaction added.

## M1-002 verification evidence

- Initial PR CI run `31369159435`: FAILED on one compile error (`ConnectionSecretReference` value type required an explicitly nullable local).
- Fix commit: `528a944fdebe018a083033a3df9d2039650e80b6`.
- Follow-up static test-signature hardening commit: `66ccdd3cdb1c1aa66b512185fe0b5f6f04e8832a`.
- CI run `31369329964`: SUCCESS.
- `dotnet restore Monitor.sln`: ✅.
- Release build with `--warnaserror`: ✅ 0 warnings / 0 errors.
- `dotnet test Monitor.sln --configuration Release --no-build`: ✅ 11 passed / 0 failed / 0 skipped.
- Browser visual review of SQL Connection Lab: ⏳ PENDING.

## Security boundary

For SQL Login:

`Registration -> opaque secret reference -> backend secret resolver -> transient SqlConnectionStringBuilder -> non-pooled SqlConnection`

The application must never log, render, serialize or persist the resolved password or full connection string.

## Merge gate

Do not merge PR #8 to stable `main` until the SQL Connection Lab receives a visual workflow check. Code/build/test gates are green.

## Next action

Open Servers -> SQL Connection Lab, visually verify registration/auth-mode switching/result states, then merge PR #8. After merge, begin M1-003 lightweight SQL identity collection using the verified connection boundary.
