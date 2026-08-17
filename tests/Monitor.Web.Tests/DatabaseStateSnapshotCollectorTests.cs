using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class DatabaseStateSnapshotCollectorTests
{
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CollectAsync_MapsBoundedDatabaseStateRows()
    {
        var states = new[]
        {
            new SqlDatabaseStateRow("AppDb", "ONLINE"),
            new SqlDatabaseStateRow("Warehouse", "RECOVERY_PENDING"),
            new SqlDatabaseStateRow("Legacy", "SUSPECT")
        };
        var modules = new SqlHealthModulesRow(
            0, 0, 1, 1, 0, 0,
            2, 0, CollectedAt.AddHours(-1),
            0, 0, 0,
            10_000, 8_000, 2_000,
            0, 0,
            DatabaseStates: states);
        var collector = new SqlServerSnapshotCollector(
            new FakeSecretStore(new SqlLoginSecret("reader", "password")),
            new FakeQuery(new SqlSnapshotRow("SQL01", "17", "Enterprise", null, 3_600, 7, 5, Modules: modules)),
            new FixedTimeProvider(CollectedAt));

        var snapshot = await collector.CollectAsync(Registration());

        Assert.NotNull(snapshot.Databases?.Items);
        Assert.Equal(3, snapshot.Databases!.Items!.Count);
        Assert.Equal("Warehouse", snapshot.Databases.Items[1].Name);
        Assert.Equal("RECOVERY_PENDING", snapshot.Databases.Items[1].State);
    }

    [Fact]
    public async Task InvalidDatabaseStateEvidenceFailsClosed()
    {
        var states = new[] { new SqlDatabaseStateRow("", "ONLINE") };
        var modules = new SqlHealthModulesRow(
            0, 0, 0, 0, 0, 0,
            1, 0, CollectedAt,
            0, 0, 0,
            10, 8, 2,
            0, 0,
            DatabaseStates: states);
        var collector = new SqlServerSnapshotCollector(
            new FakeSecretStore(new SqlLoginSecret("reader", "password")),
            new FakeQuery(new SqlSnapshotRow("SQL01", "17", "Enterprise", null, 100, 5, 5, Modules: modules)),
            new FixedTimeProvider(CollectedAt));

        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(() => collector.CollectAsync(Registration()));

        Assert.Equal(SnapshotCollectionFailure.Failed, exception.Failure);
    }

    [Fact]
    public void QueryUsesBoundedLogicalDatabaseStateOnly()
    {
        var sql = SqlSnapshotQuery.CommandText;

        Assert.Contains("DatabaseStatesJson", sql, StringComparison.Ordinal);
        Assert.Contains("TOP (50)", sql, StringComparison.Ordinal);
        Assert.Contains("x.name", sql, StringComparison.Ordinal);
        Assert.Contains("x.state_desc", sql, StringComparison.Ordinal);
        Assert.Contains("x.database_id > 4", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("sys.tables", sql, StringComparison.OrdinalIgnoreCase);
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
