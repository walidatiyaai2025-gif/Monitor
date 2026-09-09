# Website Monitoring acceptance criteria

The feature is acceptable only when all applicable items are true:

- Approved targets can be created/edited/paused without storing plaintext secrets.
- Scheduler performs bounded checks and survives IIS recycle without losing target state.
- DNS/TCP/TLS/HTTP/content/latency evidence is retained within explicit bounds.
- Failure classification is deterministic from observed evidence and never overclaims root cause.
- Consecutive failure threshold prevents one transient miss from opening a production incident by default.
- Consecutive recovery threshold prevents flapping from auto-resolving too early.
- Website findings reconcile through the existing incident repository/workflow.
- Maintenance suppresses notifications while preserving appropriate monitoring evidence according to policy.
- Recipient groups can route opening/escalation/recovery messages.
- SMTP credential is protected/external-reference only; no plaintext credential is persisted/logged/exported.
- Alert deduplication/cooldown prevents notification storms.
- Email delivery failure is separately observable and does not become a false website outage.
- URL destination policy blocks SSRF/private destinations by default and supports explicit approved internal allowlists.
- Redirect destination policy cannot bypass SSRF controls.
- UI shows current state, last check/success, latency, certificate expiry, incident and bounded history truthfully.
- Build/tests/Windows production-candidate/package validation are Green before merge.
- This feature does not claim P0 production acceptance and does not alter `#162 -> #116 -> #111`.
