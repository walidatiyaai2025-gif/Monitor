# Project Status

**Updated:** 2026-08-10 10:51 +03:00  
**Stable branch:** `main`  
**Stable merge:** `dfbfa19cf37f82be0df4c8855bb214779c48fdc8`  
**Active branch:** `agent/m1-001-server-registration`  
**Active issue:** #3  
**Overall:** 🟡 M1-001 IMPLEMENTED — CI / PR VERIFICATION PENDING

## M0 — Visual Foundation

- PR #2 merged to `main` on 2026-08-10.
- Admin login, premium application shell, SQL Command Center, Servers, Server Details, Database Health, Memory Health, Alerts and Settings are in stable `main`.
- UI-004 Database/Memory command-view code was included in the merged M0 head `eb7e099fc2bdef05ab47df7da5bf331795fa1a2e`.
- UI-004 implementation was validated by CI run `31367759961`: SUCCESS.
- Frontend motion/filtering continues to generate no SQL traffic.

## M1-001 — Server registration and secure secret boundary — IMPLEMENTED

Current branch implementation:

- Administrator-only `/servers/register` workflow.
- Server/host, optional instance, optional port, display name and environment metadata.
- Windows Integrated and SQL Login authentication modes.
- SQL Login username is safe registration metadata; plaintext password is never exposed by registration summaries.
- SQL Login password is passed directly from the validated request into an `IConnectionSecretProtector` backed by ASP.NET Core Data Protection.
- Temporary M1 registration store holds protected cipher text only for SQL passwords.
- Windows Integrated registrations store no password.
- Duplicate host/instance/port registrations are rejected within the current application session.
- Invalid form redisplay clears password input/model-state value so secrets are never echoed back.
- Servers screen now exposes an active `Register SQL Server` action.
- Registered-target list displays safe metadata and only whether a protected credential exists.
- Registration UI clearly separates M1-001 from M1-002: no SQL connection is attempted yet.

## Security boundary

Browser POST -> MVC validation -> Data Protection -> protected temporary store.

Plaintext SQL passwords must not be written to source control, logs, TempData, ViewModels, rendered HTML, status files, or registration summaries.

## Verification

- M0 stable merge: ✅ `dfbfa19cf37f82be0df4c8855bb214779c48fdc8`.
- M1-001 implementation head before tracking-doc updates: `fad3228dd83f8e675271fe469036745377c3bd83`.
- `dotnet restore` / Release build: ⏳ pending PR-triggered CI for M1-001.
- `dotnet test`: N/A — no test project exists yet.
- visual registration workflow review: ⏳ pending.

## Next action

Open a pull request for M1-001, run the restore/build gate, fix any CI finding, visually review the registration workflow, then mark M1-001 VERIFIED. After merge, begin M1-002 Test Connection with sanitized diagnostics.
