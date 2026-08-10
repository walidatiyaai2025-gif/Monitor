using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class DatabaseHealthProjectionTests
{
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Collector_MapsValidatedDatabaseStateBreakdown()
    {
        var row = new SqlSnapshotRow(
            "SQL01",
            "17.0.1",
            "Enterprise",
            null,
            3600,
            10,
            5,
            DatabaseHealth: new SqlDatabaseHealthRow(
                OnlineCount: 5,
                RestoringCount: 1,
                RecoveringCount: 1,
                RecoveryPendingCount: 1,
                SuspectCount: 1,
                EmergencyCount: 0,
                OfflineCount: 1,
                CopyingCount: 0,
                OfflineSecondaryCount: 0,
                OtherCount: 0,
                ReadOnlyCount: 2));
        var collector = new SqlServerSnapshotCollector(
            new FakeSecretStore(),
            new FakeQuery(row),
            new FixedTimeProvider(CollectedAt));

        var snapshot = await collector.CollectAsync(Registration());

        Assert.NotNull(snapshot.DatabaseHealth);
        Assert.Equal(10, snapshot.DatabaseHealth.TotalCount);
        Assert.Equal(5, snapshot.DatabaseHealth.UnavailableCount);
        Assert.Equal(3, snapshot.DatabaseHealth.RecoveryCount);
        Assert.Equal(2, snapshot.DatabaseHealth.CriticalCount);
        Assert.Equal(1, snapshot.DatabaseHealth.SuspectCount);
        Assert.Equal(2, snapshot.DatabaseHealth.ReadOnlyCount);
    }

    [Fact]
    public async Task Collector_RejectsInconsistentDatabaseStateCounts()
    {
        var row = new SqlSnapshotRow(
            "SQL01",
            "17.0.1",
            "Enterprise",
            null,
            3600,
            3,
            2,
            DatabaseHealth: new SqlDatabaseHealthRow(
                OnlineCount: 2,
                RestoringCount: 0,
                RecoveringCount: 0,
                RecoveryPendingCount: 0,
                SuspectCount: 0,
                EmergencyCount: 0,
                OfflineCount: 0,
                CopyingCount: 0,
                OfflineSecondaryCount: 0,
                OtherCount: 0,
                ReadOnlyCount: 0));
        var collector = new SqlServerSnapshotCollector(
            new FakeSecretStore(),
            new FakeQuery(row),
            new FixedTimeProvider(CollectedAt));

        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(
            () => collector.CollectAsync(Registration()));

        Assert.Equal(SnapshotCollectionFailure.Failed, exception.Failure);
        Assert.Equal("Snapshot collection failed.", exception.Message);
    }

    [Fact]
    public void Query_RemainsOneSelectWithDatabaseStateProjection()
    {
        var sql = SqlSnapshotQuery.CommandText;

        Assert.Equal(1, sql.Split("SELECT", StringSplitOptions.None).Length - 1);
        Assert.Contains("DbSuspectCount", sql, StringComparison.Ordinal);
        Assert.Contains("DbRecoveryPendingCount", sql, StringComparison.Ordinal);
        Assert.Contains("DbOfflineSecondaryCount", sql, StringComparison.Ordinal);
        Assert.Contains("d.is_read_only", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadService_ExposesDatabaseHealthOnlyFromCachedLiveSnapshot()
    {
        var registration = Registration();
        var repository = new InMemoryServerRegistrationRepository();
        repository.Upsert(registration);
        var detail = new DatabaseHealthSnapshot(
            OnlineCount: 8,
            RestoringCount: 0,
            RecoveringCount: 1,
            RecoveryPendingCount: 0,
            SuspectCount: 1,
            EmergencyCount: 0,
            OfflineCount: 0,
            CopyingCount: 0,
            OfflineSecondaryCount: 0,
            OtherCount: 0,
            ReadOnlyCount: 1);
        var snapshot = new ServerHealthSnapshot(
            registration.Id,
            "REAL-SQL01",
            "17.0.1",
            "Enterprise",
            null,
            3600,
            10,
            8,
            CollectedAt,
            DatabaseHealth: detail);
        var service = new MonitorReadService(
            new DemoMonitorService(),
            repository,
            new FakeCache(new SnapshotCacheResult(snapshot, SnapshotFreshness.Fresh, TimeSpan.FromSeconds(4))));

        var card = (await service.GetServersAsync())[0];

        Assert.Equal(ServerDataSource.LiveFresh, card.Source);
        Assert.NotNull(card.DatabaseHealth);
        Assert.Equal(1, card.DatabaseHealth.SuspectCount);
        Assert.Equal(1, card.DatabaseHealth.RecoveryCount);
        Assert.Equal(1, card.DatabaseHealth.ReadOnlyCount);
    }

    private static ServerRegistration Registration() => new(
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        "Database Health SQL",
        new SqlServerEndpoint("sql01.internal", port: 1433),
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        DateTimeOffset.UtcNow);

    private sealed class FakeSecretStore : IConnectionSecretStore
    {
        public ValueTask<SqlLoginSecret?> ResolveAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default) => ValueTask.FromResult<SqlLoginSecret?>(null);
    }

    private sealed class FakeQuery(SqlSnapshotRow row) : ISqlSnapshotQuery
    {
        public Task<SqlSnapshotRow> ExecuteAsync(
            ServerRegistration registration,
            SqlLoginSecret? secret,
            CancellationToken cancellationToken) => Task.FromResult(row);
    }

    private sealed class FakeCache(SnapshotCacheResult result) : IServerHealthSnapshotCache
    {
        public Task<SnapshotCacheResult> GetAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default) => Task.FromResult(result);

        public Task<SnapshotCacheResult> RefreshAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
