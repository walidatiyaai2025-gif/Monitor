using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class IoLatencyProjectionTests
{
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 17, 9, 15, 0, TimeSpan.Zero);

    [Fact]
    public void Build_UsesCumulativeCountersAndUptimeWithoutInventingIntervalHistory()
    {
        const long mb = 1024L * 1024L;
        var storage = new StorageHealthSnapshot(
            1_000,
            800,
            200,
            [
                new IoFileSnapshot("db1/data", 100, 100, 1_000, 3_000, 100 * mb, 50 * mb),
                new IoFileSnapshot("db2/log", 10, 0, 500, 0, 0, 0)
            ]);

        var result = IoLatencyProjection.Build(storage, 10, 8);

        Assert.Equal(2, result.Count);
        Assert.Equal("db2/log", result[0].FileKey);
        Assert.Equal(50d, result[0].WeightedLatencyMs);
        Assert.Equal(0d, result[0].ThroughputMbPerSecond);
        Assert.Equal(IoLatencyBand.Severe, result[0].LatencyBand);
        Assert.Equal(B400Severity.Critical, result[0].Severity);
        Assert.True(result[0].Hotspot);

        Assert.Equal("db1/data", result[1].FileKey);
        Assert.Equal(20d, result[1].WeightedLatencyMs);
        Assert.Equal(15d, result[1].ThroughputMbPerSecond);
        Assert.Equal(50d, result[1].WriteSharePercent);
        Assert.Equal(IoLatencyBand.High, result[1].LatencyBand);
        Assert.Equal(B400Severity.Info, result[1].Severity);
        Assert.False(result[1].Hotspot);
    }

    [Fact]
    public void Build_MissingFilesOrUptimeReturnsNoSyntheticHealthyRows()
    {
        Assert.Empty(IoLatencyProjection.Build(new StorageHealthSnapshot(10, 8, 2), 3_600));

        var storage = new StorageHealthSnapshot(
            10,
            8,
            2,
            [new IoFileSnapshot("db/data", 1, 1, 1, 1, 1, 1)]);

        Assert.Empty(IoLatencyProjection.Build(storage, null));
        Assert.Empty(IoLatencyProjection.Build(storage, 0));
    }

    [Fact]
    public async Task Collector_MapsBoundedLogicalFileIoEvidence()
    {
        var modules = ValidModules([
            new SqlIoFileRow("db1/data", 11, 7, 120, 240, 4_096, 8_192)
        ]);
        var collector = CollectorFor(modules);

        var snapshot = await collector.CollectAsync(SqlLoginRegistration());

        var file = Assert.Single(snapshot.Storage!.IoFiles!);
        Assert.Equal("db1/data", file.FileKey);
        Assert.Equal(11, file.Reads);
        Assert.Equal(7, file.Writes);
        Assert.Equal(120, file.ReadStallMs);
        Assert.Equal(240, file.WriteStallMs);
        Assert.Equal(4_096, file.BytesRead);
        Assert.Equal(8_192, file.BytesWritten);
    }

    [Fact]
    public async Task Collector_InvalidOrOverBoundFileIoEvidenceFailsClosed()
    {
        var invalid = CollectorFor(ValidModules([
            new SqlIoFileRow("db1/data", -1, 0, 0, 0, 0, 0)
        ]));
        var invalidFailure = await Assert.ThrowsAsync<SnapshotCollectionException>(() => invalid.CollectAsync(SqlLoginRegistration()));
        Assert.Equal(SnapshotCollectionFailure.Failed, invalidFailure.Failure);

        var tooMany = CollectorFor(ValidModules(
            Enumerable.Range(1, 13)
                .Select(index => new SqlIoFileRow($"db{index}/data", index, index, index, index, index, index))
                .ToArray()));
        var boundFailure = await Assert.ThrowsAsync<SnapshotCollectionException>(() => tooMany.CollectAsync(SqlLoginRegistration()));
        Assert.Equal(SnapshotCollectionFailure.Failed, boundFailure.Failure);
    }

    [Fact]
    public void StorageView_WiresCachedB400FileIoWithTruthfulSafetyBoundary()
    {
        var root = FindRoot();
        var view = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Views/Operations/Storage.cshtml"));
        var controller = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Controllers/OperationsController.cs"));

        Assert.Contains("IoLatencyProjection.Build", view, StringComparison.Ordinal);
        Assert.Contains("B400 FILE I/O", view, StringComparison.Ordinal);
        Assert.Contains("Cumulative file latency and throughput", view, StringComparison.Ordinal);
        Assert.Contains("No healthy I/O state is inferred", view, StringComparison.Ordinal);
        Assert.Contains("Physical filesystem paths are never collected or rendered", view, StringComparison.Ordinal);
        Assert.Contains("it is not a recent interval rate or persisted trend", view, StringComparison.Ordinal);
        Assert.Contains("GET navigation does not contact monitored SQL", view, StringComparison.Ordinal);
        Assert.Contains("GetHealthModulesAsync(cancellationToken)", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT ", view, StringComparison.OrdinalIgnoreCase);
    }

    private static SqlHealthModulesRow ValidModules(IReadOnlyList<SqlIoFileRow> ioFiles) => new(
        0, 0, 0, 0, 0, 0,
        1, 0, CollectedAt.AddHours(-1),
        1, 1, 0,
        3_000, 2_000, 1_000,
        0, 0,
        ioFiles);

    private static SqlServerSnapshotCollector CollectorFor(SqlHealthModulesRow modules)
    {
        var row = new SqlSnapshotRow("SQL01", "17", "Enterprise", null, 3_600, 1, 1, Modules: modules);
        return new SqlServerSnapshotCollector(
            new FakeSecretStore(new SqlLoginSecret("user", "password")),
            new FakeQuery(row),
            new FixedTimeProvider(CollectedAt));
    }

    private static ServerRegistration SqlLoginRegistration() => new(
        Guid.NewGuid(),
        "SQL 01",
        new SqlServerEndpoint("sql01.internal", port: 1433),
        SqlAuthenticationMode.SqlLogin,
        new ConnectionSecretReference("sql01-login"),
        true,
        DateTimeOffset.UtcNow);

    private sealed class FakeSecretStore(SqlLoginSecret? secret) : IConnectionSecretStore
    {
        public ValueTask<SqlLoginSecret?> ResolveAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(secret);
    }

    private sealed class FakeQuery(SqlSnapshotRow row) : ISqlSnapshotQuery
    {
        public Task<SqlSnapshotRow> ExecuteAsync(
            ServerRegistration registration,
            SqlLoginSecret? secret,
            CancellationToken cancellationToken) => Task.FromResult(row);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
