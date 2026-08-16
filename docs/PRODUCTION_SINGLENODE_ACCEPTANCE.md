# P0.5 — First Production SingleNode Acceptance

Issue: #116  
Dependency: P0.4 / #115 COMPLETE  
Retention prerequisite: #162 — manual RC.61 promotion + separate read-only durable verification  
Scope: first production activation only; `Deployment:Mode=SingleNode`.

This document is the operator evidence record for the first IIS/HTTPS production cutover. Repository CI can prove package, configuration and recovery contracts, but it cannot truthfully close #116 without exercising the actual Windows/IIS environment. **#116 must remain OPEN until the external evidence pack validates 15/15 required production gates and the real operator explicitly finalizes that evidence.**

## Release candidate contract

1. Use the exact selected candidate tracked on #116. Current selection is RC.61 unless #116 explicitly selects another equivalently verified candidate.
2. Preserve the exact candidate bytes durably under #162 before cutover; do not rebuild, repackage or substitute another RC during retention.
3. Verify the versioned Windows x64 ZIP and matching `.sha256` before extracting or deploying.
4. Keep `deploy/appsettings.Production.example.json` as a schema/example only; do not place real secrets in source-controlled JSON.
5. Set `Deployment:Mode=SingleNode`. MultiNode is explicitly out of scope for P0.5.
6. Persist the application state directory and ASP.NET Data Protection key ring outside the replaceable release folder.
7. Use the existing monitored-SQL least-privilege role; the application must not require write access to monitored SQL targets.
8. Bind IIS to a trusted HTTPS certificate. HTTP or untrusted-loopback CI evidence is not production acceptance.

## Selected candidate

The live selected candidate, SHA-256, source head, tested merge ref and source Actions artifact are tracked on Issue #116. The current short operator handoff is `deploy/RC61_DURABLE_PROMOTION.md`.

For RC.61 the selected identity is:

- version `0.1.0-rc.61`;
- source run `31667721306`;
- artifact ID `9168574442`;
- source head `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`;
- tested merge `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- outer Actions artifact digest `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`;
- product SHA-256 `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- durable tag `v0.1.0-rc.61`.

## Mandatory pre-cutover durable-retention prerequisite — #162

Complete this **before** production backup/session/deployment steps. Durable retention is a repository/recoverability prerequisite; it is not one of the 15 real environment PASS gates.

### 1. Promote the exact existing candidate

Manually dispatch `.github/workflows/promote-existing-candidate.yml` **from `main`** using exactly the RC.61 inputs in `deploy/RC61_DURABLE_PROMOTION.md`:

- `candidate_version=0.1.0-rc.61`
- `source_run_id=31667721306`
- `source_artifact_id=9168574442`
- `expected_outer_artifact_digest=sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`
- `expected_product_sha256=d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`
- `source_commit=e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`
- `tested_merge_commit=158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- `release_tag=v0.1.0-rc.61`
- `acknowledge_promotion=true`

The promotion operation must preserve the selected bytes. It does not build, publish, compress or repackage RC.61 and must fail closed on source-run/artifact/digest/hash/manifest/tag/release mismatch.

### 2. Run separate read-only durable verification

After promotion is Green, separately dispatch `.github/workflows/verify-durable-release.yml` **from `main`** with:

- `release_version=0.1.0-rc.61`
- `release_tag=v0.1.0-rc.61`
- `expected_commit=158148d8bfd05f724014541bc7a0b1eab5dae1b5`
- `expected_product_sha256=d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`

The verifier is read-only (`contents: read`) and must independently confirm immutable tag provenance, exact-two release assets, asset metadata/digests/downloaded bytes and canonical checksum.

Do not proceed to cutover until #162 is ready to close: both runs are Green, tag `v0.1.0-rc.61` resolves to the approved tested merge, the release contains exactly `Monitor-0.1.0-rc.61-win-x64.zip` plus `Monitor-0.1.0-rc.61-win-x64.zip.sha256`, and the durable ZIP hash matches the selected product hash.

**Neither successful promotion nor successful durable verification marks any external production gate PASS.** They do not deploy IIS, configure a trusted certificate/app-pool identity, exercise the production SQL target, prove recycle durability or validate rollback.

## Pre-cutover environment evidence

After the #162 retention prerequisite is satisfied, record these values before changing IIS:

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

Use the packaged `_operations` scripts after the operational backup and rollback point are known. The selected product hash is an independent input; do not derive it from the companion checksum being supplied to the initializer.

```powershell
$session = .\_operations\scripts\New-ProductionAcceptanceSession.ps1 `
  -SessionRoot 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-61' `
  -ArtifactPath '.\Monitor-0.1.0-rc.61-win-x64.zip' `
  -ChecksumPath '.\Monitor-0.1.0-rc.61-win-x64.zip.sha256' `
  -CandidateVersion '0.1.0-rc.61' `
  -ExpectedProductSha256 'd0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5' `
  -SourceCommit 'e28158da67b36dfc5dbf8f4c38b5c43d99c7c728' `
  -TestedMergeCommit '158148d8bfd05f724014541bc7a0b1eab5dae1b5' `
  -HostName 'monitor.example.internal' `
  -SiteName 'Monitor' `
  -AppPoolName 'Monitor' `
  -AppPoolIdentity 'IIS AppPool\Monitor' `
  -CertificateThumbprint '<approved-machine-certificate-thumbprint>' `
  -OperationalBackupId '<validated-backup-id>' `
  -PreviousPhysicalPath 'C:\Program Files\Monitor\releases\previous' `
  -StateRoot 'C:\ProgramData\Monitor\App_Data'

$sessionManifestSha256 = $session.ManifestSha256
```

The initializer requires a fresh absolute Windows session root and refuses reuse. Before session creation it verifies exact candidate/checksum naming, requires the companion checksum and the ZIP bytes to match the independently selected `-ExpectedProductSha256`, validates readable ZIP structure and rejects secret/provider/connection-string/SQL-text-shaped metadata. A substituted ZIP and `.sha256` pair that agree with each other but do not match the selected product hash fails closed before the session workspace is created.

A successful session contains the copied candidate/checksum, `evidence/p0-5-evidence-pack.json`, bounded `evidence/proof/`, `session-manifest.json`, `session-manifest.sha256` and `OPERATOR-NEXT-STEPS.txt`. The candidate is rehashed after copy and the manifest/evidence pack remain bound to the selected product SHA-256.

`New-ProductionAcceptanceEvidencePack.ps1` remains the low-level canonical evidence-pack schema/generator used by the session initializer. The generator creates the exact fail-closed 15-gate structure and never marks a real environment gate PASS.

Session creation proves **0/15** external gates. Every evidence-pack gate remains false; final acceptance metadata is absent. The initializer does not deploy/recycle IIS, execute SQL, record a gate PASS, call GitHub or close #116/#111.

Verify `session-manifest.sha256` before the first production operation. Preserve `$sessionManifestSha256` in the approved operator record outside the mutable session files for the duration of the cutover. It is non-secret chain-of-custody evidence and is required by every gate recorder/finalizer call. Do not recompute or silently replace the expected value from a later manifest.

Before recording any gate, the packaged `Test-ProductionAcceptanceSessionBinding.ps1` verifies all of the following against that externally preserved manifest SHA-256:

- `session-manifest.json` hashes to the preserved value and `session-manifest.sha256` contains the same canonical lock;
- manifest status remains `PreparedFailClosed`, `SingleNode`, 15 gates and 0 accepted gates in the immutable anchor;
- candidate/evidence relative paths remain canonical and session-confined;
- the copied candidate ZIP and companion checksum still match `selectedProductSha256`;
- the evidence pack candidate version/source/tested-merge/artifact/hash and environment identity still match the locked session.

A pack-only, manifest-only or candidate-byte substitution therefore fails closed before a gate PASS can be recorded or final acceptance committed.

## Deployment procedure

1. Confirm #162 durable promotion and separate read-only durable verification are Green for the exact selected RC.61 identity.
2. Obtain the exact selected candidate bytes and verify their SHA-256 against the independently verified durable release.
3. Create and validate the operational backup and preserve the previous release as the rollback point.
4. Create the immutable acceptance session above with the independently selected product SHA-256; preserve `$sessionManifestSha256`, verify `session-manifest.sha256` and confirm `PreparedFailClosed` / 0/15.
5. Run `_operations/scripts/Test-IisProductionPrerequisites.ps1` and retain its non-secret output beneath the session `evidence/proof` root.
6. Run `_operations/scripts/Deploy-ProductionSingleNode.ps1` without `-Apply`; review and retain the PLAN ONLY output.
7. Apply the reviewed plan with explicit `-Apply`.
8. Run the HTTPS acceptance harness against the session-bound candidate:

```powershell
.\_operations\scripts\Accept-ProductionSingleNode.ps1 `
  -BaseUri https://monitor.example.internal/ `
  -ArtifactPath 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N\candidate\Monitor-0.1.0-rc.N-win-x64.zip' `
  -ChecksumPath 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N\candidate\Monitor-0.1.0-rc.N-win-x64.zip.sha256' `
  -EvidencePath 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N\evidence\proof\health-acceptance.json'
```

9. Authenticate through the actual trusted HTTPS endpoint.
10. Register/Test/Refresh the approved least-privilege SQL target and retain bounded non-secret evidence.
11. Recycle the IIS application pool and repeat health/auth/read checks.
12. Prove registration, protected credential, audit/history/incident and cached/read state survived the recycle.
13. Validate the operational backup, execute the approved rollback rehearsal and repeat health/auth/read checks after rollback.

`Accept-ProductionSingleNode.ps1` validates artifact checksum and the three control-plane health endpoints. It intentionally does not claim recycle, credential, SQL privilege, backup or rollback success.

## External acceptance evidence pack

The evidence-pack generator does not perform IIS deployment, recycle IIS, execute SQL, or grant production acceptance. The pack is machine-verifiable but never self-generates PASS evidence. Start a real cutover with the immutable session so candidate bytes and evidence cannot be mixed across workspaces.

Immediately after session creation verify:

- candidate copy hashes to the selected product SHA-256;
- manifest `artifactSha256` and `selectedProductSha256` both equal the independently selected product SHA-256;
- `session-manifest.sha256` matches `session-manifest.json` and the externally preserved `$sessionManifestSha256`;
- manifest state is `PreparedFailClosed`;
- evidence pack contains exactly 15 required gates and all are false;
- no closure summary or final operator acceptance metadata exists.

## Record each external gate explicitly

For each real environment gate, save one bounded text/JSON evidence file beneath the session `evidence/proof` root. Do not use screenshots/binary blobs as authoritative machine-verifiable evidence. After the operation is actually performed and reviewed, record **one gate at a time** with explicit acknowledgement:

```powershell
.\_operations\scripts\Set-ProductionAcceptanceGate.ps1 `
  -EvidencePath 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N\evidence\p0-5-evidence-pack.json' `
  -ExpectedSessionManifestSha256 $sessionManifestSha256 `
  -GateName 'iisPreflightPassed' `
  -EvidenceFile 'proof\iis-preflight.txt' `
  -AcknowledgePass
```

The recorder accepts only the exact gate names, requires explicit acknowledgement and the externally preserved session-manifest SHA-256, validates the locked session/candidate/pack binding before mutation, confines evidence to the pack root, scans for secret/provider/connection-string/SQL-text material, computes evidence hash/time itself, modifies only one gate atomically and never writes final acceptance metadata.

### Exact 15 required real-environment gates

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

Durable publication/verification is intentionally **not** an additional evidence-pack gate and cannot mark any of these 15 PASS.

## Explicitly finalize the real 15/15 operator evidence

Do not hand-edit `acceptedBy` or `acceptedAtUtc`. After all 15 real gates are performed and recorded, run:

```powershell
.\_operations\scripts\Complete-ProductionAcceptance.ps1 `
  -EvidencePath 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N\evidence\p0-5-evidence-pack.json' `
  -ExpectedSessionManifestSha256 $sessionManifestSha256 `
  -AcceptedBy 'DOMAIN\approved.operator' `
  -ClosureSummaryFile 'p0-5-closure-summary.json' `
  -AcknowledgeFinalAcceptance
```

The finalizer requires explicit final acknowledgement and the externally preserved session-manifest SHA-256, never changes a gate from FAIL to PASS, validates the locked session before prospective work, validates a prospective finalized copy, detects concurrent pack mutation, rechecks locked-session binding immediately before the authoritative commit, atomically commits only final operator metadata, validates the authoritative pack with locked-session binding again, writes the closure summary only after success and rolls final metadata back if authoritative validation fails. It does not deploy/recycle IIS, execute SQL, call GitHub or close #116/#111.

The explicit acknowledgement means the approved operator reviewed real environment evidence and asserts that all recorded gates correspond to operations actually executed on the intended production host.

## Independent closure validation

The finalizer runs the validator twice. A reviewer can independently re-run the authoritative validation with the same preserved session anchor:

```powershell
.\_operations\scripts\Test-ProductionAcceptanceEvidence.ps1 `
  -EvidencePath 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N\evidence\p0-5-evidence-pack.json' `
  -ExpectedSessionManifestSha256 $sessionManifestSha256 `
  -ClosureSummaryPath 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-N\evidence\p0-5-review-summary.json'
```

The validator fails on missing/false/unknown gates, malformed candidate/environment metadata, non-SingleNode mode, missing/out-of-root evidence, hash mismatch or secret/provider/connection-string/SQL-text content. When `-ExpectedSessionManifestSha256` is supplied it first requires the same locked-session binding and includes `sessionManifestSha256` plus `selectedProductSha256` in the PASS closure summary. Standalone validation without this parameter remains available only as the low-level evidence-pack/schema validator; the production finalizer and production review path are session-bound.

Repository CI proves validator mechanics only; real operations must still be performed on the intended Windows/IIS host.

## Mandatory restart/recycle acceptance

After the first Green trusted-HTTPS smoke:

1. Confirm at least one real SQL registration is visible from persisted application state.
2. Record its opaque registration id only.
3. Recycle the IIS application pool.
4. Confirm `/health/live`, `/health/ready`, and `/health` return expected bounded statuses again.
5. Confirm the same registration id remains present.
6. Execute approved Test Connection / bounded refresh and confirm protected credential resolution.
7. Confirm Server Details returns cached/collected evidence without fallback demo data.
8. Prove audit/history/incident operational state remains available.

These results map to `iisRecyclePassed`, `registrationDurabilityVerified`, `protectedCredentialDurabilityVerified`, `operationalStateDurabilityVerified` and `finalReadEvidencePassed`.

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

- #162 exact RC.61 manual promotion and separate read-only durable verification succeeded and the durable tag/exact-two assets/product hash were independently verified;
- the selected candidate/checksum, independently selected product SHA-256 and environment identity were captured in one verified immutable session before cutover;
- the session manifest SHA-256 was preserved outside the mutable session and all production gate recording/finalization/review remained bound to that exact anchor;
- all 15 external evidence-pack gates are PASS from the real intended environment;
- every gate has a matching evidence file and SHA-256;
- the explicit finalizer succeeds with the approved operator identity;
- the session-bound closure validator returns PASS and the closure summary is retained;
- selected candidate metadata matches Issue #116;
- operations were executed on the intended trusted-certificate Windows/IIS SingleNode host;
- no secret-bearing evidence was retained.

Finalizing the evidence pack does not close GitHub issues automatically. #116 remains OPEN until the closure summary and real evidence are reviewed and accepted. Only then may #116 close. Umbrella #111 may close only after #116 is accepted.

## Stop conditions

Do not cut over, or rollback immediately, if durable RC.61 retention/independent verification is incomplete, the externally preserved session manifest hash or `session-manifest.sha256` no longer matches `session-manifest.json`, candidate/checksum bytes do not match the independently selected product SHA-256, the evidence pack candidate/environment identity drifts from the locked session, readiness is not Green, the application starts in unintended MultiNode mode, IIS/certificate/app-pool prerequisites fail, key-ring/state paths are unavailable, protected credentials cannot resolve after recycle, monitored SQL requires unexpected write/high privilege, backup/rollback evidence is missing, or finalizer/closure validator does not return PASS.
