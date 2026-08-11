using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class P04CollectorFailureTests
{
    [Fact]
    public async Task PermissionFailure_IsMappedToSafePermissionDenied()
    {
        var collector = Collector(new SqlProbeException(SqlProbeFailureKind.Permission));

        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(
            () => collector.CollectAsync(Registration()));

        Assert.Equal(SnapshotCollectionFailure.PermissionDenied, exception.Failure);
        Assert.Equal(
            "The SQL Server login does not have the required monitoring permissions.",
            exception.Message);
    }

    [Fact]
    public async Task ProviderTimeout_IsMappedToBoundedTimeoutStatus()
    {
        var collector = Collector(new SqlProbeException(SqlProbeFailureKind.Timeout));

        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(
            () => collector.CollectAsync(Registration()));

        Assert.Equal(SnapshotCollectionFailure.TimedOut, exception.Failure);
        Assert.Equal("Snapshot collection timed out.", exception.Message);
    }

    [Fact]
    public async Task PermissionFailure_DoesNotExposeProviderTextOrSecret()
    {
        const string password = "p0-sensitive-password";
        var collector = new SqlServerSnapshotCollector(
            new StaticSecretStore(new SqlLoginSecret("p0-reader", password)),
            new ThrowingQuery(new SqlProbeException(SqlProbeFailureKind.Permission)),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(
            () => collector.CollectAsync(Registration()));

        Assert.DoesNotContain(password, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("p0-reader", exception.Message, StringComparison.Ordinal);
    }

    private static SqlServerSnapshotCollector Collector(Exception exception) => new(
        new StaticSecretStore(new SqlLoginSecret("reader", "secret")),
        new ThrowingQuery(exception),
        TimeProvider.System);

    private static ServerRegistration Registration() => new(
        Guid.Parse("51515151-5151-5151-5151-515151515151"),
        "P0 permission target",
        new SqlServerEndpoint("sql-p0.internal", 1433, encrypt: true, trustServerCertificate: true),
        SqlAuthenticationMode.SqlLogin,
        new ConnectionSecretReference("p0-permission-secret"),
        true,
        DateTimeOffset.UtcNow);

    private sealed class StaticSecretStore(SqlLoginSecret secret) : IConnectionSecretStore
    {
        public ValueTask<SqlLoginSecret?> ResolveAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<SqlLoginSecret?>(secret);
        }
    }

    private sealed class ThrowingQuery(Exception exception) : ISqlSnapshotQuery
    {
        public Task<SqlSnapshotRow> ExecuteAsync(
            ServerRegistration registration,
            SqlLoginSecret? secret,
            CancellationToken cancellationToken) =>
            Task.FromException<SqlSnapshotRow>(exception);
    }
}