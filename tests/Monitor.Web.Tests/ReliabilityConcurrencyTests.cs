using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class ReliabilityConcurrencyTests
{
    [Fact]
    public async Task B100_071_FaultInjectionHarness_FailsAtomically_ThenRecovers()
    {
        var inner = new AtomicMemoryDocumentStore();
        var faults = new FaultInjectingDocumentStore(inner);
        faults.FailNextWrites(1);

        await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(() =>
            faults.CompareExchangeAsync("monitor:test:v1", 0, "{\"value\":1}"));
        Assert.Null(await inner.ReadAsync("monitor:test:v1"));

        var recovered = await faults.CompareExchangeAsync("monitor:test:v1", 0, "{\"value\":2}");
        Assert.True(recovered.Applied);
        Assert.Equal("{\"value\":2}", (await inner.ReadAsync("monitor:test:v1"))!.PayloadJson);
        Assert.Equal(1, faults.InjectedWriteFailures);
    }

    [Fact]
    public async Task B100_072_ExpiredLease_IsReElected_AndOldOwnerCannotRenew()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 11, 2, 0, 0, TimeSpan.Zero));
        var store = new AtomicMemoryDocumentStore(time);
        var nodeA = LeaseManager(store, time, "node-a");
        var nodeB = LeaseManager(store, time, "node-b");

        var leaseA = await nodeA.TryAcquireAsync("scheduler", TimeSpan.FromSeconds(30));
        Assert.NotNull(leaseA);
        Assert.Null(await nodeB.TryAcquireAsync("scheduler", TimeSpan.FromSeconds(30)));

        time.Advance(TimeSpan.FromSeconds(31));
        var leaseB = await nodeB.TryAcquireAsync("scheduler", TimeSpan.FromSeconds(30));

        Assert.NotNull(leaseB);
        Assert.Equal("node-b", leaseB!.OwnerId);
        Assert.Null(await nodeA.RenewAsync(leaseA!));
        Assert.False(await nodeA.ReleaseAsync(leaseA!));
        Assert.True(await nodeB.ReleaseAsync(leaseB));
    }

    [Fact]
    public async Task B100_073_DedicatedStateDb_OutageReadinessDegrades_AndRecovers()
    {
        var backend = new RecoverableSqlBackend();
        var options = SqlOptions();
        var store = new SqlServerSharedStateDocumentStore(options, backend, _ => "Server=state-canary;Database=MonitorState;Integrated Security=True");
        var readiness = new SharedStateReadinessService(options, store);

        backend.Available = false;
        var down = await readiness.GetAsync();
        Assert.Equal(SharedStateReadinessStatus.Unavailable, down.Status);
        Assert.False(down.SharedStorageReady);
        Assert.DoesNotContain("state-canary", down.Message, StringComparison.OrdinalIgnoreCase);

        backend.Available = true;
        var up = await readiness.GetAsync();
        Assert.Equal(SharedStateReadinessStatus.Ready, up.Status);
        Assert.True(up.SharedStorageReady);

        var write = await store.CompareExchangeAsync("monitor:recovery:v1", 0, "{\"recovered\":true}");
        Assert.True(write.Applied);
        Assert.Equal("{\"recovered\":true}", (await store.ReadAsync("monitor:recovery:v1"))!.PayloadJson);
    }

    [Fact]
    public void B100_074_InterruptedRegistrationImport_IsRestartSafeAndRetryable()
    {
        var inner = new AtomicMemoryDocumentStore();
        var faults = new FaultInjectingDocumentStore(inner);
        faults.FailNextWrites(1);
        var registration = Registration(Guid.NewGuid(), "Finance");

        var interrupted = new SharedServerRegistrationRepository(faults);
        Assert.Throws<SharedStateStoreUnavailableException>(() => interrupted.ImportIfEmpty([registration]));
        Assert.Null(inner.ReadAsync("monitor:registrations:v1").GetAwaiter().GetResult());

        var restarted = new SharedServerRegistrationRepository(faults);
        Assert.True(restarted.ImportIfEmpty([registration]));
        var all = restarted.GetAll();
        Assert.Single(all);
        Assert.Equal(registration.Id, all[0].Id);
        Assert.False(restarted.ImportIfEmpty([Registration(Guid.NewGuid(), "MustNotOverwrite")]));
    }

    [Fact]
    public async Task B100_075_ConcurrentIncidentTransitions_ExactlyOneExpectedStateWins()
    {
        var store = new AtomicMemoryDocumentStore();
        var first = new SharedHealthIncidentRepository(store);
        var second = new SharedHealthIncidentRepository(store);
        var registrationId = Guid.NewGuid();
        var observed = new DateTimeOffset(2026, 8, 11, 2, 0, 0, TimeSpan.Zero);
        first.Apply([Finding(registrationId, observed)]);
        var incidentId = first.GetAll().Single().Id;
        using var start = new ManualResetEventSlim(false);

        var acknowledge = Task.Run(() => { start.Wait(); return first.TrySetStatus(incidentId, IncidentStatus.Open, IncidentStatus.Acknowledged); });
        var resolve = Task.Run(() => { start.Wait(); return second.TrySetStatus(incidentId, IncidentStatus.Open, IncidentStatus.Resolved); });
        start.Set();
        var results = await Task.WhenAll(acknowledge, resolve);

        Assert.Single(results.Where(value => value));
        var final = first.GetById(incidentId)!;
        Assert.Contains(final.Status, new[] { IncidentStatus.Acknowledged, IncidentStatus.Resolved });
        Assert.Equal(1, final.Occurrences);
    }

    [Fact]
    public async Task B100_076_ConcurrentAuditAppends_AreLosslessAndBounded()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 11, 2, 0, 0, TimeSpan.Zero));
        var store = new AtomicMemoryDocumentStore(time);
        const int writerCount = 8;
        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, writerCount).Select(index => Task.Run(() =>
        {
            start.Wait();
            new SharedAuditStore(store, time).Append($"node-{index}", "soak.audit", $"target-{index}", "ok");
        })).ToArray();

        start.Set();
        await Task.WhenAll(tasks);

        var events = new SharedAuditStore(store, time).Read(0, 100);
        Assert.Equal(writerCount, events.Count);
        Assert.Equal(writerCount, events.Select(item => item.Actor).Distinct(StringComparer.Ordinal).Count());
        Assert.All(events, item => Assert.Equal("soak.audit", item.Action));
    }

    [Fact]
    public async Task B100_077_CrossNodeHistory_DeduplicatesSameSnapshotTimestamp()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 11, 2, 0, 0, TimeSpan.Zero));
        var store = new AtomicMemoryDocumentStore(time);
        var nodeA = new SharedSnapshotHistoryStore(store, time);
        var nodeB = new SharedSnapshotHistoryStore(store, time);
        var registrationId = Guid.NewGuid();
        var result = SnapshotResult(registrationId, time.GetUtcNow());
        using var start = new ManualResetEventSlim(false);

        var a = Task.Run(() => { start.Wait(); nodeA.Append(result); });
        var b = Task.Run(() => { start.Wait(); nodeB.Append(result); });
        start.Set();
        await Task.WhenAll(a, b);

        var points = nodeA.Read(registrationId, TimeSpan.FromHours(24));
        Assert.Single(points);
        Assert.Equal(result.Snapshot.CollectedAtUtc, points[0].CollectedAtUtc);
    }

    [Fact]
    public async Task B100_078_CrossNodeRegistrationConflict_PreservesSingleValidRecord()
    {
        var store = new AtomicMemoryDocumentStore();
        var id = Guid.NewGuid();
        var nodeA = new SharedServerRegistrationRepository(store);
        var nodeB = new SharedServerRegistrationRepository(store);
        using var start = new ManualResetEventSlim(false);

        var a = Task.Run(() => { start.Wait(); nodeA.Upsert(Registration(id, "Finance-A")); });
        var b = Task.Run(() => { start.Wait(); nodeB.Upsert(Registration(id, "Finance-B")); });
        start.Set();
        await Task.WhenAll(a, b);

        var all = nodeA.GetAll();
        Assert.Single(all);
        Assert.Equal(id, all[0].Id);
        Assert.Contains(all[0].DisplayName, new[] { "Finance-A", "Finance-B" });
        Assert.True((await store.ReadAsync("monitor:registrations:v1"))!.Version >= 2);
    }

    [Fact]
    public async Task B100_079_DistributedManualRefresh_AllowsOnlyOneCollectorAcrossNodes()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 11, 2, 0, 0, TimeSpan.Zero));
        var documentStore = new AtomicMemoryDocumentStore(time);
        var registrations = new InMemoryServerRegistrationRepository();
        var registration = Registration(Guid.NewGuid(), "Refresh");
        registrations.Upsert(registration);
        var firstCache = new BlockingCache(registration.Id, time.GetUtcNow());
        var secondCache = new CountingCache(registration.Id, time.GetUtcNow());
        var options = CoordinationOptions();
        var first = new SnapshotRefreshService(registrations, firstCache, time, leases: LeaseManager(documentStore, time, "node-a"), coordination: options);
        var second = new SnapshotRefreshService(registrations, secondCache, time, leases: LeaseManager(documentStore, time, "node-b"), coordination: options);

        var firstTask = first.RefreshAsync(registration.Id);
        await firstCache.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondResult = await second.RefreshAsync(registration.Id);

        Assert.Equal(SnapshotRefreshStatus.Throttled, secondResult.Status);
        Assert.Equal(0, secondCache.RefreshCalls);
        firstCache.Release.TrySetResult();
        var firstResult = await firstTask;
        Assert.Equal(SnapshotRefreshStatus.Refreshed, firstResult.Status);
        Assert.Equal(1, firstCache.RefreshCalls);
    }

    [Fact]
    public async Task B100_080_MultiNodeSoakSimulation_MaintainsStateAndLeaseInvariants()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 11, 2, 0, 0, TimeSpan.Zero));
        var store = new AtomicMemoryDocumentStore(time);
        var registrations = new SharedServerRegistrationRepository(store);
        var audit = new SharedAuditStore(store, time);
        var history = new SharedSnapshotHistoryStore(store, time);
        var incidents = new SharedHealthIncidentRepository(store);
        var nodes = new[]
        {
            LeaseManager(store, time, "node-a"),
            LeaseManager(store, time, "node-b"),
            LeaseManager(store, time, "node-c")
        };
        var registrationIds = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToArray();

        for (var cycle = 0; cycle < 120; cycle++)
        {
            var registrationIndex = cycle % registrationIds.Length;
            var id = registrationIds[registrationIndex];
            registrations.Upsert(Registration(id, $"SQL-{registrationIndex:00}"));
            audit.Append($"node-{cycle % nodes.Length}", "soak.cycle", id.ToString("N"), "ok");
            history.Append(SnapshotResult(id, time.GetUtcNow()));
            var ruleIndex = (cycle / registrationIds.Length) % 4;
            incidents.Apply([Finding(id, time.GetUtcNow(), $"soak.rule.{ruleIndex}")]);

            var owner = nodes[cycle % nodes.Length];
            var lease = await owner.TryAcquireAsync("soak-scheduler", TimeSpan.FromSeconds(30));
            Assert.NotNull(lease);
            Assert.True(await owner.ReleaseAsync(lease!));
            time.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.Equal(12, registrations.GetAll().Count);
        Assert.Equal(100, audit.Read(0, 100).Count);
        Assert.Equal(48, incidents.GetAll().Count);
        foreach (var id in registrationIds)
        {
            var points = history.Read(id, TimeSpan.FromHours(24));
            Assert.Equal(10, points.Count);
            Assert.Equal(points.Count, points.Select(point => point.CollectedAtUtc).Distinct().Count());
        }
        Assert.True(store.CompareExchangeCalls > 0);
    }

    private static SharedStateDistributedLeaseManager LeaseManager(ISharedStateDocumentStore store, TimeProvider time, string node) =>
        new(store, new NodeIdentity(node), time, CoordinationOptions());

    private static DistributedCoordinationOptions CoordinationOptions() => new()
    {
        Enabled = true,
        NodeIdEnvironmentVariable = "MONITOR_NODE_ID",
        SchedulerLeaseSeconds = 90,
        RefreshLeaseSeconds = 30,
        MaxConflictRetries = 12
    };

    private static SharedStateOptions SqlOptions() => new()
    {
        Provider = SharedStateProviderKind.SqlServer,
        ConnectionStringEnvironmentVariable = "MONITOR_SHARED_STATE_SQL_CONNECTION",
        CommandTimeoutSeconds = 5
    };

    private static ServerRegistration Registration(Guid id, string name) => new(
        id,
        name,
        new SqlServerEndpoint("sql.example.internal", 1433, encrypt: true, trustServerCertificate: false),
        SqlAuthenticationMode.SqlLogin,
        new ConnectionSecretReference($"env:{name.ToUpperInvariant().Replace('-', '_')}"),
        true,
        new DateTimeOffset(2026, 8, 11, 1, 0, 0, TimeSpan.Zero));

    private static HealthFinding Finding(Guid registrationId, DateTimeOffset observed, string rule = "database.unavailable") => new(
        registrationId,
        rule,
        FindingSeverity.Warning,
        "Deterministic finding",
        "Bounded aggregate evidence.",
        observed);

    private static SnapshotCacheResult SnapshotResult(Guid registrationId, DateTimeOffset collectedAtUtc) => new(
        new ServerHealthSnapshot(registrationId, "SQL", "16.0", "Enterprise", null, 3600, 4, 4, collectedAtUtc),
        SnapshotFreshness.Fresh,
        TimeSpan.Zero);

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan amount) => _utcNow = _utcNow.Add(amount);
    }

    private sealed class AtomicMemoryDocumentStore(TimeProvider? timeProvider = null) : ISharedStateDocumentStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, SharedStateDocument> _documents = new(StringComparer.Ordinal);
        private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
        public int CompareExchangeCalls { get; private set; }
        public int ConflictCount { get; private set; }

        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                return Task.FromResult(_documents.TryGetValue(key, out var value) ? value : null);
            }
        }

        public Task<SharedStateWriteResult> CompareExchangeAsync(string key, long expectedVersion, string payloadJson, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                CompareExchangeCalls++;
                if (!_documents.TryGetValue(key, out var current))
                {
                    if (expectedVersion != 0)
                    {
                        ConflictCount++;
                        return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, null));
                    }

                    var created = new SharedStateDocument(key, 1, payloadJson, _time.GetUtcNow());
                    _documents.Add(key, created);
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, created));
                }

                if (current.Version != expectedVersion)
                {
                    ConflictCount++;
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, current));
                }

                var updated = current with
                {
                    Version = current.Version + 1,
                    PayloadJson = payloadJson,
                    UpdatedAtUtc = _time.GetUtcNow()
                };
                _documents[key] = updated;
                return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, updated));
            }
        }
    }

    private sealed class FaultInjectingDocumentStore(AtomicMemoryDocumentStore inner) : ISharedStateDocumentStore
    {
        private int _remainingWriteFailures;
        public int InjectedWriteFailures { get; private set; }
        public void FailNextWrites(int count) => _remainingWriteFailures = count;
        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default) => inner.ReadAsync(key, cancellationToken);

        public Task<SharedStateWriteResult> CompareExchangeAsync(string key, long expectedVersion, string payloadJson, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Decrement(ref _remainingWriteFailures) >= 0)
            {
                InjectedWriteFailures++;
                return Task.FromException<SharedStateWriteResult>(new SharedStateStoreUnavailableException());
            }

            return inner.CompareExchangeAsync(key, expectedVersion, payloadJson, cancellationToken);
        }
    }

    private sealed class RecoverableSqlBackend : ISharedStateSqlBackend
    {
        private readonly AtomicMemoryDocumentStore _inner = new();
        public bool Available { get; set; } = true;
        public Task<int?> ReadSchemaVersionAsync(string connectionString, int commandTimeoutSeconds, CancellationToken cancellationToken)
        {
            EnsureAvailable();
            return Task.FromResult<int?>(SqlServerSharedStateDocumentStore.SupportedSchemaVersion);
        }
        public Task<SharedStateDocument?> ReadAsync(string connectionString, string key, int commandTimeoutSeconds, CancellationToken cancellationToken)
        {
            EnsureAvailable();
            return _inner.ReadAsync(key, cancellationToken);
        }
        public Task<SharedStateWriteResult> CompareExchangeAsync(string connectionString, string key, long expectedVersion, string payloadJson, int commandTimeoutSeconds, CancellationToken cancellationToken)
        {
            EnsureAvailable();
            return _inner.CompareExchangeAsync(key, expectedVersion, payloadJson, cancellationToken);
        }
        private void EnsureAvailable()
        {
            if (!Available) throw new InvalidOperationException("Password=CANARY;Server=state-canary");
        }
    }

    private sealed class BlockingCache(Guid registrationId, DateTimeOffset collectedAtUtc) : IServerHealthSnapshotCache
    {
        public int RefreshCalls { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => RefreshAsync(registration, cancellationToken);
        public async Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return SnapshotResult(registrationId, collectedAtUtc);
        }
    }

    private sealed class CountingCache(Guid registrationId, DateTimeOffset collectedAtUtc) : IServerHealthSnapshotCache
    {
        public int RefreshCalls { get; private set; }
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => RefreshAsync(registration, cancellationToken);
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            return Task.FromResult(SnapshotResult(registrationId, collectedAtUtc));
        }
    }
}