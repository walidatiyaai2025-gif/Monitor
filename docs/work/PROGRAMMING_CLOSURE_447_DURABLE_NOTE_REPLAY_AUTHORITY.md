# Programming Closure #447 — Durable Incident-Note Replay Authority

## Gap

PR #446 moved incident-note request identity out of the rolling audit stream into a bounded durable `Armed` / `Applied` ledger, but `IncidentCollaborationService.TryAddNote` still ran the legacy audit preflight before asking the coordinated claim store for current request state.

That left one deterministic post-mutation failure falsely ambiguous:

1. durable request state is claimed `Armed`;
2. the armed audit receipt is persisted;
3. the operator note mutation succeeds;
4. the durable request state advances to `Applied`;
5. the final `incident.note.request=applied` audit append fails.

On retry the older audit preflight observed the retained armed receipt and threw `IncidentNoteRequestAmbiguousException` before the durable request ledger could report `AlreadyApplied`. The note stayed at-most-once, but replay truth was masked until the armed audit receipt eventually rolled out.

## Implementation

- `IncidentCollaborationService.TryAddNote` now detects `IIncidentNoteClaimAuditStore` before the legacy audit scan.
- For coordinated stores, the durable claim/state path is authoritative for replay state:
  - `AlreadyApplied` returns the existing idempotent no-op result;
  - `Ambiguous` still fails closed;
  - `Claimed` continues through the note mutation path.
- Legacy audit preflight remains unchanged for plain `IAuditStore` implementations that do not provide coordinated claim semantics, preserving constructor/test compatibility and the pre-#446 fallback contract.
- Write-ahead `incident.note.write.request=requested`, armed evidence, durable state ordering, rolling audit bounds, and note mutation semantics remain unchanged.

## Regression coverage

`DurableAppliedState_WinsWhenFinalAuditEvidenceFailsAndArmedReceiptRemains` forces the final applied audit append to fail after `CoordinatedIncidentNoteAuditStore` has already persisted durable `Applied` state. It proves:

- the first call surfaces the audit failure;
- exactly one note is present;
- the armed audit receipt remains while applied audit evidence is absent;
- retrying the same request key returns the duplicate/no-op result instead of false ambiguity;
- no second note is written.

The existing non-coordinated final-audit-failure regression remains intentionally ambiguous, proving legacy fallback semantics are preserved when no durable coordinated request-state store exists.

## Canonical tracking reconciliation

PR #446 / Issue #445 introduced material production request-state persistence but only added its focused `docs/work` closure ledger. It did not reconcile `docs/IMPLEMENTATION_PLAN.md`, `docs/STATUS.md`, or `docs/FEATURE_CATALOG.md` as required by `AGENTS.md`.

This PR adds a compact current-state section to all three canonical documents while preserving their historical body. The reconciliation records both:

- #445 / PR #446 — durable incident-note request state: COMPLETE / MERGED;
- #447 — durable Applied replay authority over stale legacy audit preflight: this closure.

## Safety boundary

No monitored-target SQL query or permission changes, credential behavior, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external P0 acceptance, or branch-protection mutation. Manual/external dependency remains `#162 -> #116 -> #111`; #353 remains an independent repository-admin gate.

## Validation contract

Do not merge until the exact final head is current with `main`, has zero unresolved review threads, and all repository-required checks are Green:

- Linux CI / Release build / full test suite and safety runtimes;
- Windows production-candidate;
- SQL Server 2022 Real SQL acceptance;
- protected-P0 PR metadata guard;
- protected-P0 PR commit guard.
