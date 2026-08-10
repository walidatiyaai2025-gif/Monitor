using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SharedHaFoundationTests
{
    [Fact]
    public void SharedRegistrations_AreVisibleAcrossRepositoryInstances()
    {
        var store = new MemoryDocumentStore();
        var first = new SharedServerRegistrationRepository(store);
        var second = new SharedServerRegistrationRepository(store);
        var one = Registration("Finance", "env:FINANCE");
        var two = Registration("Archive", "env:ARCHIVE");

        first.Upsert(one);
        second.Upsert(two);

        var all = first.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Equal("Archive", all[0].DisplayName);
        Assert.Equal("Finance", all[1].DisplayName);
        Assert.Equal(one.Id, second.GetById(one.Id)!.Id);
    }

    [Fact]
    public async Task SharedRegistrationPayload_PersistsOpaqueReferenceButNotCredentialCanaries()
    {
        var store = new MemoryDocumentStore();
        var repository = new SharedServerRegistrationRepository(store);
        repository.Upsert(Registration("Payroll", "env:PAYROLL"));

        var document = await store.ReadAsync("monitor:registrations:v1");

        Assert.NotNull(document);
        Assert.Contains("env:PAYROLL", document!.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("user-canary", document.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("password-canary", document.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("connectionString", document.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SharedRegistrationMutation_RetriesOptimisticConflict()
    {
        var inner = new MemoryDocumentStore();
        var store = new ConflictOnceDocumentStore(inner);
        var repository = new SharedServerRegistrationRepository(store);

        repository.Upsert(Registration("Retry", "env:RETRY"));

        Assert.Single(repository.GetAll());
        Assert.True(store.ConflictInjected);
    }

    [Fact]
    public void LocalRegistrationImport_IsDeterministicAndNeverOverwritesSharedEstate()
    {
        var store = new MemoryDocumentStore();
        var shared = new SharedServerRegistrationRepository(store);
        var imported = Registration("Imported", "env:IMPORTED");
        var later = Registration("Existing", "env:EXISTING");

        Assert.True(shared.ImportIfEmpty([imported]));
        shared.Upsert(later);
        Assert.False(shared.ImportIfEmpty([Registration("ShouldNotReplace", "env:NOPE")]));

        var all = shared.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Contains(all, item => item.Id == imported.Id);
        Assert.Contains(all, item => item.Id == later.Id);
    }

    [Fact]
    public void SharedAudit_AppendsAcrossInstancesAndReadsNewestFirst()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
        var store = new MemoryDocumentStore(time);
        var first = new SharedAuditStore(store, time);
        var second = new SharedAuditStore(store, time);

        first.Append("admin-a", "incident.transition", "one", "Open->Acknowledged");
        time.Advance(TimeSpan.FromSeconds(1));
        second.Append("admin-b", "incident.transition", "two", "Open->Resolved");

        var events = first.Read(0, 10);
        Assert.Equal(2, events.Count);
        Assert.Equal("admin-b", events[0].Actor);
        Assert.Equal("admin-a", events[1].Actor);
    }

    [Fact]
    public void SharedHistory_DeduplicatesTimestampAndPrunesExpiredEvidence()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
        var store = new MemoryDocumentStore(time);
        var history = new SharedSnapshotHistoryStore(store, time);
        var registrationId = Guid.NewGuid();
        var first = SnapshotResult(registrationId, time.GetUtcNow());

        history.Append(first);
        history.Append(first);
        Assert.Single(history.Read(registrationId, TimeSpan.FromHours(24)));

        time.Advance(TimeSpan.FromHours(25));
        history.Append(SnapshotResult(registrationId, time.GetUtcNow()));

        var points = history.Read(registrationId, TimeSpan.FromHours(24));
        Assert.Single(points);
        Assert.Equal(time.GetUtcNow(), points[0].CollectedAtUtc);
    }

    [Fact]
    public void SharedIncidents_PreserveCompareAndSetAcrossInstances()
    {
        var store = new MemoryDocumentStore();
        var first = new SharedHealthIncidentRepository(store);
        var second = new SharedHealthIncidentRepository(store);
        var registrationId = Guid.NewGuid();
        var observed = DateTimeOffset.UtcNow;
        var finding = new HealthFinding(
            registrationId,
            "database.unavailable",
            FindingSeverity.Critical,
            "Database unavailable",
            "1 database is not online.",
            observed);

        first.Apply([finding]);
        var incident = second.GetAll().Single();

        Assert.True(first.TrySetStatus(incident.Id, IncidentStatus.Open, IncidentStatus.Acknowledged));
        Assert.False(second.TrySetStatus(incident.Id, IncidentStatus.Open, IncidentStatus.Resolved));
        Assert.Equal(IncidentStatus.Acknowledged, second.GetById(incident.Id)!.Status);
    }

    [Fact]
    public void SharedIncidents_FreshReconcileResolvesMissingRule()
    {
        var store = new MemoryDocumentStore();
        var repository = new SharedHealthIncidentRepository(store);
        var registrationId = Guid.NewGuid();
        var observed = DateTimeOffset.UtcNow;
        repository.Apply(
        [
            new HealthFinding(
                registrationId,
                "memory.pressure",
                FindingSeverity.Warning,
                "Pressure",
                "Low memory signal.",
                observed)
        ]);

        repository.Reconcile(registrationId, observed.AddMinutes(1), [], canResolve: true);

        Assert.Equal(IncidentStatus.Resolved, repository.GetAll().Single().Status);
    }

    [Fact]
    public async Task DistributedLease_AllowsOneOwnerUntilExpiryAndRejectsStaleRelease()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
        var store = new MemoryDocumentStore(time);
        var options = CoordinationOptions();
        var first = new SharedStateDistributedLeaseManager(store, new NodeIdentity("node-a"), time, options);
        var second = new SharedStateDistributedLeaseManager(store, new NodeIdentity("node-b"), time, options);

        var firstLease = await first.TryAcquireAsync("scheduler", TimeSpan.FromSeconds(30));
        var blocked = await second.TryAcquireAsync("scheduler", TimeSpan.FromSeconds(30));

        Assert.NotNull(firstLease);
        Assert.Null(blocked);

        time.Advance(TimeSpan.FromSeconds(31));
        var secondLease = await second.TryAcquireAsync("scheduler", TimeSpan.FromSeconds(30));

        Assert.NotNull(secondLease);
        Assert.False(await first.ReleaseAsync(firstLease!));
        Assert.True(await second.ReleaseAsync(secondLease!));
    }

    [Fact]
    public async Task DistributedLease_RenewalAdvancesVersionAndExpiry()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
        var store = new MemoryDocumentStore(time);
        var manager = new SharedStateDistributedLeaseManager(store, new NodeIdentity("node-a"), time, CoordinationOptions());

        var lease = await manager.TryAcquireAsync("scheduler", TimeSpan.FromSeconds(30));
        time.Advance(TimeSpan.FromSeconds(10));
        var renewed = await manager.RenewAsync(lease!);

        Assert.NotNull(renewed);
        Assert.True(renewed!.Version > lease!.Version);
        Assert.True(renewed.ExpiresAtUtc > lease.ExpiresAtUtc);
    }

    [Fact]
    public void SharedSchedulerStatus_IsVisibleAcrossInstances()
    {
        var store = new MemoryDocumentStore();
        var first = new SharedSchedulerStatusStore(store);
        var second = new SharedSchedulerStatusStore(store);
        var started = DateTimeOffset.UtcNow;
        var status = new SchedulerStatus(true, true, started, null, 4, 2, 1, 1);

        first.Set(status);

        Assert.Equal(status, second.Get());
    }

    [Fact]
    public async Task CoordinatedManualRefresh_NoLeaseMeansNoCollectorCall()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var registration = Registration("Refresh", "env:REFRESH");
        registrations.Upsert(registration);
        var cache = new CountingCache(registration.Id);
        var refresh = new SnapshotRefreshService(
            registrations,
            cache,
            TimeProvider.System,
            observer: null,
            leases: new NeverAcquireLeaseManager(),
            coordination: CoordinationOptions());

        var result = await refresh.RefreshAsync(registration.Id);

        Assert.Equal(SnapshotRefreshStatus.Throttled, result.Status);
        Assert.Equal(0, cache.RefreshCalls);
    }

    [Fact]
    public void Coordination_NodeIdentityIsRequiredOnlyWhenEnabled()
    {
        var disabled = new DistributedCoordinationOptions();
        var local = NodeIdentity.Resolve(disabled, _ => null);
        Assert.Equal("single-node", local.Value);

        var enabled = CoordinationOptions();
        Assert.Throws<InvalidOperationException>(() => NodeIdentity.Resolve(enabled, _ => null));

        var resolved = NodeIdentity.Resolve(enabled, _ => "node-01");
        Assert.Equal("node-01", resolved.Value);
        Assert.Equal("[node]", resolved.ToString());
    }

    [Fact]
    public void HaState_ImportWithoutSharedRegistrationFailsClosed()
    {
        var options = new HaStateOptions
        {
            UseSharedRegistrations = false,
            ImportLocalRegistrationsWhenSharedEmpty = true
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void MultiNodeReadiness_DoesNotExposeProviderOrNodeValues()
    {
        var readiness = DeploymentReadinessEvaluator.Evaluate(
            new DeploymentTopologyOptions { Mode = DeploymentTopology.MultiNode },
            new SharedStateOptions
            {
                Provider = SharedStateProviderKind.SqlServer,
                ConnectionStringEnvironmentVariable = "CANARY_CONNECTION_ENV"
            },
            new HaStateOptions
            {
                UseSharedRegistrations = true,
                UseSharedOperationalState = true
            },
            CoordinationOptions());

        var text = readiness.Message + "|" + string.Join("|", readiness.NodeLocalState);
        Assert.False(readiness.Ready);
        Assert.DoesNotContain("CANARY_CONNECTION_ENV", text, StringComparison.Ordinal);
        Assert.DoesNotContain("node-01", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection string", text, StringComparison.OrdinalIgnoreCase);
    }

    private static DistributedCoordinationOptions CoordinationOptions() =>
        new()
        {
            Enabled = true,
            NodeIdEnvironmentVariable = "MONITOR_NODE_ID",
            SchedulerLeaseSeconds = 90,
            RefreshLeaseSeconds = 30,
            MaxConflictRetries = 12
        };

    private static ServerRegistration Registration(string name, string secretReference) =>
        new(
            Guid.NewGuid(),
            name,
            new SqlServerEndpoint("sql.example.internal", 1433, encrypt: true, trustServerCertificate: false),
            SqlAuthenticationMode.SqlLogin,
            new ConnectionSecretReference(secretReference),
            true,
            DateTimeOffset.UtcNow);

    private static SnapshotCacheResult SnapshotResult(Guid registrationId, DateTimeOffset collectedAtUtc) =>
        new(
            new ServerHealthSnapshot(registrationId, "SQL", "16.0", "Enterprise", null, 3600, 4, 4, collectedAtUtc),
            SnapshotFreshness.Fresh,
            TimeSpan.Zero);

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan amount) => _utcNow = _utcNow.Add(amount);
    }

    private sealed class MemoryDocumentStore(TimeProvider? timeProvider = null) : ISharedStateDocumentStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, SharedStateDocument> _documents = new(StringComparer.Ordinal);
        private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_documents.TryGetValue(key, out var document) ? document : null);
            }
        }

        public Task<SharedStateWriteResult> CompareExchangeAsync(string key, long expectedVersion, string payloadJson, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (!_documents.TryGetValue(key, out var current))
                {
                    if (expectedVersion != 0)
                    {
                        return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, null));
                    }

                    var created = new SharedStateDocument(key, 1, payloadJson, _timeProvider.GetUtcNow());
                    _documents[key] = created;
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, created));
                }

                if (current.Version != expectedVersion)
                {
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, current));
                }

                var updated = current with { Version = current.Version + 1, PayloadJson = payloadJson, UpdatedAtUtc = _timeProvider.GetUtcNow() };
                _documents[key] = updated;
                return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, updated));
            }
        }
    }

    private sealed class ConflictOnceDocumentStore(MemoryDocumentStore inner) : ISharedStateDocumentStore
    {
        private int _remaining = 1;
        public bool ConflictInjected { get; private set; }

        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default) => inner.ReadAsync(key, cancellationToken);

        public async Task<SharedStateWriteResult> CompareExchangeAsync(string key, long expectedVersion, string payloadJson, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _remaining, 0) == 1)
            {
                ConflictInjected = true;
                return new SharedStateWriteResult(SharedStateWriteStatus.Conflict, await inner.ReadAsync(key, cancellationToken));
            }

            return await inner.CompareExchangeAsync(key, expectedVersion, payloadJson, cancellationToken);
        }
    }

    private sealed class NeverAcquireLeaseManager : IDistributedLeaseManager
    {
        public Task<DistributedLeaseHandle?> TryAcquireAsync(string resource, TimeSpan duration, CancellationToken cancellationToken = default) => Task.FromResult<DistributedLeaseHandle?>(null);
        public Task<DistributedLeaseHandle?> RenewAsync(DistributedLeaseHandle lease, CancellationToken cancellationToken = default) => Task.FromResult<DistributedLeaseHandle?>(null);
        public Task<bool> ReleaseAsync(DistributedLeaseHandle lease, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class CountingCache(Guid registrationId) : IServerHealthSnapshotCache
    {
        public int RefreshCalls { get; private set; }
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => RefreshAsync(registration, cancellationToken);
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            return Task.FromResult(SnapshotResult(registrationId, DateTimeOffset.UtcNow));
        }
    }
}
