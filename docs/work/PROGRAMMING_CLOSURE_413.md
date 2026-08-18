# Programming Closure #413 — shared-state schema-v1 readiness fingerprint

## Baseline

- Base: `main@bc2dcb3f6f72fbaa7d3bb4111ecde2058b87fc1e`.
- Readiness already verifies supported schema version, document-table existence, and runtime `SELECT`/`INSERT`/`UPDATE` permissions.
- The SQL backend nevertheless depends on a specific v1 document table shape that readiness did not fingerprint.

## Gap

A database could retain `MonitorSharedStateSchema.SchemaVersion = 1`, the expected table name, and the correct runtime permissions while the core document columns or primary key drifted. Such a database could be reported `Ready` even though the backend read/CAS contract was no longer the installed schema-v1 contract.

## Closure

For supported schema v1 only, the read-only readiness query now requires:

- exactly four columns on `dbo.MonitorSharedStateDocuments`;
- `DocumentKey` = built-in `nvarchar(128) NOT NULL`, non-computed;
- `Version` = built-in `bigint NOT NULL`, non-computed;
- `PayloadJson` = built-in `nvarchar(max) NOT NULL`, non-computed;
- `UpdatedAtUtc` = built-in `datetime2(7) NOT NULL`, non-computed;
- the primary key is unique and contains exactly `DocumentKey` as its sole key column.

Only after this core fingerprint succeeds are the existing runtime permission checks evaluated. Fingerprint drift throws inside the SQL probe and is redacted by the existing provider boundary, so readiness becomes `Unavailable`. Missing/unsupported schema-version state retains the existing `SchemaMismatch` classification.

No migration, repair, probe document, schema-version change, or permission expansion is performed.

## Regression coverage

A Real SQL Server 2022 regression creates an ephemeral canonical v1 state database using the same non-sysadmin runtime acceptance login and proves:

1. canonical v1 shape reports `Ready` with zero documents;
2. `UpdatedAtUtc datetime2(7) -> datetime2(3)` drift reports `Unavailable` and restoring `(7)` returns `Ready`;
3. dropping the sole `DocumentKey` primary key reports `Unavailable` and recreating it returns `Ready`;
4. adding one unexpected column reports `Unavailable` and removing it returns `Ready`;
5. document count remains zero across every readiness probe, proving the fingerprint is read-only.

Existing unit readiness redaction/schema-mismatch regressions remain applicable because structural failures travel through the same fail-closed provider boundary.

## Real SQL gate contract

During the PR, the repository path filter was found not to select `real-sql-acceptance` for SharedState SQL changes. The workflow trigger is corrected in the same closure so future changes to the SharedState SQL contract cannot silently bypass the Real SQL gate. The pull-request path set now includes:

- `scripts/sql/monitor_shared_state_v1.sql`;
- `scripts/sql/monitor_state_least_privilege.sql`;
- `src/Monitor.Web/Services/SharedStateStore.cs`;
- `tests/Monitor.Web.Tests/SharedStateSchemaFingerprintRealSqlTests.cs`.

The workflow behavior, credentials, permissions and test command are otherwise unchanged.

## Safety boundary

Read-only SharedState schema/readiness hardening only. No monitored-target query or permission expansion, shared-state schema-version change, migration, probe writes, secret disclosure, autonomous remediation, release promotion, production IIS/SQL mutation, external production acceptance, protected-P0 completion, or branch-protection mutation. External/manual dependency order remains `#162 -> #116 -> #111`; #353 remains a separate repository-admin action.

## Definition of Done

The exact final PR head must be current with `main`, have zero unresolved review threads, and pass repository CI, Real SQL acceptance, Windows production-candidate, and protected-P0 guards before merge.
