# Project Status

**Updated:** 2026-08-10 11:41 +03:00  
**Branch:** `agent/m1-real-snapshot-ui`  
**Target:** `M1-005`  
**Issue:** TBD  
**PR:** TBD  
**Overall:** 🟡 M1-005 IMPLEMENTED — LOCAL VERIFICATION COMPLETE

## M1-005 — First real snapshot in the UI

- Optional `Monitor:PrimaryServer` metadata bootstrap with no stored credentials.
- First enabled registration replaces only the first demo estate card.
- Fresh and stale cached data are labeled per card and in page banners.
- Collection failure safely preserves the clearly labeled development fallback.
- Real details use identity/database values only; CPU, memory and jobs are Not collected.
- MVC reads through the cache once per request and never calls SQL directly.
- 30 total tests pass, including live mapping, stale labeling, fallback and configuration security.

## M1-004 — ServerHealthSnapshot contract and cache

- Canonical immutable `ServerHealthSnapshot` contract.
- Per-registration cache with 30-second fresh window and five-minute stale fallback.
- Single-flight collection prevents duplicate SQL calls from concurrent consumers.
- Refresh failure preserves the last good snapshot and labels it Stale.
- Caller cancellation does not cancel shared collection required by other callers.
- Newer collection timestamps win; future-clock ages clamp safely to zero.
- 25 total tests pass, including concurrency, freshness, stale fallback and cancellation.
- CI run `31370422613`: SUCCESS (Release build + 25 tests).

## M1-003 — Lightweight SQL collector

- Single SQL command for server name, version, edition, instance, uptime and database counts.
- One immutable `SqlServerIdentitySnapshot` result with collection timestamp.
- Shared structured connection-string factory for Test Connection and collection.
- Seven-second overall budget, cancellation propagation and safe categorized failures.
- Invalid counts and partial rows fail safely; secrets and provider exception text remain internal.
- 21 total tests pass, including one-query, redaction, cancellation and mapping paths.
- CI run `31369800023`: SUCCESS (Release build + 21 tests).

## M1-002 — Test Connection workflow

- Administrator-only POST endpoint accepts a server registration ID only.
- Official `Microsoft.Data.SqlClient` provider with structured connection-string construction.
- Five-second connection timeout within a seven-second overall budget.
- Request cancellation propagation and pooling disabled for truthful tests.
- Fixed safe outcomes for authentication, network, certificate, timeout and unexpected failures.
- Raw SQL exceptions, credentials, secret references and connection strings are not returned.
- 16 total tests pass, including controller authorization/antiforgery and redaction paths.
- CI run `31368995784`: SUCCESS (Release build + 16 tests).

## M1-001 — Server registration and secure secret boundary

- Validated SQL Server endpoint and authentication-mode model.
- Opaque connection-secret reference excluded from JSON serialization.
- Backend-only configuration secret store using User Secrets/environment configuration.
- In-memory registration repository containing no passwords or connection strings.
- Five unit/contract tests covering model invariants, non-disclosure and fail-closed secret resolution.
- CI now runs the test project after the warnings-as-errors Release build.
- CI run `31368239695`: SUCCESS (Release build + 5 tests).

## Implemented and verified in current branch

- ASP.NET Core .NET 8 solution and web project.
- Secure development Admin cookie login using a PBKDF2-SHA256 hash.
- Premium responsive shell and reusable health-state design language.
- SQL Command Center with exactly one centralized live visual pulse.
- Client-only heartbeat/clock/countdown; no fetch, polling or SQL calls.
- Servers, Server Details, Database Health, Memory Health, Alerts, Settings.
- Explicit DEVELOPMENT DATA banners and coming-soon states.
- Demo snapshot provider shared by multiple screens.
- Visual Studio launch profile opens `/login` automatically.
- CI workflow for restore + Release build with warnings treated as errors.

## UI-002 — Command Center Visual Upgrade — VERIFIED

- SQL estate topology/radar using the existing shared preview snapshot.
- Central Snapshot Core visualization.
- Server health nodes linked to Server Details.
- Highest-priority incident focus surface.
- Local-only snapshot-age progression and scan-phase transitions.
- Reduced-motion accessibility handling and responsive topology behavior.
- Adds no fetch calls, polling calls, SQL queries or independent server timers.
- CI run `31365813089`: SUCCESS.

## UI-003 — Servers & Server Details Operational Upgrade — VERIFIED

- Estate summary for reachability, attention state, database availability, SQL Agent health and offline count.
- Local-only server state filtering and name search.
- Per-server health score presentation, CPU/memory pressure bars, database availability and Agent compliance.
- Live-looking snapshot freshness progression performed only in the browser.
- Server Details command header with health envelope and attention assessment.
- DBA Focus panel that explains what should be inspected next for the represented preview state.
- Cached snapshot policy remains explicit; detailed screens do not continuously query SQL.
- UI assets are isolated in `ui003.css` / `ui003.js` and reuse the existing design tokens.
- No fetch, polling or SQL traffic added by the UI interactions.
- Final implementation head `771850b8fecd5791e9e29f426400dd930d0e47bd` validated by CI run `31366381962`: SUCCESS.

## Verification evidence

- Initial CI run `31364310669`: FAILED on one nullable warning in `AccountController` promoted by `--warnaserror`.
- Fix commit: `f25b1937869eea75e4ba2d39f0df5f879c653a01`.
- CI run `31364393808`: SUCCESS.
- Launch profile commit: `d934217684f663d3cb69db5d70bba69cfb3b1167`.
- Launch profile CI run `31365254269`: SUCCESS.
- UI-002 implementation commit: `bbeaf0817d666d7ee6af8ca1c16a83e9c6fb808b`.
- UI-002 CI run `31365813089`: SUCCESS.
- UI-003 final implementation commit: `771850b8fecd5791e9e29f426400dd930d0e47bd`.
- UI-003 CI run `31366381962`: SUCCESS.
- `dotnet restore`: ✅ VERIFIED by GitHub Actions.
- `dotnet build --configuration Release --no-restore --warnaserror`: ✅ VERIFIED by GitHub Actions.
- `dotnet test Monitor.sln --configuration Release --no-build`: ✅ 30 PASSED locally.
- visual acceptance: ✅ USER ACCEPTED on 2026-08-10.

## Merge gate

M0 PR #2 merged to stable `main` at `dfbfa19`.

## Next action

Push M1-005, verify CI, open a focused PR, then begin M1-006 throttled refresh.
