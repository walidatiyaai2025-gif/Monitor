using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SnapshotRefreshServiceTests
{
    [Fact]
    public async Task RepeatedRefreshInsideInterval_IsThrottledWithoutCacheCall()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var registration = Registration();
        repository.Upsert(registration);
        var cache = new FakeCache(registration.Id);
        var service = new SnapshotRefreshService(repository, cache, new FixedTimeProvider());

        var first = await service.RefreshAsync(registration.Id);
        var second = await service.RefreshAsync(registration.Id);

        Assert.Equal(SnapshotRefreshStatus.Refreshed, first.Status);
        Assert.Equal(SnapshotRefreshStatus.Throttled, second.Status);
        Assert.Equal(1, cache.RefreshCount);
        Assert.True(second.RetryAfterSeconds > 0);
    }

    [Fact]
    public async Task UnknownRegistration_IsRejectedWithoutCacheCall()
    {
        var cache = new FakeCache(Guid.NewGuid());
        var service = new SnapshotRefreshService(
            new InMemoryServerRegistrationRepository(), cache, new FixedTimeProvider());

        var result = await service.RefreshAsync(Guid.NewGuid());

        Assert.Equal(SnapshotRefreshStatus.RegistrationNotFound, result.Status);
        Assert.Equal(0, cache.RefreshCount);
    }

    private static ServerRegistration Registration() => new(
        Guid.NewGuid(), "SQL", new SqlServerEndpoint("sql01"),
        SqlAuthenticationMode.IntegratedSecurity, null, true, DateTimeOffset.UtcNow);

    private sealed class FakeCache(Guid id) : IServerHealthSnapshotCache
    {
        public int RefreshCount { get; private set; }
        private readonly SnapshotCacheResult _result = new(
            new ServerHealthSnapshot(id, "SQL", "17", "Enterprise", null, 1, 1, 1, DateTimeOffset.UtcNow),
            SnapshotFreshness.Fresh,
            TimeSpan.Zero);

        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => Task.FromResult(_result);
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            RefreshCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
    }
}
