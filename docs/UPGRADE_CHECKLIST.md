# Upgrade / Migration Checklist

Use this checklist for every production Monitor upgrade. Do not replace the active deployment in place without a rollback point.

## 1. Before the change window

- [ ] Identify the exact source commit/tag and release artifact checksum.
- [ ] Confirm the release CI passed Release build with warnings-as-errors and the full test suite.
- [ ] Read `docs/STATUS.md`, `docs/DECISIONS.md` and release notes for persistence/configuration changes.
- [ ] Export/create an operational backup from Administrator Settings and record its opaque backup ID.
- [ ] Back up deployment configuration separately. Do not copy secrets into the ticket/change record.
- [ ] Confirm Data Protection key material is protected and recoverable according to the selected key-store mode.
- [ ] Confirm the dedicated Monitor state database backup/HA policy when shared state is enabled.
- [ ] Confirm the previous application publish directory is intact and executable.
- [ ] Confirm `scripts/Smoke-Monitor.ps1` succeeds against the currently active version.

## 2. Configuration diff

Compare the current production configuration with `deploy/appsettings.Production.example.json`.

- [ ] New required sections/keys have explicit values.
- [ ] `AllowedHosts` contains only intended production host names.
- [ ] `WebSecurity:TrustedProxies` / `TrustedNetworks` contain only approved proxies/CIDRs.
- [ ] `Deployment:Mode` is not changed to MultiNode unless every readiness prerequisite is already satisfied.
- [ ] Shared-state connection string, Data Protection KEK, node ID and Admin credential hash/salt remain environment/service secrets, not JSON/source values.
- [ ] Local file paths stay outside `wwwroot` and remain writable only by the service identity.

## 3. Database/schema changes

If release notes include a Monitor state schema migration:

- [ ] Stop all Monitor nodes that can write shared state before applying a non-online migration.
- [ ] Back up the dedicated Monitor state database.
- [ ] Apply only the versioned migration script matching the release.
- [ ] Do not re-run `monitor_shared_state_v1.sql` as an upgrade shortcut if the schema version differs.
- [ ] Re-apply/review `monitor_state_least_privilege.sql` after schema changes.

Monitored SQL targets are not application-state databases and must never receive Monitor schema migrations.

## 4. Deploy candidate

- [ ] Publish/extract to a new versioned directory.
- [ ] Copy only non-secret production configuration required by the release.
- [ ] Apply filesystem ACLs to the new directory.
- [ ] Stop/drain the active IIS app/service node.
- [ ] Switch IIS physical path or Windows Service binary path to the new directory.
- [ ] Start the candidate.

For multi-node deployments, upgrade one node at a time only when the release explicitly supports mixed-version operation. Otherwise schedule coordinated downtime.

## 5. Acceptance gate

Run:

```powershell
.\scripts\Smoke-Monitor.ps1 -BaseUri https://monitor.example.internal
```

Then verify:

- [ ] `/health/live` healthy.
- [ ] `/health/ready` healthy.
- [ ] `/health` returns expected aggregate status without secret/provider detail.
- [ ] Login succeeds and secure cookie/header behavior is intact.
- [ ] Administrator Settings shows expected deployment/shared-state/credential/backup readiness.
- [ ] Existing registrations are present.
- [ ] A safe explicit Test Connection succeeds for a representative target.
- [ ] Cached Dashboard/Servers/health navigation does not initiate monitored SQL collection.
- [ ] Scheduler behavior matches the configured enabled/disabled state.
- [ ] No new errors or secret-bearing output appears in logs.

## 6. Commit or roll back

If every acceptance item is Green, retain the previous version through the agreed rollback window and close the change with the deployed tag/commit/checksum.

If any readiness/smoke/data-protection/persistence check fails, stop the candidate and follow `docs/ROLLBACK_RUNBOOK.md`. Do not repair production by deleting Monitor state, key rings or shared documents unless the recovery runbook explicitly calls for that action.
