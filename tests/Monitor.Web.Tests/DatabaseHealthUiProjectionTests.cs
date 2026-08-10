using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class DatabaseHealthUiProjectionTests
{
    [Fact]
    public async Task ReadService_ProjectsCachedDatabaseDetailIntoLiveServerCard()
    {
        var registration = Registration();
        var repository = new InMemoryServerRegistrationRepository();
        repository.Upsert(registration);
        var databaseDetail = new DatabaseHealthDetailSnapshot(
            Restoring: 1,
            Recovering: 2,
            RecoveryPending: 1,
            Suspect: 1,
            Emergency: 0,
            OfflineOrOther: 1);
        var snapshot = new ServerHealthSnapshot(
            registration.Id,
            "REAL-SQL01",
            "17.0.1",
            "Enterprise",
            null,
            3600,
            12,
            6,
            DateTimeOffset.UtcNow,
            Databases: databaseDetail);
        var service = new MonitorReadService(
            new DemoMonitorService(),
            repository,
            new FakeCache(new SnapshotCacheResult(snapshot, SnapshotFreshness.Fresh, TimeSpan.FromSeconds(5))));

        var card = (await service.GetServersAsync())[0];

        Assert.Equal(ServerDataSource.LiveFresh, card.Source);
        Assert.Same(databaseDetail, card.DatabaseHealth);
        Assert.Equal(1, card.DatabaseHealth!.Restoring);
        Assert.Equal(2, card.DatabaseHealth.Recovering);
        Assert.Equal(1, card.DatabaseHealth.Suspect);
        Assert.Equal(1, card.DatabaseHealth.OfflineOrOther);
    }

    [Fact]
    public async Task DemoFallback_DoesNotFabricateDatabaseDetail()
    {
        var service = new MonitorReadService(
            new DemoMonitorService(),
            new InMemoryServerRegistrationRepository(),
            new FakeCache());

        var cards = await service.GetServersAsync();

        Assert.All(cards, card => Assert.Null(card.DatabaseHealth));
        Assert.All(cards, card => Assert.Equal(ServerDataSource.Demo, card.Source));
    }

    [Fact]
    public async Task ProviderTimeout_RemainsSnapshotTimeout()
    {
        var collector = new SqlServerSnapshotCollector(
            new NoSecretStore(),
            new TimeoutQuery(),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(
            () => collector.CollectAsync(Registration()));

        Assert.Equal(SnapshotCollectionFailure.TimedOut, exception.Failure);
        Assert.Equal("Snapshot collection timed out.", exception.Message);
    }

    private static ServerRegistration Registration() => new(
        Guid.Parse("55555555-5555-5555-5555-555555555555"),
        "Database UI SQL",
        new SqlServerEndpoint("sql01.internal", port: 1433),
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        DateTimeOffset.UtcNow);

    private sealed class FakeCache(SnapshotCacheResult? result = null) : IServerHealthSnapshotCache
    {
        public Task<SnapshotCacheResult> GetAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default) => Task.FromResult(result!);

        public Task<SnapshotCacheResult> RefreshAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default) => Task.FromResult(result!);
    }

    private sealed class NoSecretStore : IConnectionSecretStore
    {
        public ValueTask<SqlLoginSecret?> ResolveAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default) => ValueTask.FromResult<SqlLoginSecret?>(null);
    }

    private sealed class TimeoutQuery : ISqlSnapshotQuery
    {
        public Task<SqlSnapshotRow> ExecuteAsync(
            ServerRegistration registration,
            SqlLoginSecret? secret,
            CancellationToken cancellationToken) =>
            Task.FromException<SqlSnapshotRow>(new SqlProbeException(SqlProbeFailureKind.Timeout));
    }
}
