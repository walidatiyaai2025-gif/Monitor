# Programming Closure #428 — Serialize Mutable Real SQL Acceptance Fixtures

## Base

`main@c18df80ca266c4c6e580c70c600d057011374635`

## Gap

The Real SQL acceptance lane runs multiple xUnit test collections concurrently against one mutable SQL Server 2022 container. Some Real SQL fixtures create/drop temporary databases while the production collector intentionally validates cross-query count invariants. A database mutation between collector queries can therefore create a transient internally inconsistent acceptance snapshot and fail the lane even when no product code changed.

This was observed on PR #426 where the exact head failed once inside `LeastPrivilegeSql2022_TestAndCollectorReturnRealEvidence` and then passed on rerun without a code change.

## Closure

- The `Run real SQL acceptance tests` invocation now passes `-- xUnit.ParallelizeTestCollections=false` after the `dotnet test` RunSettings delimiter.
- The execution change is scoped only to `.github/workflows/real-sql-acceptance.yml`.
- Normal `ci.yml` keeps its existing parallel test execution.
- Production collector validation and SQL invariants are unchanged.

## Regression coverage

The existing repository-level `P05WorkflowSupplyChainTests` suite locks the workflow contract:

1. Real SQL still filters `Category=RealSql`;
2. exactly one collection-serialization switch exists in the Real SQL workflow;
3. the same switch is absent from normal CI.

Keeping the contract in the established supply-chain suite also makes the Windows production-candidate gate select this workflow-hardening PR without broadening the production-candidate path policy.

## Tracking note

The same-PR ledger records the #428 delta without whole-file rewriting canonical summaries whose history is repository contract-locked.

## Safety boundary

Acceptance-lane determinism only. No production collector invariant relaxation, no monitored-target permission/query expansion, no production IIS/SQL mutation, no RC.61 publication, no external P0 acceptance, and no branch-protection mutation. Dependency order remains `#162 -> #116 -> #111`; #353 remains repository-admin only.
