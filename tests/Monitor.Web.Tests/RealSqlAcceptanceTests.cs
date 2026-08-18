using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Monitor.Web.Controllers;
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

        var registration = Registration(environment, trustServerCertificate: true);
        var store = new AcceptanceSecretStore(
            new ConnectionSecretReference(SecretReferenceValue),
            new SqlLoginSecret(environment.Username, environment.Password));
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
    public async Task FullJourney_RegisterCollectViewRefreshRestartAndViewAgain()
    {
        var environment = RealSqlEnvironment.Load();
        if (environment is null) return;

        var directory = Path.Combine(Path.GetTempPath(), $"monitor-p0-real-journey-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var registrationPath = Path.Combine(directory, "registrations.json");
            var secretPath = Path.Combine(directory, "secrets.json");
            var keyRingPath = Path.Combine(directory, "keyring");
            var configuration = new ConfigurationBuilder().Build();
            var protection = CreateProtectionProvider(keyRingPath);
            var credentialPolicy = new CredentialPolicyOptions { AllowLocalOwnedCredentials = true };
            var secrets = new ProtectedFileConnectionSecretStore(secretPath, protection, configuration, [], credentialPolicy);
            var registrations = new FileServerRegistrationRepository(registrationPath);
            var tester = new ServerConnectionTester(secrets, new SqlConnectionProbe());
            var collector = new SqlServerSnapshotCollector(secrets, new SqlSnapshotQuery(), TimeProvider.System);
            var cache = new ServerHealthSnapshotCache(collector, TimeProvider.System);
            var observer = new RecordingObserver();
            var connectionLab = new ConnectionLabController(
                registrations,
                tester,
                secrets,
                cache,
                observer,
                credentialPolicy: credentialPolicy);
            var input = new ConnectionLabRegistrationInput
            {
                DisplayName = "P0 real SQL journey",
                Host = environment.Host,
                Port = environment.Port,
                AuthenticationMode = SqlAuthenticationMode.SqlLogin,
                SqlUsername = environment.Username,
                SqlPassword = environment.Password,
                Encrypt = true,
                TrustServerCertificate = true
            };

            var registerAction = await connectionLab.Register(input, CancellationToken.None);

            var redirect = Assert.IsType<RedirectToActionResult>(registerAction);
            Assert.Equal("Servers", redirect.ActionName);
            Assert.Equal("Operations", redirect.ControllerName);
            var registration = Assert.Single(registrations.GetAll());
            Assert.StartsWith("local:v1:", registration.SecretReference?.Value ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal(1, observer.CallCount);
            Assert.NotNull(cache.Peek(registration.Id));

            var firstRead = new MonitorReadService(new DemoMonitorService(), registrations, cache);
            var firstOperations = new OperationsController(new DemoMonitorService(), firstRead);
            var firstView = Assert.IsType<ViewResult>(
                await firstOperations.ServerDetails(registration.Id.ToString("D"), CancellationToken.None));
            var firstModel = Assert.IsType<ServerDetailsViewModel>(firstView.Model);
            Assert.Equal(ServerDataSource.LiveFresh, firstModel.Server.Source);
            Assert.NotNull(firstModel.Evidence?.Backups);
            Assert.NotNull(firstModel.Evidence?.Jobs);
            Assert.NotNull(firstModel.Evidence?.Storage);

            var refresh = new SnapshotRefreshService(registrations, cache, TimeProvider.System, observer);
            var refreshResult = await refresh.RefreshAsync(registration.Id);
            Assert.Equal(SnapshotRefreshStatus.Refreshed, refreshResult.Status);
            Assert.Equal(SnapshotFreshness.Fresh, refreshResult.Freshness);
            Assert.Equal(2, observer.CallCount);

            var registrationFile = await File.ReadAllTextAsync(registrationPath);
            var secretFile = await File.ReadAllTextAsync(secretPath);
            Assert.DoesNotContain(environment.Username, registrationFile, StringComparison.Ordinal);
            Assert.DoesNotContain(environment.Password, registrationFile, StringComparison.Ordinal);
            Assert.DoesNotContain(environment.Username, secretFile, StringComparison.Ordinal);
            Assert.DoesNotContain(environment.Password, secretFile, StringComparison.Ordinal);

            var restartedRegistrations = new FileServerRegistrationRepository(registrationPath);
            var restartedSecrets = new ProtectedFileConnectionSecretStore(
                secretPath,
                CreateProtectionProvider(keyRingPath),
                new ConfigurationBuilder().Build(),
                [],
                credentialPolicy);
            var restartedRegistration = Assert.Single(restartedRegistrations.GetAll());
            Assert.Equal(registration.Id, restartedRegistration.Id);
            Assert.Equal(registration.SecretReference, restartedRegistration.SecretReference);

            var restartedTester = new ServerConnectionTester(restartedSecrets, new SqlConnectionProbe());
            var restartedConnection = await restartedTester.TestAsync(restartedRegistration);
            Assert.Equal(ConnectionTestStatus.Succeeded, restartedConnection.Status);

            var restartedCollector = new SqlServerSnapshotCollector(restartedSecrets, new SqlSnapshotQuery(), TimeProvider.System);
            var restartedCache = new ServerHealthSnapshotCache(restartedCollector, TimeProvider.System);
            var restartedSnapshot = await restartedCache.RefreshAsync(restartedRegistration);
            Assert.Equal(SnapshotFreshness.Fresh, restartedSnapshot.Freshness);

            var restartedRead = new MonitorReadService(new DemoMonitorService(), restartedRegistrations, restartedCache);
            var restartedOperations = new OperationsController(new DemoMonitorService(), restartedRead);
            var restartedView = Assert.IsType<ViewResult>(
                await restartedOperations.ServerDetails(registration.Id.ToString("D"), CancellationToken.None));
            var restartedModel = Assert.IsType<ServerDetailsViewModel>(restartedView.Model);
            Assert.Equal(ServerDataSource.LiveFresh, restartedModel.Server.Source);
            Assert.Equal(registration.Id.ToString("D"), restartedModel.Server.Id);
            Assert.NotNull(restartedModel.Evidence?.Memory);
            Assert.NotNull(restartedModel.Evidence?.Backups);
            Assert.NotNull(restartedModel.Evidence?.Jobs);
            Assert.NotNull(restartedModel.Evidence?.Storage);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "RealSql")]
    public async Task RealSql_BadPassword_IsClassifiedWithoutSecretLeakage()
    {
        var environment = RealSqlEnvironment.Load();
        if (environment is null) return;

        const string badPassword = "Definitely-Wrong-P0!9382";
        var reference = new ConnectionSecretReference(SecretReferenceValue);
        var store = new AcceptanceSecretStore(reference, new SqlLoginSecret(environment.Username, badPassword));
        var tester = new ServerConnectionTester(store, new SqlConnectionProbe());
        var result = await tester.TestAsync(Registration(environment, trustServerCertificate: true));
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
        var reference = new ConnectionSecretReference(SecretReferenceValue);
        var store = new AcceptanceSecretStore(reference, new SqlLoginSecret(environment.Username, environment.Password));
        var tester = new ServerConnectionTester(store, new SqlConnectionProbe());
        var result = await tester.TestAsync(Registration(environment, trustServerCertificate: false));
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
        var store = new AcceptanceSecretStore(reference, new SqlLoginSecret(environment.Username, environment.Password));
        var tester = new ServerConnectionTester(store, new SqlConnectionProbe());
        var unreachable = new ServerRegistration(Guid.NewGuid(), "P0 closed-port target", new SqlServerEndpoint("127.0.0.1", 1, encrypt: true, trustServerCertificate: true), SqlAuthenticationMode.SqlLogin, reference, true, DateTimeOffset.UtcNow);
        var result = await tester.TestAsync(unreachable);
        Assert.Equal(ConnectionTestStatus.NetworkUnavailable, result.Status);
        Assert.Equal("The SQL Server could not be reached.", result.Message);
    }

    [Fact]
    [Trait("Category", "RealSql")]
    public async Task RealProbe_AcceptedButSilentTcpEndpoint_IsTimedOut()
    {
        var environment = RealSqlEnvironment.Load();
        if (environment is null) return;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var accepted = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var acceptTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(accepted.Token);
            await Task.Delay(TimeSpan.FromSeconds(9), accepted.Token);
        }, accepted.Token);
        var reference = new ConnectionSecretReference(SecretReferenceValue);
        var store = new AcceptanceSecretStore(reference, new SqlLoginSecret(environment.Username, environment.Password));
        var tester = new ServerConnectionTester(store, new SqlConnectionProbe());
        var registration = new ServerRegistration(Guid.NewGuid(), "P0 silent TCP timeout", new SqlServerEndpoint("127.0.0.1", port, encrypt: true, trustServerCertificate: true), SqlAuthenticationMode.SqlLogin, reference, true, DateTimeOffset.UtcNow);
        try
        {
            var result = await tester.TestAsync(registration);
            Assert.Equal(ConnectionTestStatus.TimedOut, result.Status);
            Assert.Equal("Connection timed out.", result.Message);
        }
        finally
        {
            accepted.Cancel();
            listener.Stop();
            try { await acceptTask; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    [Trait("Category", "RealSql")]
    public async Task RealSql_InsufficientServerPermissions_FailClosedSafely()
    {
        var environment = RealSqlEnvironment.Load();
        if (environment is null) return;
        var reference = new ConnectionSecretReference("p0-limited-sql-ci");
        var store = new AcceptanceSecretStore(reference, new SqlLoginSecret(environment.LimitedUsername, environment.LimitedPassword));
        var registration = Registration(environment, true, reference);
        var tester = new ServerConnectionTester(store, new SqlConnectionProbe());
        var collector = new SqlServerSnapshotCollector(store, new SqlSnapshotQuery(), TimeProvider.System);
        var connection = await tester.TestAsync(registration);
        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(() => collector.CollectAsync(registration));
        Assert.Equal(ConnectionTestStatus.Succeeded, connection.Status);
        Assert.Equal(SnapshotCollectionFailure.Failed, exception.Failure);
        Assert.Equal("Snapshot collection failed.", exception.Message);
        var serialized = JsonSerializer.Serialize(exception.Message);
        Assert.DoesNotContain(environment.LimitedUsername, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(environment.LimitedPassword, serialized, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "RealSql")]
    public async Task RealSql_MissingMsdbPermissions_FailClosedSafely()
    {
        var environment = RealSqlEnvironment.Load();
        if (environment is null) return;
        var reference = new ConnectionSecretReference("p0-no-msdb-sql-ci");
        var store = new AcceptanceSecretStore(reference, new SqlLoginSecret(environment.NoMsdbUsername, environment.NoMsdbPassword));
        var registration = Registration(environment, true, reference);
        var tester = new ServerConnectionTester(store, new SqlConnectionProbe());
        var collector = new SqlServerSnapshotCollector(store, new SqlSnapshotQuery(), TimeProvider.System);
        var connection = await tester.TestAsync(registration);
        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(() => collector.CollectAsync(registration));
        Assert.Equal(ConnectionTestStatus.Succeeded, connection.Status);
        Assert.Equal(SnapshotCollectionFailure.Failed, exception.Failure);
        Assert.Equal("Snapshot collection failed.", exception.Message);
        var serialized = JsonSerializer.Serialize(exception.Message);
        Assert.DoesNotContain(environment.NoMsdbUsername, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(environment.NoMsdbPassword, serialized, StringComparison.Ordinal);
    }

    private static IDataProtectionProvider CreateProtectionProvider(string keyRingPath) => DataProtectionProvider.Create(new DirectoryInfo(keyRingPath), configuration => configuration.SetApplicationName("Monitor.SqlSecrets.v1"));

    private static ServerRegistration Registration(RealSqlEnvironment environment, bool trustServerCertificate, ConnectionSecretReference? reference = null) => new(
        Guid.Parse("40404040-4040-4040-4040-404040404040"), "P0 SQL Server 2022 Acceptance", new SqlServerEndpoint(environment.Host, environment.Port, encrypt: true, trustServerCertificate: trustServerCertificate), SqlAuthenticationMode.SqlLogin, reference ?? new ConnectionSecretReference(SecretReferenceValue), true, DateTimeOffset.UtcNow);

    private sealed class AcceptanceSecretStore(ConnectionSecretReference expectedReference, SqlLoginSecret secret) : IConnectionSecretStore
    {
        public ValueTask<SqlLoginSecret?> ResolveAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<SqlLoginSecret?>(reference == expectedReference ? secret : null);
        }
    }

    private sealed class RecordingObserver : ISnapshotObserver
    {
        public int CallCount { get; private set; }
        public void Observe(SnapshotCacheResult result) => CallCount++;
    }

    private sealed record RealSqlEnvironment(string Host, int Port, string Username, string Password, string LimitedUsername, string LimitedPassword, string NoMsdbUsername, string NoMsdbPassword)
    {
        public static RealSqlEnvironment? Load()
        {
            var required = string.Equals(Environment.GetEnvironmentVariable("MONITOR_REQUIRE_REAL_SQL"), "1", StringComparison.Ordinal);
            var host = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_HOST");
            var portText = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PORT");
            var username = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_USERNAME");
            var password = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PASSWORD");
            var limitedUsername = Environment.GetEnvironmentVariable("MONITOR_LIMITED_SQL_USERNAME");
            var limitedPassword = Environment.GetEnvironmentVariable("MONITOR_LIMITED_SQL_PASSWORD");
            var noMsdbUsername = Environment.GetEnvironmentVariable("MONITOR_NO_MSDB_SQL_USERNAME");
            var noMsdbPassword = Environment.GetEnvironmentVariable("MONITOR_NO_MSDB_SQL_PASSWORD");
            if (string.IsNullOrWhiteSpace(host) || !int.TryParse(portText, out var port) || port is < 1 or > 65535 || string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(limitedUsername) || string.IsNullOrEmpty(limitedPassword) || string.IsNullOrWhiteSpace(noMsdbUsername) || string.IsNullOrEmpty(noMsdbPassword))
            {
                if (required) throw new InvalidOperationException("MONITOR_REQUIRE_REAL_SQL=1 but the real SQL acceptance environment is incomplete.");
                return null;
            }
            return new RealSqlEnvironment(host.Trim(), port, username.Trim(), password, limitedUsername.Trim(), limitedPassword, noMsdbUsername.Trim(), noMsdbPassword);
        }
    }
}
