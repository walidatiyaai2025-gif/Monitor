using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class OperatorSurfaceTruthfulnessTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public async Task Dashboard_NoUnavailableTargets_ReportsZeroUnavailableAsHealthy()
    {
        var registration = Registration("11111111-1111-1111-1111-111111111111", "SQL-01");
        var service = Service(
            [registration],
            new Dictionary<Guid, SnapshotCacheResult>
            {
                [registration.Id] = Fresh(registration.Id, 5, 5)
            });

        var dashboard = await service.GetDashboardAsync();
        var unavailable = Assert.Single(dashboard.Metrics, metric => metric.Name == "Unavailable");
        var databases = Assert.Single(dashboard.Metrics, metric => metric.Name == "Databases online");

        Assert.Equal("0", unavailable.Value);
        Assert.Equal(HealthState.Healthy, unavailable.State);
        Assert.Equal(HealthState.Healthy, databases.State);
    }

    [Fact]
    public async Task Dashboard_MissingSnapshot_KeepsDatabaseEstateUnknownAndUnavailableWarning()
    {
        var registration = Registration("22222222-2222-2222-2222-222222222222", "SQL-02");
        var service = Service([registration], new Dictionary<Guid, SnapshotCacheResult>());

        var dashboard = await service.GetDashboardAsync();
        var unavailable = Assert.Single(dashboard.Metrics, metric => metric.Name == "Unavailable");
        var databases = Assert.Single(dashboard.Metrics, metric => metric.Name == "Databases online");

        Assert.Equal("1", unavailable.Value);
        Assert.Equal(HealthState.Warning, unavailable.State);
        Assert.Equal("0 / 0", databases.Value);
        Assert.Equal(HealthState.Unknown, databases.State);
    }

    [Fact]
    public async Task Dashboard_PartialTargetCoverage_DoesNotClaimHealthyDatabaseEstate()
    {
        var observed = Registration("33333333-3333-3333-3333-333333333333", "SQL-03");
        var missing = Registration("44444444-4444-4444-4444-444444444444", "SQL-04");
        var service = Service(
            [observed, missing],
            new Dictionary<Guid, SnapshotCacheResult>
            {
                [observed.Id] = Fresh(observed.Id, 5, 5)
            });

        var dashboard = await service.GetDashboardAsync();
        var databases = Assert.Single(dashboard.Metrics, metric => metric.Name == "Databases online");
        var unavailable = Assert.Single(dashboard.Metrics, metric => metric.Name == "Unavailable");

        Assert.Equal("5 / 5", databases.Value);
        Assert.Equal(HealthState.Unknown, databases.State);
        Assert.Equal(HealthState.Warning, unavailable.State);
    }

    [Fact]
    public void OperatorViews_RemoveSyntheticScoreReachabilityConnectivityAndCollectorClaims()
    {
        var servers = File.ReadAllText(Path.Combine(Root, "src", "Monitor.Web", "Views", "Operations", "Servers.cshtml"));
        var dashboard = File.ReadAllText(Path.Combine(Root, "src", "Monitor.Web", "Views", "Operations", "Dashboard.cshtml"));
        var script = File.ReadAllText(Path.Combine(Root, "src", "Monitor.Web", "wwwroot", "js", "site.js"));

        Assert.DoesNotContain("Health score", servers, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Reachable on page", servers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cached evidence on page", servers, StringComparison.Ordinal);
        Assert.Contains("reachability is not inferred", servers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<small>Evidence</small><strong>@evidenceLabel</strong>", servers, StringComparison.Ordinal);

        Assert.DoesNotContain("Signal integrity", dashboard, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQL ESTATE LIVE", dashboard, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Monitoring Active", dashboard, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span>connected</span>", dashboard, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<strong class=\"inline-live\"><span class=\"live-dot\" aria-hidden=\"true\"></span>Running</strong>", dashboard, StringComparison.Ordinal);
        Assert.Contains("Evidence coverage", dashboard, StringComparison.Ordinal);
        Assert.Contains("with cached evidence", dashboard, StringComparison.Ordinal);
        Assert.Contains("Snapshot source", dashboard, StringComparison.Ordinal);
        Assert.Contains("Cached state", dashboard, StringComparison.Ordinal);

        Assert.DoesNotContain("Health state analyzing", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Incident correlation", script, StringComparison.Ordinal);
        Assert.Contains("No SQL polling on navigation", script, StringComparison.Ordinal);
        Assert.Contains("Backend refresh is explicit", script, StringComparison.Ordinal);
    }

    private static MonitorReadService Service(
        IReadOnlyList<ServerRegistration> registrations,
        IReadOnlyDictionary<Guid, SnapshotCacheResult> snapshots)
    {
        var repository = new InMemoryServerRegistrationRepository();
        foreach (var registration in registrations) repository.Upsert(registration);
        return new MonitorReadService(new DemoMonitorService(), repository, new MappingCache(snapshots));
    }

    private static ServerRegistration Registration(string id, string name) => new(
        Guid.Parse(id),
        name,
        new SqlServerEndpoint("private-host"),
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        DateTimeOffset.UtcNow);

    private static SnapshotCacheResult Fresh(Guid id, int online, int total) => new(
        new ServerHealthSnapshot(
            id,
            "SQL",
            "17.0",
            "Enterprise",
            "MSSQLSERVER",
            3600,
            online,
            total,
            DateTimeOffset.UtcNow),
        SnapshotFreshness.Fresh,
        TimeSpan.Zero);

    private sealed class MappingCache(IReadOnlyDictionary<Guid, SnapshotCacheResult> values) : IServerHealthSnapshotCache
    {
        public SnapshotCacheResult? Peek(Guid registrationId) =>
            values.TryGetValue(registrationId, out var value) ? value : null;

        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) =>
            values.TryGetValue(registration.Id, out var value)
                ? Task.FromResult(value)
                : Task.FromException<SnapshotCacheResult>(new SnapshotCollectionException(
                    SnapshotCollectionFailure.NetworkUnavailable,
                    "No cached snapshot."));

        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default) =>
            GetAsync(registration, cancellationToken);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
