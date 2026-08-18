using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SnapshotOperationsServiceTests
{
    private static readonly Guid RegistrationId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void History_DeduplicatesAndKeepsAllowlistedMetrics()
    {
        var store = new InMemorySnapshotHistoryStore(new FixedTimeProvider(Now));
        var result = Result(Now.AddMinutes(-1));
        store.Append(result);
        store.Append(result);
        var point = Assert.Single(store.Read(RegistrationId, TimeSpan.FromHours(1)));
        Assert.Equal(10, point.DatabaseTotal);
        Assert.Equal(85, point.MemoryPercent);
        Assert.Equal(2, point.BlockedRequests);
    }

    [Fact]
    public void History_EvictsOlderThanRetention()
    {
        var store = new InMemorySnapshotHistoryStore(new FixedTimeProvider(Now));
        store.Append(Result(Now.AddHours(-25)));
        store.Append(Result(Now));
        Assert.Single(store.Read(RegistrationId, TimeSpan.FromHours(24)));
    }

    [Fact]
    public void ScheduleOptions_ValidateSafeBounds()
    {
        new SnapshotScheduleOptions().Validate();
        Assert.Throws<InvalidOperationException>(() => new SnapshotScheduleOptions { Interval = TimeSpan.FromSeconds(5) }.Validate());
        Assert.Throws<InvalidOperationException>(() => new SnapshotScheduleOptions { MaxConcurrency = 9 }.Validate());
        Assert.False(new SnapshotScheduleOptions().Enabled);
    }

    [Fact]
    public void TrendRead_AllowsOnlyFixedWindows()
    {
        var repository = new InMemoryServerRegistrationRepository();
        repository.Upsert(new(RegistrationId, "SQL", new("host"), SqlAuthenticationMode.IntegratedSecurity, null, true, Now));
        var store = new InMemorySnapshotHistoryStore(new FixedTimeProvider(Now));
        store.Append(Result(Now));
        var service = new TrendReadService(repository, store);
        Assert.NotNull(service.Read(RegistrationId, "6h"));
        Assert.Null(service.Read(RegistrationId, "7d"));
    }

    [Fact]
    public async Task CollectionCycle_UnexpectedFailurePropagatesAndFinalizesSchedulerStatus()
    {
        var repository = new InMemoryServerRegistrationRepository();
        repository.Upsert(new(RegistrationId, "SQL", new("host"), SqlAuthenticationMode.IntegratedSecurity, null, true, Now));
        var status = new SchedulerStatusStore();
        var cycle = new SnapshotCollectionCycle(
            repository,
            new UnexpectedFailureCache(),
            new NoOpObserver(),
            new SnapshotScheduleOptions { Enabled = true },
            status: status,
            timeProvider: new FixedTimeProvider(Now));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => cycle.RunOnceAsync(CancellationToken.None));
        var terminal = status.Get();

        Assert.Equal("unexpected collection failure", exception.Message);
        Assert.True(terminal.Enabled);
        Assert.False(terminal.Running);
        Assert.Equal(Now, terminal.LastStartedUtc);
        Assert.Equal(Now, terminal.LastCompletedUtc);
        Assert.Equal(1, terminal.Attempted);
        Assert.Equal(0, terminal.Succeeded);
        Assert.Equal(1, terminal.Failed);
        Assert.Equal(0, terminal.SkippedBackoff);
    }

    private static SnapshotCacheResult Result(DateTimeOffset collectedAt) => new(
        new(RegistrationId, "SQL", "17", "Enterprise", null, 100, 10, 10, collectedAt,
            new(1000, 200, 500, 85, false, false, "Available"), Blocking: new(2, 500), Performance: new(3, 1, 0)),
        SnapshotFreshness.Fresh, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    private sealed class UnexpectedFailureCache : IServerHealthSnapshotCache
    {
        public SnapshotCacheResult? Peek(Guid registrationId) => null;

        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) =>
            RefreshAsync(registration, cancellationToken);

        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default) =>
            Task.FromException<SnapshotCacheResult>(new InvalidOperationException("unexpected collection failure"));
    }

    private sealed class NoOpObserver : ISnapshotObserver
    {
        public void Observe(SnapshotCacheResult result) { }
    }
}
