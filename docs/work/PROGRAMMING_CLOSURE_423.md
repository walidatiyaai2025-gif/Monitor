# Programming Closure #423 — atomic shared-state schema guard and document execution

## Baseline

- Base: `main@3b5e60fef2fa41c6e627468850cf3cf8532b0524`.
- #421 made the canonical schema-v1/readiness fingerprint a store-level precondition before every valid SharedState read and compare/exchange.
- `docs/work/PROGRAMMING_CLOSURE_421.md` explicitly documented the remaining concurrency boundary: the preflight and document operation used separate backend calls/connections, so administrative schema mutation between them remained possible.

## Gap

A store request could pass the full readiness fingerprint, then observe schema-version, table-shape, constraint or permission drift before the document SQL opened its separate connection. The store would still fail closed for many forms of drift, but the execution guarantee was not atomic with the preflight that authorized it.

## Closure

The production `SqlServerSharedStateSqlBackend` now adds an atomic execution guard while preserving the #421 store-level preflight as defense-in-depth:

1. open one SQL connection for the document operation;
2. begin one explicit `SERIALIZABLE` transaction;
3. lock the canonical schema metadata row with `HOLDLOCK` and the target document key/range with `HOLDLOCK` (plus `UPDLOCK` for CAS);
4. execute the existing canonical schema-v1 fingerprint on the same connection and transaction;
5. require `SupportedSchemaVersion` exactly;
6. execute the document Read or CAS on that same transaction;
7. materialize the bounded result before committing.

The CAS SQL no longer owns an independent `BEGIN TRANSACTION` / `COMMIT TRANSACTION`; transaction ownership now sits around both the schema guard and CAS so there is one atomic execution boundary.

The canonical readiness SQL is not forked or weakened. Readiness itself remains read-only. Invalid caller key/version/payload validation still occurs before provider access. Caller cancellation, redacted `SharedStateStoreUnavailableException`, returned-document validation, payload bounds and CAS conflict semantics remain unchanged.

## Regression coverage

`SharedStateAtomicExecutionContractTests` locks the production source ordering for both Read and CAS:

`Serializable transaction -> execution lock -> canonical schema guard -> document SQL -> commit`

It also requires the schema-row/document-range lock hints, same-transaction command binding, and prevents reintroduction of embedded CAS transaction ownership.

`SharedStateAtomicExecutionRealSqlTests` uses SQL Server 2022 and the production execution-lock helper. While the runtime transaction is active it proves:

- an administrator cannot update `MonitorSharedStateSchema.SchemaVersion`;
- an administrator cannot acquire document-table DDL ownership to add a column;
- both attempts fail by lock timeout;
- after rollback, the same administrative changes can proceed and be restored, proving the test is exercising the execution lock rather than an unrelated permission failure.

The test database and all administrative mutations are isolated CI fixtures. Monitor runtime performs no DDL or schema repair.

## Safety boundary

SharedState control-plane concurrency and schema-integrity hardening only. No schema-version bump, migration/auto-repair, runtime DDL by Monitor, monitored-target query/permission expansion, secret disclosure, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external P0 acceptance, protected-P0 completion or branch-protection mutation.

External/manual dependency order remains `#162 -> #116 -> #111`; #353 remains a separate repository-admin action.

## Definition of Done

PR #424 stays Draft until its exact final docs-inclusive head is current with `main`, has zero unresolved review threads, and passes repository CI, SQL Server 2022 Real SQL acceptance, Windows production-candidate, protected-P0 commit guard and protected-P0 metadata guard. Only then may #423 be closed by merge.