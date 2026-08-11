# BATCH-500 — Production Acceptance & Recovery Safety

Issue: #130  
Scope: exactly 100 repository hardening tasks `B500-001..100`.  
State: **100/100 CI VERIFIED**.

BATCH-500 strengthens the first SingleNode production cutover while preserving the P0 boundary: repository CI does **not** prove the external Windows/IIS production acceptance. Issue #116 and umbrella #111 remain open until the real IIS/HTTPS/recycle/least-privilege/backup/rollback evidence is executed.

## Verification evidence

- Normal CI `31488078873`: **Green**.
- Release build: **0 warnings / 0 errors**.
- Full suite: **638/638 passed**, 0 failed, 0 skipped.
- Windows production-candidate `31488078882`: **Green end-to-end**.
- Windows gate passed Release build, 638-test suite, production PowerShell syntax parsing, `win-x64` publish, secret-free candidate validation, HTTPS health/auth smoke, process restart/auth recovery, clean package validation and artifact upload.
- Windows artifact: `Monitor-0.1.0-rc.26-win-x64`, Actions artifact ID `9099961916`.
- 100 mapped xUnit tests are named `B500_001..B500_100`.
- Read-policy contract endpoint: `GET /production/v1/acceptance-contract`.

## Guardrails

- External IIS acceptance remains mandatory.
- No browser-to-SQL access.
- No autonomous remediation.
- No AI-generated SQL execution.
- No plaintext credentials/full connection strings/raw provider exceptions/arbitrary SQL text in evidence contracts.
- Deterministic, side-effect-free safety helpers.
- Missing or contradictory evidence fails closed.
- SingleNode remains the first production topology.

## B500-001..010 — Deployment evidence validation

| Task | Outcome | State |
|---|---|---|
| B500-001 | Normalize production environment names | CI VERIFIED |
| B500-002 | Require absolute HTTPS evidence URI | CI VERIFIED |
| B500-003 | Normalize release artifact filename | CI VERIFIED |
| B500-004 | Validate SHA-256 evidence shape | CI VERIFIED |
| B500-005 | Validate Git commit SHA shape | CI VERIFIED |
| B500-006 | Compute non-negative evidence age | CI VERIFIED |
| B500-007 | Enforce bounded evidence freshness | CI VERIFIED |
| B500-008 | Detect missing required evidence fields | CI VERIFIED |
| B500-009 | Sanitize bounded host labels | CI VERIFIED |
| B500-010 | Produce stable deployment-evidence fingerprint | CI VERIFIED |

## B500-011..020 — IIS configuration readiness

| Task | Outcome | State |
|---|---|---|
| B500-011 | Normalize application-pool identity | CI VERIFIED |
| B500-012 | Require Integrated pipeline mode | CI VERIFIED |
| B500-013 | Require No Managed Code runtime | CI VERIFIED |
| B500-014 | Require AlwaysRunning start mode | CI VERIFIED |
| B500-015 | Require preload enabled | CI VERIFIED |
| B500-016 | Reject 32-bit worker mode | CI VERIFIED |
| B500-017 | Require disabled idle timeout | CI VERIFIED |
| B500-018 | Validate HTTPS binding shape | CI VERIFIED |
| B500-019 | Require production host header | CI VERIFIED |
| B500-020 | Enumerate IIS readiness blockers | CI VERIFIED |

## B500-021..030 — HTTPS certificate readiness

| Task | Outcome | State |
|---|---|---|
| B500-021 | Normalize certificate hostname | CI VERIFIED |
| B500-022 | Compute remaining certificate days | CI VERIFIED |
| B500-023 | Band certificate expiry risk | CI VERIFIED |
| B500-024 | Require RSA 2048+ key | CI VERIFIED |
| B500-025 | Reject weak signature algorithms | CI VERIFIED |
| B500-026 | Match exact and one-label wildcard SAN | CI VERIFIED |
| B500-027 | Normalize certificate thumbprint | CI VERIFIED |
| B500-028 | Require trusted error-free chain | CI VERIFIED |
| B500-029 | Compute bounded certificate risk score | CI VERIFIED |
| B500-030 | Fail closed on certificate readiness | CI VERIFIED |

## B500-031..040 — Restart/recycle durability

| Task | Outcome | State |
|---|---|---|
| B500-031 | Require state path outside release root | CI VERIFIED |
| B500-032 | Require key-ring path outside release root | CI VERIFIED |
| B500-033 | Preserve registration count | CI VERIFIED |
| B500-034 | Preserve snapshot evidence count | CI VERIFIED |
| B500-035 | Keep audit sequence monotonic | CI VERIFIED |
| B500-036 | Keep incident sequence monotonic | CI VERIFIED |
| B500-037 | Require protected credential resolution | CI VERIFIED |
| B500-038 | Require health recovery | CI VERIFIED |
| B500-039 | Enforce restart SLA | CI VERIFIED |
| B500-040 | Enumerate durability blockers | CI VERIFIED |

## B500-041..050 — Backup and rollback safety

| Task | Outcome | State |
|---|---|---|
| B500-041 | Enforce backup freshness | CI VERIFIED |
| B500-042 | Require backup SHA-256 | CI VERIFIED |
| B500-043 | Require backup manifest | CI VERIFIED |
| B500-044 | Preserve previous release | CI VERIFIED |
| B500-045 | Include durable state in backup | CI VERIFIED |
| B500-046 | Include Data Protection key ring | CI VERIFIED |
| B500-047 | Require restore validation | CI VERIFIED |
| B500-048 | Require rollback smoke | CI VERIFIED |
| B500-049 | Enforce rollback SLA | CI VERIFIED |
| B500-050 | Enumerate rollback blockers | CI VERIFIED |

## B500-051..060 — Deployed least-privilege SQL

| Task | Outcome | State |
|---|---|---|
| B500-051 | Require monitored login non-sysadmin | CI VERIFIED |
| B500-052 | Require server-state read | CI VERIFIED |
| B500-053 | Require VIEW ANY DATABASE equivalent | CI VERIFIED |
| B500-054 | Require definition metadata access | CI VERIFIED |
| B500-055 | Require SQL Agent metadata read | CI VERIFIED |
| B500-056 | Forbid target DML privilege | CI VERIFIED |
| B500-057 | Forbid target DDL privilege | CI VERIFIED |
| B500-058 | Forbid impersonation privilege | CI VERIFIED |
| B500-059 | Require successful collection | CI VERIFIED |
| B500-060 | Enumerate least-privilege blockers | CI VERIFIED |

## B500-061..070 — Health/authentication production smoke

| Task | Outcome | State |
|---|---|---|
| B500-061 | Normalize bounded health states | CI VERIFIED |
| B500-062 | Validate liveness endpoint | CI VERIFIED |
| B500-063 | Validate readiness endpoint | CI VERIFIED |
| B500-064 | Validate aggregate health endpoint | CI VERIFIED |
| B500-065 | Require authenticated Administrator login | CI VERIFIED |
| B500-066 | Require protected-route success | CI VERIFIED |
| B500-067 | Require antiforgery enforcement | CI VERIFIED |
| B500-068 | Require Secure authentication cookie | CI VERIFIED |
| B500-069 | Require HTTPS-only smoke target | CI VERIFIED |
| B500-070 | Enumerate production-smoke blockers | CI VERIFIED |

## B500-071..080 — Cutover/change-window safety

| Task | Outcome | State |
|---|---|---|
| B500-071 | Compute cutover window duration | CI VERIFIED |
| B500-072 | Validate bounded cutover window | CI VERIFIED |
| B500-073 | Normalize change-ticket id | CI VERIFIED |
| B500-074 | Require structured change-ticket id | CI VERIFIED |
| B500-075 | Require approval quorum | CI VERIFIED |
| B500-076 | Require rollback owner | CI VERIFIED |
| B500-077 | Reject change-freeze conflict | CI VERIFIED |
| B500-078 | Require backup gate before cutover | CI VERIFIED |
| B500-079 | Enumerate cutover blockers | CI VERIFIED |
| B500-080 | Fail closed Go/No-Go decision | CI VERIFIED |

## B500-081..090 — Evidence redaction/export safety

| Task | Outcome | State |
|---|---|---|
| B500-081 | Detect password assignments | CI VERIFIED |
| B500-082 | Detect connection-string shapes | CI VERIFIED |
| B500-083 | Detect raw provider errors | CI VERIFIED |
| B500-084 | Detect arbitrary SQL text | CI VERIFIED |
| B500-085 | Normalize evidence keys | CI VERIFIED |
| B500-086 | Collapse/clamp evidence whitespace | CI VERIFIED |
| B500-087 | Generate opaque evidence ids | CI VERIFIED |
| B500-088 | Sanitize evidence host | CI VERIFIED |
| B500-089 | Export only allowlisted fields | CI VERIFIED |
| B500-090 | Fail closed on unsafe evidence | CI VERIFIED |

## B500-091..100 — Versioned release contract

| Task | Outcome | State |
|---|---|---|
| B500-091 | Format B500 task ids | CI VERIFIED |
| B500-092 | Strictly parse B500 task ids | CI VERIFIED |
| B500-093 | Verify B500-001..100 completeness | CI VERIFIED |
| B500-094 | Version B500 contract schema | CI VERIFIED |
| B500-095 | Publish deterministic feature groups | CI VERIFIED |
| B500-096 | Publish explicit safety guardrails | CI VERIFIED |
| B500-097 | Publish 100-task contract manifest | CI VERIFIED |
| B500-098 | Generate stable contract SHA-256 | CI VERIFIED |
| B500-099 | Fail closed release evaluation and reject false external-acceptance claim | CI VERIFIED |
| B500-100 | Expose Read-policy `/production/v1/acceptance-contract` | CI VERIFIED |

## Closure rule

Repository completion of B500 means `B500-001..100` are implemented and CI verified. It does **not** satisfy P0.5 external acceptance. #116/#111 must remain open until actual Windows/IIS trusted HTTPS, intended app-pool identity, real recycle durability, deployed least-privilege SQL, operational backup and rollback rehearsal are all evidenced.
