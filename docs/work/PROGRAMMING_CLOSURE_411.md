# Programming Closure #411 — truthful SQL shared-state runtime readiness

## Baseline

- Base: `main@37c36e71863c6e0d13ba890e9b435b7819efd539`.
- Existing readiness path called `ReadSchemaVersionAsync()` and reported `Ready` whenever schema version 1 was readable.
- The probe did not establish that `dbo.MonitorSharedStateDocuments` was available to the configured runtime identity or that the runtime identity retained the required `SELECT`, `INSERT`, and `UPDATE` object permissions.

## Gap

A partially deployed dedicated state database or runtime permission drift could therefore produce a false-positive `SharedStorageReady=true` even though the first document read or compare/exchange operation would fail.

## Closure

The SQL readiness query remains strictly read-only and now separates schema compatibility from runtime document capability:

1. Missing schema table or missing schema row still returns a null schema version and follows the existing `SchemaMismatch` path.
2. A readable unsupported schema version is returned immediately and remains `SchemaMismatch`; no v1 capability assumptions are made.
3. For the supported schema version only, readiness requires:
   - `dbo.MonitorSharedStateDocuments` to be metadata-visible as a user table;
   - `HAS_PERMS_BY_NAME(..., 'OBJECT', 'SELECT') = 1`;
   - `HAS_PERMS_BY_NAME(..., 'OBJECT', 'INSERT') = 1`;
   - `HAS_PERMS_BY_NAME(..., 'OBJECT', 'UPDATE') = 1`.
4. Missing/inaccessible document storage or incomplete runtime permissions causes the SQL probe to fail, which is redacted by the existing provider boundary and rendered as `Unavailable`.

No probe document is inserted or updated. Shared-state schema version, CAS semantics, key/payload limits, command timeout and least-privilege grants are unchanged.

## Regression coverage

- Unit regression proves a capability-probe failure becomes `Unavailable` without leaking backend/connection details.
- Unit regression proves a readable unsupported version remains `SchemaMismatch` with the observed version.
- Real SQL 2022 regression uses the existing non-sysadmin runtime login in an ephemeral dedicated state database:
  - exact runtime role grants produce `Ready`;
  - the document table remains empty after readiness, proving no probe mutation;
  - existing SQL-backed CAS/read and oversized-row transport regressions still pass;
  - an explicit admin-side `DENY UPDATE` to that runtime user causes the same readiness service to report `Unavailable`;
  - document count is unchanged across the drift probe.

## Safety boundary

Read-only SharedState readiness truthfulness only. No monitored-target query or permission expansion, shared-state schema-version change, probe writes, secret disclosure, autonomous remediation, release promotion, production IIS/SQL mutation, external production acceptance, protected-P0 completion, or branch-protection mutation. External/manual dependency order remains `#162 -> #116 -> #111`; #353 remains a separate repository-admin action.

## Definition of Done

The exact final PR head must be current with `main`, have zero unresolved review threads, and pass repository CI, Real SQL acceptance, Windows production-candidate, and protected-P0 guards before merge.
