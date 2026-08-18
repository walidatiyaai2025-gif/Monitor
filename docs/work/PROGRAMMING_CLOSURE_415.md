# Programming Closure #415 — fail-closed shared-state v1 installer

## Baseline

- Base: `main@d2675ca8b725ebb0921760e20d905e67da004abf`.
- Runtime readiness already verifies the core `dbo.MonitorSharedStateDocuments` schema-v1 fingerprint.
- `scripts/sql/monitor_shared_state_v1.sql` created the document table only when absent, but did not validate a table that already existed before inserting or accepting schema version 1.

## Gap

A pre-existing table named `dbo.MonitorSharedStateDocuments` could have incompatible column or primary-key shape. The installer could skip table creation, insert/accept `MonitorSharedStateSchema.SchemaVersion = 1`, commit successfully, and leave a state database labelled v1 that runtime readiness immediately rejects.

## Closure

Before the installer inserts or accepts schema version 1, it now verifies the same core document-table contract consumed by the SQL backend:

- exactly four columns;
- `DocumentKey nvarchar(128) NOT NULL`, non-computed;
- `Version bigint NOT NULL`, non-computed;
- `PayloadJson nvarchar(max) NOT NULL`, non-computed;
- `UpdatedAtUtc datetime2(7) NOT NULL`, non-computed;
- a unique primary key containing exactly `DocumentKey` as its sole key column.

A mismatch throws SQL error `51001` inside the existing `XACT_ABORT` transaction. The installer does not stamp schema version 1, migrate, repair, rename, or otherwise mutate the incompatible pre-existing document table.

Fresh canonical installation output and schema version remain unchanged, and rerunning the installer against an already canonical v1 database remains idempotent.

## Real SQL regression

The SQL Server 2022 acceptance test reads and executes the repository's actual `scripts/sql/monitor_shared_state_v1.sql` file and proves:

1. fresh installation succeeds and produces schema version 1 with `UpdatedAtUtc datetime2(7)`;
2. a second canonical rerun succeeds unchanged;
3. a separate database with a pre-existing `UpdatedAtUtc datetime2(3)` document table fails with SQL error `51001`;
4. the drifted table retains precision 3, proving no auto-repair;
5. the schema table created earlier in the failed transaction is rolled back, proving the database was not stamped v1.

The Real SQL workflow path contract already includes the installer script and this existing regression file, so this closure is selected automatically.

## Safety boundary

SharedState provisioning/data-integrity hardening only. No monitored-target query or permission expansion, schema-version bump, migration/repair, runtime probe writes, secret disclosure, autonomous remediation, release promotion, production IIS/SQL mutation, external production acceptance, protected-P0 completion, or branch-protection mutation. External/manual dependency order remains `#162 -> #116 -> #111`; #353 remains a separate repository-admin action.

## Definition of Done

The exact final PR head must be current with `main`, have zero unresolved review threads, and pass repository CI, Real SQL acceptance, Windows production-candidate, and protected-P0 guards before merge.
