using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class IoSnapshotCollectorTests
{
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CollectAsync_MapsBoundedLogicalFileIoEvidence()
    {
        var io = new[]
        {
            new SqlIoFileRow("AppDb/AppDb_Data", 100, 50, 2_000, 2_500, 104_857_600, 52_428_800),
            new SqlIoFileRow("AppDb/AppDb_Log", 10, 200, 100, 4_000, 1_048_576, 104_857_600)
        };
        var modules = new SqlHealthModulesRow(
            0, 0, 0, 0, 0, 0,
            2, 0, CollectedAt.AddHours(-1),
            2, 2, 0,
            10_000, 8_000, 2_000,
            0, 0,
            io);
        var collector = new SqlServerSnapshotCollector(
            new FakeSecretStore(new SqlLoginSecret("reader", "password")),
            new FakeQuery(new SqlSnapshotRow("SQL01", "17", "Enterprise", null, 3_600, 2, 2, Modules: modules)),
            new FixedTimeProvider(CollectedAt));

        var snapshot = await collector.CollectAsync(Registration());

        Assert.NotNull(snapshot.Storage?.IoFiles);
        Assert.Equal(2, snapshot.Storage!.IoFiles!.Count);
        Assert.Equal("AppDb/AppDb_Data", snapshot.Storage.IoFiles[0].FileKey);
        Assert.Equal(104_857_600, snapshot.Storage.IoFiles[0].BytesRead);
    }

    [Fact]
    public async Task InvalidIoEvidenceFailsClosed()
    {
        var io = new[] { new SqlIoFileRow("AppDb/Data", 1, 1, -1, 0, 1, 1) };
        var modules = new SqlHealthModulesRow(
            0, 0, 0, 0, 0, 0,
            1, 0, CollectedAt,
            0, 0, 0,
            10, 8, 2,
            0, 0,
            io);
        var collector = new SqlServerSnapshotCollector(
            new FakeSecretStore(new SqlLoginSecret("reader", "password")),
            new FakeQuery(new SqlSnapshotRow("SQL01", "17", "Enterprise", null, 100, 1, 1, Modules: modules)),
            new FixedTimeProvider(CollectedAt));

        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(() => collector.CollectAsync(Registration()));

        Assert.Equal(SnapshotCollectionFailure.Failed, exception.Failure);
    }

    [Fact]
    public void QueryCollectsLogicalIdentityAndNeverPhysicalPath()
    {
        var sql = SqlSnapshotQuery.CommandText;

        Assert.Contains("sys.dm_io_virtual_file_stats", sql, StringComparison.Ordinal);
        Assert.Contains("sys.master_files", sql, StringComparison.Ordinal);
        Assert.Contains("IoFilesJson", sql, StringComparison.Ordinal);
        Assert.Contains("TOP (12)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("physical_name", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static ServerRegistration Registration() => new(
        Guid.NewGuid(), "SQL 01", new SqlServerEndpoint("sql01.internal", port: 1433),
        SqlAuthenticationMode.SqlLogin, new ConnectionSecretReference("sql01-login"),
        true, DateTimeOffset.UtcNow);

    private sealed class FakeSecretStore(SqlLoginSecret? secret) : IConnectionSecretStore
    {
        public ValueTask<SqlLoginSecret?> ResolveAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default) => ValueTask.FromResult(secret);
    }

    private sealed class FakeQuery(SqlSnapshotRow row) : ISqlSnapshotQuery
    {
        public Task<SqlSnapshotRow> ExecuteAsync(ServerRegistration registration, SqlLoginSecret? secret, CancellationToken cancellationToken) => Task.FromResult(row);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
