# Programming Closure #409 — bounded SQL shared-state payload reads

## Baseline

- Base: `main@74571a803d2649630142dd5115b52c3e2c4359a2`.
- Existing application write contract: shared-state JSON payloads are valid JSON and at most 1 MiB in UTF-8.
- Existing SQL storage: `PayloadJson nvarchar(max)` with runtime `SELECT, INSERT, UPDATE` and no transport bound on returned LOB values.

## Gap

A valid-JSON row written outside the normal application write path could exceed the 1 MiB application contract. The SQL read and compare/exchange conflict paths returned raw `nvarchar(max)` and called `SqlDataReader.GetString()` before any returned-document size validation, allowing avoidable unbounded LOB transfer/materialization.

## Closure

- Added a conservative SQL transport envelope of `2 * MaximumPayloadBytes` using SQL Server `DATALENGTH(PayloadJson)`. Any payload whose UTF-16 storage exceeds that envelope is projected as `NULL` instead of being returned as a LOB.
- Read and compare/exchange result sets carry `PayloadStorageBytes`; `ReadDocument` checks the storage bound and projected NULL before calling `GetString()`.
- Application-side returned-document validation now rejects wrong keys, non-positive versions, missing timestamps, invalid/blank JSON and payloads whose exact UTF-8 size exceeds 1 MiB.
- Invalid provider state remains redacted behind `SharedStateStoreUnavailableException`.
- No shared-state schema version, CAS semantics, timeout, key format, or least-privilege grants changed.

The SQL transport envelope is intentionally conservative: any application-valid UTF-8 payload is representable within at most twice that byte count as SQL Server `nvarchar` UTF-16 storage. The exact existing 1 MiB UTF-8 contract remains authoritative after bounded transport.

## Regression coverage

- Unit regressions inject oversized, invalid-JSON, wrong-key, non-positive-version and invalid CAS result documents and require fail-closed/redacted behavior.
- Normal read/CAS behavior is preserved for valid returned documents.
- Real SQL 2022 regression creates an ephemeral dedicated state database, grants the existing runtime role to the same non-sysadmin acceptance login, writes an oversized valid-JSON row through runtime `INSERT`, and requires both low-level read and stale-CAS conflict paths to reject it. A normal SQL-backed CAS/read is proven first.

## Safety boundary

Repository-side SharedState transport/data-integrity hardening only. No monitored-target query or permission expansion, shared-state schema-version change, secret disclosure, autonomous remediation, release promotion, production IIS/SQL mutation, external production acceptance, protected-P0 completion, or branch-protection mutation. External/manual dependency order remains `#162 -> #116 -> #111`; #353 remains a separate repository-admin action.

## Definition of Done

The exact final PR head must be current with `main`, have zero unresolved review threads, and pass repository CI, Real SQL acceptance, Windows production-candidate, and protected-P0 guards before merge.
