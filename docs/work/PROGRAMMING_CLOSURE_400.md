# Programming Closure #400 — Incident-note at-most-once retry semantics

## Objective

Close the cross-store idempotency gap where an incident note can be durably written to operator metadata but the final `incident.note.request/applied` audit receipt fails, allowing a retry with the same request key to create a second note.

## State contract

For one bounded/hashed incident-note request key:

1. **No durable marker and no applied receipt** — first write may proceed.
2. **`incident.note.write.request = requested` exists, but no `incident.note.write.commit = armed` and no applied receipt exist** — mutation was never armed; the request remains safely retryable under the #396 write-ahead contract.
3. **`incident.note.write.commit = armed` exists but no `incident.note.request = applied` receipt exists** — the request crossed the final pre-mutation boundary and its outcome is ambiguous across audit/operator-metadata stores. Retry fails closed before `AddIncidentNote` with `IncidentNoteRequestAmbiguousException` and does not claim the prior request succeeded.
4. **`incident.note.request = applied` exists** — completion is confirmed; retry is an idempotent no-op using the existing `false` return contract.

The second durable marker is necessary because a request-intent append may persist and then throw before metadata mutation. Treating intent alone as ambiguous would unnecessarily break safe retry; treating an armed request as retryable could duplicate a note after the metadata write succeeds but the final receipt fails.

## Implementation

- `IncidentCollaborationService.TryAddNote(...)` keeps the existing applied-receipt check first;
- an existing `incident.note.write.commit/armed` marker without an applied receipt produces `IncidentNoteRequestAmbiguousException` before metadata mutation;
- first attempt persists `incident.note.write.request/requested`, then immediately persists `incident.note.write.commit/armed` as the final durable pre-mutation boundary, then writes operator metadata and finally records `incident.note.request/applied`;
- the exception derives from `ArgumentException`, so the existing PRG-safe controller rejection path reports an unresolved prior outcome instead of claiming "already applied";
- request-key hashing/redaction, bounded audit reads and normal applied-receipt deduplication remain unchanged.

## Regression coverage

- intent marker persists and its append throws before the request is armed: retry remains safe, writes one note and reaches the normal applied receipt;
- intent + armed markers persist, the note mutation succeeds, then the final applied receipt throws: same-key retry is ambiguous and cannot create a second note;
- a normal successful request creates one note and subsequent same-key retry remains an applied-receipt no-op;
- an audit failure before any durable write still leaves operator metadata unchanged.

## Safety boundary

Incident-note idempotency/accountability hardening only. No owner/transition/credential behavior change, monitored-SQL query or permission expansion, autonomous remediation, RC.61 publication, real production IIS/SQL mutation, external P0 acceptance or branch-protection mutation. Manual/external order remains `#162 -> #116 -> #111`; #353 remains repository-admin only.

## Validation contract

PR #402 remains Draft until the final exact head is based on current `main`, all repository-selected CI/Windows/protected-P0 gates are Green (and Real SQL if selected by the path contract), and unresolved review threads are zero. Exact final run IDs are recorded in the PR verification comment before merge.
