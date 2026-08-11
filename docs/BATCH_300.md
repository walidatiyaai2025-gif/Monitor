# BATCH-300 — SQL Estate Intelligence & Safe Operations

Issue #97 is the canonical umbrella for this 100-code-task program. Implementation evidence: GitHub Actions run `31464569180`; Release build succeeded with **0 warnings / 0 errors** under `--warnaserror`; complete suite **390/390 passed**.

## Guardrails

- No autonomous remediation or AI-generated SQL execution.
- No browser-to-SQL access.
- No plaintext credentials, connection strings, raw provider errors or SQL text in UI/audit/export/diagnostics.
- Intelligence helpers are deterministic and side-effect free.
- `/intelligence/contract` is read-only and protected by the named Read policy.
- Export contracts are bounded, formula-safe and versioned.

## 1 — Estate identity

| Task | Code outcome | Status |
|---|---|---|
| B300-001 | Bounded server/name normalization | CI VERIFIED — RUN 31464569180 |
| B300-002 | Safe normalized tag tokens | CI VERIFIED — RUN 31464569180 |
| B300-003 | Defensive SQL version parser | CI VERIFIED — RUN 31464569180 |
| B300-004 | Safe major-version extraction | CI VERIFIED — RUN 31464569180 |
| B300-005 | Deterministic version-family bucketing | CI VERIFIED — RUN 31464569180 |
| B300-006 | SQL edition classification | CI VERIFIED — RUN 31464569180 |
| B300-007 | Uptime-band classification | CI VERIFIED — RUN 31464569180 |
| B300-008 | Opaque stable estate identifiers | CI VERIFIED — RUN 31464569180 |
| B300-009 | Safe server/instance display labels | CI VERIFIED — RUN 31464569180 |
| B300-010 | Supported-major fail-closed policy | CI VERIFIED — RUN 31464569180 |

## 2 — Capacity forecasting

| Task | Code outcome | Status |
|---|---|---|
| B300-011 | Capacity sample normalization | CI VERIFIED — RUN 31464569180 |
| B300-012 | Daily growth calculation | CI VERIFIED — RUN 31464569180 |
| B300-013 | Shrinking/flat/growing trend classification | CI VERIFIED — RUN 31464569180 |
| B300-014 | Bounded headroom percentage | CI VERIFIED — RUN 31464569180 |
| B300-015 | Days-to-threshold projection | CI VERIFIED — RUN 31464569180 |
| B300-016 | Bounded threshold date projection | CI VERIFIED — RUN 31464569180 |
| B300-017 | Capacity growth-band classification | CI VERIFIED — RUN 31464569180 |
| B300-018 | Forecast-horizon clamping | CI VERIFIED — RUN 31464569180 |
| B300-019 | Required-capacity calculation | CI VERIFIED — RUN 31464569180 |
| B300-020 | Composite deterministic capacity projection | CI VERIFIED — RUN 31464569180 |

## 3 — Backup compliance

| Task | Code outcome | Status |
|---|---|---|
| B300-021 | Recovery-model classification | CI VERIFIED — RUN 31464569180 |
| B300-022 | Future-safe backup age calculation | CI VERIFIED — RUN 31464569180 |
| B300-023 | Full-backup RPO overdue detection | CI VERIFIED — RUN 31464569180 |
| B300-024 | Recovery-model-aware log-backup requirement | CI VERIFIED — RUN 31464569180 |
| B300-025 | Log-backup RPO overdue detection | CI VERIFIED — RUN 31464569180 |
| B300-026 | Bounded backup compliance score | CI VERIFIED — RUN 31464569180 |
| B300-027 | Backup risk thresholds | CI VERIFIED — RUN 31464569180 |
| B300-028 | Safe backup compliance reasons | CI VERIFIED — RUN 31464569180 |
| B300-029 | Stable compliance labels | CI VERIFIED — RUN 31464569180 |
| B300-030 | Composite backup compliance evaluation | CI VERIFIED — RUN 31464569180 |

## 4 — Database-state intelligence

| Task | Code outcome | Status |
|---|---|---|
| B300-031 | Database-state normalization | CI VERIFIED — RUN 31464569180 |
| B300-032 | Database-state classification | CI VERIFIED — RUN 31464569180 |
| B300-033 | Strict online-state predicate | CI VERIFIED — RUN 31464569180 |
| B300-034 | Actionable database-state predicate | CI VERIFIED — RUN 31464569180 |
| B300-035 | Availability percentage score | CI VERIFIED — RUN 31464569180 |
| B300-036 | Unavailable-database count | CI VERIFIED — RUN 31464569180 |
| B300-037 | Restoring-database count | CI VERIFIED — RUN 31464569180 |
| B300-038 | Deterministic worst-state selection | CI VERIFIED — RUN 31464569180 |
| B300-039 | Failover-readiness predicate | CI VERIFIED — RUN 31464569180 |
| B300-040 | Full database-state summary | CI VERIFIED — RUN 31464569180 |

## 5 — Runtime pressure

| Task | Code outcome | Status |
|---|---|---|
| B300-041 | Runtime percentage normalization | CI VERIFIED — RUN 31464569180 |
| B300-042 | Memory pressure scoring | CI VERIFIED — RUN 31464569180 |
| B300-043 | Blocking/wait pressure scoring | CI VERIFIED — RUN 31464569180 |
| B300-044 | Scheduler pressure scoring | CI VERIFIED — RUN 31464569180 |
| B300-045 | Pending-I/O pressure scoring | CI VERIFIED — RUN 31464569180 |
| B300-046 | Composite bounded runtime score | CI VERIFIED — RUN 31464569180 |
| B300-047 | Runtime pressure classification | CI VERIFIED — RUN 31464569180 |
| B300-048 | Runtime hotspot predicate | CI VERIFIED — RUN 31464569180 |
| B300-049 | Active pressure-domain signals | CI VERIFIED — RUN 31464569180 |
| B300-050 | Composite runtime evaluation | CI VERIFIED — RUN 31464569180 |

## 6 — Fleet risk

| Task | Code outcome | Status |
|---|---|---|
| B300-051 | Fleet severity normalization | CI VERIFIED — RUN 31464569180 |
| B300-052 | Age/suppression/maintenance weighting | CI VERIFIED — RUN 31464569180 |
| B300-053 | Top-signal fleet aggregate score | CI VERIFIED — RUN 31464569180 |
| B300-054 | Fleet risk-level classification | CI VERIFIED — RUN 31464569180 |
| B300-055 | Deterministic top-risk ordering | CI VERIFIED — RUN 31464569180 |
| B300-056 | Actionable fleet-risk count | CI VERIFIED — RUN 31464569180 |
| B300-057 | Suppressed-risk count | CI VERIFIED — RUN 31464569180 |
| B300-058 | Fleet risk distribution | CI VERIFIED — RUN 31464569180 |
| B300-059 | Safe fleet risk keys | CI VERIFIED — RUN 31464569180 |
| B300-060 | Composite fleet risk summary | CI VERIFIED — RUN 31464569180 |

## 7 — Alert routing

| Task | Code outcome | Status |
|---|---|---|
| B300-061 | Environment alias normalization | CI VERIFIED — RUN 31464569180 |
| B300-062 | Deterministic escalation tiers | CI VERIFIED — RUN 31464569180 |
| B300-063 | Suppression/maintenance-aware route decision | CI VERIFIED — RUN 31464569180 |
| B300-064 | Explicit paging predicate | CI VERIFIED — RUN 31464569180 |
| B300-065 | Escalation-tier cooldown policy | CI VERIFIED — RUN 31464569180 |
| B300-066 | Bounded owner fallback | CI VERIFIED — RUN 31464569180 |
| B300-067 | Opaque deterministic alert dedupe key | CI VERIFIED — RUN 31464569180 |
| B300-068 | Safe route-reason projection | CI VERIFIED — RUN 31464569180 |
| B300-069 | Overnight quiet-window semantics | CI VERIFIED — RUN 31464569180 |
| B300-070 | Composite alert-routing decision | CI VERIFIED — RUN 31464569180 |

## 8 — Operator safety

| Task | Code outcome | Status |
|---|---|---|
| B300-071 | Control-character-safe text normalization | CI VERIFIED — RUN 31464569180 |
| B300-072 | Secret/connection-shape detector | CI VERIFIED — RUN 31464569180 |
| B300-073 | Secret-safe bounded operator notes | CI VERIFIED — RUN 31464569180 |
| B300-074 | Strict route-ID validation | CI VERIFIED — RUN 31464569180 |
| B300-075 | Safe bounded filenames | CI VERIFIED — RUN 31464569180 |
| B300-076 | Spreadsheet formula neutralization | CI VERIFIED — RUN 31464569180 |
| B300-077 | Safe correlation-ID preservation/fallback | CI VERIFIED — RUN 31464569180 |
| B300-078 | Opaque note/value fingerprints | CI VERIFIED — RUN 31464569180 |
| B300-079 | Diagnostics ZIP entry allowlist | CI VERIFIED — RUN 31464569180 |
| B300-080 | Sensitive-key/value redaction | CI VERIFIED — RUN 31464569180 |

## 9 — Export contracts

| Task | Code outcome | Status |
|---|---|---|
| B300-081 | Export row-count clamp | CI VERIFIED — RUN 31464569180 |
| B300-082 | LF-only line-ending normalization | CI VERIFIED — RUN 31464569180 |
| B300-083 | Quoted/formula-safe CSV cells | CI VERIFIED — RUN 31464569180 |
| B300-084 | Versioned UTF-8-BOM CSV contract | CI VERIFIED — RUN 31464569180 |
| B300-085 | SHA-256 export checksum | CI VERIFIED — RUN 31464569180 |
| B300-086 | Export manifest contract | CI VERIFIED — RUN 31464569180 |
| B300-087 | Versioned bounded manifest JSON | CI VERIFIED — RUN 31464569180 |
| B300-088 | Safe deterministic download filenames | CI VERIFIED — RUN 31464569180 |
| B300-089 | Deterministic export ordering | CI VERIFIED — RUN 31464569180 |
| B300-090 | Bounded JSON export | CI VERIFIED — RUN 31464569180 |

## 10 — Compatibility & release acceptance

| Task | Code outcome | Status |
|---|---|---|
| B300-091 | Canonical B300 task-ID formatter | CI VERIFIED — RUN 31464569180 |
| B300-092 | Strict B300 task-ID parser | CI VERIFIED — RUN 31464569180 |
| B300-093 | 100-task completeness verifier | CI VERIFIED — RUN 31464569180 |
| B300-094 | BATCH-200 compatibility predicate | CI VERIFIED — RUN 31464569180 |
| B300-095 | Release readiness percentage | CI VERIFIED — RUN 31464569180 |
| B300-096 | Autonomous-remediation guardrail invariant | CI VERIFIED — RUN 31464569180 |
| B300-097 | Browser-to-SQL guardrail invariant | CI VERIFIED — RUN 31464569180 |
| B300-098 | Secret-canary guardrail invariant | CI VERIFIED — RUN 31464569180 |
| B300-099 | Fail-closed release gate evaluation | CI VERIFIED — RUN 31464569180 |
| B300-100 | Read-policy protected `/intelligence/contract` endpoint | CI VERIFIED — RUN 31464569180 |

## Verification

- Implementation run: `31464569180`.
- Build: **Green — 0 warnings / 0 errors**.
- Tests: **390/390 passed**.
- B300-specific acceptance coverage: **100 mapped tests, one per task**.
- Program status: **BATCH-300 100/100 CI VERIFIED** pending final code+docs PR gate and squash merge.
