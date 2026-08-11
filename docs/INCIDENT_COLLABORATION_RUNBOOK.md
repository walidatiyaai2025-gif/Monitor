# Incident Collaboration Runbook

## Purpose

Coordinate deterministic Monitor incidents without changing the underlying evidence. Collaboration metadata is a separate control-plane layer: assignee, bounded notes, recommendation acknowledgment, resolution notes and reopen reasons.

## Ownership

- Assign an incident to an operator/team from Enterprise Operations.
- Owner changes are audited as previous-to-next state.
- Use assignee filters to focus the bounded incident queue.

## Notes

- Notes are bounded and reject credential/connection-shaped material.
- Note requests carry a replay key; duplicate normal submissions are ignored using a hashed audit receipt.
- UI rendering uses Razor encoding. Do not paste HTML, secrets, SQL text or raw provider errors.
- Retention governance can logically prune aged notes using reversible audit-backed receipts.

## SLA buckets

Open incidents are projected as Fresh, Aging or Breached using deterministic age thresholds. Resolved incidents use the Resolved bucket. SLA buckets are presentation/triage metadata and do not rewrite severity or evidence.

## Resolution and reopen

1. Validate cached evidence and the approved external DBA action, if any.
2. Resolve using a bounded resolution note.
3. If fresh evidence returns, reopen with a bounded reason.
4. Resolution/reopen operator context remains separate from `HealthIncident.Evidence` and is audited.

## Recommendations

Acknowledge deterministic recommendations only after review. Recommendation acknowledgment is not execution. Monitor does not autonomously run remediation SQL.
