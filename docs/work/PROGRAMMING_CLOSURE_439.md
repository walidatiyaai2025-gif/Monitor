# Programming Closure #439 — Concurrent Incident Note Idempotency

## Base

`main@cadcf0258134db4be2a6f097d35c3a40b6ee116f`

## Gap

`IncidentCollaborationService.TryAddNote` checked the audit trail for an already-applied or already-armed request key and then appended the `armed` write receipt in a separate operation. Two requests routed to different nodes could both complete the preflight read before either node persisted `armed`, allowing both to mutate incident notes for the same request key.

The operator metadata stores themselves are already serialized/CAS-safe. The missing invariant was exclusive ownership of the transition from "no receipt" to the durable `armed` receipt.

## Closure

- Add `CoordinatedIncidentNoteAuditStore` as the production `IAuditStore` wrapper used for incident-note claims.
- Preserve the existing audit schema and event names; no operational-store migration is introduced.
- Preserve write-ahead semantics: `incident.note.write.request=requested` is still written before the claim and, by itself, remains safely retryable.
- In SingleNode mode, the singleton audit wrapper serializes the short claim section with an in-process gate.
- In MultiNode mode, the wrapper acquires a distributed lease whose resource is a SHA-256-derived identifier for the bounded receipt target.
- After coordination is established, the wrapper re-reads the durable audit state. `applied` returns idempotently, an existing `armed` receipt fails closed as ambiguous, and only an empty receipt state may append the new `armed` receipt.
- The distributed lease is released immediately after the durable claim. The `armed` receipt—not lease lifetime—is the safety boundary before `IOperatorMetadataStore.AddIncidentNote` executes.
- If MultiNode exclusivity cannot be established, the request fails closed instead of writing a note without a claim.
- Lease-release shared-state outages do not erase a successful durable claim; the lease can expire naturally while the `armed` receipt prevents a competing mutation.

## Deterministic regression coverage

`IncidentNoteMultiNodeIdempotencyTests` creates two independent collaboration-service instances representing `node-a` and `node-b`. Both use:

- separate `SharedAuditStore` / `SharedOperatorMetadataStore` instances;
- one shared CAS document store;
- separate `SharedStateDistributedLeaseManager` instances with different node identities;
- a two-party preflight barrier that forces both requests to observe the pre-claim state before they compete for ownership.

For the same incident and request key, the regression requires exactly one successful add, exactly one persisted note, exactly one durable `armed` receipt, and exactly one final `applied` receipt. The losing request may resolve as already-applied or ambiguous, but it must never write a second note.

Existing `WriteAheadAuditMutationTests` and `IncidentNoteAtMostOnceTests` continue to define the crash/retry semantics and must remain green.

## Safety boundary

The change does not alter incident-note validation, audit field bounds, shared audit/operator document schemas, SQL monitoring permissions, credential handling, production IIS/SQL state, RC.61 publication, P0 acceptance, or repository branch protection.

Manual/external dependency remains `#162 -> #116 -> #111`; repository-admin gate #353 remains untouched.

## Verification

Pending required PR CI gates before merge. The issue is not considered closed until those gates pass.
