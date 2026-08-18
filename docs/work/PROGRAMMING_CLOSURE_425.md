# Programming Closure #425 — exact SharedState key identity across SQL collation

## Baseline

- Base: `main@3720c7aaa3e86ac3eb599685f39e98fa0a6ecb64`.
- #423 made the schema guard and document Read/CAS atomic under one SQL transaction/locking boundary.
- `ISharedStateDocumentStore` and its fake/test implementation use ordinal key identity, while SQL Server v1 inherits the database collation for `DocumentKey nvarchar(128) PRIMARY KEY`.

## Gap

On a case-insensitive SQL Server collation, `WHERE DocumentKey = @DocumentKey` can match a persisted key whose casing differs from the requested key. Before this closure, SQL Read selected `DocumentKey` but `ReadDocument(...)` rebuilt the document with the caller key, while CAS results omitted `DocumentKey` entirely. The store's existing ordinal returned-key validator therefore could not detect a collation alias. For CAS, allowing an alias to reach mutation would also risk changing the wrong logical document identity.

A blanket lowercase-only contract is not safe: distributed lease resources currently allow mixed-case ASCII and form SharedState keys from those resources. This closure therefore preserves the accepted key alphabet and fixes provider identity instead of normalizing callers.

## Closure

The production SQL backend now preserves exact persisted identity without requiring a product-specific SQL collation:

1. the existing atomic execution lock captures the actual persisted `DocumentKey` together with the row version;
2. when a row is found under database-collation equality, the lock compares the UTF-16 NVARCHAR bytes using `CONVERT(varbinary(256), ...)`;
3. a byte-different key raises a fail-closed SQL error before Read/CAS document execution and therefore before any CAS mutation;
4. Read returns the actual SQL `DocumentKey` instead of substituting caller input;
5. CAS result rows now include actual `DocumentKey` for applied and existing-conflict results;
6. the backend repeats `StringComparison.Ordinal` persisted-key validation before transaction commit so a future SQL/result-shape regression rolls back rather than committing under a mismatched identity;
7. the store keeps its existing ordinal returned-document validation and redacts provider/SQL failures as `SharedStateStoreUnavailableException`.

Exact mixed-case keys remain valid. There is no lowercase normalization, no migration and no schema-version bump.

## Regression coverage

`SharedStateKeyIdentityTests` proves:

- exact mixed-case keys remain accepted;
- a backend read that returns a different-case key is redacted as unavailable;
- a backend CAS result that returns a different-case key is redacted as unavailable;
- the production source retains the byte-exact pre-mutation guard, actual-key result shape and pre-commit identity checks.

`SharedStateKeyIdentityRealSqlTests` creates an isolated SQL Server 2022 database with explicit `Latin1_General_100_CI_AS` collation and proves:

- exact mixed-case Read succeeds;
- a case-different Read alias fails closed;
- a case-different CAS alias fails closed;
- the rejected alias CAS leaves persisted version/payload unchanged;
- exact-key CAS still succeeds afterward.

## Safety boundary

SharedState control-plane identity hardening only. No key normalization/migration, schema-v2 change, Monitor runtime DDL, monitored-target SQL/query/permission expansion, secret disclosure, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external P0 acceptance, protected-P0 completion or branch-protection mutation.

External/manual dependency order remains `#162 -> #116 -> #111`; #353 remains a separate repository-admin action.

## Definition of Done

PR #426 stays Draft until its exact final docs-inclusive head is current with `main`, has zero unresolved review threads, and passes repository CI, SQL Server 2022 Real SQL acceptance, Windows production-candidate, protected-P0 commit guard and protected-P0 metadata guard. Only then may #425 be closed by merge.