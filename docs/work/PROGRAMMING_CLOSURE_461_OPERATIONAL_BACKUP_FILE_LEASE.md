# Programming closure #461 — Operational backup cross-process file lease

## Gap

`OperationalBackupService` shared one backup root across workers but serialized filesystem access only with an instance-local `_fileGate`. `CreateAsync` performed atomic replacement and retention pruning under that local gate, while `ValidateAsync` released the gate before reading/deserializing/checking the backup and `GetReadiness` scanned the same directory under only the local gate.

Overlapping IIS workers could therefore prune a backup while a peer was validating it, scan inventory during peer create/prune activity, or run independent create/prune transactions against the same retention set.

## Implementation

PR for #461 reuses the existing bounded `CrossProcessFileLease` primitive with one stable sibling sidecar derived from the canonical backup root: `<backup-root>.lock`. The sidecar is outside the `monitor-backup-*.json` artifact pattern and cannot enter readiness or retention inventory.

- lease acquisition is ordered through the existing process-local `_fileGate`;
- `CreateAsync` holds the shared lease across directory creation, atomic backup write, retention pruning, and final file metadata read;
- `ValidateAsync` holds the shared lease across path resolution, bounded file open/read, JSON deserialization, identity verification, checksum/structure validation, and file disposal;
- `GetReadiness` holds the shared lease across the bounded directory scan;
- bundle format, secret exclusions, size/count bounds, backup identifier rules, atomic write semantics, retention ordering, and restore behavior remain unchanged.

## Regression contract

`OperationalBackupCrossProcessTests` holds the shared sidecar from an independent file handle and proves:

1. `CreateAsync` waits for the shared lease before writing a backup artifact;
2. an independently-created service instance waits for the shared lease before validating an existing backup;
3. an independently-created service instance waits for the shared lease before scanning readiness inventory.

The tests specifically distinguish the shared lease from instance-local `_fileGate` behavior.

## Validation

The exact final PR head must pass both protected-P0 guards, Linux Release build/full tests, real SQL Server 2022 acceptance, and Windows production-candidate build/tests/package/smokes before merge. Exact run IDs and counts are recorded on the issue/PR evidence trail so validation metadata does not require a post-CI source/docs commit.

## Safety boundary

No monitored-target SQL query or permission expansion, no credential behavior change, no backup bundle format change, no restore target mutation outside existing explicit restore behavior, no autonomous remediation, no RC.61 publication, no production IIS/SQL mutation, no external acceptance, and no branch-protection mutation. The manual/external dependency remains `#162 -> #116 -> #111`; #353 remains repository-admin only.
