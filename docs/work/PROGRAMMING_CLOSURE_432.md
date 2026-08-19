# Programming Closure #432 — Serialize Local Credential Replacement And Orphan Cleanup

## Base

`main@cbac7fba14b1d332f6405ae4382fadb4b767d350`

## Gap

`CredentialLifecycleService.CleanupOrphanedOwnedSecretsAsync` could run concurrently with local credential replacement. A replacement creates and validates a new Monitor-owned local secret before committing the registration. Cleanup could snapshot registrations during that interval, classify the uncommitted candidate as orphaned, delete it, and allow replacement to commit a registration that points to a missing secret.

The same lifecycle boundary also applies to external replacement because it may delete a previously owned local secret after committing an external reference.

## Closure

- Added one `SemaphoreSlim` mutation gate owned by the singleton `CredentialLifecycleService`.
- `ReplaceWithLocalCredentialAsync`, `ReplaceWithExternalReferenceAsync`, and `CleanupOrphanedOwnedSecretsAsync` acquire the same gate before any owned-secret lifecycle mutation.
- Cancellation is preserved while waiting for the gate.
- Existing candidate compensation, commit-failure handling, write-ahead audit behavior, external-reference semantics, and local-credential deployment policy remain unchanged.
- Multi-node policy is unchanged: local-owned credential creation remains disabled when deployment policy disallows it.

## Regression coverage

`CredentialLifecycleConcurrencyTests.Cleanup_WaitsForLocalReplacementCommit_AndPreservesCandidateSecret` creates a deterministic interleaving without sleeps or timing windows:

1. local replacement acquires the lifecycle mutation gate, creates its candidate secret, and blocks inside Test Connection;
2. orphan cleanup is invoked while replacement is blocked;
3. cleanup must return an incomplete task because it is waiting on the same mutation gate, and no secret deletion may occur;
4. Test Connection is released successfully, replacement commits, and only the previous owned secret is deleted;
5. cleanup resumes after commit and leaves the now-active candidate secret intact.

The pre-fix implementation fails the contract because cleanup can complete synchronously during step 2 and delete the candidate before registration commit.

## Safety boundary

Credential lifecycle serialization only. No plaintext credential material is persisted or logged, no external secret-provider behavior is changed, no production IIS/SQL configuration is mutated, and no external/manual acceptance gate is closed by this repository change.
