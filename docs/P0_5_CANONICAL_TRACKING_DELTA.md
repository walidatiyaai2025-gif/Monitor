# P0.5 Canonical Tracking Delta

**Updated:** 2026-08-17  
**Parents:** #261 / #260 / #258 / #162 / #116 / #111  
**Selected cutover candidate:** **RC.61**  
**Repository hardening state:** **COMPLETE through merged PR #262; #258/#260/#261 COMPLETE**  
**Durable RC.61 publication:** **PENDING MANUAL PROMOTION + SEPARATE READ-ONLY VERIFICATION; #162 OPEN**  
**Real Windows/IIS production acceptance:** **PENDING EXTERNAL**

This delta records repository-only P0.5 retention, workflow-supply-chain, durable-release, locked-session and Acceptance Control Toolkit provenance hardening that occurred after the original RC.61 candidate was selected. It does not replace the live external acceptance checklist in #116 and cannot grant production acceptance.

## Selected RC.61 identity

- version `0.1.0-rc.61`;
- source production-candidate run `31667721306`;
- Actions artifact ID `9168574442`;
- artifact name `Monitor-0.1.0-rc.61-win-x64`;
- source head `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`;
- tested merge `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- product SHA-256 `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- outer Actions artifact digest `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`;
- intended durable tag `v0.1.0-rc.61`.

Fresh source-artifact verification on 2026-08-17 confirmed source run `31667721306` is still `completed/success` on `.github/workflows/production-candidate.yml` with head `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`, and artifact `9168574442` still exists with exact name `Monitor-0.1.0-rc.61-win-x64`, size `4,824,061` bytes, `expired=false`, the expected outer digest, source/head repository ID `1329517438`, and GitHub expiry `2026-09-12T04:41:34Z`.

No promotion is inferred from readiness evidence. Verification on 2026-08-17 still shows `promote-existing-candidate` with zero workflow runs and tag/release `v0.1.0-rc.61` absent. Issue #162 was therefore explicitly reopened as the truthful manual-execution gate. Repository-side acceptance-control hardening does not change that fact and does not supersede RC.61.

## Retention and hardening ledger

| Issue / PR | State | Repository result |
|---|---|---|
| #159 / #160 | COMPLETE | Durable pushed-version-tag publication path; verified same-run ZIP + `.sha256`; no rebuild/repackage/clobber path. |
| #162 / #163 | IMPLEMENTATION COMPLETE / EXECUTION OPEN | Exact existing-candidate promotion capability for RC.61 is implemented. Manual `promote-existing-candidate` dispatch plus separate read-only `verify-durable-release` run remain required before #162 may close. |
| PR #164 | COMPLETE | Initial operator handoff for exact RC.61 promotion. |
| #168 / #171 | COMPLETE | External Actions pinned to approved immutable SHAs; obsolete privileged one-shot workflow removed. |
| #173 / #174 | COMPLETE | Active pinned Actions moved to official native Node 24 releases. |
| #176 / #177 | COMPLETE | .NET SDK fail-closed at `8.0.424`; Real SQL Server image pinned by exact digest. |
| #178 / #179 | COMPLETE | Linux jobs pinned to `ubuntu-24.04`. |
| #180 / #181 | COMPLETE | Repository NuGet restore source/mapping policy locked to approved nuget.org v3 source. |
| #182 / #183 | COMPLETE | Exact direct PackageReference ID/version allowlist. |
| #184 / #185 | COMPLETE | Linux checkout credentials are not persisted. |
| #186 / #187 | COMPLETE | Bounded contention-aware shared operator metadata CAS behavior. |
| #188 / #189 | COMPLETE | Workflow regression guard rejects trusted-context triggers and `write-all`. |
| #190 / #191 | COMPLETE | Windows production candidate pinned to `windows-2025`, non-persisting checkout and repository SDK lock. |
| #192 / #193 | COMPLETE | Exactly two write-capable workflows allowlisted with narrow trigger/permission boundaries. |
| #194 / #195 | COMPLETE | Release/tag mutations serialized by one non-cancelling tag-derived concurrency namespace. |
| #196 / #197 | COMPLETE | Promotion writes fail closed unless manually dispatched from `refs/heads/main`. |
| #198 / #199 | COMPLETE | Job-scoped `GH_TOKEN` removed; token exposure limited to the exact shell steps invoking GitHub CLI. PR #199 merged as `b615acd313ae3dcc733dda54f41771adead78d96` from exact head `30829fcde3bd6ec4be60483be1e10f8cf0612c37`. |
| #200 / #201 | COMPLETE | Tagged releases must contain exactly the approved ZIP and companion `.sha256`; normal CI `31893312477` and Windows `31893312462` Green on exact head `7be4b679a3a9751a8225edf059fcf458da4299dd`. |
| #202 / #203 | COMPLETE | Exact source-run/artifact binding, canonical checksum and durable-release metadata validation hardened. |
| #204 / #205 | COMPLETE | Promotion requires exact outer artifact digest and hardened Windows-safe ZIP/provenance validation. |
| #206 / #207 | COMPLETE | Durable release REST asset metadata, IDs, sizes, digests and bytes are bound fail-closed. |
| #208 / #209 | COMPLETE | Shared durable-release verification made exact-ID and TOCTOU-safe; CI `31915563581`, Windows `31915563637` Green on exact head `50989cf58e07558759187d649d7c02b22db0a651`. |
| #210 / #211 | COMPLETE | Verifier workspace is private, trusted-root confined and no-clobber/atomic-output hardened. |
| #212 / #213 | COMPLETE | Verified output publication made directory-atomic; normal CI `31932943983` Green on exact head `9a31a7c74e1ed5e46fdb6c2fb5c4480f9971d139`. |
| #214 / #215 | COMPLETE | Durable release tag provenance is snapshotted and bound to the approved tested merge; CI `31933321364`, Windows `31933321429` Green. |
| #216 / #217 | COMPLETE | Separate manual-only `verify-durable-release.yml` added with `contents: read` only; exact tag/commit/product verification is independent closure evidence for #162; CI `31933642305` Green. |
| #218 / #219 | COMPLETE | Shared durable-release toolchain capability preflight fails fast on jq/realpath/stat/mktemp/find/sha256sum/mv semantic drift; CI `31935989980` and Windows `31935989954` Green on exact head `ca1e40acfac635650df32cd0bc60ed63df224380`, 919/919 tests. |
| #243 / #245 | COMPLETE | Short `deploy/RC61_DURABLE_PROMOTION.md` reconciled with the hardened promotion/verification input contracts, including outer artifact digest and separate read-only verifier; PR #245 merged as `75661cfc730f60667d1786a9bcd6ca9427ef2faa` after CI #1656 and Windows #146 Green. |
| #256 / #257 | COMPLETE | Production acceptance session requires an independently selected product SHA-256, rejects a mutually consistent substituted ZIP/checksum pair, re-hashes the copied candidate and binds both manifest/evidence to the selected product identity without changing RC.61. |
| #258 + #260 / #259 | COMPLETE | Gate recording, finalization and production review are bound to the externally preserved session-manifest SHA-256 and exact six-file Acceptance Control Toolkit sidecar while RC.61 bytes remain unchanged. Exact source head `8d79361cccf98acfc0a1753d16de943458887389` passed CI #1751 / `31991194175`, Real SQL #112 / `31991194515`, and Windows production-candidate #170 / `31991194198`; PR #259 squash-merged as `c22c4e5e4f59576cbb41b8fc46886474f8749ebb`. #260 is CLOSED / COMPLETED. |
| #261 / #262 | COMPLETE | Provenance-hardened toolkit export/verification is merged. Exact source head `b422eaaee53d931a62a43b3c36a53b68cd4f3e27` passed CI #1786 / `31992503009` with 984/984 tests and Windows production-candidate #186 / `31992502977` end-to-end; Real SQL was not path-selected because SQL/runtime data paths were unchanged. PR #262 merged as `a448eb715af9b3a2fcfe89ce92807b71fc7e1127`; #261 is CLOSED / COMPLETED. |

All later CI-generated candidates mentioned by hardening PRs are implementation evidence only. **None supersedes RC.61** unless #116 explicitly selects another equivalently verified candidate.

## Locked-session Acceptance Control Toolkit boundary

PR #259 established the safe compatibility boundary for selected RC.61:

- RC.61 product/deployment bytes remain byte-for-byte unchanged;
- later Session/Gate/Finalizer/Reviewer controls are a sidecar, not a rebuild or repackaging of RC.61;
- the immutable session locks `OperatorToolingCommit` and the SHA-256 of exactly six acceptance-control scripts;
- every later evidence mutation/review re-hashes those six files and verifies the externally preserved `session-manifest.json` SHA-256;
- RC.61 candidate-bundled deployment/preflight/HTTPS tooling remains distinct from the sidecar acceptance-control state machinery.

The exact PR #259 source head `8d79361cccf98acfc0a1753d16de943458887389` is retained as historical proof of the locked-session implementation that passed all required repository gates before merge.

## #261 / PR #262 provenance hardening — COMPLETE

PR #262 closed the remaining manual provenance gap around how the six-file sidecar is staged. The final contract is fail-closed:

1. `Export-ProductionAcceptanceToolkit.ps1` requires an independently supplied exact 40-hex tooling commit, verifies Git `HEAD` equals it, requires tracked state to be clean, requires all six approved scripts to be tracked/present, rejects an output path inside the source checkout, and exports only to a fresh directory.
2. The exporter writes deterministic `toolkit-manifest.json` containing schema/name, exact tooling commit, exact six filenames and each SHA-256, plus canonical `toolkit-manifest.sha256`. No candidate bytes, source archive, credentials or secrets are included.
3. `Test-ProductionAcceptanceToolkit.ps1` independently requires both the expected tooling commit and expected toolkit-manifest SHA-256, checks the manifest lock, exact eight-entry root set (six scripts + manifest + lock), exact file order/names/hashes and rejects missing/extra/modified/commit-drift cases.
4. `New-ProductionAcceptanceSession.ps1` requires `ExpectedOperatorToolkitManifestSha256`, verifies the staged toolkit manifest/lock + six current scripts before session creation, and records `operatorToolkitManifestSha256` beside `operatorToolingCommit` and `operatorToolingFiles` in the immutable session manifest.
5. `Test-ProductionAcceptanceSessionBinding.ps1` re-verifies the current toolkit manifest hash/lock, exact commit/file-set entries and all six current script hashes against the locked session before any Gate/Finalizer/Reviewer operation can proceed.
6. Windows runtime covers clean export/verify plus wrong commit, dirty tracked checkout, manifest tamper, extra file, modified file and missing file negatives. The future candidate workflow also exports and independently verifies the toolkit from exact Git HEAD before staging the six acceptance-control scripts and its manifest/lock.
7. The provenance-hardened cutover toolkit identity is the exact tested PR #262 source head `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`, recorded on #261. `main`, `latest`, a moving branch ref or an unrecorded later commit is not accepted as the tooling identity.

Exact-head evidence is final: CI #1786 / run `31992503009` Green with 984/984 tests and applicable safety runtimes; Windows production-candidate #186 / run `31992502977` Green end-to-end through provenance runtime, session/recorder/finalizer, publish, HTTPS/auth restart smoke, verified toolkit packaging, ZIP/SHA-256 and artifact upload. PR #262 merged as `a448eb715af9b3a2fcfe89ce92807b71fc7e1127` and #261 closed completed.

## Current #162 execution contract

Repository implementation is complete. The remaining retention operation is intentionally manual and fail-closed, and #162 is OPEN until both required runs and immutable release evidence exist.

### Step 1 — promotion

Dispatch `.github/workflows/promote-existing-candidate.yml` from `main` using the exact RC.61 identity in `deploy/RC61_DURABLE_PROMOTION.md` and `docs/P05_EXISTING_CANDIDATE_PROMOTION.md`.

The required identity includes:

- `candidate_version`: `0.1.0-rc.61`;
- `source_run_id`: `31667721306`;
- `source_artifact_id`: `9168574442`;
- `expected_outer_artifact_digest`: `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`;
- `expected_product_sha256`: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- `source_commit`: `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`;
- `tested_merge_commit`: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- `release_tag`: `v0.1.0-rc.61`;
- `acknowledge_promotion`: `true`.

The workflow itself requires `refs/heads/main`, validates source run/artifact/repository identity, exact artifact digest, selected product SHA, embedded source/tested-merge provenance and performs no rebuild or repackaging.

### Step 2 — independent verification

After promotion is Green, separately dispatch `.github/workflows/verify-durable-release.yml` from `main` with:

- `release_version`: `0.1.0-rc.61`;
- `release_tag`: `v0.1.0-rc.61`;
- `expected_commit`: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- `expected_product_sha256`: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`.

This workflow is read-only (`contents: read`) and independently verifies tag provenance, release metadata, exact-two assets, asset IDs/sizes/digests/downloaded bytes and canonical checksum. The promotion workflow's own post-publication checks are not a substitute for this second run.

Keep #162 open until both runs are Green and the tag, exact-two assets and durable product hash are independently verified. Do not infer #162 completion from repository hardening alone.

## Canonical reconciliation state

The current repository tracking boundary is now explicit:

1. `docs/STATUS.md`, `docs/FEATURE_CATALOG.md` and `docs/IMPLEMENTATION_PLAN.md` remain the canonical project surfaces; this delta is the authoritative post-RC.61 P0.5 chain-of-custody reconciliation for merged #259/#262 and the still-open manual #162 execution boundary until those large surfaces receive a later bounded refresh;
2. merged PR #259 is COMPLETE with exact-head CI/Real-SQL/Windows evidence recorded above; #258 and #260 are CLOSED / COMPLETED;
3. merged PR #262 is COMPLETE with exact tested source head `b422eaaee53d931a62a43b3c36a53b68cd4f3e27`, exact-head CI/Windows evidence and merge `a448eb715af9b3a2fcfe89ce92807b71fc7e1127`; #261 is CLOSED / COMPLETED;
4. #162 is intentionally OPEN because on 2026-08-17 the promotion workflow still had zero runs and release/tag `v0.1.0-rc.61` was absent;
5. `deploy/RC61_ACCEPTANCE_CONTROL_TOOLKIT.md` and `docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md` carry the clean-commit export, independent toolkit-manifest SHA-256 and session-binding operator contract;
6. `deploy/RC61_DURABLE_PROMOTION.md` remains the selected RC.61 retention handoff and is not replaced by acceptance-control provenance hardening.

No repository tracking update may convert repository CI, toolkit export/verification, candidate packaging or synthetic evidence into a real production gate PASS.

## External production boundary

Issues #116 and #111 remain OPEN. No repository CI, release-retention hardening, durable publication, independent release verification, UI completion, candidate packaging, toolkit provenance verification or synthetic 15/15 evidence can satisfy the actual production gate.

The real #116 checklist still requires the intended trusted-certificate Windows/IIS SingleNode host, actual app-pool identity, validated pre-cutover backup, immutable acceptance session, trusted HTTPS authentication, least-privilege monitored SQL Test/Refresh, IIS recycle durability, operational-state durability, rollback/recovery rehearsal, 15 SHA-bound real gate records, explicit final operator acknowledgement and independent human review.

This file does not dispatch promotion, create or mutate a release/tag, select a new candidate, deploy IIS, execute SQL, mark a real external gate PASS, or close #162/#116/#111.
