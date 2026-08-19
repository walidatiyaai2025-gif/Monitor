# Website Monitoring Epic

## Status

Planned feature work on `feature/website-monitoring-foundation`. This does **not** change the active P0 production dependency order `#162 -> #116 -> #111` and must not be merged ahead of P0 closure without an explicit priority decision.

## Goal

Add first-class website/application monitoring to Monitor so operators can register one or more HTTP/HTTPS endpoints, verify availability and expected behavior on a schedule, classify likely failure domains, open/reconcile incidents, and notify configured people or groups by email.

## Functional scope

### Website targets

Each target must support:

- Display name and URL.
- Environment (`production`, `staging`, `test`, `development`).
- Check interval and timeout within bounded policy limits.
- Enabled/paused state and maintenance suppression.
- Expected HTTP status or status range.
- Optional expected text/content marker with bounded response-body inspection.
- Optional redirect policy and expected final host.
- Optional custom `Host` header only when explicitly allowed by policy.
- Tags / owner / business service metadata for routing.
- One or more notification recipient groups.

Secrets such as Basic/Bearer credentials must never be stored in plaintext configuration or incident evidence. Any future authenticated check must use the existing protected/external secret-reference patterns.

### Probe pipeline

For each target, collect bounded evidence for:

1. DNS resolution.
2. TCP connection to resolved address/port.
3. TLS handshake and certificate validation for HTTPS.
4. HTTP request/response.
5. Redirect outcome.
6. Status-code expectation.
7. Bounded content marker expectation when configured.
8. Response duration / timeout.

A successful check records availability and latency without manufacturing unsupported diagnostics.

### Failure classification

Classify only from observed evidence and keep an explicit confidence/reason trail. Initial categories:

- `dns.failure` — DNS resolution failed / no usable address.
- `network.connect-failure` — DNS succeeded but TCP connect failed/refused/unreachable.
- `network.timeout` — connect/request timed out without sufficient application evidence.
- `tls.invalid` — certificate trust/name/chain/expiry validation failed.
- `tls.expiring` — certificate is valid but inside configured warning window.
- `http.4xx` — application/web-server returned client error.
- `http.5xx` — application/web-server/proxy returned server error.
- `http.unexpected-status` — response returned but outside configured contract.
- `content.mismatch` — HTTP succeeded but required bounded marker was absent.
- `redirect.unexpected` — redirect/final-host contract failed.
- `performance.slow` — successful response exceeds configured latency threshold.
- `unknown` — evidence is insufficient for a narrower diagnosis.

Do not claim "application bug", "network outage", "IIS failure", "load balancer failure", or similar as fact unless the collected evidence proves it. The UI/email may show a "likely layer" inference with the supporting evidence.

### Incident lifecycle

Reuse the existing `IHealthIncidentRepository` / incident workflow semantics instead of creating a second incident system.

- Stable incident key per website target + rule.
- Open on confirmed failure after configurable consecutive-failure threshold.
- Update occurrence count/evidence while still failing.
- Acknowledge via existing incident workflow.
- Auto-resolve only after configurable consecutive-success threshold.
- Reopen if the same rule fails again.
- Maintenance/suppression must not page/email.
- Deduplicate repeated notifications with cooldown policy.
- Record recovery notification when an incident resolves.

### Email notifications

Add notification recipients and groups with bounded, validated email addresses.

Support:

- Individual recipients.
- Named groups (for example `Web Team`, `Network Team`, `Management`).
- To/CC/BCC policy if required later; initial implementation can use To only.
- Rule/environment/severity routing.
- Cooldown/deduplication.
- Initial alert, escalation reminder, and recovery messages.
- Test-email action for administrators.
- SMTP over TLS with credentials obtained only through protected/external secret references.
- No SMTP password in `appsettings.json`, logs, audit, incidents, exports, or email bodies.

Email content should include target, environment, first/last seen time, HTTP/TLS/DNS/TCP evidence, probable failure layer, incident id, and recovery state. Do not include secret-bearing headers or full response bodies.

### Dashboard / operator UI

Add a Website Monitoring area with:

- Target inventory and enable/pause/maintenance state.
- Current Up / Degraded / Down / Unknown status.
- Last checked and last success.
- Response time and rolling availability based on retained evidence.
- Certificate days remaining.
- Active incident count/severity.
- Latest classified failure and evidence summary.
- Target details page with bounded check history.
- Incident link / acknowledge / resolve operations through existing incident workflow.
- Recipient-group management and SMTP readiness/test controls for authorized admins.

### Security and abuse boundaries

Website monitoring creates outbound network traffic and therefore requires explicit SSRF controls:

- Only `http` and `https` schemes.
- Reject embedded URL credentials.
- Normalize and validate host/port.
- Default deny loopback, link-local, multicast, unspecified, and cloud metadata addresses.
- Re-evaluate resolved addresses on every check to prevent DNS rebinding bypass.
- Provide an explicit allowlist policy for approved private/internal ranges required by the organization.
- Bound redirect count and apply the same destination policy on every redirect hop.
- Bound response-body bytes inspected.
- Bound timeout, concurrency, target count, retained history, and email queue size.
- Never execute page JavaScript or arbitrary scripts in the first implementation.
- Do not use browser automation for the availability MVP; synthetic browser journeys can be a later separately-governed feature.

### Scheduling / HA

- SingleNode: hosted scheduler with bounded concurrency and durable target/check state.
- MultiNode: before activation, use existing SharedState/distributed-lease primitives so one due check is owned by one node and notification/incident reconciliation remains idempotent.
- A worker restart must not lose registered targets, active incidents, or notification cooldown state.

## Suggested implementation slices

### WM-1 — foundation

- Target contracts + validation.
- Probe result/evidence contracts.
- DNS/TCP/TLS/HTTP probe service.
- Bounded diagnostic classifier.
- Unit tests for classification and SSRF/destination policy.

### WM-2 — durable targets + scheduler

- File-backed SingleNode target repository under stable App_Data.
- Bounded check-history store.
- Hosted scheduler, concurrency/timeout limits, restart durability.
- Multi-worker file lease safety.

### WM-3 — incident integration

- Convert classified website findings into existing `HealthFinding` / incident reconciliation.
- Consecutive failure/recovery thresholds.
- Stable rule IDs and recommendation catalog entries.

### WM-4 — email notification engine

- SMTP options/readiness.
- Protected/external secret reference for SMTP credential.
- Recipient/group repository.
- Dedup/cooldown/escalation/recovery notification state.
- Bounded outbox / retry policy.
- Admin test-email action.

### WM-5 — UI and reports

- Website inventory/detail/history pages.
- Dashboard summary cards.
- Recipient group and notification settings UI.
- Availability/latency/certificate reporting/export.

### WM-6 — HA hardening

- SharedState target/check/outbox state as required.
- Distributed ownership/idempotency tests.
- Windows production-candidate and deployment-package validation.

## Definition of done

A production-configured Monitor instance can register approved website targets, run bounded checks on schedule, distinguish observed DNS/TCP/TLS/HTTP/content/latency failure classes, create/reconcile durable incidents, suppress maintenance noise, email configured individual/group recipients without leaking secrets, show current/history evidence in the UI, survive IIS recycle, and pass repository CI + Windows production-candidate gates. MultiNode activation requires separate SharedState/distributed-ownership acceptance.
