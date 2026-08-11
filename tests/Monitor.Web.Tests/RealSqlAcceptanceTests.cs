using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class RealSqlAcceptanceTests
{
    private const string SecretReferenceValue = "p0-real-sql-ci";

    [Fact]
    [Trait("Category", "RealSql")]
    public async Task LeastPrivilegeSql2022_TestAndCollectorReturnRealEvidence()
    {
        var environment = RealSqlEnvironment.Load();
        if (environment is null) return;

        var registration = Registration(environment, environment.Username, trustServerCertificate: true);
        var store = SecretStore(environment.Username, environment.Password);
        var tester = new ServerConnectionTester(store, new SqlConnectionProbe());
        var collector = new SqlServerSnapshotCollector(store, new SqlSnapshotQuery(), TimeProvider.System);

        var connection = await tester.TestAsync(registration);
        var snapshot = await collector.CollectAsync(registration);

        Assert.Equal(ConnectionTestStatus.Succeeded, connection.Status);
        Assert.StartsWith("16.", connection.ServerVersion ?? string.Empty, StringComparison.Ordinal);
        Assert.StartsWith("16.", snapshot.ProductVersion, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.ServerName));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Edition));
        Assert.True(snapshot.UptimeSeconds >= 0);
        Assert.True(snapshot.DatabaseTotal >= 6);
        Assert.Equal(snapshot.DatabaseTotal, snapshot.DatabaseOnline);

        Assert.NotNull(snapshot.Memory);
        Assert.True(snapshot.Memory!.TotalPhysicalMemoryKb > 0);
        Assert.True(snapshot.Memory.AvailablePhysicalMemoryKb >= 0);
        Assert.InRange(snapshot.Memory.SqlProcessMemoryUtilizationPercent, 0, 100);

        Assert.NotNull(snapshot.Databases);
        Assert.Equal(0, snapshot.Databases!.Suspect);
        Assert.Equal(0, snapshot.Databases.RecoveryPending);

        Assert.NotNull(snapshot.Backups);
        Assert.True(snapshot.Backups!.BackedUpLast24Hours >= 1);
        Assert.True(snapshot.Backups.MissingFullBackupLast24Hours >= 1);
        Assert.NotNull(snapshot.Backups.LastFullBackupAtUtc);

        Assert.NotNull(snapshot.Jobs);
        Assert.True(snapshot.Jobs!.TotalJobs >= 1);
        Assert.True(snapshot.Jobs.EnabledJobs >= 1);

        Assert.NotNull(snapshot.Storage);
        Assert.True(snapshot.Storage!.TotalAllocatedBytes > 0);
        Assert.True(snapshot.Storage.DataAllocatedBytes > 0);
        Assert.True(snapshot.Storage.LogAllocatedBytes > 0);

        Assert.NotNull(snapshot.Blocking);
        Assert.True(snapshot.Blocking!.BlockedRequests >= 0);
        Assert.True(snapshot.Blocking.MaxWaitMilliseconds >= 0);

        Assert.NotNull(snapshot.Performance);
        Assert.True(snapshot.Performance!.ActiveRequests >= 0);
        Assert.True(snapshot.Performance.RunnableTasks >= 0);
        Assert.True(snapshot.Performance.PendingIoRequests >= 0);

        var serialized = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain(environment.Password, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(environment.Username, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "RealSql")]
    public async Task RealSql_BadPassword_IsClassifiedWithoutSecretLeakage()
    {
        var environment = RealSqlEnvironment.Load();
        if (environment is null) return;

        const string badPassword = "Definitely-Wrong-P0!9382";
        var store = SecretStore(environment.Username, badPassword);
        var tester = new ServerConnectionTester(store, new SqlConnectionProbe());

        var result = await tester.TestAsync(Registration(environment, environment.Username, trustServerCertificate: true));
        var serialized = JsonSerializer.Serialize(result);

        Assert.Equal(ConnectionTestStatus.AuthenticationFailed, result.Status);
        Assert.Equal("Authentication failed.", result.Message);
        Assert.DoesNotContain(badPassword, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(environment.Username, serialized, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "RealSql")]
    public async Task RealSql_SelfSignedCertificate_IsRejectedWhenTrustIsDisabled()
    {
        var environment = RealSqlEnvironment.Load();
        if (environment is null) return;

        var store = SecretStore(environment.Username, environment.Password);
        var tester = new ServerConnectionTester(store, new SqlConnectionProbe());

        var result = await tester.TestAsync(Registration(environment, environment.Username, trustServerCertificate: false));

        Assert.Equal(ConnectionTestStatus.CertificateRejected, result.Status);
        Assert.Equal("SQL Server certificate validation failed.", result.Message);
        Assert.DoesNotContain(environment.Password, JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "RealSql")]
    public async Task RealProbe_ClosedLocalPort_IsNetworkUnavailable()
    {
        var environment = RealSqlEnvironment.Load();
        if (environment is null) return;

        var reference = new ConnectionSecretReference(SecretReferenceValue);
        var store = SecretStore(environment.Username, environment.Password);
        var tester = new ServerConnectionTester(store, new SqlConnectionProbe());
        var unreachable = new ServerRegistration(
            Guid.NewGuid(),
            "P0 closed-port target",
            new SqlServerEndpoint("127.0.0.1", 1, encrypt: true, trustServerCertificate: true),
            SqlAuthenticationMode.SqlLogin,
            reference,
            true,
            DateTimeOffset.UtcNow);

        var result = await tester.TestAsync(unreachable);

        Assert.Equal(ConnectionTestStatus.NetworkUnavailable, result.Status);
        Assert.Equal("The SQL Server could not be reached.", result.Message);
    }

    [Fact]
    [Trait("Category", "RealSql")]
    public async Task RealSql_MissingServerStatePermission_FailsClosedAsPermissionDenied()
    {
        var environment = RealSqlEnvironment.Load();
        if (environment is null) return;

        var store = SecretStore(environment.NoServerStateUsername, environment.Password);
        var collector = new SqlServerSnapshotCollector(store, new SqlSnapshotQuery(), TimeProvider.System);

        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(
            () => collector.CollectAsync(Registration(environment, environment.NoServerStateUsername, trustServerCertificate: true)));

        Assert.Equal(SnapshotCollectionFailure.PermissionDenied, exception.Failure);
        Assert.Equal("The SQL Server login does not have the required monitoring permissions.", exception.Message);
        Assert.DoesNotContain(environment.Password, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(environment.NoServerStateUsername, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "RealSql")]
    public async Task RealSql_MissingMsdbAgentPermission_FailsClosedAsPermissionDenied()
    {
        var environment = RealSqlEnvironment.Load();
        if (environment is null) return;

        var store = SecretStore(environment.NoMsdbUsername, environment.Password);
        var collector = new SqlServerSnapshotCollector(store, new SqlSnapshotQuery(), TimeProvider.System);

        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(
            () => collector.CollectAsync(Registration(environment, environment.NoMsdbUsername, trustServerCertificate: true)));

        Assert.Equal(SnapshotCollectionFailure.PermissionDenied, exception.Failure);
        Assert.Equal("The SQL Server login does not have the required monitoring permissions.", exception.Message);
        Assert.DoesNotContain(environment.Password, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(environment.NoMsdbUsername, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "RealSql")]
    public async Task RealNetwork_StalledSqlHandshake_IsBoundedAsTimeout()
    {
        var environment = RealSqlEnvironment.Load();
        if (environment is null) return;

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var acceptTask = listener.AcceptTcpClientAsync().AsTask();
        var store = SecretStore(environment.Username, environment.Password);
        var collector = new SqlServerSnapshotCollector(store, new SqlSnapshotQuery(), TimeProvider.System);
        var stalled = new ServerRegistration(
            Guid.NewGuid(),
            "P0 stalled SQL handshake",
            new SqlServerEndpoint("127.0.0.1", endpoint.Port, encrypt: true, trustServerCertificate: true),
            SqlAuthenticationMode.SqlLogin,
            new ConnectionSecretReference(SecretReferenceValue),
            true,
            DateTimeOffset.UtcNow);
        var stopwatch = Stopwatch.StartNew();
        var collection = collector.CollectAsync(stalled);

        using var accepted = await acceptTask.WaitAsync(TimeSpan.FromSeconds(3));
        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(() => collection);
        stopwatch.Stop();

        Assert.Equal(SnapshotCollectionFailure.TimedOut, exception.Failure);
        Assert.Equal("Snapshot collection timed out.", exception.Message);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(9));
    }

    private static AcceptanceSecretStore SecretStore(string username, string password) => new(
        new ConnectionSecretReference(SecretReferenceValue),
        new SqlLoginSecret(username, password));

    private static ServerRegistration Registration(
        RealSqlEnvironment environment,
        string username,
        bool trustServerCertificate) => new(
        Guid.Parse("40404040-4040-4040-4040-404040404040"),
        $"P0 SQL Server 2022 Acceptance {username}",
        new SqlServerEndpoint(
            environment.Host,
            environment.Port,
            encrypt: true,
            trustServerCertificate: trustServerCertificate),
        SqlAuthenticationMode.SqlLogin,
        new ConnectionSecretReference(SecretReferenceValue),
        true,
        DateTimeOffset.UtcNow);

    private sealed class AcceptanceSecretStore(
        ConnectionSecretReference expectedReference,
        SqlLoginSecret secret) : IConnectionSecretStore
    {
        public ValueTask<SqlLoginSecret?> ResolveAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<SqlLoginSecret?>(
                reference == expectedReference ? secret : null);
        }
    }

    private sealed record RealSqlEnvironment(
        string Host,
        int Port,
        string Username,
        string NoServerStateUsername,
        string NoMsdbUsername,
        string Password)
    {
        public static RealSqlEnvironment? Load()
        {
            var required = string.Equals(
                Environment.GetEnvironmentVariable("MONITOR_REQUIRE_REAL_SQL"),
                "1",
                StringComparison.Ordinal);
            var host = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_HOST");
            var portText = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PORT");
            var username = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_USERNAME");
            var noServerStateUsername = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_NO_SERVER_STATE_USERNAME");
            var noMsdbUsername = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_NO_MSDB_USERNAME");
            var password = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PASSWORD");

            if (string.IsNullOrWhiteSpace(host) ||
                !int.TryParse(portText, out var port) || port is < 1 or > 65535 ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(noServerStateUsername) ||
                string.IsNullOrWhiteSpace(noMsdbUsername) ||
                string.IsNullOrEmpty(password))
            {
                if (required)
                {
                    throw new InvalidOperationException(
                        "MONITOR_REQUIRE_REAL_SQL=1 but the real SQL acceptance environment is incomplete.");
                }

                return null;
            }

            return new RealSqlEnvironment(
                host.Trim(),
                port,
                username.Trim(),
                noServerStateUsername.Trim(),
                noMsdbUsername.Trim(),
                password);
        }
    }
}