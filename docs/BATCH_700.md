# BATCH-700 — Full UI Completion

**Parent:** #220  
**Foundation:** #221  
**Health:** #222  
**Audit/history:** #223  
**Recommendations/reports:** #224  
**Enterprise/admin/final:** #225

## Objective

Close the gap between backend capability completion and a complete visible operator product. A route is not considered UI-complete merely because a Razor view exists.

## Non-negotiable boundaries

- Browser GETs for monitored data remain cache/control-plane only.
- Missing evidence is explicit; no synthetic zero or fake health state.
- No autonomous remediation or AI SQL execution.
- No credential, connection-string, SQL text, raw provider-error, filesystem-path, or exception-detail disclosure.
- Mutations retain role policies and antiforgery.
- Desktop, tablet, and 390px mobile are acceptance targets.
- Status uses text as well as color; keyboard/focus behavior is required.

## Visible route inventory — current main baseline

| Area | Route | Controller/action | Current surface | B700 disposition |
|---|---|---|---|---|
| Operations | `/dashboard` | Operations/Dashboard | Command Center | shell acceptance |
| Operations | `/servers` | Operations/Servers | estate list | shell acceptance |
| Operations | `/servers/{id}` | Operations/ServerDetails | server evidence | shell acceptance |
| Operations | `/alerts` | Operations/Alerts | incident center | shell acceptance |
| Health | `/database-health` | Operations/DatabaseHealth | generic health partial | dedicated page in #222 |
| Health | `/memory-health` | Operations/MemoryHealth | dedicated view | consistency in #222 |
| Health | `/performance-health` | Portal/Performance | basic cached table | dashboard completion in #222 |
| Health | `/backups` | Operations/Backups | generic HealthModules view | dedicated page in #222 |
| Health | `/jobs` | Operations/Jobs | generic HealthModules view | dedicated page in #222 |
| Health | `/storage` | Operations/Storage | generic HealthModules view | dedicated page in #222 |
| Health | `/blocking` | Operations/Blocking | generic HealthModules view | dedicated page in #222 |
| Intelligence | `/enterprise/fleet` | FleetIntelligence/Index | fleet view | drill-down audit in #225 |
| Intelligence | `/enterprise` | EnterpriseOperations/Overview | enterprise overview | drill-down audit in #225 |
| Intelligence | `/recommendations` | Portal/Recommendations | recommendation cards | filters/context in #224 |
| Intelligence | `/reports` | Portal/Reports | export cards | metadata/failure UX in #224 |
| Admin | `/servers/connections` | ConnectionLab/Index | target management | state completion in #225 |
| Admin | `/observability` | Observability/Index | telemetry view | hierarchy/states in #225 |
| Admin | `/audit` | Operations/Audit | minimal table | full operator UX in #223 |
| Admin | `/enterprise/readiness` | EnterpriseHelp/Readiness | readiness view | grouped checklist in #225 |
| Admin | `/settings` | Operations/Settings | settings view | information architecture in #225 |
| Help | `/enterprise/help` | EnterpriseHelp/Help | operator help | runbook navigation in #225 |
| History | `/history/{registrationId}` | Operations/History | minimal table | full operator UX in #223 |
| Safety | `/error` | Error/ServerError | added in #221 | safe 500 surface |
| Safety | `/access-denied` | Error/AccessDenied | added in #221 | safe 403 surface |
| Safety | `/error/status/{statusCode}` | Error/Status | added in #221 | safe status routing |

Download/report/diagnostic endpoints are governed separately from page routes; #224 verifies their discoverability and role-safe presentation.

## Foundation #221

- [x] UI700-001 — visible route/controller/view/navigation inventory recorded here.
- [x] UI700-002 — dedicated safe 403/404/500 Razor surfaces added.
- [x] UI700-003 — production exception/status handling and cookie access-denied path are wired to the safe endpoints.
- [x] UI700-004 — reusable page-heading contract added for gradual page adoption.
- [x] UI700-005 — reusable portal state contract added for empty/unavailable/stale/error states.
- [x] UI700-006 — active navigation matching uses route boundaries; Reports is exact so downloads do not masquerade as page navigation.
- [x] UI700-007 — mobile navigation toggle is keyboard-aware, Escape-closeable, and collapses after navigation.
- [x] UI700-008 — shared responsive table/card/state CSS contracts added.
- [x] UI700-009 — `portal.css` expanded beyond sidebar-only glue into portal/error/state/mobile contracts.
- [ ] UI700-010 — regression suite must be Green in GitHub Actions before #221 closes.

## Closure rule

Do not mark BATCH-700 complete from task bookkeeping alone. Every child batch requires its exact implementation PR, Release build warnings-as-errors, full tests, route/policy acceptance, and final docs reconciliation. BATCH-700 never substitutes for external production acceptance #116/#111.
