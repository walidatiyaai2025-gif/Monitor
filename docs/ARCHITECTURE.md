# Architecture

## Core monitored-SQL flow

```text
Monitored SQL Server
        |
        v
Central Collector
        |
        v
ServerHealthSnapshot
        |
        +--> Cache / Monitoring Store
        |
        v
ASP.NET Core Backend
        |
        v
Browser UI
```

The browser never connects directly to monitored SQL Servers.

## Zero-SQL read boundary

M8 makes normal monitoring GETs cache/Peek-only. Dashboard, Servers, Server Details, health modules and incident navigation do not initiate monitored SQL collection. Collection remains an explicit backend action through manual refresh or the validated scheduler. SignalR, if introduced, remains downstream delivery only.

## Registration and connection secrets

Registration metadata persists behind `IServerRegistrationRepository` and contains endpoint/auth metadata plus opaque secret references, never plaintext credential values.

`IConnectionSecretStore` owns credential resolution. External `env:<alias>` references read process environment directly and never downgrade to configuration fallback when provider-owned resolution fails.

M7-005..M7-016 replace process-only UI-entered SQL Login credentials with a protected local store. The server generates `local:v1` references; username/password payloads are protected using ASP.NET Data Protection with a reference-scoped purpose. Ciphertext is written atomically outside `wwwroot`, and the Data Protection key ring is persisted separately outside `wwwroot`. A lost/different key ring or tampered ciphertext fails closed. Registration JSON still contains only the opaque reference.

The protected local secret store and key ring are node-local and therefore are not an HA/shared credential solution.

## Durable Monitor-owned operational state

Audit, snapshot history and incidents use independent versioned files under the Monitor operational-state root. Candidate state is durably committed before becoming live in process. Invalid/corrupt state fails closed. These stores preserve their existing bounded contracts and exclude SQL credentials/text/endpoints/provider errors/job commands/arbitrary payloads.

## HA topology guard

M7-004 adds explicit `Deployment:Mode`. `SingleNode` is supported. `MultiNode` is recognized but startup rejects it until shared registration/operational state and distributed coordination exist.

Remaining node-local boundaries include registration/operational stores, protected local credential store + key ring, login-attempt limiting, snapshot cache/single-flight and scheduler ownership/backoff/status. A local file or network-share path is not treated as a distributed transaction/coordination primitive.

## Next shared-state capability

M7-017 introduces a generic `ISharedStateDocumentStore` capability and the first real provider backed by a **dedicated Monitor-owned SQL Server database**. It must not implicitly reuse a monitored target. Provider connection material remains outside appsettings/source control. M7-017 is storage capability only; MultiNode remains blocked until M7-018 migrates required repositories/coordination and adds distributed ownership/single-flight semantics.
