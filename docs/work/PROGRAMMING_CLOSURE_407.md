# Programming Closure #407 — Bounded operational backup assembly

## Objective

Reject an operational backup as soon as the compact serialized content already admitted to the candidate proves the final bundle cannot fit the configured `MaxBundleBytes`, instead of first accumulating all retained history and only failing after final serialization.

## Implementation

- added `BackupAssemblyBudget`, which tracks a conservative serialized lower bound by compact-serializing each admitted item;
- because the final bundle necessarily contains every admitted item plus section wrappers, array separators, manifest fields and pretty-print whitespace, item-content bytes alone exceeding `MaxBundleBytes` proves the final bundle cannot be valid;
- registrations and incidents are admitted through the budget before history collection;
- history is read registration-by-registration and each point is admitted immediately, so later registrations are never read once the candidate is provably oversized;
- audit items are admitted through the same budget;
- no item is truncated or omitted; exceeding the lower-bound budget throws the existing `Operational backup exceeds the configured bundle size limit.` failure;
- the existing final `SerializeToUtf8Bytes(bundle, FileJson)` exact-size check remains authoritative, so any candidate that survives the lower-bound check still must pass the original exact bundle limit;
- section ordering, manifest hashes, format/schema, backup ID generation, validation, retention and restore behavior remain unchanged for valid bundles.

## Regression coverage

- a 100-registration synthetic history source with a deliberately small budget proves collection stops before all registrations are read once the bundle cannot fit;
- within-budget history remains deterministically ordered by registration ID and collection time;
- generic item admission rejects only after compact serialized item content exceeds the configured lower-bound budget.

## Safety boundary

Backup availability/reliability hardening only. No backup/persistence schema change, no silent data truncation, monitored-SQL query or permission expansion, secret disclosure, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external P0 acceptance or branch-protection mutation. Manual/external order remains `#162 -> #116 -> #111`; #353 remains repository-admin only.

## Validation contract

The final exact PR head must be current with `main`, pass repository-selected CI, Real SQL acceptance if selected by the path contract, Windows production-candidate and protected-P0 guards, and have zero unresolved review threads before merge. Exact run IDs are recorded in the PR verification comment.
