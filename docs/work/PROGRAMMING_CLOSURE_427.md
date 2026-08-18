# Programming Closure #427 — Persisted Distributed-Lease Authority

## Base

`main@27e8d145e48e20d03ead4da313142c0b076348c5`

## Gap

`SharedStateDistributedLeaseManager.RenewAsync` and `ReleaseAsync` accepted a caller-supplied `DistributedLeaseHandle` as the ownership assertion. A publicly constructible handle carrying another owner's current resource/version could therefore reach CompareExchange without proving that the persisted lease owner was the local node. In addition, an owner could renew a lease after its persisted TTL elapsed when no contender had yet reacquired and advanced the version.

## Closure

- Renew and Release derive and validate the lease key and duration before mutation.
- Both operations read the current persisted lease document first.
- Mutation authority now requires the exact persisted document version to equal the handle version.
- The persisted lease envelope must be unreleased, have the same duration, and have an owner matching the local `NodeIdentity` with ordinal identity.
- The persisted lease must still be active at the mutation-time check. Expired ownership cannot be renewed or released; a fresh `TryAcquireAsync` is required.
- CompareExchange remains the final race gate after read-side authority validation, so a concurrent ownership/version change still prevents mutation.
- No lease key format, payload schema, duration range or normal acquire semantics changed.

## Regression coverage

`DistributedLeaseAuthorityTests` proves:

1. a locally forged current-version handle cannot renew or release another node's persisted lease and does not advance its version;
2. an expired owner cannot renew or release the stale lease before any contender reacquires it, while a fresh acquire succeeds and advances version;
3. a valid active persisted owner can still renew and release normally.

## Tracking note

The canonical tracking documents retain their existing history because prior whole-file reconciliation on this repository demonstrated truncation/history-loss risk and repository contract tests lock that history. This same-PR ledger records the #427 implementation delta without rewriting unrelated canonical history.

## Safety boundary

Distributed coordination/control-plane integrity only. No monitored-target SQL query/permission expansion, SharedState schema/key migration, secret disclosure, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external P0 acceptance, protected-P0 completion or branch-protection mutation. External/manual order remains `#162 -> #116 -> #111`; #353 remains repository-admin only.
