# Maintenance & Suppression Runbook

## Purpose

Use maintenance windows to pause **scheduled snapshot collection** for a registered server during approved work. Use alert suppression to change **operator actionability presentation** during a known-noise window. Neither feature changes monitored SQL configuration or incident evidence.

## Before maintenance

1. Confirm the target registration, environment, group and tags in Enterprise Operations.
2. Record the approved UTC start/end and a bounded reason. Windows are start-inclusive and end-exclusive.
3. Use alert suppression only when noisy deterministic findings are expected. Suppression never resolves an incident.
4. Re-open the server details page and verify the maintenance/suppression badge from Monitor-owned metadata.

## During maintenance

- Scheduled collection is skipped while maintenance is active.
- If operator policy cannot be read, scheduled collection fails closed.
- A manual refresh remains available to Operator/Administrator roles. Treat it as an explicit override; Monitor audits the override before and after the refresh.
- Navigation and reports continue to use cached/control-plane data and do not initiate collection.

## After maintenance

1. Allow the window to expire or explicitly update the profile under the Administrator policy.
2. Request one bounded manual refresh only when new evidence is required.
3. Confirm freshness and deterministic findings before resolving incidents.
4. Review the audit trail for metadata changes and any maintenance overrides.

## Safety

Do not place credentials, connection strings, SQL text or provider errors in maintenance/suppression reasons. Do not use suppression to hide unresolved risk; evidence and lifecycle state remain intact.
