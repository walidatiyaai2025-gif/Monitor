# P0.5 — First Production SingleNode Acceptance

Issue: #116  
Dependency: P0.4 / #115 COMPLETE  
Scope: first production activation only; `Deployment:Mode=SingleNode`.

This document is the operator evidence record for the first IIS/HTTPS production cutover. Repository CI can prove package, configuration and recovery contracts, but it cannot truthfully close #116 without exercising the actual Windows/IIS environment. **#116 must remain OPEN until the external evidence pack validates 15/15 required production gates.**

## Release candidate contract

1. Use the versioned Windows x64 ZIP and matching `.sha256` produced by the production-candidate workflow.
2. Verify the SHA-256 before extracting or deploying.
3. Keep `deploy/appsettings.Production.example.json` as a schema/example only; do not place real secrets in source-controlled JSON.
4. Set `Deployment:Mode=SingleNode`. MultiNode is explicitly out of scope for P0.5.
5. Persist the application state directory and ASP.NET Data Protection key ring outside the replaceable release folder.
6. Use the existing monitored-SQL least-privilege role; the application must not require write access to monitored SQL targets.
7. Bind IIS to a trusted HTTPS certificate. HTTP or untrusted-loopback CI evidence is not production acceptance.

## Selected candidate

The live selected candidate, SHA-256, source head, tested merge ref and Actions artifact are tracked on Issue #116 so this runbook does not drift every time an equivalent later RC is generated. Preserve those exact values in the external evidence pack before cutover.

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

## Deployment procedure

1. Obtain the exact candidate selected on #116 and verify its SHA-256.
2. Create and validate the operational backup and preserve the previous release as the rollback point.
3. Run `_operations/scripts/Test-IisProductionPrerequisites.ps1` and retain its non-secret output as evidence.
4. Run `_operations/scripts/Deploy-ProductionSingleNode.ps1` without `-Apply`; review and retain the PLAN ONLY output.
5. Apply the reviewed plan with explicit `-Apply`.
6. Run the HTTPS acceptance harness:

```powershell
.\_operations\scripts\Accept-ProductionSingleNode.ps1 `
  -BaseUri https://monitor.example.internal/ `
  -ArtifactPath .\Monitor-<version>-win-x64.zip `
  -ChecksumPath .\Monitor-<version>-win-x64.zip.sha256 `
  -EvidencePath .\evidence\health-acceptance.json
```

7. Authenticate through the actual trusted HTTPS endpoint.
8. Register/Test/Refresh the approved least-privilege SQL target and retain bounded non-secret evidence.
9. Recycle the IIS application pool and repeat health/auth/read checks.
10. Prove registration, protected credential, audit/history/incident and cached/read state survived the recycle.
11. Validate the operational backup, execute the approved rollback rehearsal and repeat health/auth/read checks after rollback.

`Accept-ProductionSingleNode.ps1` validates artifact checksum and the three control-plane health endpoints. It intentionally does **not** claim recycle, credential, SQL privilege, backup or rollback success.

## External acceptance evidence pack

Issue #141 adds a machine-verifiable closure record. The pack does not perform IIS deployment, does not recycle IIS, does not execute SQL and does not flip any gate to PASS. It only records operator-proven external evidence and verifies that the evidence is complete, bounded, hash-matched and secret-safe.

### 1. Create the fail-closed pack

Use the packaged `_operations` scripts and the exact candidate metadata from #116:

```powershell
.\_operations\scripts\New-ProductionAcceptanceEvidencePack.ps1 `
  -CandidateVersion '0.1.0-rc.N' `
  -ArtifactFileName 'Monitor-0.1.0-rc.N-win-x64.zip' `
  -ArtifactSha256 '<64-hex-product-sha256>' `
  -SourceCommit '<40-hex-source-head>' `
  -TestedMergeCommit '<40-hex-tested-merge-ref>' `
  -HostName 'monitor.example.internal' `
  -SiteName 'Monitor' `
  -AppPoolName 'Monitor' `
  -AppPoolIdentity 'IIS AppPool\Monitor' `
  -CertificateThumbprint '<approved-machine-certificate-thumbprint>' `
  -OperationalBackupId '<validated-backup-id>' `
  -PreviousPhysicalPath 'C:\Program Files\Monitor\releases\previous' `
  -StateRoot 'C:\ProgramData\Monitor\App_Data' `
  -OutputPath '.\evidence\p0-5-evidence-pack.json'
```

The generator creates exactly 15 required gates and sets every `passed` field to `false`. It cannot create a completed production acceptance.

### 2. Attach evidence to each gate

For each external gate, save one bounded text/JSON evidence file beneath the pack's evidence root. Do not use screenshots or binary blobs as the authoritative machine-verifiable evidence. Update only that gate's four fields after the operator has actually performed the step:

- `passed`: `true` only after the real environment check succeeds.
- `verifiedAtUtc`: ISO-8601 UTC timestamp.
- `evidenceRef`: relative local path beneath the evidence root; no absolute path or `..` traversal.
- `evidenceSha256`: SHA-256 of that exact evidence file.

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

After all gates are truly complete, set `acceptedBy` to the operator identity and `acceptedAtUtc` to an ISO-8601 timestamp not earlier than the latest gate timestamp.

### 3. Run the fail-closed closure validator

```powershell
.\_operations\scripts\Test-ProductionAcceptanceEvidence.ps1 `
  -EvidencePath '.\evidence\p0-5-evidence-pack.json' `
  -ClosureSummaryPath '.\evidence\p0-5-closure-summary.json'
```

The validator fails if any required gate is missing or false, if an unknown gate/property is injected, if candidate metadata is malformed, if the deployment mode is not exactly SingleNode, if a gate evidence file is missing or escapes the evidence root, if any evidence SHA-256 differs, or if pack/evidence content contains password/connection-string/provider-error/arbitrary SQL text material. It generates a PASS closure summary only after all 15/15 gates validate.

A validator PASS is necessary but still represents evidence supplied from the real environment; repository CI only proves the validator's behavior. The actual production operations must still be performed on the intended Windows/IIS host.

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

- all 15 external evidence-pack gates are PASS;
- every gate has a matching evidence file and SHA-256;
- the closure validator returns PASS and writes the closure summary;
- the selected candidate metadata matches Issue #116;
- the operations were executed on the intended trusted-certificate Windows/IIS SingleNode host;
- no secret-bearing evidence was retained.

Only then may #116 be closed. Umbrella #111 may close only after #116 is accepted.

## Stop conditions

Do not cut over, or rollback immediately, if readiness is not Green, the checksum does not match, the application starts in an unintended MultiNode mode, IIS/certificate/app-pool prerequisites fail, the key ring/state paths are unavailable, protected credentials cannot resolve after recycle, monitored SQL requires unexpected write/high privilege, backup/rollback evidence is missing, or the closure validator does not return PASS.
