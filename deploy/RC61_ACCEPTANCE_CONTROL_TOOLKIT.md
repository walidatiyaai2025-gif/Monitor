# RC.61 Acceptance Control Toolkit sidecar

Issues #260 and #261 track this compatibility/provenance boundary. **RC.61 product/deployment bytes remain unchanged** and the selected product candidate remains byte-for-byte immutable.

## Why this is a sidecar

RC.61 was produced before the later selected-product-hash, locked-session and toolkit-provenance hardening. Its immutable product ZIP must not be rebuilt or repackaged merely to gain newer operator controls. The cutover therefore separates two identities:

1. **Product/deployment candidate:** exact RC.61 ZIP + companion checksum tracked on #116/#162.
2. **Acceptance Control Toolkit:** exactly six PowerShell scripts exported from one exact reviewed repository commit, with a deterministic `toolkit-manifest.json` and `toolkit-manifest.sha256`.

The sidecar may initialize/lock the acceptance session, record the 15 real gates, finalize the evidence pack and independently validate closure. It does not replace RC.61 application/deployment bytes and cannot create a production PASS by itself.

## Exact six sidecar files

The exported toolkit contains exactly these six scripts plus its manifest and manifest lock:

- `New-ProductionAcceptanceSession.ps1`
- `New-ProductionAcceptanceEvidencePack.ps1`
- `Test-ProductionAcceptanceSessionBinding.ps1`
- `Set-ProductionAcceptanceGate.ps1`
- `Complete-ProductionAcceptance.ps1`
- `Test-ProductionAcceptanceEvidence.ps1`
- `toolkit-manifest.json`
- `toolkit-manifest.sha256`

Do not add a seventh acceptance-control script to the session identity and do not substitute a similarly named file.

## Source provenance rule

After #261 / PR #262 completes, the authoritative tooling commit is the **exact final PR #262 head that passes all required exact-head Actions and is recorded on #261/#260/#258/#116**. The earlier PR #259 head remains historical evidence for the locked-session implementation, but the provenance-hardened cutover toolkit must use the later exact reviewed #262 commit.

**Do not use `main`, `latest`, a moving branch name, or an unrecorded later commit at cutover time.**

On an approved admin workstation, obtain one clean Git checkout at the independently supplied exact 40-hex tooling commit. Verify the tracked checkout is clean and export the toolkit to a fresh directory outside the checkout:

```powershell
$operatorToolingCommit = '<exact-final-PR-262-head-recorded-on-261-260-258-116>'
$acceptanceTools = "C:\ProgramData\Monitor\AcceptanceTooling\$operatorToolingCommit"

$toolkit = .\scripts\Export-ProductionAcceptanceToolkit.ps1 `
  -ExpectedToolingCommit $operatorToolingCommit `
  -OutputDirectory $acceptanceTools

$operatorToolkitManifestSha256 = $toolkit.ToolkitManifestSha256
```

`Export-ProductionAcceptanceToolkit.ps1` fails closed unless Git `HEAD` equals the independently supplied commit, tracked state is clean, all six required files are tracked/present, and the output directory is fresh and outside the source checkout. It copies only the six approved scripts, writes deterministic file SHA-256 entries to `toolkit-manifest.json`, and writes the canonical manifest lock.

Independently verify the staged directory before session creation:

```powershell
.\scripts\Test-ProductionAcceptanceToolkit.ps1 `
  -ToolkitRoot $acceptanceTools `
  -ExpectedToolingCommit $operatorToolingCommit `
  -ExpectedToolkitManifestSha256 $operatorToolkitManifestSha256
```

The verifier requires exactly the six scripts plus `toolkit-manifest.json` and `toolkit-manifest.sha256`; it rejects missing/extra entries, commit mismatch, manifest/lock drift, malformed metadata and any file SHA-256 mismatch.

Preserve **both** non-secret external anchors outside the mutable acceptance session:

- `OperatorToolingCommit` — exact 40-hex reviewed source commit;
- `OperatorToolkitManifestSha256` — exact 64-hex SHA-256 returned by the exporter and independently verified.

## Session binding

Run the initializer **from the verified exported toolkit directory** and pass both independent anchors:

```powershell
$session = & "$acceptanceTools\New-ProductionAcceptanceSession.ps1" `
  ... `
  -OperatorToolingCommit $operatorToolingCommit `
  -ExpectedOperatorToolkitManifestSha256 $operatorToolkitManifestSha256 `
  ...

$sessionManifestSha256 = $session.ManifestSha256
```

Before creating the session, the initializer re-hashes `toolkit-manifest.json`, verifies `toolkit-manifest.sha256`, validates its exact commit/file-set contract, re-hashes all six scripts, and rejects any mismatch with the independently supplied toolkit-manifest SHA-256.

The initializer writes into the SHA-locked `session-manifest.json`:

- `operatorToolingCommit`;
- `operatorToolkitManifestSha256`;
- `operatorToolingFiles`, containing the SHA-256 of each of the six sidecar files;
- selected RC.61 product hash and candidate identity;
- immutable production environment identity and canonical session paths.

Preserve the returned `ManifestSha256` outside mutable session files.

On every later Gate/Finalizer/Reviewer operation, `Test-ProductionAcceptanceSessionBinding.ps1` verifies all three tooling layers again:

1. current `toolkit-manifest.json` still hashes to the toolkit-manifest SHA-256 locked in the session manifest;
2. `toolkit-manifest.sha256` still contains that canonical hash and the manifest still records the exact tooling commit/six-file set;
3. each current sidecar file still hashes to both its toolkit-manifest entry and the locked session `operatorToolingFiles` entry.

A modified manifest, modified lock, missing/extra/substituted sidecar file, or file-hash drift fails closed before production evidence can advance.

## Which tooling comes from RC.61 vs the sidecar

Use the **verified sidecar** for acceptance-control state:

- session initialization;
- evidence-pack generation;
- session binding verification;
- one-gate-at-a-time PASS recording;
- final operator acceptance metadata;
- independent closure validation.

Continue to treat RC.61 as the immutable product/deployment candidate. Its packaged deployment/preflight/HTTPS tooling may still be used where the production runbook explicitly calls the candidate-bundled `_operations` path. The sidecar never rewrites RC.61 and never makes its historical `_operations` payload appear newer than it is.

Future Windows production candidates also generate and independently verify an exact-commit toolkit manifest before staging the six acceptance-control scripts. That packaging verification does not change the selected RC.61 candidate and is not production acceptance.

## Stop conditions

Do not create or advance an acceptance session when:

- the exact final provenance-hardened tooling commit has not been recorded on #261/#260/#258/#116;
- the source checkout is not the exact reviewed commit or tracked state is dirty;
- toolkit export/independent verification did not succeed;
- the independently preserved toolkit-manifest SHA-256 is missing or differs from the staged manifest/lock;
- any one of the six sidecar files is missing, added, substituted or edited;
- the session manifest does not record the expected tooling commit, toolkit-manifest SHA-256 and six file hashes;
- `Test-ProductionAcceptanceSessionBinding.ps1` fails;
- RC.61 product/checksum/hash identity differs from #116/#162.

This sidecar is repository-side safety tooling only. It does not dispatch #162, deploy IIS, execute monitored SQL writes, satisfy a real external gate, or close #116/#111.
