# Website Monitoring correlation model

Website monitoring should distinguish observed failure classification from correlated dependency evidence.

## Correlation examples

- Website HTTP 5xx + linked SQL target has a concurrent critical database/connectivity incident: show `Correlated dependency evidence: SQL` without claiming SQL is the proven root cause.
- Website TCP failure + other targets on the same host/edge fail at the same time: raise confidence toward listener/network/edge-path impact.
- External probe fails while an approved internal probe succeeds: likely edge/WAF/load-balancer/Internet-path problem; retain both observations.
- Internal and external probes both fail DNS: strong DNS/name-resolution evidence.
- Internal and external probes both return the same HTTP 5xx: strong evidence that the HTTP/application path itself is returning a server-side failure.
- One website fails while sibling websites on the same host remain healthy: reduce confidence in host/network-wide outage and favor target-specific routing/application evidence.

## Rules

Correlation is advisory evidence only. It must include timestamps and source incident/check identifiers. It must never silently rewrite an observed website failure class or manufacture a root cause.

Future service mapping may link a website target to one or more Monitor SQL/server registrations so the Incident Center can surface temporally overlapping dependency incidents.
