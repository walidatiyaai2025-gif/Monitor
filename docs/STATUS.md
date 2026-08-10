# Project Status

**Updated:** 2026-08-10 14:32 +03:00  
**Branch:** `agent/m7-004-ha-topology-guard`  
**Target:** M7-004 fail-closed HA / multi-node topology guard  
**Issue:** #47  
**PR:** #48  
**Overall:** 🟢 M0–M6 VERIFIED — M7-001..M7-004 CI VERIFIED

## M7-004 — HA / multi-node deployment guard — CI VERIFIED

- Adds explicit `Deployment:Mode` with `SingleNode` as the default and only currently supported topology.
- Topology validation runs before persistence/services are activated.
- Selecting `MultiNode` fails application startup with a deterministic redacted message explaining that shared registration, operational-state and coordination providers are required.
- The guard deliberately does not treat local files, process memory or a network-share path as distributed coordination.
- Existing SQL, registration, operational-state, secret and collector service contracts remain unchanged.
- Administrator Settings now displays the effective topology, safety status and the bounded list of state that is still node-local.
- Node-local state explicitly includes registration/operational stores, runtime SQL credentials, login limiter, snapshot cache/single-flight and scheduler ownership/backoff/runtime status.
- Settings is now Administrator-policy protected; it exposes no mutation control for topology.
- CI run `31383750309`: SUCCESS — Release build 0 warnings / 0 errors; 94/94 tests passed; Razor compiled in Release.

## M7-003 — Durable operational state — CI VERIFIED

- Audit/history/incidents survive restart behind unchanged interfaces using independent atomic files.
- Final CI `31383226721`: 89/89 tests; Release build 0 warnings / 0 errors.

## M7-002 — External SQL secret provider — CI VERIFIED

- `env:<alias>` routes directly to strict process-environment variables behind `IConnectionSecretStore` and fails closed without config fallback.
- Final CI `31382052980`: 82/82 tests; Release build 0 warnings / 0 errors.

## M7-001 — Durable registration metadata — CI VERIFIED

- Dynamic registrations survive restart without persisting SQL credential values.
- Final CI `31381074579`: 72/72 tests; Release build 0 warnings / 0 errors.

## Stable architecture guardrails

- Browser/UI components never connect directly to monitored SQL Servers.
- Snapshot cache remains the shared evidence/read boundary.
- Recommendations and Advisor output remain human-review only and cannot execute production SQL.
- Secret-provider routing remains behind `IConnectionSecretStore`.
- Registration and operational file stores are explicitly single-node implementations.
- `MultiNode` deployment is fail-closed until real shared state and distributed coordination are present.
- No secret, endpoint or monitored SQL payload is included in topology validation/readiness output.

## Merge gate

Run GitHub Actions on the final docs head. Confirm `main` has not introduced an overlapping topology/shared-state change, then merge PR #48 only if restore, Release build with warnings-as-errors, Razor compilation and all tests remain Green.

## Next action

After M7-004 merge, create the next shared-state provider capability slice. Do not enable `MultiNode` until registration, operational state, scheduler ownership and required coordination primitives are backed by a real shared implementation.
