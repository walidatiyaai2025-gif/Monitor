# Programming Closure #419 — verify shared-state schema metadata table fingerprint

## Baseline

- Base: `main@4c7c46369164a92372a056c13a808bdff6c60da2`.
- Document-table columns, PK, and integrity CHECKs are already part of schema-v1 provisioning/readiness truthfulness.
- `dbo.MonitorSharedStateSchema` itself was only existence/read checked; its canonical metadata contract was not fingerprinted.

## Gap

A pre-existing schema metadata table could remain queryable at `Id = 1` while lacking canonical columns, the sole `Id` primary key, the enabled/trusted `Id = 1` CHECK, or the `SYSUTCDATETIME()` installation default. Such a table could be accepted as v1 even though duplicate/noncanonical metadata rows or noncanonical installation timestamps were possible.

## Closure

Both installer and runtime readiness now require the canonical metadata-table contract:

- exactly `Id tinyint NOT NULL`, `SchemaVersion int NOT NULL`, and `InstalledAtUtc datetime2(7) NOT NULL`;
- a unique primary key whose sole key column is `Id`;
- enabled/trusted `CK_MonitorSharedStateSchema_Id` normalizing to `Id = 1`;
- `DF_MonitorSharedStateSchema_InstalledAtUtc` bound to `InstalledAtUtc` and normalizing to `SYSUTCDATETIME()`;
- installer metadata drift throws SQL error `51002` before document-table provisioning or schema stamping;
- runtime metadata drift throws internally and remains redacted through the existing `Unavailable` readiness contract;
- no auto-repair/migration or readiness probe write is introduced.

## Real SQL regression

SQL Server 2022 acceptance proves canonical `Ready`, then fail-closed readiness for missing PK, disabled/untrusted Id CHECK, missing/wrong InstalledAtUtc default, and extra-column drift, with restoration to `Ready` after canonical repair. The document row count remains zero throughout readiness probing.

A separate installer fixture creates a pre-existing metadata table with the three queryable columns but no PK/CHECK/default. Running the repository installer must fail with error `51002`, preserve the pre-existing row/table as-is, and leave `dbo.MonitorSharedStateDocuments` absent, proving validation happens before provisioning and without auto-repair.

## Workflow-selected gates

Changed paths include `src/Monitor.Web/Services/SharedStateStore.cs`, schema-v1 SQL, a `Category=RealSql` regression, and this ledger. Exact-head DoD requires CI, Real SQL acceptance, Windows production-candidate, protected-P0 commit guard, and protected-P0 metadata guard.

## Safety boundary

SharedState schema metadata verification only. No schema-version bump, migration/auto-repair, monitored-target permission expansion, runtime probe writes, secret disclosure, autonomous remediation, release promotion, production IIS/SQL mutation, external production acceptance, protected-P0 completion, or branch-protection mutation. External/manual dependency order remains `#162 -> #116 -> #111`; #353 remains a separate repository-admin action.

## Definition of Done

The exact final PR head must be current with `main`, have zero unresolved review threads, and pass CI, Real SQL acceptance, Windows production-candidate, protected-P0 commit guard, and protected-P0 metadata guard.