# BATCH-100 → BATCH-200 Upgrade Compatibility

BATCH-200 is additive over the BATCH-100 release. Existing registration, credential, snapshot, incident, backup, observability, security and deployment contracts remain supported unless a future ADR explicitly versions them.

## Compatibility checks

- `ServerRegistration` and `HealthIncident` evidence contracts are not expanded with operator metadata; collaboration remains a separate durable control-plane layer.
- Legacy `/reports/servers.csv` remains available while versioned reporting uses `/reports/servers-v2.csv`.
- Existing `/diagnostics/package`, `/health/live`, `/health/ready`, server-detail, incident-detail and snapshot-refresh routes remain intact.
- BATCH-200 does not require a new monitored-SQL permission or query.
- Existing File/InMemory/Shared operator metadata formats are not destructively migrated by retention governance; pruning is represented by audit-backed receipts.
- MultiNode still requires the BATCH-100 shared-state, coordination, key-management and credential readiness gates.
- New export, fleet, help, readiness and governance GETs use Monitor-owned/cache state only.

## Upgrade sequence

1. Back up the current BATCH-100 operational state using the existing runbook.
2. Deploy the BATCH-200 build using the existing IIS/Windows Service/reverse-proxy topology.
3. Run `/health/live` and `/health/ready` smoke probes.
4. Authenticate and verify `/enterprise/readiness`, `/enterprise`, `/enterprise/fleet` and `/enterprise/help`.
5. Verify one versioned report and confirm no credential/endpoint material is present.
6. In MultiNode, validate cross-node operator metadata and maintenance policy consistency before enabling normal operations.

Rollback continues to use the BATCH-100 deployment/rollback procedure because BATCH-200 avoids destructive domain-schema migration.
