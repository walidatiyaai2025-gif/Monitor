using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class RealSqlJourneyAcceptanceTests
{
    [Fact]
    [Trait("Category", "RealSql")]
    public async Task AddTestRegisterCollectViewRefreshRestartView_WorksAgainstRealSql()
    {
        var environment = RealSqlEnvironment.Load();
        if (environment is null) return;

        using var directory = new TemporaryDirectory();
        var registrationFile = Path.Combine(directory.Path, "registrations.json");
        var secretFile = Path.Combine(directory.Path, "secrets.json");
        var keyRing = Path.Combine(directory.Path, "keyring");
        var latestSnapshotFile = Path.Combine(directory.Path, "latest-snapshots.json");

        var firstSecrets = CreateSecretStore(secretFile, keyRing);
        var secretReference = await firstSecrets.StoreAsync(environment.Username, environment.Password);
        var registration = new ServerRegistration(
            Guid.NewGuid(),
            "P0 full real SQL journey",
            new SqlServerEndpoint(
                environment.Host,
                environment.Port,
                encrypt: true,
                trustServerCertificate: true),
            SqlAuthenticationMode.SqlLogin,
            secretReference,
            true,
            DateTimeOffset.UtcNow);

        var tester = new ServerConnectionTester(firstSecrets, new SqlConnectionProbe());
        var tested = await tester.TestAsync(registration);
        Assert.Equal(ConnectionTestStatus.Succeeded, tested.Status);

        var registrations = new FileServerRegistrationRepository(registrationFile);
        registrations.Upsert(registration);
        Assert.Equal(registration.Id, registrations.GetById(registration.Id)!.Id);

        var collector = new SqlServerSnapshotCollector(firstSecrets, new SqlSnapshotQuery(), TimeProvider.System);
        var latestStore = new FileLatestSnapshotStore(latestSnapshotFile);
        var cache = new ServerHealthSnapshotCache(collector, TimeProvider.System, latestSnapshotStore: latestStore);

        var collected = await cache.GetAsync(registration);
        Assert.Equal(registration.Id, collected.Snapshot.RegistrationId);
        Assert.True(collected.Snapshot.DatabaseTotal >= 6);

        var reads = new MonitorReadService(new DemoMonitorService(), registrations, cache);
        var firstView = await reads.GetServerAsync(registration.Id.ToString("D"));
        Assert.NotNull(firstView);
        Assert.NotEqual(ServerDataSource.RegisteredUnavailable, firstView!.Server.Source);
        Assert.NotNull(firstView.Evidence);

        var refreshed = await cache.RefreshAsync(registration);
        Assert.Equal(registration.Id, refreshed.Snapshot.RegistrationId);
        Assert.True(refreshed.Snapshot.CollectedAtUtc >= collected.Snapshot.CollectedAtUtc);

        // Simulate an application restart by reconstructing every durable primitive.
        var restartedRegistrations = new FileServerRegistrationRepository(registrationFile);
        var restartedSecrets = CreateSecretStore(secretFile, keyRing);
        var restoredRegistration = restartedRegistrations.GetById(registration.Id);
        Assert.NotNull(restoredRegistration);
        var restoredSecret = await restartedSecrets.ResolveAsync(secretReference);
        Assert.NotNull(restoredSecret);
        Assert.Equal(environment.Username, restoredSecret!.Username);
        Assert.Equal(environment.Password, restoredSecret.Password);

        var noSqlCollector = new NoSqlAllowedCollector();
        var restartedCache = new ServerHealthSnapshotCache(
            noSqlCollector,
            TimeProvider.System,
            latestSnapshotStore: new FileLatestSnapshotStore(latestSnapshotFile));
        var restartedReads = new MonitorReadService(
            new DemoMonitorService(),
            restartedRegistrations,
            restartedCache);

        var restartedView = await restartedReads.GetServerAsync(registration.Id.ToString("D"));

        Assert.NotNull(restartedView);
        Assert.NotEqual(ServerDataSource.RegisteredUnavailable, restartedView!.Server.Source);
        Assert.NotNull(restartedView.Evidence);
        Assert.Equal(refreshed.Snapshot.CollectedAtUtc, restartedView.Evidence!.CollectedAtUtc);
        Assert.Equal(0, noSqlCollector.CallCount);

        var persistedRegistration = await File.ReadAllTextAsync(registrationFile);
        var persistedSecrets = await File.ReadAllTextAsync(secretFile);
        var persistedLatest = await File.ReadAllTextAsync(latestSnapshotFile);
        Assert.DoesNotContain(environment.Password, persistedRegistration, StringComparison.Ordinal);
        Assert.DoesNotContain(environment.Password, persistedSecrets, StringComparison.Ordinal);
        Assert.DoesNotContain(environment.Password, persistedLatest, StringComparison.Ordinal);
        Assert.DoesNotContain(environment.Username, persistedLatest, StringComparison.Ordinal);
    }

    private static ProtectedFileConnectionSecretStore CreateSecretStore(string secretFile, string keyRing)
    {
        var provider = DataProtectionProvider.Create(
            new DirectoryInfo(keyRing),
            configuration => configuration.SetApplicationName("Monitor.SqlSecrets.v1"));
        return new ProtectedFileConnectionSecretStore(
            secretFile,
            provider,
            new ConfigurationBuilder().Build(),
            [],
            new CredentialPolicyOptions { AllowLocalOwnedCredentials = true });
    }

    private sealed class NoSqlAllowedCollector : ISqlServerSnapshotCollector
    {
        public int CallCount { get; private set; }
        public Task<ServerHealthSnapshot> CollectAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromException<ServerHealthSnapshot>(
                new InvalidOperationException("Restart view must not execute monitored SQL."));
        }
    }

    private sealed record RealSqlEnvironment(string Host, int Port, string Username, string Password)
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
            var password = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PASSWORD");

            if (string.IsNullOrWhiteSpace(host) ||
                !int.TryParse(portText, out var port) || port is < 1 or > 65535 ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrEmpty(password))
            {
                if (required)
                    throw new InvalidOperationException("MONITOR_REQUIRE_REAL_SQL=1 but the real SQL journey environment is incomplete.");
                return null;
            }

            return new(host.Trim(), port, username.Trim(), password);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"monitor-p0-real-journey-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public void Dispose() => Directory.Delete(Path, true);
    }
}