# Website Monitoring operator model

## Roles

- Viewer: read website status/history/incidents.
- Operator: acknowledge/reopen/resolve incidents according to existing incident authorization policy.
- Administrator: manage targets, maintenance/suppression, recipient groups, SMTP readiness/test operations.

## Guardrails

- Target writes must be attributable and audited.
- Email test/send operations must be attributable and audited without secret-bearing audit data.
- Manual `check now` should use the same bounded probe path and concurrency controls as scheduled checks.
- A target cannot be configured with an unapproved destination merely because the current user is an administrator; outbound destination policy remains authoritative.
- Maintenance windows suppress paging/email as configured but do not rewrite historical evidence.
- No automatic remediation, IIS restart, DNS change, firewall change, load-balancer mutation, or application restart is part of Website Monitoring MVP.
