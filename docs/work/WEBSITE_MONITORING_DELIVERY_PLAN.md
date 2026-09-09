# Website Monitoring delivery plan

## Phase A — Availability foundation

Deliver target validation, fail-closed outbound destination policy, deterministic failure classification, and unit tests. No scheduler or outbound traffic is activated merely by merging the contracts.

## Phase B — Real probes

Implement one bounded probe pipeline that resolves DNS, validates destination policy, establishes TCP/TLS as applicable, performs HTTP, applies redirect policy, validates expected status/content and records latency/certificate evidence. Manual Check Now and scheduled checks must share this service.

## Phase C — Durable operations

Add durable target/check-state/history storage under the stable App_Data boundary plus SingleNode multi-worker lease safety and a bounded hosted scheduler.

## Phase D — Incidents and correlation

Project confirmed website failures into the existing Monitor incident workflow; add recovery confirmation, recommendations, maintenance suppression and optional service mapping to existing SQL/server registrations for timestamped correlation evidence.

## Phase E — Notifications

Add recipient groups, SMTP readiness, protected credential reference, bounded durable outbox, cooldown/deduplication, escalation and recovery emails.

## Phase F — UI / reporting

Add website inventory/details/history, active incidents, availability/latency/certificate views and notification administration using the existing design system.

## Phase G — MultiNode

Only after explicit HA acceptance, coordinate due-check ownership/outbox/incident state through existing SharedState/distributed lease primitives.
