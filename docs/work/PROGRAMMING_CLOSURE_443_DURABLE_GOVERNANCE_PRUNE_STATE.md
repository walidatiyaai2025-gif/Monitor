# Programming Closure #443 — Durable Governance Prune State

## Gap

Governance retention used `governance.prune.server`, `governance.prune.incident`, and `governance.prune.note` audit events as semantic tombstones. Both the File and Shared audit stores are intentionally rolling and physically retain only the newest 1000 events. Once a prune receipt aged out, previously hidden operator metadata could become visible again and orphan cleanup could be proposed repeatedly.

Audit evidence is therefore not a valid durable source of truth for retention state.

## Implementation

- Added `IGovernancePruneStateStore` with bounded Server, Incident, and Note marker domains.
- Bound marker counts directly to the existing operator metadata limits: 5000 servers, 1000 incidents, and 20 notes per incident (20,000 note markers).
- Added topology-matched implementations:
  - in-memory for explicit in-memory operational mode;
  - atomic JSON files outside `wwwroot` for SingleNode file persistence;
  - separate SharedState documents per marker kind for MultiNode.
- Shared marker mutations use the existing compare/exchange mutation primitive. The mutation callback is re-evaluated against the latest document after a CAS conflict, so distinct concurrent markers cannot overwrite each other.
- Governance `Apply` persists the semantic marker before appending the unchanged audit evidence event. A failed durable-state write cannot be represented as a successful audit prune.
- `GovernanceRetentionService` and `IncidentCollaborationService` consume the durable marker store instead of scanning rolling audit for prune semantics.
- The production DI container uses one singleton prune-state store selected by the same operational topology as operator metadata.
- Startup eagerly materializes any still-retained legacy `governance.prune.*` audit receipts into the durable store for upgrade compatibility.
- Direct-construction compatibility uses a lazy legacy adapter so service/controller constructors perform no audit I/O. This was required after the first PR test run exposed that eager migration interfered with the deterministic incident-note multi-node rendezvous test.

## Preserved semantics

- Audit remains bounded and retains its existing event shape and 1000-event physical cap.
- #441 remains authoritative for incident visibility: a marker hides incident metadata only while the current incident is actually retention-eligible. Open/Acknowledged and recently resolved incarnations remain visible.
- Incident-note request idempotency remains on its dedicated coordinated audit-claim path; this change does not weaken or replace that mechanism.
- The operational backup/restore contract is unchanged. Operator metadata was already outside that contract, so this closure does not silently expand backup scope.
- No monitored-SQL query, target permission, secret handling, autonomous remediation, external production acceptance, RC.61 publication, or branch-protection state is changed.

## Regression coverage

`GovernancePruneStateTests` adds deterministic coverage for:

1. materializing a retained legacy note-prune receipt into file-backed state;
2. appending 1001 newer audit events so the legacy receipt is physically evicted;
3. reopening the file-backed prune store and proving the note remains hidden after restart;
4. preserving #441 state-aware visibility for Open and recently resolved incidents while hiding retention-expired resolved incidents;
5. forcing two SharedState nodes through a compare/exchange conflict and proving both distinct markers survive the retry;
6. rejecting growth beyond the existing 5000-server operator metadata bound.

The existing `IncidentNoteMultiNodeIdempotencyTests` also remains a regression guard against constructor-time audit reads: the initial PR head exposed that eager compatibility migration could block its two-node preflight barrier; the lazy adapter removes that side effect.

## Validation contract

PR #444 must not merge until the exact final head has all repository-required gates Green:

- Linux CI / Release build / full test suite and safety runtimes;
- Windows `production-candidate` end-to-end;
- SQL Server 2022 Real SQL acceptance;
- protected-P0 PR metadata guard;
- protected-P0 PR commit guard;
- zero unresolved review threads.

Issue #443 is closed only by the merged PR. Manual/external gates #162, #116, #111 and repository-admin gate #353 remain open and unchanged.
