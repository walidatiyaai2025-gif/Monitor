# Reverse-proxy deployment

Monitor treats forwarded client/scheme metadata as a trust boundary. Forwarded headers are disabled unless at least one trusted proxy IP or CIDR is explicitly configured.

## Recommended topology

```text
Operator browser --HTTPS--> approved reverse proxy/load balancer --HTTP/HTTPS private hop--> Monitor
```

Keep the Monitor listener private. Only the approved proxy and management networks should be able to reach it.

## Monitor configuration

Example for one proxy:

```json
"WebSecurity": {
  "SessionIdleMinutes": 30,
  "SessionAbsoluteHours": 8,
  "HstsDays": 365,
  "HstsIncludeSubDomains": true,
  "HstsPreload": false,
  "TrustedProxies": ["10.20.30.40"],
  "TrustedNetworks": []
}
```

Example for a controlled proxy subnet:

```json
"TrustedProxies": [],
"TrustedNetworks": ["10.20.30.0/24"]
```

Do not use `0.0.0.0/0`, `::/0`, wildcard trust, or clear the known-proxy lists in code. Monitor deliberately accepts only `X-Forwarded-For` and `X-Forwarded-Proto`, requires symmetric forwarded headers, and processes one forwarding hop.

## Proxy behavior

The proxy must:

- terminate HTTPS with an approved certificate;
- overwrite rather than append untrusted client-supplied forwarded headers;
- set the effective original client IP in `X-Forwarded-For`;
- set `X-Forwarded-Proto: https` for HTTPS requests;
- preserve the application host expected by `AllowedHosts`;
- restrict direct access to the Monitor backend listener;
- enforce request/body/header limits appropriate for an operator web application.

Monitor must not trust `X-Forwarded-Host` or arbitrary forwarding metadata.

## HSTS and HTTPS

Production responses use HSTS according to `WebSecurity`. HSTS should be emitted by one clearly owned layer; if corporate policy centralizes HSTS at the edge, align Monitor and proxy configuration so directives do not conflict. Do not enable preload until the complete domain/subdomain policy is approved.

## Health probes

Use:

- `/health/live` for process liveness.
- `/health/ready` for admission/readiness.

Do not point load-balancer probes at Dashboard, monitored server pages, login POST or Test Connection. Health probes are intentionally control-plane-only.

## Verification

From outside the backend network, confirm the backend listener cannot be reached directly. Through the public/internal production hostname, run:

```powershell
.\scripts\Smoke-Monitor.ps1 -BaseUri https://monitor.example.internal
```

Then inspect response headers and confirm:

- HTTPS is preserved to the application.
- CSP is present and does not contain `unsafe-inline` or `unsafe-eval`.
- `X-Content-Type-Options: nosniff`.
- `X-Frame-Options: DENY`.
- HSTS is present in Production.
- A spoofed forwarded header from an untrusted source does not change the application-observed client/scheme metadata.
