# Programming Closure #396 — Durable write-ahead audit before operator mutations

## Objective

Require a durable attributable request-intent audit marker before the first confirmed operator/control-plane state mutation. If the request audit cannot be persisted, the operation fails closed with zero mutation. Existing outcome audits remain authoritative for success/rejection details.

## Covered mutation paths

- server monitoring enable/disable before registration update and cache eviction;
- Operations incident acknowledge/resolve/reopen before workflow mutation;
- incident resolve/reopen-with-note before transition mutation;
- incident owner, note, reopen-note and resolution-note metadata before metadata mutation;
- enterprise server operator-profile update before metadata upsert;
- enterprise recommendation acknowledgment/reopen before metadata mutation;
- local/external credential replacement and orphan-owned-secret cleanup through a write-ahead audited lifecycle wrapper registered for `ICredentialLifecycleService`.

## Safety and compatibility

- request audit failure propagates before the first state mutation;
- existing post-operation audit action/outcome markers are retained;
- incident note idempotency continues to depend only on `incident.note.request = applied`; `incident.note.write.request = requested` alone does not suppress a safe retry;
- credential request audit payloads include only actor, registration ID / bounded operation category, never username, password, secret reference or connection material;
- actor validation, authorization, antiforgery, workflow/CAS behavior, credential candidate compensation/cleanup, monitored-SQL collection/permissions and external production gates are unchanged.

## Regression coverage

- server lifecycle audit failure leaves registration state and cache untouched;
- incident owner audit failure leaves assignee unchanged;
- incident note audit failure leaves notes unchanged;
- a durable note request-intent followed by an audit exception can be retried safely and is deduplicated only after the existing applied receipt is persisted;
- local credential replacement audit failure performs zero secret write, zero connection test and zero registration switch;
- credential cleanup audit failure performs zero owned-secret deletion;
- existing incident-transition enrichment tests now lock both write-ahead request markers and the original outcome markers.

## Validation contract

PR #397 must remain Draft until the final exact head / PR merge ref is Green on normal CI, Real SQL acceptance, Windows production-candidate and protected-P0 guards, with zero unresolved review threads and no merge conflict. Exact run IDs and final head are recorded in the PR verification comment immediately before merge.

## Boundary

Programming/accountability hardening only. No secret disclosure, monitored-SQL permission/query expansion, autonomous remediation, RC.61 publication, real production IIS/SQL mutation, external P0 acceptance or branch-protection mutation. Manual/external order remains `#162 -> #116 -> #111`; #353 remains repository-admin only.
