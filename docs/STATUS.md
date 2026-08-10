# Project Status

**Updated:** 2026-08-10 10:51 +03:00  
**Branch:** `agent/m0-visual-foundation`  
**Target:** `0.0.1-ui-preview`  
**Issue:** #1  
**PR:** #2  
**Overall:** 🟡 BASELINE M0 ACCEPTED — UI-004 CI VERIFIED / TARGETED VISUAL REVIEW PENDING

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
- No fetch, polling or SQL traffic added by the UI interactions.
- Final implementation head `771850b8fecd5791e9e29f426400dd930d0e47bd` validated by CI run `31366381962`: SUCCESS.

## UI-004 — Database Health & Memory Health Command Views — VERIFIED BY CI

- Database estate availability summary with online/unavailable/impacted/offline counts.
- Database operational matrix with local-only health filtering and availability bars.
- Database attention queue focused on unavailable databases and unreachable instances.
- Memory estate pressure summary with average, peak, elevated and warning counts.
- Per-instance memory heatmap with pressure thresholds and browser-only freshness age.
- DBA memory triage panel explaining what to inspect next when real counters are connected.
- Future diagnostic enrichment is surfaced explicitly: OS memory, max server memory, memory clerks, PLE/grants and evidence-backed recommendations.
- UI-004 assets are isolated in `ui004.css` / `ui004.js` and reuse the existing design tokens.
- No fetch, polling or SQL traffic added by UI-004.
- Final UI-004 implementation head `eb7e099fc2bdef05ab47df7da5bf331795fa1a2e` validated by CI run `31367759961`: SUCCESS (Restore + Release Build).

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
- UI-004 final implementation commit: `eb7e099fc2bdef05ab47df7da5bf331795fa1a2e`.
- UI-004 CI run `31367759961`: SUCCESS.
- `dotnet restore`: ✅ VERIFIED by GitHub Actions.
- `dotnet build --configuration Release --no-restore --warnaserror`: ✅ VERIFIED by GitHub Actions.
- `dotnet test`: N/A — no test project exists yet.
- baseline visual acceptance: ✅ USER ACCEPTED before UI-004 landed.
- targeted UI-004 browser review: ⏳ PENDING.

## Merge gate

The previously accepted M0 baseline remains accepted, but UI-004 was added afterwards. Do not merge PR #2 until Database Health and Memory Health receive a targeted visual check. `main` remains stable.

## Next action

Open Database Health and Memory Health from the refreshed branch, complete the targeted visual check, then merge PR #2. After merge, begin M1-001 on a new task branch.
