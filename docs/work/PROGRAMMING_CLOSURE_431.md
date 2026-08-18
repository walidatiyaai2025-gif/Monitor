# Programming Closure #431 — Distributed Manual-Refresh Lease Heartbeat

## Base

`main@cbac7fba14b1d332f6405ae4382fadb4b767d350`

## Gap

`SnapshotRefreshService` acquired the distributed `refresh:{registrationId}` lease only once and released it after `cache.RefreshAsync`. The configured refresh lease can be as short as 15 seconds, while the bounded collection path can consume up to 7 seconds in the primary SQL collector plus three independent 3-second enrichment windows before secret-resolution and scheduling overhead. A valid refresh could therefore cross its original lease expiry, allowing a second node to reacquire and start duplicate collection while the first node was still active.

Caller cancellation had the same ownership-lifetime risk because `ServerHealthSnapshotCache` intentionally keeps its shared local collection flight running after an individual waiter cancels; releasing the distributed lease immediately on caller cancellation would expose that still-running collection to a second node.

## Closure

- A coordination-enabled manual refresh starts a lease heartbeat immediately before the cache refresh.
- Renewal is scheduled at one-third of the lease duration, or earlier when the remaining expiry margin is smaller.
- Every successful renewal advances the in-memory handle to the latest persisted lease version.
- Final Release therefore uses the latest renewed handle/version rather than the original acquisition handle.
- A null renewal or `SharedStateStoreUnavailableException` marks coordination authority as lost. The completed cache result is not observed and is not reported as `Refreshed` or `RetainedStale`; the caller receives a fail-closed throttled coordination result.
- Caller cancellation is still surfaced as cancellation, but the already-started shared cache flight is allowed to settle while the heartbeat retains distributed ownership; only then is the lease released.
- SingleNode behavior, local concurrency gating, the 15-second manual throttle, disabled/not-found handling, shared-state acquisition failure behavior and best-effort release fallback are preserved.

## Deterministic regression coverage

`DistributedRefreshLeaseHeartbeatTests` proves:

1. a blocked refresh renews before the original 15-second lease expiry, remains exclusive after crossing that original expiry, and releases the renewed version;
2. if another node legitimately reacquires after expiry before renewal, the stale node cannot renew, suppresses observer publication/success, and cannot release the replacement owner's persisted lease.

The tests use a controlled renewal-delay boundary plus the real `SharedStateDistributedLeaseManager` and a shared compare/exchange document store, so no wall-clock sleeps are required to cross lease expiry.

## Safety boundary

HA/control-plane coordination only. No monitored-target SQL query/permission expansion, production IIS/SQL mutation, SharedState key/schema migration, RC.61 publication, external P0 acceptance, or branch-protection mutation. Manual/external dependency remains `#162 -> #116 -> #111`; #353 remains repository-admin only.
