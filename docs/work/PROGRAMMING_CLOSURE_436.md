# Programming Closure #436 — Atomic Server Registration Field Mutations

## Base

`main@1e91304216ee53a40e3638316350de71e07423e6`

## Gap

The shared registration repository already uses compare/exchange for its document, but `Upsert` replaces a caller-supplied full `ServerRegistration`. A node can therefore read an older aggregate, another node can change a different field, and the stale node can later retry successfully with the whole stale aggregate. This can lose `IsEnabled`, overwrite a newer credential reference, or resurrect an old owned-secret reference.

The process-local mutation gate introduced by #435/#437 prevents this ordering inside one process but cannot serialize independent nodes.

## Closure

- Add explicit registration field-mutation results: `Applied`, `NotFound`, `Conflict`, and `Unchanged`.
- Add `TryReplaceSecretReference(id, expectedReference, nextReference)` and `SetEnabled(id, enabled)` to the repository contract.
- In-memory implementation uses `ConcurrentDictionary.TryUpdate` retry loops.
- File implementation performs each field mutation under its durable store lock and rolls back memory if persistence fails.
- Shared implementation applies the field mutation inside `SharedStateDocumentMutation.Mutate`, so every compare/exchange retry re-deserializes the latest shared document before deciding the new field value.
- Shared registration format remains version 1 and continues using `monitor:registrations:v1`; no data migration is introduced.
- Production DI uses `AtomicSharedServerRegistrationRepository` whenever shared registrations are enabled.
- Production credential lifecycle uses a fail-closed atomic implementation. After Test Connection succeeds, commit replaces only the expected credential reference. If another node already changed that reference, the operation audits `conflict`, does not overwrite the newer value, and removes a newly-created local candidate when applicable.
- Target enable/disable uses the repository field mutation instead of re-persisting a full stale registration.
- Existing write-ahead request audit and process-local mutation gate remain in place.

## Deterministic regression coverage

`AtomicServerRegistrationMutationTests` covers:

1. a shared credential mutation whose first CAS is blocked while another node disables the target; retry must preserve both the new credential reference and `IsEnabled=false`;
2. two nodes attempting credential replacement from the same expected prior reference; the stale node must return `Conflict` and preserve the winner;
3. the production atomic credential lifecycle returning a clear concurrent-change failure without overwriting the competing reference;
4. in-memory field mutations preserving independent latest fields and rejecting stale expected references;
5. file-backed field mutations preserving independent latest fields across reopen and rejecting stale expected references.

## Safety boundary

No credential plaintext is added to registration state or audit. External secret-provider behavior, monitored-target SQL permissions/queries, production IIS/SQL state, RC.61 publication, P0 acceptance, and repository branch protection are unchanged.

Manual/external dependency remains `#162 -> #116 -> #111`; repository-admin gate #353 remains untouched.
