# Project Status

This file is the current operational status. The pre-#425 append-only historical status ledger is preserved byte-for-byte at [`docs/archive/STATUS_PRE_425_2026-08-18.md`](archive/STATUS_PRE_425_2026-08-18.md). Detailed per-closure evidence remains in `docs/work/` and the linked PRs/issues.

## Current programming closure — #425 / PR #426

**Scope:** exact SharedState document-key identity across SQL Server collation.  
**Base:** `main@3720c7aaa3e86ac3eb599685f39e98fa0a6ecb64`.  
**Branch:** `agent/425-exact-shared-state-key-identity`.  
**Implementation:** production Read/CAS locks capture the actual persisted `DocumentKey`, compare the persisted/requested NVARCHAR bytes before document execution/mutation, return the actual persisted key from SQL results and re-check ordinal identity before commit. Mixed-case keys remain supported; no lowercase normalization or key migration is introduced.  
**Regression:** unit/source-contract coverage plus SQL Server 2022 acceptance against explicit `Latin1_General_100_CI_AS`; exact mixed-case Read/CAS succeeds, case-different aliases fail closed, and rejected alias CAS leaves the persisted row unchanged.  
**Pre-canonical-doc evidence:** head `a070c123183048dc418ecd19bcd6f98b1028d8f0` passed normal CI `32159604946`, Real SQL `32159604935`, Windows production-candidate `32159604932`, protected-P0 commits `32159604926` and protected-P0 metadata `32159605011`. Final merge still requires the exact docs-inclusive head to be current with `main`, Green on the same five gates and have zero unresolved review threads.  
**Safety:** no key normalization/migration, schema-v2, Monitor runtime DDL, monitored-target SQL/query/permission expansion, secret disclosure, autonomous remediation, RC.61 publication, production IIS/SQL mutation, external P0 acceptance or branch-protection mutation.

## Latest merged SharedState closures

- **#423 / PR #424 — atomic schema guard + document execution: COMPLETE / MERGED.** Exact head `84c509dec867ff5e1b4e913b2b318b81fa927171` passed CI `32158179450`, Real SQL `32158179479`, Windows production-candidate `32158179740`, protected-P0 commit `32158179498` and metadata `32158179675`; squash merge `3720c7aaa3e86ac3eb599685f39e98fa0a6ecb64`. The schema fingerprint and Read/CAS now execute under one SERIALIZABLE transaction/locking boundary.
- **#421 / PR #422 — schema readiness before document execution: COMPLETE / MERGED.** Exact head `9c3cd090e9032892a97d7394c12700a679435834`; squash merge `3b5e60fef2fa41c6e627468850cf3cf8532b0524`.

## Earlier programming-closure baseline

- **PR #369 — security/credential/operator-accountability hardening: COMPLETE / MERGED.** Exact head `e99678c32ae0af38f1d1529a63425325182d9266` passed normal CI `32125557707`, Real SQL `32125557099`, Windows production-candidate `32125557073` and both protected-P0 guards; squash merge `bbd8e5eb11ee8e4a7e34fbe91519e166fe087bc5`. Issues #368/#370/#371/#372/#373/#374 are repository-complete.
- **PR #363 — evidence/auth/refresh/readiness/operator-surface truthfulness: COMPLETE / MERGED** as `c8515f310091bcb62af488d9132c4f330c182bf8`.
- **BATCH-800 / Issue #287: COMPLETE (100/100)** via B800-100 / PR #335, squash merge `a6832d99f629cdbd3a93887199fe608a3ae474ec`, exact head `4379dbc0e1b346cb51bebf8e7467823c58f2361c`, CI `32093252549`, Real SQL `32093252670`, Windows production-candidate `32093252563`.
- BATCH-100 through BATCH-800 repository hardening/UI task accounting remains **760 completed IDs**. Historical detail is preserved in the archive linked above and batch ledgers.

## CURRENT P0 — Real SQL Production MVP

**Umbrella:** #111.  
**Active release gate:** #116 / P0.5 First Production SingleNode.  
**Selected candidate:** RC.61 (`Monitor-0.1.0-rc.61-win-x64.zip`) remains the repository-selected cutover candidate unless #116 explicitly selects a later equivalently verified candidate.  
**Required remaining external/manual order:** `#162 -> #116 -> #111`. **No #116 production mutation while #162 is OPEN.**  
**Repository-admin gate:** #353 remains a separate branch-protection apply/readback action and is not application programming work.

### P0 release chain

| Release | State |
|---|---|
| P0.1 / #112 | COMPLETE |
| P0.2 / #113 | COMPLETE |
| P0.3 / #114 | COMPLETE |
| P0.4 / #115 | COMPLETE — SQL Server 2022 real-engine acceptance |
| P0.5 / #116 | ACTIVE / BLOCKED BEFORE MUTATION BY #162 |

### RC.61 manual handoff

The approved operator sequence remains:

`Invoke-Rc61DurablePromotion.ps1 preview -> explicit -AcknowledgePromotion -> one exact promotion run -> separate IndependentVerificationCommand -> Test-Rc61CutoverReadiness.ps1 with both exact run IDs`

Ambiguous discovery, timeout or failure means **do not redispatch**. Successful repository preflight/promotion readiness remains **0/15 external gates** and never substitutes for real IIS/HTTPS acceptance.

## Stable product/security guardrails

- Monitoring/navigation GETs remain cache/control-plane only and do not initiate monitored-SQL collection.
- Browser code never connects directly to monitored SQL.
- Credentials, full connection strings, current secret references, raw provider errors and arbitrary SQL text stay outside UI/audit/telemetry/exports/diagnostics/evidence.
- Mutations require POST + antiforgery + named authorization policy.
- No autonomous remediation or AI-generated SQL execution.
- Missing, stale, truncated, unavailable or permission-limited evidence must stay explicit; it must not become synthetic healthy/zero state.
- MultiNode remains fail-closed until separately accepted; SingleNode production acceptance remains the current P0 path.
- Repository CI/synthetic evidence cannot close #162, #116 or #111.

## Overall

🟢 P0.1–P0.4 complete · 🟢 BATCH-100..800 repository scope complete · 🟢 programming closures through #423 merged · 🟡 #425/PR #426 exact SharedState key-identity closure in final repository validation · 🟡 #162 manual durable RC.61 publication/verification pending · ⛔ #116 real production mutation blocked while #162 is open · 🔴 #111 production MVP not yet accepted.