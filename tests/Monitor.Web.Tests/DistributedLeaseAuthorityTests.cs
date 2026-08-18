using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class DistributedLeaseAuthorityTests
{
    [Fact]
    public async Task ForgedCurrentVersionHandle_CannotRenewOrReleasePersistedOtherOwner()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 18, 16, 45, 0, TimeSpan.Zero));
        var store = new MemoryDocumentStore(time);
        var owner = Manager(store, time, "node-b");
        var attacker = Manager(store, time, "node-a");

        var ownerLease = await owner.TryAcquireAsync("scheduler", TimeSpan.FromSeconds(30));
        Assert.NotNull(ownerLease);

        var forged = ownerLease! with { OwnerId = "node-a" };
        Assert.Null(await attacker.RenewAsync(forged));
        Assert.False(await attacker.ReleaseAsync(forged));

        var persisted = await store.ReadAsync("monitor:lease:v1:scheduler");
        Assert.NotNull(persisted);
        Assert.Equal(ownerLease.Version, persisted!.Version);
        Assert.Contains("\"ownerId\":\"node-b\"", persisted.PayloadJson, StringComparison.Ordinal);
        Assert.True(await owner.ReleaseAsync(ownerLease));
    }

    [Fact]
    public async Task ExpiredOwner_CannotRenewOrReleaseUntilFreshAcquire()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 18, 16, 45, 0, TimeSpan.Zero));
        var store = new MemoryDocumentStore(time);
        var owner = Manager(store, time, "node-a");

        var lease = await owner.TryAcquireAsync("scheduler", TimeSpan.FromSeconds(30));
        Assert.NotNull(lease);
        time.Advance(TimeSpan.FromSeconds(31));

        Assert.Null(await owner.RenewAsync(lease!));
        Assert.False(await owner.ReleaseAsync(lease));

        var stale = await store.ReadAsync("monitor:lease:v1:scheduler");
        Assert.NotNull(stale);
        Assert.Equal(lease.Version, stale!.Version);

        var reacquired = await owner.TryAcquireAsync("scheduler", TimeSpan.FromSeconds(30));
        Assert.NotNull(reacquired);
        Assert.True(reacquired!.Version > lease.Version);
        Assert.True(await owner.ReleaseAsync(reacquired));
    }

    [Fact]
    public async Task ActivePersistedOwner_CanRenewAndReleaseNormally()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 18, 16, 45, 0, TimeSpan.Zero));
        var store = new MemoryDocumentStore(time);
        var owner = Manager(store, time, "node-a");

        var lease = await owner.TryAcquireAsync("scheduler", TimeSpan.FromSeconds(30));
        Assert.NotNull(lease);
        time.Advance(TimeSpan.FromSeconds(10));

        var renewed = await owner.RenewAsync(lease!);
        Assert.NotNull(renewed);
        Assert.True(renewed!.Version > lease.Version);
        Assert.True(renewed.ExpiresAtUtc > lease.ExpiresAtUtc);
        Assert.True(await owner.ReleaseAsync(renewed));
    }

    private static SharedStateDistributedLeaseManager Manager(
        ISharedStateDocumentStore store,
        TimeProvider timeProvider,
        string nodeId) =>
        new(
            store,
            new NodeIdentity(nodeId),
            timeProvider,
            new DistributedCoordinationOptions
            {
                Enabled = true,
                NodeIdEnvironmentVariable = "MONITOR_NODE_ID",
                SchedulerLeaseSeconds = 90,
                RefreshLeaseSeconds = 30,
                MaxConflictRetries = 12
            });

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount) => _utcNow = _utcNow.Add(amount);
    }

    private sealed class MemoryDocumentStore(TimeProvider timeProvider) : ISharedStateDocumentStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, SharedStateDocument> _documents = new(StringComparer.Ordinal);

        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                return Task.FromResult(_documents.TryGetValue(key, out var document) ? document : null);
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
