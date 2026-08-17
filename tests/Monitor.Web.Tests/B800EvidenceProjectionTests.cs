using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800EvidenceProjectionTests
{
    [Fact]
    public void TempDbProjection_DerivesOnlyPointInTimeFactsAndPreservesUnknowns()
    {
        var projection = AdvancedEvidenceProjection.BuildTempDb(new TempDbHealthSnapshot(
            8,
            2,
            [
                new TempDbFileSnapshot(1, "tempdev", 100 * 1_048_576L, 50 * 1_048_576L, 10, 5, 100, 100),
                new TempDbFileSnapshot(2, "temp2", 100 * 1_048_576L, null, 10, 5, 100, 100)
            ]));

        Assert.NotNull(projection);
        Assert.Equal(2, projection!.FileCount);
        Assert.Null(projection.UsedPercent);
        Assert.Null(projection.UsedImbalancePercent);
        Assert.Equal(10, projection.ReadLatencyMs);
        Assert.Equal(20, projection.WriteLatencyMs);
        Assert.Equal(8, projection.RecommendedFileCount);
    }

    [Fact]
    public void TempDbProjection_DoesNotRecommendFromTruncatedEvidence()
    {
        var projection = AdvancedEvidenceProjection.BuildTempDb(new TempDbHealthSnapshot(
            64,
            40,
            Enumerable.Range(1, 32)
                .Select(index => new TempDbFileSnapshot(index, $"temp{index}", 1_048_576, 524_288, 0, 0, 0, 0))
                .ToArray(),
            true));

        Assert.NotNull(projection);
        Assert.True(projection!.IsTruncated);
        Assert.Null(projection.RecommendedFileCount);
        Assert.Null(projection.ReadLatencyMs);
        Assert.Null(projection.WriteLatencyMs);
    }

    [Fact]
    public void TransactionLogProjection_DoesNotTurnPartialStatsIntoZeroOrHealthy()
    {
        var rows = AdvancedEvidenceProjection.BuildTransactionLogs(new TransactionLogHealthSnapshot(
            2,
            [
                new TransactionLogDatabaseSnapshot("App", "FULL", 100 * 1_048_576L, 75 * 1_048_576L, 250, 25, "ACTIVE_TRANSACTION", 300, true),
                new TransactionLogDatabaseSnapshot("Replica", "FULL", null, null, null, null, null, 600, false)
            ]));

        Assert.Equal(2, rows.Count);
        Assert.Equal(75, rows[0].UsedPercent);
        Assert.Equal(LogVlfBand.Elevated, rows[0].VlfBand);
        Assert.Equal("ACTIVE_TRANSACTION", rows[0].ReuseWait);
        Assert.True(rows[0].TruncationBlocked);
        Assert.False(rows[1].HasDetailedStats);
        Assert.Null(rows[1].UsedPercent);
        Assert.Null(rows[1].VlfBand);
        Assert.Null(rows[1].ReuseWait);
        Assert.Null(rows[1].TruncationBlocked);
        Assert.Equal(600, rows[1].LogBackupAgeSeconds);
    }

    [Fact]
    public void HaProjection_ReportsObservedFactsWithoutQuorumOrRpoClassification()
    {
        var projection = AdvancedEvidenceProjection.BuildHa(new HaHealthSnapshot(
            true,
            2,
            2,
            [
                new HaReplicaSnapshot("Ag", "sql-a", true, "SYNCHRONOUS_COMMIT", "AUTOMATIC", "PRIMARY", "CONNECTED", "ONLINE", "HEALTHY"),
                new HaReplicaSnapshot("Ag", "sql-b", false, "SYNCHRONOUS_COMMIT", "AUTOMATIC", "SECONDARY", "DISCONNECTED", null, "NOT_HEALTHY")
            ],
            [
                new HaDatabaseReplicaSnapshot("Ag", "sql-a", "App", true, true, "SYNCHRONIZED", "HEALTHY", false, null, 0, 0, 0),
                new HaDatabaseReplicaSnapshot("Ag", "sql-b", "App", false, false, "SYNCHRONIZING", "PARTIALLY_HEALTHY", true, "SUSPEND_FROM_REDO", 128, 64, 9)
            ]));

        Assert.NotNull(projection);
        Assert.True(projection!.IsHadrEnabled);
        Assert.Equal(1, projection.DisconnectedReplicas);
        Assert.Equal(1, projection.UnhealthyReplicas);
        Assert.Equal(1, projection.UnsynchronizedDatabases);
        Assert.Equal(1, projection.SuspendedDatabases);
        Assert.Equal(9, projection.MaxSecondaryLagSeconds);
        Assert.Equal(128, projection.MaxLogSendQueueKb);
        Assert.Equal(64, projection.MaxRedoQueueKb);
    }

    [Fact]
    public async Task MonitorReadService_PassesAdvancedEvidenceToServerAndHealthModuleModels()
    {
        var registration = new ServerRegistration(
            Guid.Parse("70707070-7070-7070-7070-707070707070"),
            "Primary SQL",
            new SqlServerEndpoint("private-host"),
            SqlAuthenticationMode.IntegratedSecurity,
            null,
            true,
            DateTimeOffset.UtcNow);
        var registrations = new InMemoryServerRegistrationRepository();
        registrations.Upsert(registration);

        var tempDb = new TempDbHealthSnapshot(8, 1, [new TempDbFileSnapshot(1, "tempdev", 1_048_576, 524_288, 1, 1, 1, 1)]);
        var logs = new TransactionLogHealthSnapshot(1, [new TransactionLogDatabaseSnapshot("App", "FULL", 1_048_576, 524_288, 8, 2, "NOTHING", 60, true)]);
        var ha = new HaHealthSnapshot(false, 0, 0, [], []);
        var snapshot = new ServerHealthSnapshot(
            registration.Id,
            "REAL-SQL01",
            "17.0.1",
            "Enterprise",
            "MSSQLSERVER",
            3600,
            1,
            1,
            DateTimeOffset.UtcNow,
            TempDb: tempDb,
            TransactionLogs: logs,
            Ha: ha);
        var result = new SnapshotCacheResult(snapshot, SnapshotFreshness.Fresh, TimeSpan.FromSeconds(5));
        var service = new MonitorReadService(new DemoMonitorService(), registrations, new FakeCache(result));

        var details = await service.GetServerAsync(registration.Id.ToString("D"));
        var module = Assert.Single(await service.GetHealthModulesAsync());

        Assert.Same(tempDb, details!.Evidence!.TempDb);
        Assert.Same(logs, details.Evidence.TransactionLogs);
        Assert.Same(ha, details.Evidence.Ha);
        Assert.Same(tempDb, module.TempDb);
        Assert.Same(logs, module.TransactionLogs);
        Assert.Same(ha, module.Ha);
    }

    private sealed class FakeCache(SnapshotCacheResult result) : IServerHealthSnapshotCache
    {
        public SnapshotCacheResult? Peek(Guid registrationId) => result;
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }
}
