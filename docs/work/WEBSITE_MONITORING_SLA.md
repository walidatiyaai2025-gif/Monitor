# Website Monitoring availability and SLA semantics

Availability must be computed from retained observed checks, not fabricated scores.

- `Up`: confirmed successful check satisfying configured HTTP/content contract.
- `Degraded`: response succeeds but a non-outage policy such as latency or certificate-expiry warning is active.
- `Down`: failure-confirmation threshold reached for an outage-class rule.
- `Unknown`: insufficient/current evidence is unavailable.

Rolling availability may be shown only for windows covered by retained check evidence. Maintenance treatment must be explicit in the displayed calculation (included or excluded) rather than silently discarded. A single transient failed probe before confirmation may be visible in history without immediately changing the confirmed incident state.
