using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class DistributedRefreshLeaseHeartbeatTests
{
    [Fact]
    public async Task LongRunningRefresh_RenewsBeforeOriginalExpiry_AndReleasesLatestVersion()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 18, 17, 15, 0, TimeSpan.Zero));
        var store = new MemoryDocumentStore(time);
        var registrations = new InMemoryServerRegistrationRepository();
        var registration = Registration();
        registrations.Upsert(registration);
        var firstCache = new BlockingCache(registration.Id, time.GetUtcNow());
        var secondCache = new CountingCache(registration.Id, time.GetUtcNow());
        var delay = new ControlledDelay();
        var options = CoordinationOptions();
        var nodeA = new SignalingLeaseManager(Manager(store, time, "node-a", options));
        var nodeB = Manager(store, time, "node-b", options);
        var first = new SnapshotRefreshService(
            registrations,
            firstCache,
            time,
            observer: null,
            leases: nodeA,
            coordination: options,
            concurrencyGate: null,
            leaseRenewalDelay: delay.DelayAsync);
        var second = new SnapshotRefreshService(
            registrations,
            secondCache,
            time,
            leases: nodeB,
            coordination: options);

        var firstTask = first.RefreshAsync(registration.Id);
        await firstCache.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await delay.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        time.Advance(TimeSpan.FromSeconds(5));
        delay.ReleaseFirst();
        var renewed = await nodeA.RenewCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(renewed);
        Assert.Equal(2, renewed!.Version);

        time.Advance(TimeSpan.FromSeconds(11));
        var secondResult = await second.RefreshAsync(registration.Id);

        Assert.Equal(SnapshotRefreshStatus.Throttled, secondResult.Status);
        Assert.Equal(0, secondCache.RefreshCalls);

        firstCache.Release.TrySetResult();
        var firstResult = await firstTask;
        Assert.Equal(SnapshotRefreshStatus.Refreshed, firstResult.Status);
        Assert.NotNull(nodeA.ReleasedHandle);
        Assert.Equal(renewed.Version, nodeA.ReleasedHandle!.Version);

        var persisted = await store.ReadAsync(ResourceKey(registration.Id));
        Assert.NotNull(persisted);
        Assert.Equal(3, persisted!.Version);
        Assert.Contains("\"released\":true", persisted.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LostRenewalAuthority_SuppressesObserverAndSuccessfulRefreshResult()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 18, 17, 15, 0, TimeSpan.Zero));
        var store = new MemoryDocumentStore(time);
        var registrations = new InMemoryServerRegistrationRepository();
        var registration = Registration();
        registrations.Upsert(registration);
        var cache = new BlockingCache(registration.Id, time.GetUtcNow());
        var observer = new CountingObserver();
        var delay = new ControlledDelay();
        var options = CoordinationOptions();
        var nodeA = new SignalingLeaseManager(Manager(store, time, "node-a", options));
        var nodeB = Manager(store, time, "node-b", options);
        var first = new SnapshotRefreshService(
            registrations,
            cache,
            time,
            observer,
            nodeA,
            options,
            concurrencyGate: null,
            leaseRenewalDelay: delay.DelayAsync);

        var firstTask = first.RefreshAsync(registration.Id);
        await cache.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await delay.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        time.Advance(TimeSpan.FromSeconds(16));
        var replacement = await nodeB.TryAcquireAsync(
            $"refresh:{registration.Id:N}",
            TimeSpan.FromSeconds(options.RefreshLeaseSeconds));
        Assert.NotNull(replacement);
        Assert.Equal("node-b", replacement!.OwnerId);

        delay.ReleaseFirst();
        var renewed = await nodeA.RenewCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(renewed);

        cache.Release.TrySetResult();
        var result = await firstTask;

        Assert.Equal(SnapshotRefreshStatus.Throttled, result.Status);
        Assert.Contains("coordination", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, observer.CallCount);
        Assert.NotNull(nodeA.ReleasedHandle);
        Assert.Equal(1, nodeA.ReleasedHandle!.Version);

        var persisted = await store.ReadAsync(ResourceKey(registration.Id));
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted!.Version);
        Assert.Contains("\"ownerId\":\"node-b\"", persisted.PayloadJson, StringComparison.Ordinal);
        Assert.True(await nodeB.ReleaseAsync(replacement));
    }

    private static string ResourceKey(Guid registrationId) => $"monitor:lease:v1:refresh:{registrationId:N}";

    private static ServerRegistration Registration() => new(
        Guid.NewGuid(),
        "SQL",
        new SqlServerEndpoint("sql01"),
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        new DateTimeOffset(2026, 8, 18, 17, 0, 0, TimeSpan.Zero));

    private static DistributedCoordinationOptions CoordinationOptions() => new()
    {
        Enabled = true,
        NodeIdEnvironmentVariable = "MONITOR_NODE_ID",
        SchedulerLeaseSeconds = 90,
        RefreshLeaseSeconds = 15,
        MaxConflictRetries = 12
    };

    private static SharedStateDistributedLeaseManager Manager(
        ISharedStateDocumentStore store,
        TimeProvider time,
        string nodeId,
        DistributedCoordinationOptions options) =>
        new(store, new NodeIdentity(nodeId), time, options);

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan amount) => _utcNow = _utcNow.Add(amount);
    }

    private sealed class ControlledDelay
    {
        private readonly TaskCompletionSource _releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        public TaskCompletionSource<TimeSpan> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);
            if (call == 1)
            {
                Started.TrySetResult(delay);
                return _releaseFirst.Task.WaitAsync(cancellationToken);
            }

            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public void ReleaseFirst() => _releaseFirst.TrySetResult();
    }

    private sealed class SignalingLeaseManager(IDistributedLeaseManager inner) : IDistributedLeaseManager
    {
        public TaskCompletionSource<DistributedLeaseHandle?> RenewCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DistributedLeaseHandle? ReleasedHandle { get; private set; }

        public Task<DistributedLeaseHandle?> TryAcquireAsync(
            string resource,
            TimeSpan duration,
            CancellationToken cancellationToken = default) =>
            inner.TryAcquireAsync(resource, duration, cancellationToken);

        public async Task<DistributedLeaseHandle?> RenewAsync(
            DistributedLeaseHandle lease,
            CancellationToken cancellationToken = default)
        {
            var renewed = await inner.RenewAsync(lease, cancellationToken);
            RenewCompleted.TrySetResult(renewed);
            return renewed;
        }

        public async Task<bool> ReleaseAsync(
            DistributedLeaseHandle lease,
            CancellationToken cancellationToken = default)
        {
            ReleasedHandle = lease;
            return await inner.ReleaseAsync(lease, cancellationToken);
        }
    }

    private sealed class BlockingCache(Guid registrationId, DateTimeOffset collectedAtUtc) : IServerHealthSnapshotCache
    {
        private readonly SnapshotCacheResult _result = Result(registrationId, collectedAtUtc);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<SnapshotCacheResult> GetAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default) => Task.FromResult(_result);

        public async Task<SnapshotCacheResult> RefreshAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return _result;
        }
    }

    private sealed class CountingCache(Guid registrationId, DateTimeOffset collectedAtUtc) : IServerHealthSnapshotCache
    {
        private readonly SnapshotCacheResult _result = Result(registrationId, collectedAtUtc);
        public int RefreshCalls { get; private set; }

        public Task<SnapshotCacheResult> GetAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default) => Task.FromResult(_result);

        public Task<SnapshotCacheResult> RefreshAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            return Task.FromResult(_result);
        }
    }

    private sealed class CountingObserver : ISnapshotObserver
    {
        public int CallCount { get; private set; }
        public void Observe(SnapshotCacheResult result) => CallCount++;
    }

    private static SnapshotCacheResult Result(Guid registrationId, DateTimeOffset collectedAtUtc) => new(
        new ServerHealthSnapshot(
            registrationId,
            "SQL",
            "17.0",
            "Enterprise",
            null,
            3600,
            1,
            1,
            collectedAtUtc),
        SnapshotFreshness.Fresh,
        TimeSpan.Zero);

    private sealed class MemoryDocumentStore(TimeProvider timeProvider) : ISharedStateDocumentStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, SharedStateDocument> _documents = new(StringComparer.Ordinal);

        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                return Task.FromResult(_documents.TryGetValue(key, out var value) ? value : null);
            }
        }

        public Task<SharedStateWriteResult> CompareExchangeAsync(
            string key,
            long expectedVersion,
            string payloadJson,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (!_documents.TryGetValue(key, out var current))
                {
                    if (expectedVersion != 0)
                    {
                        return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, null));
                    }

                    var created = new SharedStateDocument(key, 1, payloadJson, timeProvider.GetUtcNow());
                    _documents.Add(key, created);
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, created));
                }

                if (current.Version != expectedVersion)
                {
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, current));
                }

                var updated = current with
                {
                    Version = current.Version + 1,
                    PayloadJson = payloadJson,
                    UpdatedAtUtc = timeProvider.GetUtcNow()
                };
                _documents[key] = updated;
                return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, updated));
            }
        }
    }
}
