# Programming Closure #439 — Concurrent Incident Note Idempotency

## Base

`main@cadcf0258134db4be2a6f097d35c3a40b6ee116f`

## Gap

`IncidentCollaborationService.TryAddNote` checked the audit trail for an already-applied or already-armed request key and then appended the `armed` write receipt in a separate operation. Two requests routed to different nodes could both complete the preflight read before either node persisted `armed`, allowing both to mutate incident notes for the same request key.

The operator metadata stores themselves are already serialized/CAS-safe. The missing invariant was exclusive ownership of the transition from "no receipt" to the durable `armed` receipt.

## Closure

- Add `CoordinatedIncidentNoteAuditStore` as the production `IAuditStore` wrapper used for incident-note claims.
- Preserve the existing `monitor:audit:v1` document key, format version, bounded event contract, and event names; no operational-store migration is introduced.
- Preserve write-ahead semantics: `incident.note.write.request=requested` is still written before the claim and, by itself, remains safely retryable.
- In SingleNode in-memory/file modes, the singleton audit wrapper serializes the short claim section with an in-process gate.
- When shared operational state is enabled, `applied/armed` evaluation and insertion of the new `armed` event execute inside one `SharedStateDocumentMutation.Mutate` compare/exchange loop on the shared audit document.
- Every CAS retry re-deserializes the latest audit state before deciding whether the request can be armed. `applied` returns idempotently, an existing `armed` receipt fails closed as ambiguous, and only an empty receipt state may append the new `armed` receipt.
- The durable `armed` audit receipt is the safety boundary before `IOperatorMetadataStore.AddIncidentNote` executes; no lease lifetime or cross-node clock comparison participates in correctness.
- Shared-state unavailability or repeated CAS contention fails closed before the note mutation.

## Deterministic regression coverage

`IncidentNoteMultiNodeIdempotencyTests` creates two independent collaboration-service instances representing separate application nodes. Both use:

- separate `SharedAuditStore` / `SharedOperatorMetadataStore` instances;
- one shared CAS document store;
- separate `CoordinatedIncidentNoteAuditStore` instances configured for shared operational state;
- a two-party preflight barrier that forces both requests to observe the pre-claim state before they compete for the same shared-audit CAS transition.

For the same incident and request key, the regression requires exactly one successful add, exactly one persisted note, exactly one durable `armed` receipt, and exactly one final `applied` receipt. The losing request may resolve as already-applied or ambiguous, but it must never write a second note.

Existing `WriteAheadAuditMutationTests` and `IncidentNoteAtMostOnceTests` continue to define the crash/retry semantics and must remain green.

## Safety boundary

The change does not alter incident-note validation, audit field bounds, shared audit/operator document schemas, SQL monitoring permissions, credential handling, production IIS/SQL state, RC.61 publication, P0 acceptance, or repository branch protection.

Manual/external dependency remains `#162 -> #116 -> #111`; repository-admin gate #353 remains untouched.

## Verification

Pending required PR CI gates before merge. The issue is not considered closed until those gates pass.
