# Project Status

**Updated:** 2026-08-11 01:40 +03:00  
**Branch:** `agent/b100-7`  
**Target:** BATCH-100 / Batch 7 — Web/application security hardening  
**Issues:** #55 umbrella · #68 Batch 7  
**PR:** pending creation after implementation/docs head  
**Overall:** 🟢 M0–M6 VERIFIED · M7-001..M7-018 CI VERIFIED · M8 CI VERIFIED · B100-001..060 CI VERIFIED · 🟡 B100-061..070 IMPLEMENTED / FINAL CI PENDING

## BATCH-100 / Batch 7 — IMPLEMENTED, FINAL CI PENDING

B100-061..070 are implemented on `agent/b100-7`. The verified program count remains **60/100** until the final Release build/tests on the canonical-docs head succeed.

### B100-061..070 delivered

- CSP is centralized in `SecurityHeadersMiddleware`, removes `unsafe-inline`/`unsafe-eval`, denies framing/object embedding, constrains form/image/style/script/connect sources and emits a cryptographically random per-request nonce.
- A reflection-based acceptance test fails if any MVC/API `POST`, `PUT`, `PATCH` or `DELETE` action lacks `[ValidateAntiForgeryToken]`.
- Cookie authentication uses a configurable 30-minute idle lifetime plus an immutable session-start claim enforcing an 8-hour absolute lifetime that sliding renewal cannot extend.
- Login-attempt limiter keys are SHA-256-derived from normalized remote-IP/username material; raw IP/username values are not retained in limiter keys. Lockout is bounded to five failures per five-minute window and lockout outcomes are audited.
- Audit fields are bounded/control-character normalized and secret-bearing connection/credential patterns are replaced with `[redacted]`.
- Forwarded-header processing remains disabled unless at least one trusted proxy/network is explicitly configured. Enabled policy accepts only `X-Forwarded-For`/`X-Forwarded-Proto`, requires header symmetry and limits forwarding to one hop.
- HSTS is explicit/configurable with startup validation; the default is 365 days with subdomains enabled.
- SQL registration metadata now rejects control characters, overlong values and connection-string delimiter injection in host/instance metadata. Display names and secret references are bounded.
- Incident rule filters use strict bounded token normalization instead of trim/truncate-only behavior.
- Acceptance tests verify `SqlConnectionStringBuilder` treats ApplicationName/SQL username/password payloads as values rather than injected connection-string keys.
- Secret-canary tests verify audit, telemetry and login-attempt keys do not echo sensitive input.

## Security configuration defaults

```json
"WebSecurity": {
  "SessionIdleMinutes": 30,
  "SessionAbsoluteHours": 8,
  "HstsDays": 365,
  "HstsIncludeSubDomains": true,
  "HstsPreload": false,
  "TrustedProxies": [],
  "TrustedNetworks": []
}
```

Empty trusted-forwarder arrays intentionally mean Monitor does **not** process forwarded headers. Reverse-proxy deployments must explicitly add the trusted proxy IP/CIDR instead of trusting arbitrary clients.

## Stable guardrails

- Browser monitoring GETs remain cache-only; Batch 7 adds no monitored-SQL read path.
- Forwarded client/scheme metadata is fail-closed unless deployment trust is configured.
- Security telemetry/audit never needs request bodies, passwords, complete connection strings or provider exception text.
- SQL connection target metadata is constructed only through validated registration fields and `SqlConnectionStringBuilder`.
- Recommendations and Advisor remain advisory-only; no autonomous SQL execution path exists.
- `main` remains stable; this batch merges only after final CI succeeds.

## Merge gate

Create the Batch 7 PR against `main`, run GitHub Actions on the final code + canonical-docs head, require Release build with `--warnaserror` and all tests Green, then mark B100-061..070 CI VERIFIED and squash-merge only if `main` has not moved into overlapping security code.

## Next action

After Batch 7 verification/merge, execute **B100-071..080 — reliability & concurrency verification** from #55 / `docs/BATCH_100.md`.
