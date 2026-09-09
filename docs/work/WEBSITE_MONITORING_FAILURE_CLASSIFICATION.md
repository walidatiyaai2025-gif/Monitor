# Website Monitoring failure classification

Classification must be evidence-led and bounded.

| Rule | Observed evidence | Probable layer |
|---|---|---|
| `dns.failure` | Host cannot resolve to an approved usable address | DNS/name resolution |
| `network.connect-failure` | DNS succeeded; TCP connection failed/refused/unreachable | Network/listener path |
| `network.timeout` | Connect/request exceeded bounded timeout without stronger application evidence | Network/proxy/application unknown |
| `tls.invalid` | HTTPS TLS handshake or certificate trust/name/validity failed | TLS/certificate |
| `tls.expiring` | Certificate valid but within configured expiry warning window | Certificate lifecycle |
| `http.4xx` | HTTP response 4xx observed | HTTP/application/auth/routing |
| `http.5xx` | HTTP response 5xx observed | Web server/proxy/application |
| `http.unexpected-status` | HTTP response outside configured expected contract | HTTP/application contract |
| `redirect.unexpected` | Redirect hop/final host violates configured contract | HTTP/proxy/routing |
| `content.mismatch` | Expected bounded content marker absent from successful response | Application/content |
| `performance.slow` | Successful response exceeds configured latency threshold | Performance path |
| `unknown` | Evidence insufficient for a narrower classification | Unknown |

The system must not convert a probable layer into a statement of proven root cause. For example, an HTTP 500 proves the HTTP/application path returned a server error; it does not by itself prove whether the defect is application code, IIS, reverse proxy, dependency, or database.
