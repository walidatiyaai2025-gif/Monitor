# Website Monitoring outbound destination / SSRF policy

Website monitoring is an outbound request feature and must be fail-closed.

## Default rules

- Allow only `http` and `https` schemes.
- Reject URL user-info / embedded credentials.
- Reject invalid/ambiguous host and port representations.
- Resolve DNS on every check and every redirect hop.
- Reject loopback, unspecified, multicast, link-local, and platform metadata destinations by default.
- Private/internal ranges require explicit organizational allowlist policy.
- Apply destination policy to every resolved address, not only the original hostname string.
- Apply the same policy after each redirect; bound redirect hops.
- Bound request timeout, response headers, body bytes inspected, target count, scheduler concurrency, and retained history.
- Do not execute JavaScript or arbitrary user-supplied scripts in the availability MVP.

## DNS rebinding

A hostname that initially validates can later resolve to a prohibited address. Therefore persisted target validation is not sufficient: destination authorization must run against the current resolved address set immediately before connection and after every redirect resolution.

## Evidence

Incident/audit evidence may record sanitized host/port, failure stage, status, timing, certificate metadata and a bounded reason. It must not record secret-bearing headers, cookies, credentials, or arbitrary full response bodies.
