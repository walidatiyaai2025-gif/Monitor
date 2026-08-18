# Programming Closure #441 — State-Aware Incident Retention Receipts

## Base

`main@56e1e9d941d7775cff617e60a374b2e606ff38b7`

## Gap

`GovernanceRetentionService.Apply` evaluates a dry-run plan and then appends `governance.prune.incident` receipts by deterministic incident ID. A newer health observation can reactivate the same `registrationId:ruleId` incident after the dry-run has selected an old resolved state but before the receipt is committed. `IncidentCollaborationService.QueryByAssignee` previously treated every matching receipt as a permanent ID tombstone, so a stale receipt could hide the now-live incident from the enterprise collaboration projection.

The failure is cross-node relevant because shared operational state can update the incident on one node while another node is applying retention. An in-process lock would neither cover all controller instances nor provide a multi-node correctness boundary.

## Closure

- Introduce one domain-level `IncidentRetentionPolicy.ShouldPruneOperatorMetadata` predicate for incident-retention eligibility.
- Use that predicate in `GovernanceRetentionService.DryRun`, so candidate selection retains the existing orphan-or-old-resolved semantics.
- Make `GovernanceRetentionService.IsIncidentPruned` state-aware: a receipt is effective only while the current incident still satisfies the retention predicate.
- Make `IncidentCollaborationService.QueryByAssignee` evaluate the same predicate against the current incident before honoring a prune receipt.
- Capture one `TimeProvider` value per collaboration query to keep the cutoff stable across a projection.
- Preserve note retention exactly as-is; immutable note IDs continue to use their existing prune receipts.
- Preserve server retention, audit action names, receipt outcomes, audit scan bounds, metadata schemas, and shared-state document schemas.
- No in-process or distributed lock is added. Correctness comes from reevaluating the current incident state at the read boundary, which works with both local repositories and shared multi-node operational state.

## Deterministic regression coverage

`GovernanceRetentionTests.StaleIncidentPruneReceipt_DoesNotHideIncidentReactivatedBeforeReceiptCommit` intercepts the prune-receipt append inside `Apply`, reactivates the selected incident immediately before the receipt is stored, and then requires:

- the prune receipt still exists as audit evidence;
- the current incident state is `Open`;
- `IsIncidentPruned` returns false;
- the collaboration projection contains the active incident.

`GovernanceRetentionTests.StaleIncidentPruneReceipt_DoesNotHideNewResolvedStateInsideConfiguredWindow` applies a receipt to an old resolved state, creates a newer incarnation inside a non-default retention window, and requires both the governance service and collaboration projection to keep it visible.

Existing `B200_052_PruneReceiptHidesResolvedIncidentFromCollaborationProjection` and `GovernanceReceiptBeyondFirstAuditPageRemainsEffective` continue to prove that genuinely stale resolved incidents remain hidden and bounded audit paging remains effective. Existing note-retention coverage remains unchanged.

## Safety boundary

The change does not alter monitored SQL access, snapshot collection, incident mutation rules, operator-note idempotency, credential handling, production IIS/SQL state, RC publication, P0 acceptance, or repository branch protection.

Manual/external dependency remains `#162 -> #116 -> #111`; repository-admin gate #353 remains untouched.

## Verification

Pending required PR CI gates before merge. Issue #441 is not considered closed until Linux CI, Windows production-candidate, Real SQL acceptance, protected-P0 metadata, and protected-P0 commit guards pass.
