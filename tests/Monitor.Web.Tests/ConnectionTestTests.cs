using Microsoft.Data.SqlClient;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class ConnectionTestTests
{
    [Fact]
    public async Task IntegratedProfile_DoesNotResolveSecret_AndUsesBoundedProbeSettings()
    {
        var secretStore = new FakeSecretStore();
        var factory = new SqlConnectionProfileFactory(secretStore);
        var registration = Registration(
            new SqlServerEndpoint("sql01.internal", port: 1433, encrypt: true, trustServerCertificate: false),
            SqlAuthenticationMode.IntegratedSecurity);

        var profile = await factory.BuildAsync(registration);

        Assert.True(profile.Success);
        Assert.Equal(0, secretStore.ResolveCount);
        var builder = new SqlConnectionStringBuilder(profile.ConnectionString!);
        Assert.True(builder.IntegratedSecurity);
        Assert.Equal("sql01.internal,1433", builder.DataSource);
        Assert.Equal("master", builder.InitialCatalog);
        Assert.Equal(5, builder.ConnectTimeout);
        Assert.Equal(0, builder.ConnectRetryCount);
        Assert.False(builder.Pooling);
        Assert.True((bool)builder.Encrypt);
        Assert.False(builder.TrustServerCertificate);
        Assert.True(string.IsNullOrEmpty(builder.UserID));
        Assert.True(string.IsNullOrEmpty(builder.Password));
    }

    [Fact]
    public async Task SqlLoginProfile_ResolvesExternalSecret_OnlyInsideProfileFactory()
    {
        const string password = "external-only-password";
        var secretStore = new FakeSecretStore(new SqlLoginSecret("monitor_reader", password));
        var factory = new SqlConnectionProfileFactory(secretStore);
        var registration = Registration(
            new SqlServerEndpoint("sql01.internal", instanceName: "APP01"),
            SqlAuthenticationMode.SqlLogin,
            new ConnectionSecretReference("prod-sql"));

        var profile = await factory.BuildAsync(registration);

        Assert.True(profile.Success);
        Assert.Equal(1, secretStore.ResolveCount);
        var builder = new SqlConnectionStringBuilder(profile.ConnectionString!);
        Assert.False(builder.IntegratedSecurity);
        Assert.Equal("sql01.internal\\APP01", builder.DataSource);
        Assert.Equal("monitor_reader", builder.UserID);
        Assert.Equal(password, builder.Password);
    }

    [Fact]
    public async Task MissingSqlSecret_FailsBeforeAnyNetworkProbe()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var registration = Registration(
            new SqlServerEndpoint("sql01.internal"),
            SqlAuthenticationMode.SqlLogin,
            new ConnectionSecretReference("missing-secret"));
        repository.Upsert(registration);

        var secretStore = new FakeSecretStore();
        var probe = new FakeProbe(new SqlProbeOutcome(true, "should-not-run", "0"));
        ISqlConnectionTester tester = new SqlConnectionTester(
            repository,
            new SqlConnectionProfileFactory(secretStore),
            probe);

        var result = await tester.TestAsync(registration.Id);

        Assert.Equal(ConnectionTestStatus.SecretUnavailable, result.Status);
        Assert.Equal(0, probe.OpenCount);
        Assert.DoesNotContain("missing-secret", result.Message, StringComparison.Ordinal);
        Assert.Null(result.DataSource);
        Assert.Null(result.ServerVersion);
    }

    [Fact]
    public async Task SuccessfulProbe_ReturnsSafeOperationalMetadata()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var registration = Registration(new SqlServerEndpoint("sql01.internal"));
        repository.Upsert(registration);
        var probe = new FakeProbe(new SqlProbeOutcome(true, "sql01.internal", "16.0.1000.6"));
        ISqlConnectionTester tester = new SqlConnectionTester(
            repository,
            new SqlConnectionProfileFactory(new FakeSecretStore()),
            probe);

        var result = await tester.TestAsync(registration.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(ConnectionTestStatus.Success, result.Status);
        Assert.Equal("sql01.internal", result.DataSource);
        Assert.Equal("16.0.1000.6", result.ServerVersion);
        Assert.Equal(1, probe.OpenCount);
        Assert.DoesNotContain("Password", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProbeFailures_MapToSanitizedCategories()
    {
        var cases = new (SqlProbeFailureKind ProbeFailure, ConnectionTestStatus ExpectedStatus)[]
        {
            (SqlProbeFailureKind.Authentication, ConnectionTestStatus.AuthenticationFailed),
            (SqlProbeFailureKind.Timeout, ConnectionTestStatus.Timeout),
            (SqlProbeFailureKind.Certificate, ConnectionTestStatus.CertificateFailure),
            (SqlProbeFailureKind.Network, ConnectionTestStatus.NetworkFailure),
            (SqlProbeFailureKind.InvalidConfiguration, ConnectionTestStatus.InvalidConfiguration),
            (SqlProbeFailureKind.Unexpected, ConnectionTestStatus.UnexpectedFailure)
        };

        foreach (var testCase in cases)
        {
            var repository = new InMemoryServerRegistrationRepository();
            var registration = Registration(new SqlServerEndpoint("sql01.internal"));
            repository.Upsert(registration);
            var tester = new SqlConnectionTester(
                repository,
                new SqlConnectionProfileFactory(new FakeSecretStore()),
                new FakeProbe(new SqlProbeOutcome(false, FailureKind: testCase.ProbeFailure)));

            var result = await tester.TestAsync(registration.Id);

            Assert.Equal(testCase.ExpectedStatus, result.Status);
            Assert.False(result.IsSuccess);
            Assert.Null(result.DataSource);
            Assert.Null(result.ServerVersion);
            Assert.DoesNotContain("sql01.internal", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task DisabledRegistration_IsNotContacted()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var registration = Registration(new SqlServerEndpoint("sql01.internal"), isEnabled: false);
        repository.Upsert(registration);
        var probe = new FakeProbe(new SqlProbeOutcome(true));
        ISqlConnectionTester tester = new SqlConnectionTester(
            repository,
            new SqlConnectionProfileFactory(new FakeSecretStore()),
            probe);

        var result = await tester.TestAsync(registration.Id);

        Assert.Equal(ConnectionTestStatus.RegistrationDisabled, result.Status);
        Assert.Equal(0, probe.OpenCount);
    }

    private static ServerRegistration Registration(
        SqlServerEndpoint endpoint,
        SqlAuthenticationMode mode = SqlAuthenticationMode.IntegratedSecurity,
        ConnectionSecretReference? secretReference = null,
        bool isEnabled = true) => new(
            Guid.NewGuid(),
            "SQL 01",
            endpoint,
            mode,
            secretReference,
            isEnabled,
            DateTimeOffset.UtcNow);

    private sealed class FakeSecretStore(SqlLoginSecret? secret = null) : IConnectionSecretStore
    {
        public int ResolveCount { get; private set; }

        public ValueTask<SqlLoginSecret?> ResolveAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResolveCount++;
            return ValueTask.FromResult(secret);
        }
    }

    private sealed class FakeProbe(SqlProbeOutcome outcome) : ISqlConnectionProbe
    {
        public int OpenCount { get; private set; }

        public Task<SqlProbeOutcome> OpenAsync(string connectionString, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            return Task.FromResult(outcome);
        }
    }
}
