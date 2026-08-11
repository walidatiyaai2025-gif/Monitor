# P0.5 — First Production SingleNode Acceptance

Issue: #116  
Dependency: P0.4 / #115 COMPLETE  
Scope: first production activation only; `Deployment:Mode=SingleNode`.

This document is the operator evidence record for the first IIS/HTTPS production cutover. Repository CI can prove package, configuration and recovery contracts, but it cannot truthfully close #116 without exercising the actual Windows/IIS environment. **#116 must remain OPEN until the external evidence pack validates 15/15 required production gates and the real operator explicitly finalizes that evidence.**

## Release candidate contract

1. Use the versioned Windows x64 ZIP and matching `.sha256` produced by the production-candidate workflow.
2. Verify the SHA-256 before extracting or deploying.
3. Keep `deploy/appsettings.Production.example.json` as a schema/example only; do not place real secrets in source-controlled JSON.
4. Set `Deployment:Mode=SingleNode`. MultiNode is explicitly out of scope for P0.5.
5. Persist the application state directory and ASP.NET Data Protection key ring outside the replaceable release folder.
6. Use the existing monitored-SQL least-privilege role; the application must not require write access to monitored SQL targets.
7. Bind IIS to a trusted HTTPS certificate. HTTP or untrusted-loopback CI evidence is not production acceptance.

## Selected candidate

The live selected candidate, SHA-256, source head, tested merge ref and Actions artifact are tracked on Issue #116 so this runbook does not drift every time an equivalent later RC is generated. Preserve those exact values in one immutable acceptance session before cutover.

## Pre-cutover evidence

Record these values before changing IIS:

- Release version/tag.
- Source commit SHA and exact tested merge SHA.
- Artifact filename and product SHA-256.
- IIS site and application pool.
- Approved application-pool identity.
- HTTPS hostname and approved machine-certificate thumbprint.
- Previous release physical path.
- Operational backup ID.
- Stable `App_Data` state root.

## Initialize one immutable production acceptance session

Issue #150 adds a fail-closed session initializer so the selected candidate bytes, checksum and external-environment metadata are bound into one fresh workspace before any production mutation. Use the packaged `_operations` scripts after the operational backup and rollback point are known:

```powershell
.\_operations\scripts\New-ProductionAcceptanceSession.ps1 `
  -SessionRoot 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N' `
  -ArtifactPath '.\Monitor-0.1.0-rc.N-win-x64.zip' `
  -ChecksumPath '.\Monitor-0.1.0-rc.N-win-x64.zip.sha256' `
  -CandidateVersion '0.1.0-rc.N' `
  -SourceCommit '<40-hex-source-head>' `
  -TestedMergeCommit '<40-hex-tested-merge-ref>' `
  -HostName 'monitor.example.internal' `
  -SiteName 'Monitor' `
  -AppPoolName 'Monitor' `
  -AppPoolIdentity 'IIS AppPool\Monitor' `
  -CertificateThumbprint '<approved-machine-certificate-thumbprint>' `
  -OperationalBackupId '<validated-backup-id>' `
  -PreviousPhysicalPath 'C:\Program Files\Monitor\releases\previous' `
  -StateRoot 'C:\ProgramData\Monitor\App_Data'
```

The session initializer requires a fresh absolute Windows session root and refuses reuse. Before creating the session it verifies the exact candidate filename, exact checksum filename/content, actual SHA-256 and readable ZIP structure. It rejects secret-like metadata, provider errors, connection-string material and arbitrary SQL text.

A successful session contains:

- `candidate/Monitor-<version>-win-x64.zip` and its matching `.sha256` copied from the selected bytes;
- `evidence/p0-5-evidence-pack.json` created through `New-ProductionAcceptanceEvidencePack.ps1`;
- `evidence/proof/` as the bounded root for real gate evidence;
- `session-manifest.json` with candidate/environment identity and `status = PreparedFailClosed`;
- `session-manifest.sha256` locking the exact session manifest;
- `OPERATOR-NEXT-STEPS.txt` with the deterministic cutover sequence.

Session creation proves **0/15** external gates. Every evidence-pack gate remains `false`, `acceptedBy` remains empty, `acceptedAtUtc` remains null and no production acceptance is granted. The initializer does **not** deploy or recycle IIS, execute SQL, record a gate PASS, finalize acceptance, call GitHub or close #116/#111.

Verify `session-manifest.sha256` before the first production operation. After initialization, use the candidate copy and evidence pack inside that same session for the complete cutover; do not mix evidence or candidate bytes from another workspace.

## Deployment procedure

1. Obtain the exact candidate selected on #116 and verify its SHA-256.
2. Create and validate the operational backup and preserve the previous release as the rollback point.
3. Create the immutable acceptance session above; verify `session-manifest.sha256` and confirm `PreparedFailClosed` / 0/15.
4. Run `_operations/scripts/Test-IisProductionPrerequisites.ps1` and retain its non-secret output beneath the session `evidence/proof` root.
5. Run `_operations/scripts/Deploy-ProductionSingleNode.ps1` without `-Apply`; review and retain the PLAN ONLY output.
6. Apply the reviewed plan with explicit `-Apply`.
7. Run the HTTPS acceptance harness against the session-bound candidate:

```powershell
.\_operations\scripts\Accept-ProductionSingleNode.ps1 `
  -BaseUri https://monitor.example.internal/ `
  -ArtifactPath 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N\candidate\Monitor-0.1.0-rc.N-win-x64.zip' `
  -ChecksumPath 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N\candidate\Monitor-0.1.0-rc.N-win-x64.zip.sha256' `
  -EvidencePath 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N\evidence\proof\health-acceptance.json'
```

8. Authenticate through the actual trusted HTTPS endpoint.
9. Register/Test/Refresh the approved least-privilege SQL target and retain bounded non-secret evidence.
10. Recycle the IIS application pool and repeat health/auth/read checks.
11. Prove registration, protected credential, audit/history/incident and cached/read state survived the recycle.
12. Validate the operational backup, execute the approved rollback rehearsal and repeat health/auth/read checks after rollback.

`Accept-ProductionSingleNode.ps1` validates artifact checksum and the three control-plane health endpoints. It intentionally does **not** claim recycle, credential, SQL privilege, backup or rollback success.

## External acceptance evidence pack

Issue #141 adds a machine-verifiable closure record. The pack does not perform IIS deployment, does not recycle IIS and does not execute SQL. The fail-closed generator never marks a production gate PASS. Issue #144 adds an explicit one-gate-at-a-time recorder so the operator does not have to hand-edit gate timestamps, evidence references or SHA-256 values. Issue #147 removes the manual final JSON-edit step by adding a fail-closed finalizer for `acceptedBy`, `acceptedAtUtc` and the closure summary. Issue #150 binds the selected candidate/checksum and the initial fail-closed pack into one immutable session workspace before cutover.

### 1. Use the session-generated fail-closed pack

`New-ProductionAcceptanceSession.ps1` invokes the canonical `New-ProductionAcceptanceEvidencePack.ps1` with the candidate/environment metadata supplied for that session. The low-level generator remains the evidence schema authority, but operators should start a real cutover with the session initializer so candidate bytes and evidence cannot be accidentally mixed across workspaces.

Immediately after session creation, verify:

- the candidate copy hashes to the selected product SHA-256;
- `session-manifest.sha256` matches `session-manifest.json`;
- the manifest state is `PreparedFailClosed`;
- the evidence pack contains exactly 15 required gates and all 15 are false;
- no closure summary or final operator acceptance metadata exists.

The generator/session initializer cannot create a completed production acceptance.

### 2. Record each external gate explicitly

For each real environment gate, save one bounded text/JSON evidence file beneath the session's `evidence/proof` root. Do not use screenshots or binary blobs as the authoritative machine-verifiable evidence. After the operator has actually performed and reviewed a gate, record that **one gate at a time** with explicit `-AcknowledgePass`:

```powershell
.\_operations\scripts\Set-ProductionAcceptanceGate.ps1 `
  -EvidencePath 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N\evidence\p0-5-evidence-pack.json' `
  -GateName 'iisPreflightPassed' `
  -EvidenceFile 'proof\iis-preflight.txt' `
  -AcknowledgePass
```

The recorder:

- accepts only the exact 15 production gate names;
- refuses to infer PASS merely because an evidence file exists;
- requires `-AcknowledgePass` for every PASS assertion;
- requires a relative evidence file beneath the pack root and rejects absolute/traversal/query/fragment paths;
- scans the existing pack and the new evidence for secret-like keys/values, connection strings, SQL client/provider errors and arbitrary SQL text;
- computes the evidence SHA-256 and UTC verification timestamp itself;
- atomically changes only the named gate;
- refuses to overwrite an existing PASS unless `-ReplaceExistingPass` is explicitly supplied;
- refuses to modify a pack that already contains final `acceptedBy` / `acceptedAtUtc` metadata;
- never writes a closure summary and never sets final operator acceptance metadata.

`-AcknowledgePass` is an operator assertion, not an automated semantic verdict. The evidence must first come from the actual trusted-certificate IIS environment and must truthfully prove the named gate.

The 15 required gates are:

1. `artifactChecksumVerified`
2. `iisPreflightPassed`
3. `deploymentPlanReviewed`
4. `cutoverApplied`
5. `trustedHttpsHealthPassed`
6. `administratorAuthenticationPassed`
7. `leastPrivilegeSqlVerified`
8. `iisRecyclePassed`
9. `registrationDurabilityVerified`
10. `protectedCredentialDurabilityVerified`
11. `operationalStateDurabilityVerified`
12. `operationalBackupValidated`
13. `rollbackRehearsed`
14. `postRollbackHealthPassed`
15. `finalReadEvidencePassed`

### 3. Explicitly finalize the real 15/15 operator evidence

Do **not** hand-edit `acceptedBy` or `acceptedAtUtc`. After all 15 gates were actually performed and recorded, run the dedicated finalizer against the same session pack:

```powershell
.\_operations\scripts\Complete-ProductionAcceptance.ps1 `
  -EvidencePath 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N\evidence\p0-5-evidence-pack.json' `
  -AcceptedBy 'DOMAIN\approved.operator' `
  -ClosureSummaryFile 'p0-5-closure-summary.json' `
  -AcknowledgeFinalAcceptance
```

The finalizer is deliberately fail-closed:

- it requires explicit `-AcknowledgeFinalAcceptance` and a bounded non-secret operator identity;
- it refuses an already accepted pack, an existing closure summary, or a rooted/traversal summary path;
- it never changes any gate from FAIL to PASS and never creates missing gate evidence;
- it creates a **prospective** finalized copy first and invokes `Test-ProductionAcceptanceEvidence.ps1` against all 15 SHA-bound evidence files before touching the authoritative pack;
- it re-hashes the authoritative pack after prospective validation and aborts if another process/operator changed it concurrently;
- it atomically commits only the final operator acceptance metadata (`acceptedBy` and `acceptedAtUtc`);
- it validates the authoritative finalized pack again and writes the closure summary only after that second validation succeeds;
- if the final authoritative validation unexpectedly fails, it restores the original unaccepted pack and removes any partial closure summary;
- it does not deploy or recycle IIS, execute SQL, call GitHub, close #116/#111, or infer production acceptance from repository CI.

The explicit final acknowledgement means: the named operator has reviewed the real environment evidence and asserts that all recorded gates correspond to operations that were actually executed on the intended production host.

### 4. Re-run the fail-closed closure validator when reviewing or transferring evidence

The finalizer already runs the validator twice. An operator/reviewer can independently re-run it at any time after finalization:

```powershell
.\_operations\scripts\Test-ProductionAcceptanceEvidence.ps1 `
  -EvidencePath 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N\evidence\p0-5-evidence-pack.json' `
  -ClosureSummaryPath 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N\evidence\p0-5-review-summary.json'
```

The validator fails if any required gate is missing or false, if an unknown gate/property is injected, if candidate metadata is malformed, if the deployment mode is not exactly SingleNode, if a gate evidence file is missing or escapes the evidence root, if any evidence SHA-256 differs, or if pack/evidence content contains password/connection-string/provider-error/arbitrary SQL text material. It generates a PASS closure summary only after all 15/15 gates validate and final operator acceptance metadata is valid.

A validator/finalizer PASS is necessary but still represents evidence supplied from the real environment; repository CI only proves the session/recorder/finalizer/validator behavior. The actual production operations must still be performed on the intended Windows/IIS host.

## Mandatory restart/recycle acceptance

After the first Green HTTPS smoke:

1. Confirm at least one real SQL registration is visible from persisted application state.
2. Record its opaque registration id only; never record a password or current secret value.
3. Recycle the IIS application pool.
4. Confirm `/health/live`, `/health/ready`, and `/health` return the expected bounded statuses again.
5. Confirm the same registration id remains present after recycle.
6. Execute the approved Test Connection / bounded refresh path and confirm the protected credential resolves after recycle.
7. Confirm the Server Details page returns cached/collected evidence without fallback demo data.
8. Prove audit/history/incident operational state remains available.

These results map to `iisRecyclePassed`, `registrationDurabilityVerified`, `protectedCredentialDurabilityVerified`, `operationalStateDurabilityVerified` and `finalReadEvidencePassed` in the evidence pack.

## Least-privilege target acceptance

- Confirm the monitored SQL login is not `sysadmin`.
- Confirm the exact `scripts/sql/monitored_sql_least_privilege.sql` baseline or a stricter equivalent is in effect.
- Confirm normal Monitor collection succeeds.
- Confirm no application workflow requires INSERT/UPDATE/DELETE/DDL against the monitored target.

Record only bounded non-secret evidence. Never store the login password, full connection string, raw provider error or arbitrary SQL text.

## Backup and rollback acceptance

Before declaring P0.5 complete:

1. Validate the pre-cutover operational backup.
2. Preserve the previous versioned release directory and recorded previous IIS physical path.
3. Follow `docs/ROLLBACK_RUNBOOK.md` as the controlled rollback test.
4. Never delete Data Protection keys or Monitor-owned encrypted secrets during rollback.
5. Re-run health/auth/read smoke after rollback/restoration.
6. Confirm durable registrations and protected credentials remain recoverable.

These results map to `operationalBackupValidated`, `rollbackRehearsed` and `postRollbackHealthPassed`.

## Final P0.5 closure rule

P0.5 can be marked COMPLETE only when all of the following are true:

- the selected candidate/checksum and environment identity were captured in one verified immutable session before cutover;
- all 15 external evidence-pack gates are PASS from the real intended environment;
- every gate has a matching evidence file and SHA-256;
- the explicit finalizer succeeds with the approved operator identity;
- the closure validator returns PASS and the closure summary is retained;
- the selected candidate metadata matches Issue #116;
- the operations were executed on the intended trusted-certificate Windows/IIS SingleNode host;
- no secret-bearing evidence was retained.

Finalizing the evidence pack **does not close GitHub issues automatically**. #116 must remain OPEN until the closure summary and real evidence are reviewed and accepted. Only then may #116 be closed. Umbrella #111 may close only after #116 is accepted.

## Stop conditions

Do not cut over, or rollback immediately, if the session manifest lock is invalid, candidate/checksum bytes do not match, readiness is not Green, the application starts in an unintended MultiNode mode, IIS/certificate/app-pool prerequisites fail, the key ring/state paths are unavailable, protected credentials cannot resolve after recycle, monitored SQL requires unexpected write/high privilege, backup/rollback evidence is missing, or the finalizer/closure validator does not return PASS.
