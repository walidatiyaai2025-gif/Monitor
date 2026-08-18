# Programming closure #459 — File operator metadata cross-process state

## Gap

SingleNode `FileOperatorMetadataStore` persisted server and incident operator metadata in one `operator-metadata.json` envelope but loaded that envelope into process-local `_state` and serialized access only with an instance-local `_gate`. `AtomicJsonFile.Save` protected replacement integrity, not the complete read -> mutate -> write transaction across overlapping IIS workers sharing `App_Data`.

Two workers could therefore read stale metadata, lose a peer server or incident mutation when persisting the whole envelope, or overwrite a peer incident note.

## Implementation

PR #460 uses the existing bounded `CrossProcessFileLease` primitive with one stable `operator-metadata.json.lock` sidecar for the entire envelope. The lock order is process gate -> cross-process lease -> authoritative disk reload -> read/mutation -> existing validation -> atomic persist.

- constructor loads the initial envelope while holding the lease;
- `GetServer`, `GetIncident`, and `Snapshot` reload authoritative disk state under the lease;
- `UpsertServer` and all incident mutations reload under the same envelope lease before read-modify-write;
- existing `OperatorMetadataSnapshotValidator`, normalization, bounds, note retention, and `AtomicJsonFile.Save` behavior remain unchanged;
- MultiNode `SharedOperatorMetadataStore` and its compare/exchange contract are unchanged.

## Regression contract

`FileOperatorMetadataStoreConcurrencyTests` creates two independent store instances before either mutates and proves:

1. peer server and incident writes are visible to a previously-created reader, including `Snapshot`;
2. a server update from one instance and an incident assignment from another both survive the shared whole-envelope persist;
3. incident notes appended by two independent instances both survive restart.

These scenarios deterministically expose the stale-constructor-cache behavior of the previous implementation without depending on timing races.

## Validation

Pre-canonical-doc head `f26b7f6a86b75713fe8ffed0ccdb401070a1b19f` passed Linux Release build and the full test step plus repository safety runtimes. SQL Server 2022 Real SQL and Windows production-candidate were selected for this source change and must complete Green again on the exact final docs-inclusive PR head before merge. Both protected-P0 guards must also remain Green.

## Safety boundary

No monitored-target SQL query or permission expansion, no credential behavior change, no MultiNode SharedState contract change, no autonomous remediation, no RC.61 publication, no production IIS/SQL mutation, no external acceptance, and no branch-protection mutation. The manual/external dependency remains `#162 -> #116 -> #111`; #353 remains repository-admin only.
