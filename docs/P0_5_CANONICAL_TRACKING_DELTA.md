# P0.5 Canonical Tracking Delta

**Updated:** 2026-08-16  
**Parents:** #162 / #116 / #111  
**Selected cutover candidate:** **RC.61**  
**Repository hardening state:** **COMPLETE through PR #219**  
**Durable RC.61 publication:** **PENDING MANUAL PROMOTION + SEPARATE READ-ONLY VERIFICATION**  
**Real Windows/IIS production acceptance:** **PENDING EXTERNAL**

This delta records repository-only P0.5 retention, workflow-supply-chain and durable-release hardening that occurred after the original RC.61 candidate was selected. It does not replace the live external acceptance checklist in #116 and cannot grant production acceptance.

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

Fresh source-artifact verification on 2026-08-16 confirmed artifact `9168574442` still exists with size `4,824,061` bytes, `expired=false`, the expected outer digest, source/head repository ID `1329517438`, and GitHub expiry `2026-09-12T04:41:34Z`.

No promotion has been inferred from readiness evidence. At the latest check, `promote-existing-candidate` had zero workflow runs and tag/release `v0.1.0-rc.61` was absent.

## Retention and hardening ledger — COMPLETE

| Issue / PR | State | Repository result |
|---|---|---|
| #159 / #160 | COMPLETE | Durable pushed-version-tag publication path; verified same-run ZIP + `.sha256`; no rebuild/repackage/clobber path. |
| #162 / #163 | IMPLEMENTATION COMPLETE | Exact existing-candidate promotion capability for RC.61; manual dispatch remains intentionally external. |
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

All later CI-generated candidates mentioned by hardening PRs are implementation evidence only. **None supersedes RC.61** unless #116 explicitly selects another equivalently verified candidate.

## Current #162 execution contract

Repository implementation is complete. The remaining retention operation is intentionally manual and fail-closed.

### Step 1 — promotion

Dispatch `.github/workflows/promote-existing-candidate.yml` from `main` using the exact RC.61 identity in `deploy/RC61_DURABLE_PROMOTION.md` and `docs/P05_EXISTING_CANDIDATE_PROMOTION.md`.

The required identity includes:

- source run `31667721306`;
- artifact ID `9168574442`;
- expected outer artifact digest `sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382`;
- expected product SHA-256 `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`;
- source head `e28158da67b36dfc5dbf8f4c38b5c43d99c7c728`;
- tested merge `158148d8bfd05f724014541bc7a0b1eab5dae1b5`;
- tag `v0.1.0-rc.61`;
- explicit promotion acknowledgement.

### Step 2 — independent verification

After promotion is Green, separately dispatch `.github/workflows/verify-durable-release.yml` from `main`. This workflow is read-only and independently verifies tag provenance, release metadata, exact-two assets, asset IDs/sizes/digests/downloaded bytes and canonical checksum. The promotion workflow's own post-publication checks are not a substitute for this second run.

Keep #162 open until both runs are Green and the tag, exact-two assets and durable product hash are independently verified.

## Canonical reconciliation state

The canonical repository tracking is now intentionally aligned:

1. `docs/STATUS.md` records repository P0.5 hardening through PR #219 and keeps RC.61 publication pending manual #162;
2. `docs/FEATURE_CATALOG.md` records the selected-candidate promotion implementation separately from actual publication;
3. `docs/IMPLEMENTATION_PLAN.md` has been fully reconciled and records durable-release hardening through PR #219; the obsolete connector-size limitation no longer applies;
4. `deploy/RC61_DURABLE_PROMOTION.md` is synchronized with the current promotion and independent-verification workflow inputs through PR #245;
5. this delta records the same current boundary and no longer carries transient `IN VERIFICATION` states from already merged hardening work.

## External production boundary

Issues #116 and #111 remain OPEN. No repository CI, release-retention hardening, durable publication, independent release verification, UI completion, candidate packaging or synthetic 15/15 evidence can satisfy the actual production gate.

The real #116 checklist still requires the intended trusted-certificate Windows/IIS SingleNode host, actual app-pool identity, validated pre-cutover backup, immutable acceptance session, trusted HTTPS authentication, least-privilege monitored SQL Test/Refresh, IIS recycle durability, operational-state durability, rollback/recovery rehearsal, 15 SHA-bound real gate records, explicit final operator acknowledgement and independent human review.

This file does not dispatch promotion, create or mutate a release/tag, select a new candidate, deploy IIS, execute SQL, or close #162/#116/#111.
