# Project Status

**Updated:** 2026-08-11 01:46 +03:00  
**Branch:** `agent/b100-7`  
**Target:** BATCH-100 / Batch 7 — Web/application security hardening  
**Issues:** #55 umbrella · #68 Batch 7  
**PR:** #69 — BATCH-100/7: harden web and application security  
**Overall:** 🟢 M0–M6 VERIFIED · M7-001..M7-018 CI VERIFIED · M8 CI VERIFIED · B100-001..070 CI VERIFIED · 🟡 FINAL CANONICAL-DOCS CI PENDING BEFORE MERGE

## BATCH-100 / Batch 7 — CI VERIFIED

B100-061..070 are implemented on `agent/b100-7` and the implementation merge-result CI run `31439153733` is Green. The verified program count is now **70/100**.

### CI evidence

- PR: #69.
- Implementation merge-result run: `31439153733`.
- Release build: **0 warnings / 0 errors** with `--warnaserror`.
- Tests: **199 passed / 0 failed / 0 skipped**.
- One earlier compatibility failure was found in the Batch 6 incident-filter test after rule IDs moved from truncate-to-80 behavior to strict fail-closed normalization. The test contract was corrected to require rejection of overlong rule IDs; the security policy was not weakened.
- A final GitHub Actions run is still required on this canonical documentation head before squash-merge.

### B100-061..070 delivered

- CSP is centralized in `SecurityHeadersMiddleware`, removes `unsafe-inline`/`unsafe-eval`, denies framing/object embedding, constrains form/image/style/script/connect sources and emits a cryptographically random per-request nonce.
- A reflection-based acceptance test fails if any MVC/API `POST`, `PUT`, `PATCH` or `DELETE` action lacks `[ValidateAntiForgeryToken]`.
- Cookie authentication uses a configurable 30-minute idle lifetime plus an immutable session-start claim enforcing an 8-hour absolute lifetime that sliding renewal cannot extend.
- Login-attempt limiter keys are SHA-256-derived from normalized remote-IP/username material; raw IP/username values are not retained in limiter keys. Lockout is bounded to five failures per five-minute window and lockout outcomes are audited.
- Audit fields are bounded/control-character normalized and secret-bearing connection/credential patterns are replaced with `[redacted]`.
- Forwarded-header processing remains disabled unless at least one trusted proxy/network is explicitly configured. Enabled policy accepts only `X-Forwarded-For`/`X-Forwarded-Proto`, requires header symmetry and limits forwarding to one hop.
- HSTS is explicit/configurable with startup validation; the default is 365 days with subdomains enabled.
- SQL registration metadata rejects control characters, overlong values and connection-string delimiter injection in host/instance metadata. Display names and secret references are bounded.
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
- `main` remains stable until the final canonical-docs CI succeeds.

## Merge gate

Require the final PR #69 merge-result GitHub Actions run on this code + canonical-docs head to pass Release build with `--warnaserror` and all tests, confirm `main` has not moved into overlapping security code, then squash-merge.

## Next action

After Batch 7 merge, execute **B100-071..080 — reliability & concurrency verification** from #55 / `docs/BATCH_100.md`.
