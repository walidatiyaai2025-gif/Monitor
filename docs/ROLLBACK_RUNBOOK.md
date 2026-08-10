# Rollback / Recovery Runbook

Use this runbook when a new Monitor deployment fails readiness, smoke tests, authentication, persistence or operational acceptance.

## Principles

- Stop the failing candidate before changing state.
- Prefer application-version rollback over ad-hoc production edits.
- Never delete Data Protection keys, Monitor-owned secrets, shared-state documents or operational files to make startup succeed.
- Never downgrade the dedicated Monitor state schema by overwriting it with an older creation script.
- Do not expose provider errors, connection strings, passwords or secret references in incident tickets/log excerpts.

## 1. Classify the failure

Record only safe metadata:

- release/tag/commit;
- deployment node label;
- health endpoint/status category;
- timestamp;
- affected deployment mode;
- whether the failure occurred before or after a state migration.

Do not paste request bodies, environment-variable values, connection strings or raw provider exceptions.

## 2. Stop/drain the candidate

IIS:

```powershell
Stop-WebAppPool -Name 'Monitor'
```

Windows Service:

```powershell
Stop-Service Monitor
```

For multi-node systems, remove only the failing node from the load balancer first unless the release requires coordinated rollback.

## 3. Application-only rollback

Use this path when no incompatible state/schema migration was committed.

1. Point IIS physical path or the Windows Service binary path back to the previous versioned publish directory.
2. Restore the previous non-secret configuration file if the configuration contract changed.
3. Keep current protected secrets/key rings unless the previous release explicitly requires a different supported key-store mode.
4. Start the previous version.
5. Run `scripts/Smoke-Monitor.ps1`.
6. Verify login, Administrator readiness, registrations and cached navigation.

## 4. Operational-state restore

Use this only when Monitor-owned operational data was mutated incompatibly or corrupted and the versioned backup contract supports the target release.

1. Keep the application stopped.
2. Identify the pre-change Monitor operational backup ID created during the upgrade checklist.
3. Start the compatible Monitor version in a controlled maintenance state if the restore command requires the application UI/service.
4. Run **Dry-run Validate** first.
5. Restore only after validation succeeds.
6. For file-backed persistence, restart Monitor after restore because singleton state is loaded at process startup.
7. Run smoke/readiness checks before operator access.

Operational backup does not contain SQL passwords, protected secret ciphertext, Data Protection keys/KEKs or provider connection material. Those must be recovered through their own approved secret/key backup process.

## 5. Dedicated state database rollback

If a versioned state-schema migration was applied:

- use the migration-specific rollback/restore procedure documented with that release;
- prefer restoring the pre-migration database backup to a controlled database over manual table edits;
- stop all writing Monitor nodes while performing an incompatible schema rollback;
- re-apply the runtime least-privilege role after database restore if security metadata was not included/restored as expected;
- confirm the schema version through `/health/ready` before returning traffic.

Never run `monitor_shared_state_v1.sql` over a newer/different schema version as a rollback shortcut; the script intentionally fails closed on version mismatch.

## 6. Key/credential recovery

If startup or SQL credential resolution fails after deployment:

- verify the correct Data Protection key-ring location/mode is available;
- verify the shared-key KEK environment variable is present only when shared key-ring mode is selected;
- verify external secret references resolve under the service identity;
- do not create a new key ring over encrypted local credentials as a repair action;
- use the tested credential-reference replacement workflow if a target credential itself changed.

## 7. Return to service

Run:

```powershell
.\scripts\Smoke-Monitor.ps1 -BaseUri https://monitor.example.internal
```

Then verify:

- `/health/live`, `/health/ready`, `/health` are healthy;
- authentication succeeds;
- security headers/cookies remain correct;
- expected registrations/state are present;
- no monitored SQL collection occurs from ordinary browser GETs;
- an explicit representative Test Connection succeeds;
- scheduler state matches configuration;
- logs contain no secret/provider canaries.

Only then re-enable normal load-balancer/operator traffic.

## 8. Post-incident

Record the failed release, successful rollback target, safe failure category, affected state migration (if any), and corrective follow-up. Keep the failed candidate artifact for reproducibility; do not reuse it for production without a new reviewed commit and CI verification.
