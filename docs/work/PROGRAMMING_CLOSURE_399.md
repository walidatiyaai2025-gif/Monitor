# Programming Closure #399 — Bounded local restore rollback snapshots

## Objective

Bound the pre-overwrite rollback snapshot used by local operational restore so recovery cannot read an arbitrarily large previous registration/audit/incidents/history file into memory before mutation or rollback.

## Implementation

- local restore snapshots the exact opened read handle rather than `File.Exists` followed by `File.ReadAllTextAsync`;
- the registration target uses a 16 MiB rollback-snapshot ceiling aligned with the durable registration store raw-file bound;
- audit/incidents/history targets use a 128 MiB ceiling aligned with the bounded operational JSON store;
- production defaults remain fixed while internal constructor parameters allow deterministic small-bound regression coverage;
- an oversized existing target fails before that target is overwritten;
- missing target semantics are unchanged: restore creates the file and rollback removes it if a later operation fails;
- if a later operational target is oversized after the registration operation has applied, the existing restore transaction unwinds the earlier registration operation back to its exact previous text.

## Regression coverage

- oversized existing registration fails closed before overwrite;
- oversized later audit target triggers rollback of an already-applied registration restore and preserves the oversized audit file unchanged;
- missing local targets retain the existing successful creation semantics.

## Safety boundary

Recovery/data-integrity hardening only. No backup/persistence schema change, monitored-SQL query or permission expansion, secret disclosure, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external P0 acceptance or branch-protection mutation. Manual/external order remains `#162 -> #116 -> #111`; #353 remains repository-admin only.

## Validation contract

The final exact PR head must be based on current `main`, pass repository-selected CI, Real SQL acceptance, Windows production-candidate and protected-P0 guards, and have zero unresolved review threads before merge. Exact run IDs are recorded in the PR verification comment.
