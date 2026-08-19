# Programming Closure #445 — Durable Incident-Note Idempotency

## Gap

Incident-note request idempotency used the rolling audit stream as both evidence and request-state storage. `IncidentCollaborationService` preflight scanned at most 1000 events and `CoordinatedIncidentNoteAuditStore` observed/updated the same audit state. File and Shared audit persistence physically retain only the newest 1000 events.

After enough unrelated audit traffic, both an `incident.note.write.commit=armed` receipt and the later `incident.note.request=applied` receipt could disappear. Reusing the same request key could then be treated as a first request and write a duplicate operator note.

## Implementation

- Added `IIncidentNoteRequestStateStore` with explicit `Armed` and `Applied` states.
- Added InMemory, File, and SharedState implementations selected by the existing operational-state topology.
- State is split into 64 deterministic SHA-256 shards with a hard maximum of 512 receipts per shard (32,768 receipts total). Saturation fails closed rather than evicting idempotency history.
- File mode persists shard envelopes with `AtomicJsonFile` outside `wwwroot`.
- MultiNode mode stores each shard in a separate SharedState document and uses `SharedStateDocumentMutation.Mutate`; the claim callback is re-evaluated against the latest document on every compare/exchange retry.
- `CoordinatedIncidentNoteAuditStore` now uses the durable request store in production:
  - a first request durably writes `Armed` before emitting the armed audit event and before any note mutation;
  - an existing `Applied` request returns replay success without another note;
  - an existing `Armed` request remains fail-closed/ambiguous;
  - after the note mutation, `Applied` is persisted before the applied audit evidence append.
- The prior audit-derived claim behavior remains only as a constructor-compatible fallback for direct tests/callers that do not inject the new store. Production DI always injects the durable singleton.
- Startup scans the still-retained 1000-event legacy audit window and materializes existing incident-note armed/applied receipts into the durable store. Applied wins over Armed for the same target.

## Failure ordering

The ordering is deliberately fail-closed:

1. durable `Armed` claim;
2. armed audit evidence;
3. operator-note mutation;
4. durable `Applied` state;
5. applied audit evidence.

A failure before step 3 cannot write a duplicate note. A failure after step 1 leaves the request ambiguous. A failure after the note mutation but before/while final evidence is emitted leaves either Armed (ambiguous) or Applied (safe replay), never a forgotten successful request merely because audit history rolls over.

## Regression coverage

- `AppliedRequest_RemainsDuplicateAfterAuditEvictionAndFileRestart` writes a note, appends 1001 newer audit events until both incident-note audit receipts are absent, reopens the file-backed request state, retries the same request key, and proves no second note is written.
- `ArmedRequest_RemainsAmbiguousAfterAuditEvictionAndFileRestart` proves a claimed-but-unapplied request remains ambiguous after audit eviction and file restart.
- `LegacyAuditReceipts_MaterializeWithAppliedWinningOverArmed` proves upgrade migration and state precedence.
- Existing `SameRequestKey_AcrossTwoNodes_WritesAtMostOneNote` now routes both nodes through `SharedIncidentNoteRequestStateStore`, preserving deterministic concurrent first-use coverage while exercising the new CAS-backed production path.

## Preserved boundaries

- Rolling audit remains capped at 1000 events and retains its existing schema/event names.
- Operator note text/count validation and metadata bounds are unchanged.
- No monitored-SQL query, SQL permission, credential handling, autonomous remediation, production IIS gate, RC.61 publication, or branch-protection setting is changed.
- Manual/external gates #162, #116, #111 and repository-admin gate #353 remain open and independent.

## Validation contract

PR #446 must not merge until the exact final head has all repository-required checks Green:

- Linux CI / Release build / full test suite and safety runtimes;
- Windows `production-candidate` end-to-end;
- SQL Server 2022 Real SQL acceptance;
- protected-P0 PR metadata guard;
- protected-P0 PR commit guard;
- zero unresolved review threads.
