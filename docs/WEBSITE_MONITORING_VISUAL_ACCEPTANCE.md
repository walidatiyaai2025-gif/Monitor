# Website Monitoring WM-7 Browser Visual Acceptance

This document closes the browser-evidence gap left after the lawful Website Monitoring implementation path `#467 -> #464` merged. It does not reopen or replace the merged SingleNode or WM-6 runtime.

## Scope

The dedicated `.github/workflows/website-monitoring-visual.yml` job exercises the real Monitor application and real cookie/RBAC login flow, then renders `/websites` in Chromium and retains screenshots as a GitHub Actions artifact.

The browser acceptance requires all of the following:

- authenticated administrator access through `/login`; no authorization middleware bypass;
- shipped fail-closed defaults remain explicit for the acceptance runtime: `WebsiteMonitoring__Enabled=false` and `WebsiteNotifications__Enabled=false`;
- target metadata may be prepared while monitoring is disabled, but no manual or scheduled outbound website probe may execute;
- the rendered summary reports monitoring `OFF` and `Check now` remains disabled;
- a wide 1440x1000 rendering is captured;
- a 390x844 responsive rendering is captured and must not introduce page-level horizontal overflow;
- Chromium is switched to `prefers-reduced-motion: reduce`, the media query must be active, and a reduced-motion rendering is captured;
- the artifact also retains the application log for failure diagnosis.

The checked-in `WebsiteMonitoringVisualAcceptanceTests` locks this workflow/script contract so ordinary CI fails if browser evidence is silently weakened or if the visual workflow starts enabling probes/notifications.

## Evidence boundary

A Green workflow run plus its retained `website-monitoring-visual-<sha>` artifact is browser/screenshot evidence for WM-7. Source-level assertions alone are not considered browser evidence.

This acceptance does **not** claim any of the following:

- external website availability or DNS/TCP/TLS/HTTP production acceptance;
- SMTP provider acceptance;
- production IIS or SQL mutation;
- RC.61 durable publication;
- #116 production acceptance or #111 closure;
- #353 repository-admin branch-protection completion.

The P0 dependency remains `#162 -> #116 -> #111`; #353 remains independent repository governance. Website Monitoring and notifications remain default-disabled in shipped configuration.
