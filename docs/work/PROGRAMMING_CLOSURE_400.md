# Programming Closure #400 — Incident-note at-most-once retry semantics

## Objective

Close the cross-store idempotency gap where an incident note can be durably written to operator metadata but the final `incident.note.request/applied` audit receipt fails, allowing a retry with the same request key to create a second note.

## State contract

For one bounded/hashed incident-note request key:

1. **No write-ahead marker and no applied receipt** — first write may proceed through the existing write-ahead audit gate.
2. **`incident.note.request = applied` exists** — completion is confirmed; retry is an idempotent no-op and returns the existing `false` contract.
3. **`incident.note.write.request = requested` exists but no applied receipt exists** — the prior outcome is ambiguous across the audit and operator-metadata stores. Retry fails closed before `AddIncidentNote` with `IncidentNoteRequestAmbiguousException`; the application does not claim the prior request was applied.

The third state intentionally replaces the permissive #396 regression that allowed retry after a durable request marker alone. Without a transaction spanning audit and operator metadata, that marker cannot distinguish "audit persisted then failed before metadata mutation" from "metadata mutation succeeded but final receipt failed". Fail-closed ambiguity is therefore the only at-most-once-safe interpretation.

## Implementation

- `IncidentCollaborationService.TryAddNote(...)` keeps the existing applied-receipt check first;
- if no applied receipt exists but the same request target has a durable `incident.note.write.request/requested` marker, it throws `IncidentNoteRequestAmbiguousException` before metadata mutation;
- first-write behavior, bounded request-key hashing/redaction, note validation and final applied receipt remain unchanged;
- the exception derives from `ArgumentException` so the existing PRG-safe controller rejection path reports the ambiguity instead of displaying "already applied".

## Regression coverage

- write-ahead succeeds, metadata note succeeds, final applied audit throws: retry with the same request key is ambiguous and cannot create a second note;
- a durable request marker whose append persisted and then threw before metadata mutation is also ambiguous on retry, preventing an unsafe guess across the two stores;
- a normal successful first note creates one write-ahead marker and one applied receipt; subsequent same-key retry returns false and leaves exactly one note;
- pre-write audit failure still leaves operator metadata unchanged.

## Safety boundary

Incident-note idempotency/accountability hardening only. No owner/transition/credential behavior change, monitored-SQL query or permission expansion, autonomous remediation, RC.61 publication, real production IIS/SQL mutation, external P0 acceptance or branch-protection mutation. Manual/external order remains `#162 -> #116 -> #111`; #353 remains repository-admin only.

## Validation contract

PR #402 remains Draft until the final exact head is based on current `main`, all repository-selected CI/Windows/protected-P0 gates are Green (and Real SQL if selected by the path contract), and unresolved review threads are zero. Exact final run IDs are recorded in the PR verification comment before merge.
