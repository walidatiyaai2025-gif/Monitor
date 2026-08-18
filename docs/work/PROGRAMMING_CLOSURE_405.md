# Programming Closure #405 — Bounded operational backup directory enumeration

## Objective

Keep operational-backup readiness and retention memory usage bounded even if the backup directory contains far more matching files than the configured managed retention count.

## Implementation

- added `BoundedBackupDirectory` as the streaming directory owner for `monitor-backup-*.json` files;
- readiness scans the full matching sequence to preserve the exact backup count while retaining only the newest five `BackupListItem` rows required by the UI;
- readiness ordering is newest `LastWriteTimeUtc` first with deterministic filename-descending tie-break;
- retention keeps only a sorted in-memory candidate set no larger than the configured `RetentionCount` (validated elsewhere as 1..100);
- each non-retained candidate is deleted during the single directory enumeration instead of materializing/sorting all files;
- retention ordering preserves the existing newest `CreationTimeUtc` first plus deterministic filename-descending tie-break;
- `OperationalBackupService.GetReadiness()` and `PruneLocked()` delegate to the bounded helper; bundle creation/validation/restore/file-size behavior is unchanged.

## Regression coverage

- a synthetic 10,000-entry readiness sequence returns the exact full count while retaining only the newest five items;
- a synthetic 10,000-entry retention sequence proves the candidate buffer never exceeds the configured limit and retains the exact newest set;
- a real temporary directory containing 250 matching files prunes to the configured seven files while readiness still returns only five recent rows.

## Safety boundary

Backup availability/reliability hardening only. No backup/persistence schema change, monitored-SQL query or permission expansion, secret disclosure, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external P0 acceptance or branch-protection mutation. Manual/external order remains `#162 -> #116 -> #111`; #353 remains repository-admin only.

## Validation contract

The final exact PR head must be current with `main`, pass repository-selected CI, Real SQL acceptance if selected by the path contract, Windows production-candidate and protected-P0 guards, and have zero unresolved review threads before merge. Exact run IDs are recorded in the PR verification comment.
