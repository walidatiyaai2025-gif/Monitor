# Programming Closure #421 — enforce shared-state schema readiness on control-plane reads and CAS writes

## Baseline

- Base: `main@7db00495be00fbd1aa4399e5683d7ed85cb7c0a2`.
- SharedState readiness already performs the full schema-v1 metadata/document fingerprint and runtime-permission verification through `ISharedStateSqlBackend.ReadSchemaVersionAsync`.
- Store-level `ReadAsync` and `CompareExchangeAsync` did not perform that preflight and called document SQL directly.

## Gap

A dedicated state database could truthfully report `SchemaMismatch` or `Unavailable` through readiness while application control-plane document I/O continued. In particular, changing `dbo.MonitorSharedStateSchema.SchemaVersion` away from supported v1 did not itself stop reads or CAS writes as long as the document-table SQL still executed.

That split made health/readiness advisory rather than an execution boundary.

## Closure

`SqlServerSharedStateDocumentStore` now reuses the existing backend schema/readiness preflight before every valid document read and compare/exchange operation:

- document execution requires `ReadSchemaVersionAsync` to complete the existing full fingerprint/permission checks and return `SupportedSchemaVersion` exactly;
- missing schema state, unsupported schema version, metadata/document fingerprint drift, incomplete runtime permissions, or provider failure prevents document execution;
- those failures remain redacted through the existing `SharedStateStoreUnavailableException` boundary;
- caller cancellation remains propagated;
- key/version/payload validation still occurs before provider access, so invalid caller input does not trigger a schema SQL call;
- returned-document validation and CAS semantics are unchanged;
- no schema object, permission, migration, or runtime DDL is introduced.

The preflight deliberately reuses the one existing schema-v1 truthfulness contract instead of creating a weaker execution-only version check.

## Unit regression

`SharedStateProviderTests` now records schema-preflight calls and proves:

- invalid key, negative expected version, invalid JSON, and oversized payload fail before schema or document backend calls;
- schema version 2 causes a redacted unavailable failure before backend read;
- schema version 2 causes a redacted unavailable failure before backend CAS write;
- supported-schema behavior and existing stale-CAS semantics remain unchanged.

## Real SQL regression

`SharedStateExecutionPreflightRealSqlTests` uses SQL Server 2022, the repository's canonical schema-v1 installer, and the non-sysadmin runtime login. It proves:

1. v1 store CAS creates a baseline document and readiness is `Ready`;
2. an administrator changes only the schema metadata row to version 2;
3. readiness becomes `SchemaMismatch` with version 2;
4. store read and store CAS both fail through the redacted unavailable contract;
5. the document row/version/payload and row count remain unchanged while schema v2 is advertised;
6. restoring metadata to version 1 returns readiness to `Ready`;
7. read resumes and CAS advances the same document from version 1 to version 2.

Administrative schema-version mutation occurs only inside the isolated CI fixture. Monitor runtime performs no DDL or schema mutation.

## Concurrency boundary

This closure makes the existing schema/readiness contract an execution precondition without changing the v1 schema or backend API. The preflight and subsequent document operation are separate backend calls; administrative schema mutation between those two calls remains an external migration-coordination concern. A future atomic migration protocol or schema-v2 design, if required, should be explicit rather than duplicating the full fingerprint into every document SQL statement.

## Workflow-selected gates

Changed paths include `src/Monitor.Web/Services/SharedStateStore.cs`, unit tests, a `Category=RealSql` regression, and this ledger. Exact-head DoD requires repository CI, Real SQL acceptance, Windows production-candidate, protected-P0 commit guard, and protected-P0 metadata guard.

## Safety boundary

SharedState runtime execution gating only. No schema change/version bump, migration/auto-repair, monitored-target permission expansion, runtime DDL, secret disclosure, autonomous remediation, release promotion, production IIS/SQL mutation, external production acceptance, protected-P0 completion, or branch-protection mutation. External/manual dependency order remains `#162 -> #116 -> #111`; #353 remains a separate repository-admin action.

## Definition of Done

The exact final PR head must be current with `main`, have zero unresolved review threads, and pass CI, Real SQL acceptance, Windows production-candidate, protected-P0 commit guard, and protected-P0 metadata guard.