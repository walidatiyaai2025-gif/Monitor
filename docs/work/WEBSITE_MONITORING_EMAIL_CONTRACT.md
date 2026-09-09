# Website Monitoring email notification contract

## Recipient model

- Individual email recipients.
- Named recipient groups containing bounded unique addresses.
- Targets reference zero or more groups; routing may add environment/severity groups.
- Address validation is required before persistence and send.

## Notification types

- Incident opened after the configured failure-confirmation threshold.
- Severity/classification materially changed.
- Escalation reminder after cooldown when still active.
- Incident recovered/resolved after the configured success-confirmation threshold.
- Administrator test message.

## Message evidence

Include only bounded non-secret evidence:

- Target display name and sanitized URL.
- Environment and incident id.
- First/last observed timestamps.
- Classification, probable layer, and confidence/reason.
- DNS/TCP/TLS/HTTP/status/content/latency evidence relevant to the classification.
- Certificate expiry date/days remaining when applicable.
- Recovery duration when resolved.

Never include Authorization/Cookie headers, SMTP credentials, protected secret values, full response bodies, query-string secrets, or internal exception stack traces.

## SMTP security

- TLS required by policy.
- SMTP credentials must be referenced through protected/external secret storage; never plaintext `appsettings.json`.
- Delivery failures must not mutate an otherwise healthy target into a website-down incident; notification delivery health is a separate operational concern.
- Outbox/retry/cooldown state must be bounded and durable before production activation.
