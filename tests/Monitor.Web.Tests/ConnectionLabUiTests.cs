using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class ConnectionLabUiTests
{
    [Fact]
    public async Task ProviderTimeout_IsReportedAsTimedOut()
    {
        var tester = new ServerConnectionTester(
            new NoSecretStore(),
            new TimeoutProbe());

        var result = await tester.TestAsync(new ServerRegistration(
            Guid.NewGuid(),
            "Integrated SQL",
            new SqlServerEndpoint("sql01.internal", port: 1433),
            SqlAuthenticationMode.IntegratedSecurity,
            null,
            true,
            DateTimeOffset.UtcNow));

        Assert.Equal(ConnectionTestStatus.TimedOut, result.Status);
        Assert.Equal("Connection timed out.", result.Message);
    }

    [Fact]
    public void ConnectionLabSummary_HasNoRawSecretReferenceField()
    {
        var summary = new ConnectionLabRegistrationSummary(
            Guid.NewGuid(),
            "SQL 01",
            "sql01.internal,1433",
            SqlAuthenticationMode.SqlLogin,
            true,
            true,
            true,
            false,
            DateTimeOffset.UtcNow);

        var serialized = System.Text.Json.JsonSerializer.Serialize(summary);

        Assert.Contains("HasSecretReference", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("SecretReference\"", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", serialized, StringComparison.Ordinal);
    }

    private sealed class NoSecretStore : IConnectionSecretStore
    {
        public ValueTask<SqlLoginSecret?> ResolveAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default) => ValueTask.FromResult<SqlLoginSecret?>(null);
    }

    private sealed class TimeoutProbe : ISqlConnectionProbe
    {
        public Task<SqlProbeResult> ProbeAsync(
            ServerRegistration registration,
            SqlLoginSecret? secret,
            CancellationToken cancellationToken) =>
            Task.FromException<SqlProbeResult>(new SqlProbeException(SqlProbeFailureKind.Timeout));
    }
}
