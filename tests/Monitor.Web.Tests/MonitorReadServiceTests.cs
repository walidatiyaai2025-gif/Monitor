using Monitor.Web.Models;
using Monitor.Web.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class MonitorReadServiceTests
{
    [Fact]
    public async Task EnabledRegistration_ReplacesOnlyFirstDemoCardWithFreshSnapshot()
    {
        var demo = new DemoMonitorService();
        var repository = new InMemoryServerRegistrationRepository();
        var registration = Registration();
        repository.Upsert(registration);
        var cache = new FakeCache(new SnapshotCacheResult(
            Snapshot(registration.Id), SnapshotFreshness.Fresh, TimeSpan.FromSeconds(8)));
        var service = new MonitorReadService(demo, repository, cache);

        var cards = await service.GetServersAsync();

        Assert.Single(cards);
        Assert.Equal(registration.Id.ToString("D"), cards[0].Id);
        Assert.Equal("REAL-SQL01", cards[0].Name);
        Assert.Equal(ServerDataSource.LiveFresh, cards[0].Source);
        Assert.Equal(92, cards[0].MemoryPercent);
        Assert.Equal(8, cards[0].LastScanSecondsAgo);
        Assert.Equal(1, cache.CallCount);
    }

    [Fact]
    public async Task StaleSnapshot_IsExplicitlyWarningAndStale()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var registration = Registration();
        repository.Upsert(registration);
        var service = new MonitorReadService(
            new DemoMonitorService(),
            repository,
            new FakeCache(new SnapshotCacheResult(
                Snapshot(registration.Id), SnapshotFreshness.Stale, TimeSpan.FromMinutes(2))));

        var card = (await service.GetServersAsync())[0];

        Assert.Equal(ServerDataSource.LiveStale, card.Source);
        Assert.Equal(HealthState.Warning, card.State);
    }

    [Fact]
    public async Task CollectionFailure_LeavesClearlyDemoFallback()
    {
        var repository = new InMemoryServerRegistrationRepository();
        repository.Upsert(Registration());
        var service = new MonitorReadService(
            new DemoMonitorService(),
            repository,
            new FakeCache(exception: new SnapshotCollectionException(
                SnapshotCollectionFailure.NetworkUnavailable,
                "The SQL Server could not be reached.")));

        var cards = await service.GetServersAsync();

        Assert.All(cards, card => Assert.Equal(ServerDataSource.RegisteredUnavailable, card.Source));
    }

    [Fact]
    public async Task NoRegistration_DoesNotCallCache()
    {
        var cache = new FakeCache();
        var service = new MonitorReadService(
            new DemoMonitorService(),
            new InMemoryServerRegistrationRepository(),
            cache);

        await service.GetServersAsync();

        Assert.Equal(0, cache.CallCount);
    }

    [Fact]
    public void ConfiguredRegistration_LoadsMetadataWithoutCredentialValues()
    {
        var values = new Dictionary<string, string?>
        {
            ["Monitor:PrimaryServer:Id"] = "33333333-3333-3333-3333-333333333333",
            ["Monitor:PrimaryServer:DisplayName"] = "Primary SQL",
            ["Monitor:PrimaryServer:Host"] = "sql01.internal",
            ["Monitor:PrimaryServer:Port"] = "1433",
            ["Monitor:PrimaryServer:AuthenticationMode"] = "SqlLogin",
            ["Monitor:PrimaryServer:SecretReference"] = "primary-reader"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var registration = ConfiguredServerRegistrationLoader.Load(configuration, TimeProvider.System);
        var json = System.Text.Json.JsonSerializer.Serialize(registration);

        Assert.NotNull(registration);
        Assert.Equal("sql01.internal", registration.Endpoint.Host);
        Assert.DoesNotContain("primary-reader", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthModules_MapExactCachedFactsWithOneCacheRead()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var registration = Registration();
        repository.Upsert(registration);
        var snapshot = Snapshot(registration.Id) with
        {
            Databases = new(1, 0, 0, 1, 0, 0),
            Backups = new(7, 3, DateTimeOffset.UtcNow.AddHours(-1)),
            Jobs = new(12, 10, 2), Storage = new(3000, 2000, 1000),
            Blocking = new(4, 900), Performance = new(5, 2, 1)
        };
        var cache = new FakeCache(new(snapshot, SnapshotFreshness.Stale, TimeSpan.FromSeconds(70)));
        var service = new MonitorReadService(new DemoMonitorService(), repository, cache);

        var row = Assert.Single(await service.GetHealthModulesAsync());

        Assert.Equal(ServerDataSource.LiveStale, row.Source);
        Assert.Equal(3, row.Backups!.MissingFullBackupLast24Hours);
        Assert.Equal(2, row.Jobs!.FailedLastRun);
        Assert.Equal(3000, row.Storage!.TotalAllocatedBytes);
        Assert.Equal(4, row.Blocking!.BlockedRequests);
        Assert.Equal(2, row.Performance!.RunnableTasks);
        Assert.Equal(1, cache.CallCount);
    }

    private static ServerRegistration Registration() => new(
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "Primary SQL",
        new SqlServerEndpoint("private-host"),
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        DateTimeOffset.UtcNow);

    private static ServerHealthSnapshot Snapshot(Guid id) => new(
        id, "REAL-SQL01", "17.0.1", "Enterprise", null,
        3600, 10, 10, DateTimeOffset.UtcNow,
        new MemoryHealthSnapshot(32_000_000, 8_000_000, 12_000_000, 92, true, false, "Low"));

    private sealed class FakeCache(
        SnapshotCacheResult? result = null,
        Exception? exception = null) : IServerHealthSnapshotCache
    {
        public int CallCount { get; private set; }

        public Task<SnapshotCacheResult> GetAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return exception is null
                ? Task.FromResult(result!)
                : Task.FromException<SnapshotCacheResult>(exception);
        }

        public Task<SnapshotCacheResult> RefreshAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default) =>
            GetAsync(registration, cancellationToken);
    }
}
