using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SqlConnectionTesterTests
{
    [Fact]
    public async Task MissingSqlLoginSecret_FailsClosedWithoutCallingProbe()
    {
        var secretStore = new FakeSecretStore(null);
        var probe = new FakeProbe();
        var tester = new ServerConnectionTester(secretStore, probe);

        var result = await tester.TestAsync(SqlLoginRegistration());

        Assert.Equal(ConnectionTestStatus.SecretUnavailable, result.Status);
        Assert.Equal(0, probe.CallCount);
    }

    [Fact]
    public async Task DisabledRegistration_IsRejectedBeforeSecretResolution()
    {
        var secretStore = new FakeSecretStore(new SqlLoginSecret("user", "password"));
        var probe = new FakeProbe();
        var tester = new ServerConnectionTester(secretStore, probe);
        var registration = SqlLoginRegistration(isEnabled: false);

        var result = await tester.TestAsync(registration);

        Assert.Equal(ConnectionTestStatus.Disabled, result.Status);
        Assert.Equal(0, secretStore.CallCount);
        Assert.Equal(0, probe.CallCount);
    }

    [Fact]
    public async Task SuccessfulProbe_ReturnsOnlySafeResult()
    {
        const string password = "sensitive-password";
        var tester = new ServerConnectionTester(
            new FakeSecretStore(new SqlLoginSecret("monitor_reader", password)),
            new FakeProbe(new SqlProbeResult("17.0.1")));

        var result = await tester.TestAsync(SqlLoginRegistration());
        var serialized = System.Text.Json.JsonSerializer.Serialize(result);

        Assert.True(result.Succeeded);
        Assert.Equal("17.0.1", result.ServerVersion);
        Assert.DoesNotContain(password, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("sql01-login", serialized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData((int)SqlProbeFailureKind.Authentication, ConnectionTestStatus.AuthenticationFailed)]
    [InlineData((int)SqlProbeFailureKind.Network, ConnectionTestStatus.NetworkUnavailable)]
    [InlineData((int)SqlProbeFailureKind.Certificate, ConnectionTestStatus.CertificateRejected)]
    [InlineData((int)SqlProbeFailureKind.Other, ConnectionTestStatus.Failed)]
    public async Task ProbeFailures_AreMappedToSafeResults(
        int failure,
        ConnectionTestStatus expected)
    {
        var tester = new ServerConnectionTester(
            new FakeSecretStore(new SqlLoginSecret("user", "password")),
            new FakeProbe(exception: new SqlProbeException((SqlProbeFailureKind)failure)));

        var result = await tester.TestAsync(SqlLoginRegistration());

        Assert.Equal(expected, result.Status);
        Assert.DoesNotContain("password", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CallerCancellation_IsPropagated()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var tester = new ServerConnectionTester(
            new FakeSecretStore(new SqlLoginSecret("user", "password")),
            new FakeProbe(exception: new OperationCanceledException(source.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => tester.TestAsync(SqlLoginRegistration(), source.Token));
    }

    [Fact]
    public async Task UnexpectedProviderFailure_DoesNotExposeExceptionText()
    {
        const string hostileText = "Password=sensitive-password;Server=private-host";
        var tester = new ServerConnectionTester(
            new FakeSecretStore(new SqlLoginSecret("user", "sensitive-password")),
            new FakeProbe(exception: new InvalidOperationException(hostileText)));

        var result = await tester.TestAsync(SqlLoginRegistration());

        Assert.Equal(ConnectionTestStatus.Failed, result.Status);
        Assert.DoesNotContain(hostileText, result.Message, StringComparison.Ordinal);
        Assert.Equal("Connection failed.", result.Message);
    }

    private static ServerRegistration SqlLoginRegistration(bool isEnabled = true) => new(
        Guid.NewGuid(),
        "SQL 01",
        new SqlServerEndpoint("sql01.internal", port: 1433),
        SqlAuthenticationMode.SqlLogin,
        new ConnectionSecretReference("sql01-login"),
        isEnabled,
        DateTimeOffset.UtcNow);

    private sealed class FakeSecretStore(SqlLoginSecret? secret) : IConnectionSecretStore
    {
        public int CallCount { get; private set; }

        public ValueTask<SqlLoginSecret?> ResolveAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(secret);
        }
    }

    private sealed class FakeProbe(
        SqlProbeResult? result = null,
        Exception? exception = null) : ISqlConnectionProbe
    {
        public int CallCount { get; private set; }

        public Task<SqlProbeResult> ProbeAsync(
            ServerRegistration registration,
            SqlLoginSecret? secret,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return exception is null
                ? Task.FromResult(result ?? new SqlProbeResult(null))
                : Task.FromException<SqlProbeResult>(exception);
        }
    }
}
