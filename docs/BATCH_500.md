# BATCH-500 — Production Acceptance & Recovery Safety

Issue: #130  
Scope: exactly 100 repository hardening tasks `B500-001..100`.

This batch strengthens the first SingleNode production cutover while preserving the existing P0 rule: **repository CI must not claim that the external Windows/IIS production acceptance has happened**. Issue #116 and umbrella #111 remain open until the real IIS/HTTPS/recycle/least-privilege/backup/rollback evidence is executed.

## Guardrails

- No browser-to-SQL access.
- No autonomous remediation.
- No AI-generated SQL execution.
- No plaintext credentials or full connection strings in evidence.
- No raw provider exceptions or arbitrary SQL text in evidence/export contracts.
- Deterministic, side-effect-free safety helpers.
- Fail closed on missing or contradictory evidence.
- SingleNode remains the first production topology.
- External IIS acceptance remains mandatory.

## Deployment evidence validation — B500-001..010

| Task | Code outcome | Status |
|---|---|---|
| B500-001 | Normalize production environment names | IMPLEMENTED — CI PENDING |
| B500-002 | Require absolute HTTPS evidence URI | IMPLEMENTED — CI PENDING |
| B500-003 | Normalize release artifact filename | IMPLEMENTED — CI PENDING |
| B500-004 | Validate SHA-256 evidence shape | IMPLEMENTED — CI PENDING |
| B500-005 | Validate Git commit SHA shape | IMPLEMENTED — CI PENDING |
| B500-006 | Compute non-negative evidence age | IMPLEMENTED — CI PENDING |
| B500-007 | Enforce bounded evidence freshness | IMPLEMENTED — CI PENDING |
| B500-008 | Detect missing required evidence fields | IMPLEMENTED — CI PENDING |
| B500-009 | Sanitize bounded host labels | IMPLEMENTED — CI PENDING |
| B500-010 | Produce stable deployment-evidence fingerprint | IMPLEMENTED — CI PENDING |

## IIS configuration readiness — B500-011..020

| Task | Code outcome | Status |
|---|---|---|
| B500-011 | Normalize application-pool identity | IMPLEMENTED — CI PENDING |
| B500-012 | Require Integrated pipeline mode | IMPLEMENTED — CI PENDING |
| B500-013 | Require No Managed Code runtime | IMPLEMENTED — CI PENDING |
| B500-014 | Require AlwaysRunning start mode | IMPLEMENTED — CI PENDING |
| B500-015 | Require preload enabled | IMPLEMENTED — CI PENDING |
| B500-016 | Reject 32-bit worker mode | IMPLEMENTED — CI PENDING |
| B500-017 | Require disabled idle timeout | IMPLEMENTED — CI PENDING |
| B500-018 | Validate HTTPS binding shape | IMPLEMENTED — CI PENDING |
| B500-019 | Require production host header | IMPLEMENTED — CI PENDING |
| B500-020 | Enumerate IIS readiness blockers | IMPLEMENTED — CI PENDING |

## HTTPS certificate readiness — B500-021..030

| Task | Code outcome | Status |
|---|---|---|
| B500-021 | Normalize certificate hostname | IMPLEMENTED — CI PENDING |
| B500-022 | Compute remaining certificate days | IMPLEMENTED — CI PENDING |
| B500-023 | Band certificate expiry risk | IMPLEMENTED — CI PENDING |
| B500-024 | Require RSA 2048+ key | IMPLEMENTED — CI PENDING |
| B500-025 | Reject weak signature algorithms | IMPLEMENTED — CI PENDING |
| B500-026 | Match exact/wildcard SAN | IMPLEMENTED — CI PENDING |
| B500-027 | Normalize certificate thumbprint | IMPLEMENTED — CI PENDING |
| B500-028 | Require trusted error-free chain | IMPLEMENTED — CI PENDING |
| B500-029 | Compute bounded certificate risk score | IMPLEMENTED — CI PENDING |
| B500-030 | Fail closed on certificate readiness | IMPLEMENTED — CI PENDING |

## Restart/recycle durability — B500-031..040

| Task | Code outcome | Status |
|---|---|---|
| B500-031 | Require state path outside release root | IMPLEMENTED — CI PENDING |
| B500-032 | Require key-ring path outside release root | IMPLEMENTED — CI PENDING |
| B500-033 | Preserve registration count | IMPLEMENTED — CI PENDING |
| B500-034 | Preserve snapshot evidence count | IMPLEMENTED — CI PENDING |
| B500-035 | Keep audit sequence monotonic | IMPLEMENTED — CI PENDING |
| B500-036 | Keep incident sequence monotonic | IMPLEMENTED — CI PENDING |
| B500-037 | Require protected credential resolution | IMPLEMENTED — CI PENDING |
| B500-038 | Require health recovery | IMPLEMENTED — CI PENDING |
| B500-039 | Enforce restart SLA | IMPLEMENTED — CI PENDING |
| B500-040 | Enumerate durability blockers | IMPLEMENTED — CI PENDING |

## Backup and rollback safety — B500-041..050

| Task | Code outcome | Status |
|---|---|---|
| B500-041 | Enforce backup freshness | IMPLEMENTED — CI PENDING |
| B500-042 | Require backup SHA-256 | IMPLEMENTED — CI PENDING |
| B500-043 | Require backup manifest | IMPLEMENTED — CI PENDING |
| B500-044 | Preserve previous release | IMPLEMENTED — CI PENDING |
| B500-045 | Include durable state in backup | IMPLEMENTED — CI PENDING |
| B500-046 | Include Data Protection key ring | IMPLEMENTED — CI PENDING |
| B500-047 | Require restore validation | IMPLEMENTED — CI PENDING |
| B500-048 | Require rollback smoke | IMPLEMENTED — CI PENDING |
| B500-049 | Enforce rollback SLA | IMPLEMENTED — CI PENDING |
| B500-050 | Enumerate rollback blockers | IMPLEMENTED — CI PENDING |

## Deployed least-privilege SQL — B500-051..060

| Task | Code outcome | Status |
|---|---|---|
| B500-051 | Require monitored login non-sysadmin | IMPLEMENTED — CI PENDING |
| B500-052 | Require server-state read | IMPLEMENTED — CI PENDING |
| B500-053 | Require VIEW ANY DATABASE equivalent | IMPLEMENTED — CI PENDING |
| B500-054 | Require definition metadata access | IMPLEMENTED — CI PENDING |
| B500-055 | Require SQL Agent metadata read | IMPLEMENTED — CI PENDING |
| B500-056 | Forbid target DML privilege | IMPLEMENTED — CI PENDING |
| B500-057 | Forbid target DDL privilege | IMPLEMENTED — CI PENDING |
| B500-058 | Forbid impersonation privilege | IMPLEMENTED — CI PENDING |
| B500-059 | Require successful collection | IMPLEMENTED — CI PENDING |
| B500-060 | Enumerate least-privilege blockers | IMPLEMENTED — CI PENDING |

## Health and authentication smoke — B500-061..070

| Task | Code outcome | Status |
|---|---|---|
| B500-061 | Normalize bounded health states | IMPLEMENTED — CI PENDING |
| B500-062 | Validate liveness endpoint | IMPLEMENTED — CI PENDING |
| B500-063 | Validate readiness endpoint | IMPLEMENTED — CI PENDING |
| B500-064 | Validate aggregate health endpoint | IMPLEMENTED — CI PENDING |
| B500-065 | Require authenticated Administrator login | IMPLEMENTED — CI PENDING |
| B500-066 | Require protected-route success | IMPLEMENTED — CI PENDING |
| B500-067 | Require antiforgery enforcement | IMPLEMENTED — CI PENDING |
| B500-068 | Require Secure authentication cookie | IMPLEMENTED — CI PENDING |
| B500-069 | Require HTTPS-only smoke target | IMPLEMENTED — CI PENDING |
| B500-070 | Enumerate production-smoke blockers | IMPLEMENTED — CI PENDING |

## Cutover change-window safety — B500-071..080

| Task | Code outcome | Status |
|---|---|---|
| B500-071 | Compute cutover window duration | IMPLEMENTED — CI PENDING |
| B500-072 | Validate bounded cutover window | IMPLEMENTED — CI PENDING |
| B500-073 | Normalize change-ticket id | IMPLEMENTED — CI PENDING |
| B500-074 | Require structured change-ticket id | IMPLEMENTED — CI PENDING |
| B500-075 | Require approval quorum | IMPLEMENTED — CI PENDING |
| B500-076 | Require rollback owner | IMPLEMENTED — CI PENDING |
| B500-077 | Reject change-freeze conflict | IMPLEMENTED — CI PENDING |
| B500-078 | Require backup gate before cutover | IMPLEMENTED — CI PENDING |
| B500-079 | Enumerate cutover blockers | IMPLEMENTED — CI PENDING |
| B500-080 | Fail closed Go/No-Go decision | IMPLEMENTED — CI PENDING |

## Evidence redaction and export safety — B500-081..090

| Task | Code outcome | Status |
|---|---|---|
| B500-081 | Detect password assignments | IMPLEMENTED — CI PENDING |
| B500-082 | Detect connection-string shapes | IMPLEMENTED — CI PENDING |
| B500-083 | Detect raw provider errors | IMPLEMENTED — CI PENDING |
| B500-084 | Detect arbitrary SQL text | IMPLEMENTED — CI PENDING |
| B500-085 | Normalize evidence keys | IMPLEMENTED — CI PENDING |
| B500-086 | Clamp evidence values/remove newlines | IMPLEMENTED — CI PENDING |
| B500-087 | Generate opaque evidence ids | IMPLEMENTED — CI PENDING |
| B500-088 | Sanitize evidence host | IMPLEMENTED — CI PENDING |
| B500-089 | Export only allowlisted fields | IMPLEMENTED — CI PENDING |
| B500-090 | Fail closed on unsafe evidence | IMPLEMENTED — CI PENDING |

## B500 release contract — B500-091..100

| Task | Code outcome | Status |
|---|---|---|
| B500-091 | Format B500 task ids | IMPLEMENTED — CI PENDING |
| B500-092 | Strictly parse B500 task ids | IMPLEMENTED — CI PENDING |
| B500-093 | Verify B500-001..100 completeness | IMPLEMENTED — CI PENDING |
| B500-094 | Version B500 contract schema | IMPLEMENTED — CI PENDING |
| B500-095 | Publish deterministic feature groups | IMPLEMENTED — CI PENDING |
| B500-096 | Publish explicit safety guardrails | IMPLEMENTED — CI PENDING |
| B500-097 | Publish 100-task contract manifest | IMPLEMENTED — CI PENDING |
| B500-098 | Generate stable contract SHA-256 | IMPLEMENTED — CI PENDING |
| B500-099 | Fail closed release evaluation and reject false external-acceptance claim | IMPLEMENTED — CI PENDING |
| B500-100 | Expose Read-policy /production/v1/acceptance-contract endpoint | IMPLEMENTED — CI PENDING |

## Verification

- 100 mapped xUnit tests are implemented as `B500_001..B500_100`.
- Release build must run with warnings-as-errors.
- Full repository suite must be Green before merge.
- `/production/v1/acceptance-contract` is Read-policy protected and publishes only a deterministic safety contract.
- Completing this batch does **not** satisfy the external P0.5 IIS acceptance gates.

## Delivery rule

`preserve P0.5 external gate -> implement B500-001..100 -> 100 mapped tests -> Release CI -> exact-head PR CI -> squash merge -> close #130 only`
