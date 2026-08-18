# Programming Closure #394 — Operational backup opened-handle validation

## Objective

Bind the operational-backup raw-size validation to the exact opened file handle that is deserialized, eliminating the pathname metadata check/open TOCTOU gap without changing the backup schema or restore transaction model.

## Implementation

- `OperationalBackupService.ValidateAsync` no longer trusts `File.Exists` or `FileInfo.Length` before opening the backup.
- The backup is opened read-only with sequential/asynchronous options.
- `MaxBundleBytes` is enforced against `FileStream.Length` on that exact opened handle before JSON deserialization.
- `FileNotFoundException` / `DirectoryNotFoundException` retain `NotFound` semantics.
- Other IO/JSON/domain failures remain generic `Invalid` validation failures without filesystem-detail leakage.
- Existing manifest/hash/domain/secret-canary checks and restore/rollback behavior are unchanged.
- Regression coverage creates an oversized sparse backup file and proves it is rejected before deserialization.

## Validation

Code/test head before this tracking commit: `af116390578965d0717bd6d59f032d8137cdd2eb`.

- CI run `32133304709`: Green after Restore, Build, full Test and repository safety runtimes.
- Protected P0 metadata run `32133304671`: Green.
- Protected P0 commits run `32133304795`: Green.
- Windows production-candidate run `32133304672`: pending at the time of this tracking commit and must be Green on the final exact head before merge.

## Safety boundary

No backup format/schema change, monitored-SQL query or permission expansion, credential disclosure, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external P0 acceptance, protected-P0 completion, or branch-protection mutation. Manual/external order remains `#162 -> #116 -> #111`; repository-admin gate #353 is unchanged.

## Follow-on prioritization

Issue #396 (durable write-ahead audit before operator mutations) is the highest current programming gap after #394 unless concurrent team work closes or supersedes it. A separate restore rollback-read bound gap was also observed in `OperationalRestoreWriter.Local(...)` and remains lower priority than #396.
