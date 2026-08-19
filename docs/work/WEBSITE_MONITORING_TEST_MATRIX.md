# Website Monitoring minimum test matrix

## Target validation / SSRF

- rejects non-http(s) schemes and embedded credentials;
- rejects loopback/link-local/multicast/unspecified/default metadata endpoints;
- rejects DNS result containing any prohibited destination according to policy;
- explicit approved internal allowlist permits intended internal address;
- redirect to prohibited destination is rejected;
- DNS re-resolution is evaluated per check/hop.

## Probe/classifier

- DNS failure -> `dns.failure`;
- DNS success + refused connect -> `network.connect-failure`;
- timeout -> `network.timeout` unless stronger evidence exists;
- TLS name/trust/expiry failure -> `tls.invalid`;
- certificate warning window -> `tls.expiring` degraded;
- HTTP 404 -> `http.4xx`;
- HTTP 500 -> `http.5xx`;
- unexpected configured status -> `http.unexpected-status`;
- missing bounded marker -> `content.mismatch`;
- slow successful response -> `performance.slow`;
- compliant response -> Up.

## Incident confirmation

- one miss below threshold does not open incident;
- threshold opens stable target+rule incident;
- continued misses update occurrence/evidence without duplicate incident;
- recovery threshold resolves once;
- failure after recovery reopens according to existing semantics;
- maintenance suppresses notification path.

## Notification

- group recipient de-duplication;
- invalid addresses rejected;
- cooldown prevents duplicate storm;
- recovery message emitted once;
- SMTP transient failure retries within bounded policy;
- credential/authorization/response body never appears in log/audit/rendered email;
- send failure does not create false website-down incident.

## Durability/concurrency

- IIS recycle preserves targets/check state/outbox;
- overlapping SingleNode workers cannot double-own due check or duplicate notification;
- bounded history/outbox/cooldown stores fail closed or prune according to policy;
- MultiNode tests required before MultiNode activation.
