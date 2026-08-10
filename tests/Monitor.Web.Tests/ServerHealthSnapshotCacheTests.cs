using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class ServerHealthSnapshotCacheTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 10, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FreshSnapshot_IsReusedWithoutAnotherCollection()
    {
        var clock = new MutableTimeProvider(Start);
        var collector = new FakeCollector(Snapshot(Start));
        var cache = new ServerHealthSnapshotCache(collector, clock);
        var registration = Registration();

        var first = await cache.GetAsync(registration);
        clock.Advance(ServerHealthSnapshotCache.FreshFor);
        var second = await cache.GetAsync(registration);

        Assert.Equal(SnapshotFreshness.Fresh, first.Freshness);
        Assert.Equal(SnapshotFreshness.Fresh, second.Freshness);
        Assert.Equal(1, collector.CallCount);
    }

    [Fact]
    public async Task Peek_ReturnsLatestWithoutCallingCollector()
    {
        var clock = new MutableTimeProvider(Start);
        var collector = new FakeCollector(Snapshot(Start));
        var cache = new ServerHealthSnapshotCache(collector, clock);
        var registration = Registration();

        Assert.Null(cache.Peek(registration.Id));
        await cache.RefreshAsync(registration);
        clock.Advance(TimeSpan.FromSeconds(31));
        var result = cache.Peek(registration.Id);

        Assert.NotNull(result);
        Assert.Equal(SnapshotFreshness.Stale, result.Freshness);
        Assert.Equal(1, collector.CallCount);
    }

    [Fact]
    public async Task ConcurrentMiss_UsesOneCollectorFlight()
    {
        var gate = new TaskCompletionSource<ServerHealthSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var collector = new FakeCollector(gate.Task);
        var cache = new ServerHealthSnapshotCache(collector, new MutableTimeProvider(Start));
        var registration = Registration();

        var requests = Enumerable.Range(0, 20).Select(_ => cache.GetAsync(registration)).ToArray();
        await collector.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));
        gate.SetResult(Snapshot(Start));
        await Task.WhenAll(requests);

        Assert.Equal(1, collector.CallCount);
    }

    [Fact]
    public async Task FailedRefresh_ReturnsRetainedSnapshotAsStale()
    {
        var clock = new MutableTimeProvider(Start);
        var collector = new FakeCollector(Snapshot(Start));
        var cache = new ServerHealthSnapshotCache(collector, clock);
        var registration = Registration();
        await cache.GetAsync(registration);

        clock.Advance(ServerHealthSnapshotCache.FreshFor + TimeSpan.FromTicks(1));
        collector.Next = Task.FromException<ServerHealthSnapshot>(new SnapshotCollectionException(
            SnapshotCollectionFailure.NetworkUnavailable,
            "The SQL Server could not be reached."));

        var result = await cache.GetAsync(registration);

        Assert.Equal(SnapshotFreshness.Stale, result.Freshness);
        Assert.Equal(2, collector.CallCount);
    }

    [Fact]
    public async Task CancelledWaiter_DoesNotCancelSharedCollection()
    {
        var gate = new TaskCompletionSource<ServerHealthSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var collector = new FakeCollector(gate.Task);
        var cache = new ServerHealthSnapshotCache(collector, new MutableTimeProvider(Start));
        var registration = Registration();
        using var source = new CancellationTokenSource();

        var cancelledWaiter = cache.GetAsync(registration, source.Token);
        var survivingWaiter = cache.GetAsync(registration);
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledWaiter);
        gate.SetResult(Snapshot(Start));
        var result = await survivingWaiter;

        Assert.Equal(SnapshotFreshness.Fresh, result.Freshness);
        Assert.Equal(1, collector.CallCount);
    }

    private static ServerRegistration Registration() => new(
        Snapshot(Start).RegistrationId,
        "SQL 01",
        new SqlServerEndpoint("sql01.internal"),
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        Start);

    private static ServerHealthSnapshot Snapshot(DateTimeOffset collectedAt) => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "SQL01", "17.0.1", "Enterprise", null, 3600, 12, 12, collectedAt);

    private sealed class FakeCollector : ISqlServerSnapshotCollector
    {
        public FakeCollector(ServerHealthSnapshot snapshot) : this(Task.FromResult(snapshot)) { }
        public FakeCollector(Task<ServerHealthSnapshot> next) => Next = next;

        public int CallCount { get; private set; }
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ServerHealthSnapshot> Next { get; set; }

        public Task<ServerHealthSnapshot> CollectAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Called.TrySetResult();
            return Next;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
        public void Advance(TimeSpan by) => value += by;
    }
}
