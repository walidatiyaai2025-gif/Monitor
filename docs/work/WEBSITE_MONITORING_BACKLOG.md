# Website Monitoring backlog

Parent scope: `docs/work/WEBSITE_MONITORING_EPIC.md`.

1. WM-1 foundation: target validation, outbound destination policy, DNS/TCP/TLS/HTTP probe, classification tests.
2. WM-2 durable targets/scheduler: SingleNode persistence, bounded history, scheduler, restart/multi-worker safety.
3. WM-3 incident integration: existing HealthFinding/IHealthIncidentRepository, confirmation/recovery thresholds, recommendations.
4. WM-4 notifications: recipient groups, SMTP readiness, protected credential reference, dedup/cooldown/outbox/recovery email.
5. WM-5 UI/reporting: target inventory/details/history, dashboard, groups/settings, availability/latency/certificate reporting.
6. WM-6 HA hardening: SharedState/distributed ownership/idempotency before MultiNode activation.

Priority: after the active P0 dependency `#162 -> #116 -> #111` unless explicitly promoted by project owner.
