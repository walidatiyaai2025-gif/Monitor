using System.Text.Json;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SqlServerSnapshotCollectorTests
{
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 10, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CollectAsync_MapsOneRowIntoSafeSnapshot()
    {
        const string password = "sensitive-password";
        var query = new FakeQuery(new SqlSnapshotRow(
            "SQL01", "17.0.1", "Enterprise Edition", null,
            3600, 12, 11));
        var collector = new SqlServerSnapshotCollector(
            new FakeSecretStore(new SqlLoginSecret("monitor_reader", password)),
            query,
            new FixedTimeProvider(CollectedAt));
        var registration = SqlLoginRegistration();

        var snapshot = await collector.CollectAsync(registration);
        var json = JsonSerializer.Serialize(snapshot);

        Assert.Equal(registration.Id, snapshot.RegistrationId);
        Assert.Equal("SQL01", snapshot.ServerName);
        Assert.Equal(3600, snapshot.UptimeSeconds);
        Assert.Equal(12, snapshot.DatabaseTotal);
        Assert.Equal(11, snapshot.DatabaseOnline);
        Assert.Equal(CollectedAt, snapshot.CollectedAtUtc);
        Assert.Equal(1, query.CallCount);
        Assert.DoesNotContain(password, json, StringComparison.Ordinal);
        Assert.DoesNotContain("sql01-login", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingSecret_FailsClosedWithoutExecutingQuery()
    {
        var query = new FakeQuery();
        var collector = new SqlServerSnapshotCollector(
            new FakeSecretStore(null), query, new FixedTimeProvider(CollectedAt));

        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(
            () => collector.CollectAsync(SqlLoginRegistration()));

        Assert.Equal(SnapshotCollectionFailure.SecretUnavailable, exception.Failure);
        Assert.Equal(0, query.CallCount);
    }

    [Fact]
    public async Task InvalidCounts_AreRejectedWithSafeFailure()
    {
        var query = new FakeQuery(new SqlSnapshotRow(
            "SQL01", "17.0.1", "Enterprise", null, 10, 2, 3));
        var collector = new SqlServerSnapshotCollector(
            new FakeSecretStore(new SqlLoginSecret("user", "password")),
            query,
            new FixedTimeProvider(CollectedAt));

        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(
            () => collector.CollectAsync(SqlLoginRegistration()));

        Assert.Equal(SnapshotCollectionFailure.Failed, exception.Failure);
        Assert.Equal("Snapshot collection failed.", exception.Message);
    }

    [Fact]
    public async Task CallerCancellation_IsPropagated()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var collector = new SqlServerSnapshotCollector(
            new FakeSecretStore(new SqlLoginSecret("user", "password")),
            new FakeQuery(exception: new OperationCanceledException(source.Token)),
            new FixedTimeProvider(CollectedAt));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => collector.CollectAsync(SqlLoginRegistration(), source.Token));
    }

    [Fact]
    public void Query_IsSingleLightweightStatementWithRequiredProjections()
    {
        var sql = SqlSnapshotQuery.CommandText;

        Assert.Equal(1, Count(sql, "SELECT"));
        Assert.Contains("SERVERPROPERTY('ServerName')", sql, StringComparison.Ordinal);
        Assert.Contains("sys.databases", sql, StringComparison.Ordinal);
        Assert.Contains("sys.dm_os_sys_info", sql, StringComparison.Ordinal);
        Assert.Contains("DatabaseOnline", sql, StringComparison.Ordinal);
    }

    private static int Count(string value, string token) =>
        value.Split(token, StringSplitOptions.None).Length - 1;

    private static ServerRegistration SqlLoginRegistration() => new(
        Guid.NewGuid(), "SQL 01", new SqlServerEndpoint("sql01.internal", port: 1433),
        SqlAuthenticationMode.SqlLogin, new ConnectionSecretReference("sql01-login"),
        true, DateTimeOffset.UtcNow);

    private sealed class FakeSecretStore(SqlLoginSecret? secret) : IConnectionSecretStore
    {
        public ValueTask<SqlLoginSecret?> ResolveAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(secret);
    }

    private sealed class FakeQuery(
        SqlSnapshotRow? row = null,
        Exception? exception = null) : ISqlSnapshotQuery
    {
        public int CallCount { get; private set; }

        public Task<SqlSnapshotRow> ExecuteAsync(
            ServerRegistration registration,
            SqlLoginSecret? secret,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return exception is null
                ? Task.FromResult(row ?? new SqlSnapshotRow("SQL", "17", "Developer", null, 1, 1, 1))
                : Task.FromException<SqlSnapshotRow>(exception);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
