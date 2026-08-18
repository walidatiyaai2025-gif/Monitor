# Programming Closure #417 — verify shared-state v1 integrity CHECK constraints

## Baseline

- Base: `main@91c9ccece17a68fb60b0dadd4f7d3c565acb4c97`.
- Schema-v1 readiness and provisioning already verify the four canonical document columns and the sole `DocumentKey` primary key.
- Canonical v1 also creates two data-integrity CHECK constraints, but those constraints were not part of either fingerprint.

## Gap

A pre-existing `dbo.MonitorSharedStateDocuments` table could have the correct column and primary-key shape while `CK_MonitorSharedStateDocuments_Version` or `CK_MonitorSharedStateDocuments_PayloadJson` was missing, disabled, untrusted, or redefined. The installer could stamp/accept schema version 1 and runtime readiness could report `Ready` even though direct SQL writes were no longer guarded by the canonical `Version >= 1` and valid-JSON invariants.

## Closure

The installer and runtime readiness now require both canonical integrity constraints as part of schema-v1 truthfulness:

- `CK_MonitorSharedStateDocuments_Version` must be enabled, trusted, and normalize to `Version >= 1`;
- `CK_MonitorSharedStateDocuments_PayloadJson` must be enabled, trusted, and normalize to `ISJSON(PayloadJson) = 1`;
- normalization ignores only incidental brackets, parentheses and whitespace; constraint names and supported semantics remain mandatory;
- installer drift still fails with SQL error `51001` inside the existing transaction and does not auto-create, repair, enable, trust, or redefine a pre-existing drifted constraint;
- readiness remains read-only and maps integrity drift to the existing redacted `Unavailable` state.

Schema version and canonical fresh-install output are unchanged.

## Real SQL regression

SQL Server 2022 acceptance now proves:

1. canonical readiness starts `Ready` with both enabled/trusted constraints;
2. a missing Version constraint makes readiness `Unavailable`;
3. recreating the canonical name with wrong semantics (`Version >= 0`) remains `Unavailable`;
4. restoring `Version >= 1` returns `Ready`;
5. disabling the JSON constraint makes readiness `Unavailable`;
6. re-enabling it without trust remains `Unavailable`;
7. `WITH CHECK CHECK` restores trust and `Ready`;
8. the readiness probe never writes a SharedState document while testing drift;
9. the repository installer still succeeds fresh and idempotently on canonical v1;
10. a pre-existing table with correct columns/PK but no integrity CHECKs fails installer error `51001`, is not auto-repaired, and is not stamped v1.

## Workflow-selected gates

The changed paths include `src/Monitor.Web/Services/SharedStateStore.cs`, the schema-v1 SQL installer, its Real SQL regression, and this ledger. Therefore the exact final head must pass repository CI, Real SQL acceptance, Windows production-candidate, protected-P0 commit guard, and protected-P0 metadata guard.

## Safety boundary

SharedState schema-v1 data-integrity verification only. No schema-version bump, migration/auto-repair, monitored-target permission expansion, runtime probe writes, secret disclosure, autonomous remediation, release promotion, production IIS/SQL mutation, external production acceptance, protected-P0 completion, or branch-protection mutation. External/manual dependency order remains `#162 -> #116 -> #111`; #353 remains a separate repository-admin action.

## Definition of Done

The exact final PR head must be current with `main`, have zero unresolved review threads, and pass CI, Real SQL acceptance, Windows production-candidate, protected-P0 commit guard, and protected-P0 metadata guard.