# RC.61 Acceptance Control Toolkit sidecar

Issue #260 tracks this compatibility boundary. Selected product candidate RC.61 remains byte-for-byte unchanged.

## Why this is a sidecar

RC.61 was produced before the later selected-product-hash and locked-session acceptance hardening. Its immutable product ZIP must not be rebuilt or repackaged merely to gain newer operator controls. The cutover therefore separates two identities:

1. **Product/deployment candidate:** exact RC.61 ZIP + companion checksum tracked on #116/#162.
2. **Acceptance Control Toolkit:** exactly six PowerShell scripts from one reviewed repository commit whose exact final head is recorded on #260/#258 after CI.

The sidecar may initialize/lock the acceptance session, record the 15 real gates, finalize the evidence pack and independently validate closure. It does **not** replace RC.61 application/deployment bytes and cannot create a production PASS by itself.

## Exact six sidecar files

Copy these files, unmodified, from the exact reviewed tooling commit into one dedicated directory:

- `scripts/New-ProductionAcceptanceSession.ps1`
- `scripts/New-ProductionAcceptanceEvidencePack.ps1`
- `scripts/Test-ProductionAcceptanceSessionBinding.ps1`
- `scripts/Set-ProductionAcceptanceGate.ps1`
- `scripts/Complete-ProductionAcceptance.ps1`
- `scripts/Test-ProductionAcceptanceEvidence.ps1`

Do not add a seventh acceptance-control script to the session identity and do not substitute a similarly named file.

## Source checkout rule

The authoritative tooling commit is the exact final PR #259 head recorded on #260/#258 after all required Actions are Green. **Do not use `main`, `latest`, a moving branch name, or an unrecorded later commit at cutover time.**

Obtain one clean checkout/source archive for that exact commit on an approved admin workstation. If Git is used, verify the checked-out `HEAD` equals the recorded 40-hex tooling commit and that tracked files are clean before copying the six files. Stage the resulting directory as a read-only operational input where practical.

Example operator variables:

```powershell
$operatorToolingCommit = '<exact-final-PR-259-head-recorded-on-260-and-116>'
$acceptanceTools = "C:\ProgramData\Monitor\AcceptanceTooling\$operatorToolingCommit"
```

Before session creation, confirm the six exact filenames above exist in `$acceptanceTools` and no file was intentionally edited after the reviewed checkout.

## Session binding

Run the initializer **from the sidecar directory** and pass the exact tooling commit as `-OperatorToolingCommit`.

The initializer writes into the SHA-locked `session-manifest.json`:

- `operatorToolingCommit`;
- `operatorToolingFiles`, containing the SHA-256 of each of the six sidecar files;
- selected RC.61 product hash and candidate identity;
- immutable production environment identity and canonical session paths.

Preserve the returned `ManifestSha256` outside mutable session files.

On every later gate/finalizer/reviewer call, `Test-ProductionAcceptanceSessionBinding.ps1` verifies both the externally preserved manifest hash and the current SHA-256 of all six scripts in its own sidecar directory. A modified, missing or substituted sidecar file fails closed before production evidence can be advanced.

## Which tooling comes from RC.61 vs the sidecar

Use the **sidecar** for acceptance-control state:

- session initialization;
- evidence-pack generation;
- session binding verification;
- one-gate-at-a-time PASS recording;
- final operator acceptance metadata;
- independent closure validation.

Continue to treat RC.61 as the immutable product/deployment candidate. Its packaged deployment/preflight/HTTPS tooling may still be used where the production runbook explicitly calls the candidate-bundled `_operations` path. The sidecar never rewrites RC.61 and never makes its historical `_operations` payload appear newer than it is.

## Stop conditions

Do not create or advance an acceptance session when:

- the exact tooling commit has not been recorded on #260/#258/#116;
- the source checkout is not known to be the exact reviewed commit;
- any one of the six sidecar files is missing or edited;
- the session manifest does not record the expected tooling commit and six file hashes;
- `Test-ProductionAcceptanceSessionBinding.ps1` fails;
- RC.61 product/checksum/hash identity differs from #116/#162.

This sidecar is repository-side safety tooling only. It does not dispatch #162, deploy IIS, execute monitored SQL writes, satisfy a real external gate, or close #116/#111.
