# Selected Release Candidate

This document is the current immutable candidate-selection authority for the remaining production gates. Live GitHub evidence always overrides stale historical text.

## Selected candidate — 2026-09-12

RC.61 is **historical only**. Its GitHub Actions artifact `9168574442` expired at `2026-09-12T04:41:36Z`, so it can no longer be the executable source for durable publication. Do not recreate RC.61 bytes, substitute another ZIP under its tag, or claim its owner gate passed.

The replacement candidate was selected from the successful Windows production-candidate run that validated the merged DBA/installer/mobile/upgrade scope:

```text
Version                    0.1.0-rc.854
Tag                        v0.1.0-rc.854
ZIP                        Monitor-0.1.0-rc.854-win-x64.zip
Checksum                   Monitor-0.1.0-rc.854-win-x64.zip.sha256
Product SHA-256            b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7
Source workflow run        34710820438
Actions artifact ID        10303396821
Artifact name              Monitor-0.1.0-rc.854-win-x64
Outer artifact digest      sha256:e1f0b7facc756758a13653c3ad2bfa5a4af9107b02e14f4682aaaabb286f01e3
Artifact expires           2026-10-12T18:22:15Z
Source PR                  #479
Source head                ef7209cbf099da65330887508ab4380a8b4196d2
Tested PR merge            e1d0daedf8b2209934a1bcd01bff5d46229df20a
Integrated main merge      0cc2087aa9da887046986d413ab46df2bcbab735
```

Independent repository-side inspection verified the downloaded Actions artifact, nested product ZIP, checksum file and embedded `_operations/release-manifest.json`. The computed product SHA-256 matched the checksum above. The live artifact API also matched the locked outer digest and exact source run/head/repository identity. The embedded manifest identifies version `0.1.0-rc.854`, runtime `win-x64`, deployment mode `SingleNode`, source head `ef7209cb...`, and tested merge `e1d0daed...`.

## Validation already passed

For the exact selected source PR head, the following gates completed successfully before merge:

- normal CI run `34710820477` / run number `3811`;
- Real SQL Server 2022 acceptance run `34710820497` / run number `576`;
- Windows production-candidate run `34710820438` / run number `854`;
- protected-P0 metadata run `34710820492`;
- protected-P0 commits run `34710820496`;
- zero unresolved review threads immediately before merge.

The Windows candidate workflow itself passed Release build, full test suite, secret-free package validation, first production smoke, restart smoke, ZIP creation, SHA-256 generation and artifact upload.

## Promotion contract

Use `scripts/Invoke-SelectedDurablePromotion.ps1`.

Preview is mandatory first:

```powershell
pwsh ./scripts/Invoke-SelectedDurablePromotion.ps1
```

It must print `READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT`, the exact tuple above, `ExternalGatesPassed = 0`, and `ProductionMutationPerformed = False`. Preview must not create a tag/release or touch IIS, SQL Server, production configuration or production data.

After reviewing the exact tuple, the repository owner may explicitly dispatch promotion through:

```powershell
pwsh ./scripts/Invoke-SelectedDurablePromotion.ps1 -AcknowledgePromotion
```

The helper fails closed when the artifact is expired, the source run is not successful, repository/source-run/artifact/digest identity differs, any nested product hash/checksum/manifest field differs, the immutable tag/release already exists, dispatch cannot be uniquely bound to one workflow run, or promotion fails. Never auto-redispatch an ambiguous or failed promotion.

Promotion uses `.github/workflows/promote-existing-candidate.yml` with its live input contract: `candidate_version`, `source_run_id`, `source_artifact_id`, `expected_outer_artifact_digest`, `expected_product_sha256`, `source_commit`, `tested_merge_commit`, `release_tag`, and explicit boolean `acknowledge_promotion=true`. It must publish immutable prerelease tag `v0.1.0-rc.854` at tested merge `e1d0daed...` with exactly the selected ZIP/checksum bytes. A separate `verify-durable-release.yml` run must then pass before production acceptance begins.

## Remaining owner/external gates

This candidate selection does **not** manufacture PASS for external gates:

- `#162` — OWNER_ONLY: execute durable publication and independent release verification for this exact selected candidate;
- `#116` — EXTERNAL_ENVIRONMENT: after #162 passes, execute real trusted-IIS/HTTPS/least-privilege/durability/backup/rollback production acceptance against this exact product hash;
- `#111` — umbrella closure only after genuine #116 acceptance;
- `#353` — OWNER_ONLY / REPOSITORY_ADMIN: apply and independently read back main branch protection with the documented provider-bound required checks.

`VERIFIED_FINAL_COMPLETE` remains forbidden until those four gates contain genuine closure evidence.
