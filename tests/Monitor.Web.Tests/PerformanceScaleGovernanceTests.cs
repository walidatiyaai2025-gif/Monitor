using Microsoft.Data.SqlClient;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class PerformanceScaleGovernanceTests
{
    [Fact]
    public void PerformanceOptions_RejectUnsafeBounds()
    {
        Assert.Throws<InvalidOperationException>(() => new PerformanceScaleOptions { SnapshotCacheMaxEntries = 15 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new PerformanceScaleOptions { ManualRefreshMaxConcurrency = 17 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new PerformanceScaleOptions { SqlMaxPoolSize = 33 }.Validate());
    }

    [Fact]
    public async Task SnapshotCache_EvictsOldestWhenCapacityIsExceeded()
    {
        var now = new DateTimeOffset(2026, 8, 10, 14, 0, 0, TimeSpan.Zero);
        var time = new FixedTimeProvider(now);
        var collector = new SequencedCollector(now);
        var options = new PerformanceScaleOptions { SnapshotCacheMaxEntries = 16 };
        var cache = new ServerHealthSnapshotCache(collector, time, options);
        var registrations = Enumerable.Range(0, 17).Select(index => Registration($"SQL-{index:00}", now.AddMinutes(-index))).ToArray();

        foreach (var registration in registrations)
        {
            await cache.RefreshAsync(registration);
        }

        Assert.Null(cache.Peek(registrations[0].Id));
        Assert.NotNull(cache.Peek(registrations[^1].Id));
        Assert.Equal(17, collector.Calls);
    }

    [Fact]
    public void HistoryRead_IsWindowAndPageBounded()
    {
        var now = new DateTimeOffset(2026, 8, 10, 14, 0, 0, TimeSpan.Zero);
        var time = new FixedTimeProvider(now);
        var store = new InMemorySnapshotHistoryStore(time);
        var registration = Registration("SQL-HISTORY", now);
        for (var index = 0; index < 20; index++)
        {
            store.Append(Result(registration.Id, now.AddMinutes(-19 + index)));
        }

        var page = store.Read(registration.Id, TimeSpan.FromHours(24), 5, 7);

        Assert.Equal(7, page.Count);
        Assert.Equal(now.AddMinutes(-14), page[0].CollectedAtUtc);
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Read(registration.Id, TimeSpan.FromHours(25), 0, 10));
    }

    [Fact]
    public void AuditDecorator_ClampsOffsetAndPageSizeBeforeInnerStore()
    {
        var inner = new RecordingAuditStore();
        var options = new PerformanceScaleOptions { AuditMaxPageSize = 25 };
        var store = new PerformanceBoundedAuditStore(inner, options);

        store.Read(-100, 999);

        Assert.Equal(0, inner.LastOffset);
        Assert.Equal(25, inner.LastLimit);
    }

    [Fact]
    public void IncidentWorkflow_ClampsUntrustedPageLimit()
    {
        var repository = new InMemoryHealthIncidentRepository();
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < 150; index++)
        {
            repository.Apply([
                new HealthFinding(Guid.NewGuid(), $"rule-{index:000}", FindingSeverity.Warning, $"Incident {index}", "bounded evidence", now.AddSeconds(index))
            ]);
        }
        var workflow = new IncidentWorkflowService(repository, new RecommendationEngine(), new AdvisorContextBuilder(), new DisabledAdvisorProvider());

        var result = workflow.Query(new IncidentQuery(Limit: 999));

        Assert.Equal(100, result.Items.Count);
        Assert.Equal(100, result.Query.Limit);
    }

    [Fact]
    public async Task ServerEstatePaging_PeeksOnlyRequestedPageAndNeverCollects()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var now = DateTimeOffset.UtcNow;
        foreach (var index in Enumerable.Range(0, 25))
        {
            repository.Upsert(Registration($"SQL-{index:00}", now.AddSeconds(index)));
        }
        var cache = new CountingPeekCache();
        var service = new MonitorReadService(new DemoMonitorService(), repository, cache, performance: new PerformanceScaleOptions());

        var page = await service.GetServersPageAsync(10, 5);

        Assert.Equal(25, page.TotalCount);
        Assert.Equal(5, page.Items.Count);
        Assert.Equal(5, cache.PeekCalls);
        Assert.Equal(0, cache.CollectionCalls);
    }

    [Fact]
    public async Task ManualRefresh_GlobalConcurrencyGateRejectsExcessWork()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var first = Registration("SQL-A", DateTimeOffset.UtcNow);
        var second = Registration("SQL-B", DateTimeOffset.UtcNow.AddSeconds(1));
        repository.Upsert(first);
        repository.Upsert(second);
        var cache = new BlockingRefreshCache();
        var options = new PerformanceScaleOptions { ManualRefreshMaxConcurrency = 1 };
        var gate = new ManualRefreshConcurrencyGate(options);
        var service = new SnapshotRefreshService(repository, cache, TimeProvider.System, concurrencyGate: gate);

        var firstRefresh = service.RefreshAsync(first.Id);
        await cache.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var secondResult = await service.RefreshAsync(second.Id);
        cache.Release.TrySetResult();
        var firstResult = await firstRefresh;

        Assert.Equal(SnapshotRefreshStatus.Refreshed, firstResult.Status);
        Assert.Equal(SnapshotRefreshStatus.Throttled, secondResult.Status);
        Assert.Equal(1, cache.MaxObservedConcurrency);
    }

    [Fact]
    public void SchedulerJitter_IsDeterministicAndBounded()
    {
        var first = SchedulerJitter.Compute("node-a", 42, 5);
        var second = SchedulerJitter.Compute("node-a", 42, 5);
        var other = SchedulerJitter.Compute("node-b", 42, 5);

        Assert.Equal(first, second);
        Assert.InRange(first, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        Assert.InRange(other, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CollectionCycle_UsesBoundedRoundRobinBatches()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var now = DateTimeOffset.UtcNow;
        foreach (var index in Enumerable.Range(0, 25))
        {
            repository.Upsert(Registration($"SQL-{index:00}", now.AddSeconds(index)));
        }
        var cache = new RecordingRefreshCache();
        var observer = new NoOpObserver();
        var options = new SnapshotScheduleOptions { MaxConcurrency = 4, MaxTargetsPerCycle = 10, MaxJitterSeconds = 0 };
        var cycle = new SnapshotCollectionCycle(repository, cache, observer, options);

        await cycle.RunOnceAsync(default);
        var firstBatch = cache.Ids.ToHashSet();
        cache.Ids.Clear();
        await cycle.RunOnceAsync(default);
        var secondBatch = cache.Ids.ToHashSet();

        Assert.Equal(10, firstBatch.Count);
        Assert.Equal(10, secondBatch.Count);
        Assert.Empty(firstBatch.Intersect(secondBatch));
    }

    [Fact]
    public void SqlPoolPolicy_IsExplicitAndBounded()
    {
        var builder = new SqlConnectionStringBuilder();
        var options = new PerformanceScaleOptions { SqlMaxPoolSize = 3, SqlPoolLifetimeSeconds = 120 };

        SqlConnectionPoolPolicy.Apply(builder, options);

        Assert.True(builder.Pooling);
        Assert.Equal(0, builder.MinPoolSize);
        Assert.Equal(3, builder.MaxPoolSize);
        Assert.Equal(120, builder.LoadBalanceTimeout);
    }

    private static ServerRegistration Registration(string name, DateTimeOffset createdAt) => new(
        Guid.NewGuid(),
        name,
        new SqlServerEndpoint("sql.example.internal", 1433),
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        createdAt);

    private static SnapshotCacheResult Result(Guid id, DateTimeOffset collectedAt) => new(
        new ServerHealthSnapshot(id, "SQL", "16.0", "Enterprise", null, 3600, 4, 4, collectedAt),
        SnapshotFreshness.Fresh,
        TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SequencedCollector(DateTimeOffset baseTime) : ISqlServerSnapshotCollector
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public Task<ServerHealthSnapshot> CollectAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            var sequence = Interlocked.Increment(ref _calls);
            return Task.FromResult(new ServerHealthSnapshot(registration.Id, registration.DisplayName, "16.0", "Enterprise", null, 3600, 4, 4, baseTime.AddSeconds(sequence)));
        }
    }

    private sealed class RecordingAuditStore : IAuditStore
    {
        public int LastOffset { get; private set; }
        public int LastLimit { get; private set; }
        public void Append(string actor, string action, string target, string outcome) { }
        public IReadOnlyList<AuditEvent> Read(int offset, int limit)
        {
            LastOffset = offset;
            LastLimit = limit;
            return [];
        }
    }

    private sealed class CountingPeekCache : IServerHealthSnapshotCache
    {
        public int PeekCalls { get; private set; }
        public int CollectionCalls { get; private set; }
        public SnapshotCacheResult? Peek(Guid registrationId)
        {
            PeekCalls++;
            return null;
        }
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            CollectionCalls++;
            throw new InvalidOperationException("Paging must not collect.");
        }
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            CollectionCalls++;
            throw new InvalidOperationException("Paging must not collect.");
        }
    }

    private sealed class BlockingRefreshCache : IServerHealthSnapshotCache
    {
        private int _active;
        private int _max;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int MaxObservedConcurrency => Volatile.Read(ref _max);
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => RefreshAsync(registration, cancellationToken);
        public async Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            var active = Interlocked.Increment(ref _active);
            while (true)
            {
                var current = Volatile.Read(ref _max);
                if (active <= current || Interlocked.CompareExchange(ref _max, active, current) == current) break;
            }
            Entered.TrySetResult();
            try
            {
                await Release.Task.WaitAsync(cancellationToken);
                return Result(registration.Id, DateTimeOffset.UtcNow);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    private sealed class RecordingRefreshCache : IServerHealthSnapshotCache
    {
        public List<Guid> Ids { get; } = [];
        private readonly object _gate = new();
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => RefreshAsync(registration, cancellationToken);
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            lock (_gate) Ids.Add(registration.Id);
            return Task.FromResult(Result(registration.Id, DateTimeOffset.UtcNow));
        }
    }

    private sealed class NoOpObserver : ISnapshotObserver
    {
        public void Observe(SnapshotCacheResult result) { }
    }
}
