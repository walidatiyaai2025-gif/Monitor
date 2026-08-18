# Programming Closure #449 — File Incident-Note Cross-Process Mutation Lease

## Gap

The SingleNode incident-note request ledger introduced by #445/#446 persisted bounded `Armed` / `Applied` state outside the rolling audit window, but `FileIncidentNoteRequestStateStore` coordinated only inside one object instance.

Each file-store instance cached shard dictionaries in `_loaded` and serialized mutations with its own `_gate`. `AtomicJsonFile.Save` protected the physical JSON document with temp-file + atomic replacement, but it did not serialize the complete authoritative read -> validate -> mutate -> persist transaction across independent store/worker processes sharing the same operational root.

That allowed stale independent instances to:

- return `Claimed` for a request another instance had already armed;
- overwrite peer entries in the same shard;
- replace a peer's durable `Applied` state with stale `Armed` state.

The risk is relevant to overlapping SingleNode workers that share `App_Data`, including IIS recycle overlap and web-garden-style concurrent workers.

## Implementation

`FileIncidentNoteRequestStateStore` now:

- uses one stable `incident-note-requests.lock` sidecar in the operational root as a cross-process-compatible mutation lease;
- opens that lease with `FileShare.None` and bounded retry for at most five seconds;
- fails closed with `IOException` if safe mutation ownership cannot be obtained within the bound;
- holds the lease across the complete disk reload -> format/bounds validation -> policy mutation -> `AtomicJsonFile.Save` transaction;
- reloads the authoritative shard from disk for every `TryClaim` and `MarkApplied` mutation instead of retaining process-local shard snapshots;
- holds the same lease across legacy receipt materialization so migration cannot race live request-state mutation.

The existing 64 shards, 512 entries/shard, target bounds, `Armed -> Applied` monotonic policy, atomic JSON replacement, SharedState CAS path and audit-receipt migration policy are unchanged.

## Regression coverage

`IncidentNoteRequestStateTests` now proves with independent file-store instances that:

1. a stale peer cannot reclaim a request already armed by another instance;
2. independent same-shard mutations retain every peer entry across restart;
3. an `Applied` request cannot be downgraded by a stale peer mutation;
4. the file store waits for the shared mutation lease before claiming instead of bypassing it.

These regressions exercise the behavior that was previously protected only by an instance-local cache/lock.

## Tracking reconciliation

PR #448 / Issue #447 is already COMPLETE / MERGED as `c0d6472b522548a8cf4ad4d2d6271ad722dd0f86`; its canonical documents still described it as a closure candidate immediately after merge. PR #450 reconciles that stale merge-state wording together with #449 so the plan/status/catalog represent current `main` truth.

## Safety boundary

No monitored-target SQL query/permission expansion, credential behavior, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external P0 acceptance or branch-protection mutation. Manual/external dependency remains `#162 -> #116 -> #111`; #353 remains repository-admin only.

## Validation contract

Do not merge until the exact docs-inclusive PR #450 head is current with `main`, has zero unresolved review threads, and all repository-required checks are Green: Linux CI, Windows production-candidate, SQL Server 2022 Real SQL when selected/required, protected-P0 metadata and protected-P0 commit guards.
