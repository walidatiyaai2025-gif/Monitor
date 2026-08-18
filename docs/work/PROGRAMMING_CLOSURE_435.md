# Programming Closure #435 — Server Registration Mutation Serialization

## Base

`main@6705eefdb5e49cc08cb77ef780c2c816880a0029`

## Gap

`ServerTargetLifecycleService.SetEnabled` and credential replacement both perform read-modify-write updates of the same `ServerRegistration` aggregate. They previously used independent synchronization: target state changes used their own local lifecycle gate, while credential replacement/cleanup used the credential lifecycle gate introduced by #432/#434.

A pause/enable request interleaved with a successful credential replacement could therefore persist a stale aggregate. In the worst ordering, the target-state write could restore the old owned secret reference after credential replacement had already deleted that old secret, leaving the registration pointing at a missing credential. The inverse ordering could also lose a concurrent `IsEnabled` change.

## Closure

- Introduce one `ServerRegistrationMutationGate` for process-local registration lifecycle mutations.
- Register that gate as a singleton in production DI.
- Inject the same singleton into `WriteAheadAuditedCredentialLifecycleService` and `ServerTargetLifecycleService`.
- Credential replacement and orphan cleanup continue to append their request audit before waiting for the mutation gate, preserving write-ahead audit semantics.
- Credential operations continue to wait with the caller cancellation token.
- Target state transitions perform their read/check/audit/upsert/cache-eviction sequence while holding the same gate, so they cannot read a credential reference that is concurrently being replaced.
- Existing direct-construction test paths retain compatibility constructors while production DI explicitly supplies the shared singleton.

## Deterministic regression coverage

`CredentialLifecycleSerializationTests.TargetDisable_WaitsForReplacementAndPreservesCommittedCredentialReference` starts a local credential replacement and blocks it inside connection testing after the candidate secret exists. A concurrent target disable then attempts to mutate the same registration through the shared gate. The test proves the target mutation waits for credential commit and that the final registration is both disabled and bound to the new credential reference; the old owned secret remains deleted.

Existing credential replacement-vs-cleanup and queued-cancellation regressions remain unchanged.

## HA follow-up

This closure is intentionally process-wide. `SharedServerRegistrationRepository` performs document-level compare/exchange, but `Upsert` still replaces the caller-supplied full `ServerRegistration`; a stale full aggregate produced on another node can therefore overwrite a newer field after a CAS retry. That cross-node field-level lost-update hazard requires repository-level atomic/conditional mutation rather than pretending a process-local semaphore is cluster-wide. It is tracked as the next programming closure.

## Safety boundary

No credential plaintext persistence, external secret-provider behavior change, monitored-target SQL query/permission expansion, production IIS/SQL mutation, RC.61 publication, external P0 acceptance, or branch-protection mutation. Manual/external dependency remains `#162 -> #116 -> #111`; #353 remains repository-admin only.
