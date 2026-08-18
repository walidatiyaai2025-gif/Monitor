# Programming Closure #403 — Shared operator-metadata validation parity

## Objective

Make shared/MultiNode operator metadata fail closed under the same domain and capacity contract already enforced by the file-backed store. Syntactically valid JSON must not bypass server/incident/note/recommendation validation merely because the persistence mode is shared state.

## Implementation

- extracted `OperatorMetadataSnapshotValidator` as the single snapshot validation owner used by file and shared operator-metadata stores;
- retained the existing File-store capacities and domain rules: maximum 5000 server profiles, 1000 incident profiles, 20 notes per incident, and 20 acknowledged recommendation keys per incident;
- retained duplicate server/incident rejection and existing server/window/tag/assignee/note/actor/recommendation-key validation;
- added explicit null-array/null-element fail-closed handling for deserialized shared state;
- shared `Load()` validates immediately after deserialization before returning state to any caller;
- shared mutation validates the complete candidate snapshot before serialization and before `CompareExchangeAsync`, so an invalid/over-capacity candidate cannot be published through CAS;
- valid shared-state read/update/CAS semantics remain unchanged; no silent repair or truncation is introduced.

## Regression coverage

- duplicate shared server profiles fail closed on read;
- syntactically valid shared incident state with null notes fails closed on read;
- over-capacity shared incident state fails closed on read;
- an exactly-at-capacity shared state rejects a mutation that would create the 1001st incident before any CompareExchange call;
- valid shared state reads and mutates through the existing validated CAS path.

## Safety boundary

Control-plane data-integrity hardening only. No persistence schema/key change, raw shared-state size/CAS algorithm change, monitored-SQL query or permission expansion, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external P0 acceptance or branch-protection mutation. Manual/external order remains `#162 -> #116 -> #111`; #353 remains repository-admin only.

## Validation contract

The final exact PR head must be based on current `main`, pass repository-selected CI, Real SQL acceptance if selected by the path contract, Windows production-candidate and protected-P0 guards, and have zero unresolved review threads before merge. Exact run IDs are recorded in the PR verification comment.
