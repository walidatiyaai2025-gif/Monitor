using System.Reflection;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class FleetIntelligenceTests
{
    [Fact]
    public void B200_041_GroupsHealthByEnvironment()
    {
        var fixture = Fixture();
        fixture.Metadata.UpsertServer(Meta(fixture.A.Id, ServerEnvironmentClass.Production, "core", ["tier-1"], fixture.Clock.UtcNow));
        fixture.Metadata.UpsertServer(Meta(fixture.B.Id, ServerEnvironmentClass.Test, "lab", ["tier-2"], fixture.Clock.UtcNow));

        var snapshot = fixture.Service().Read();

        Assert.Contains(snapshot.ByEnvironment, item => item.Key == "Production" && item.Servers == 1);
        Assert.Contains(snapshot.ByEnvironment, item => item.Key == "Test" && item.Servers == 1);
    }

    [Fact]
    public void B200_042_GroupsHealthByServerGroup()
    {
        var fixture = Fixture();
        fixture.Metadata.UpsertServer(Meta(fixture.A.Id, ServerEnvironmentClass.Production, "payments", ["tier-1"], fixture.Clock.UtcNow));
        fixture.Metadata.UpsertServer(Meta(fixture.B.Id, ServerEnvironmentClass.Production, "payments", ["tier-2"], fixture.Clock.UtcNow));

        var bucket = Assert.Single(fixture.Service().Read().ByGroup, item => item.Key == "payments");

        Assert.Equal(2, bucket.Servers);
    }

    [Fact]
    public void B200_043_GroupsHealthByTagAndTracksUntagged()
    {
        var fixture = Fixture();
        fixture.Metadata.UpsertServer(Meta(fixture.A.Id, ServerEnvironmentClass.Production, "core", ["tier-1", "finance"], fixture.Clock.UtcNow));
        fixture.Metadata.UpsertServer(Meta(fixture.B.Id, ServerEnvironmentClass.Production, "core", [], fixture.Clock.UtcNow));

        var buckets = fixture.Service().Read().ByTag;

        Assert.Contains(buckets, item => item.Key == "finance" && item.Servers == 1);
        Assert.Contains(buckets, item => item.Key == "Untagged" && item.Servers == 1);
    }

    [Fact]
    public void B200_044_CountsFreshStaleAndUnavailableSnapshots()
    {
        var fixture = Fixture();
        fixture.Cache.Set(fixture.A.Id, Result(fixture.A.Id, SnapshotFreshness.Fresh));
        fixture.Cache.Set(fixture.B.Id, Result(fixture.B.Id, SnapshotFreshness.Stale));
        var third = Registration("SQL-C");
        fixture.Registrations.Upsert(third);

        var snapshot = fixture.Service().Read();

        Assert.Equal(1, snapshot.Fresh);
        Assert.Equal(1, snapshot.Stale);
        Assert.Equal(1, snapshot.Unavailable);
    }

    [Fact]
    public void B200_045_CountsActiveMaintenanceWindows()
    {
        var fixture = Fixture();
        fixture.Metadata.UpsertServer(Meta(fixture.A.Id, ServerEnvironmentClass.Production, "core", ["tier-1"], fixture.Clock.UtcNow, maintenance: true));

        Assert.Equal(1, fixture.Service().Read().Maintenance);
    }

    [Fact]
    public void B200_046_CountsActiveSuppressionWindows()
    {
        var fixture = Fixture();
        fixture.Metadata.UpsertServer(Meta(fixture.B.Id, ServerEnvironmentClass.Production, "core", ["tier-1"], fixture.Clock.UtcNow, suppression: true));

        Assert.Equal(1, fixture.Service().Read().Suppressed);
    }

    [Fact]
    public void B200_047_RanksIncidentHotspotsByCriticalThenOpen()
    {
        var fixture = Fixture();
        fixture.Incidents.Add(Incident(fixture.A.Id, "blocking.active", FindingSeverity.Warning));
        fixture.Incidents.Add(Incident(fixture.B.Id, "blocking.active", FindingSeverity.Critical));
        fixture.Incidents.Add(Incident(fixture.A.Id, "memory.pressure", FindingSeverity.Warning));
        fixture.Metadata.UpsertServer(Meta(fixture.B.Id, ServerEnvironmentClass.Production, "core", ["tier-1"], fixture.Clock.UtcNow, suppression: true));

        var hotspot = fixture.Service().Read().RuleHotspots[0];

        Assert.Equal("blocking.active", hotspot.RuleId);
        Assert.Equal(2, hotspot.Open);
        Assert.Equal(1, hotspot.Critical);
        Assert.Equal(1, hotspot.Suppressed);
    }

    [Fact]
    public void B200_048_SumsBackupGapRiskFromCachedSnapshots()
    {
        var fixture = Fixture();
        fixture.Cache.Set(fixture.A.Id, Result(fixture.A.Id, SnapshotFreshness.Fresh, backupGaps: 3));

        Assert.Equal(3, fixture.Service().Read().Risks.BackupGaps);
    }

    [Fact]
    public void B200_049_CountsMemoryBlockingAndRunnableRiskFromCache()
    {
        var fixture = Fixture();
        fixture.Cache.Set(fixture.A.Id, Result(fixture.A.Id, SnapshotFreshness.Fresh, memoryLow: true, blockedRequests: 2, runnableTasks: 12));

        var risks = fixture.Service().Read().Risks;

        Assert.Equal(1, risks.MemoryPressure);
        Assert.Equal(1, risks.BlockingRisk);
        Assert.Equal(1, risks.RunnableRisk);
    }

    [Fact]
    public void B200_050_FleetReadUsesPeekOnlyAndNeverCollects()
    {
        var fixture = Fixture();

        _ = fixture.Service().Read();

        Assert.Equal(2, fixture.Cache.PeekCalls);
        Assert.Equal(0, fixture.Cache.GetCalls);
        Assert.Equal(0, fixture.Cache.RefreshCalls);
    }

    private static TestFixture Fixture()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var a = Registration("SQL-A");
        var b = Registration("SQL-B");
        var registrations = new RegistrationStore([a, b]);
        return new(clock, a, b, registrations, new PeekOnlyCache(), new InMemoryOperatorMetadataStore(clock), new IncidentStore());
    }

    private sealed record TestFixture(
        FixedTimeProvider Clock,
        ServerRegistration A,
        ServerRegistration B,
        RegistrationStore Registrations,
        PeekOnlyCache Cache,
        InMemoryOperatorMetadataStore Metadata,
        IncidentStore Incidents)
    {
        public FleetIntelligenceService Service() => new(Registrations, Cache, Metadata, Incidents, Clock);
    }

    private static ServerRegistration Registration(string name) => new(
        Guid.NewGuid(),
        name,
        new SqlServerEndpoint("sql.internal", 1433),
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        DateTimeOffset.Parse("2026-08-11T00:00:00Z"));

    private static ServerOperatorMetadata Meta(Guid id, ServerEnvironmentClass environment, string group, string[] tags, DateTimeOffset now, bool maintenance = false, bool suppression = false) =>
        new(
            id,
            environment,
            group,
            tags,
            maintenance ? new OperatorWindow(now.AddMinutes(-5), now.AddMinutes(5), "Maintenance") : null,
            suppression ? new OperatorWindow(now.AddMinutes(-5), now.AddMinutes(5), "Suppression") : null,
            now);

    private static HealthIncident Incident(Guid registrationId, string ruleId, FindingSeverity severity) =>
        new($"{registrationId:N}:{ruleId}:{Guid.NewGuid():N}", registrationId, ruleId, severity, "Incident", "Cached evidence", DateTimeOffset.Parse("2026-08-11T00:00:00Z"), DateTimeOffset.Parse("2026-08-11T00:00:00Z"), 1, IncidentStatus.Open);

    private static SnapshotCacheResult Result(
        Guid registrationId,
        SnapshotFreshness freshness,
        int backupGaps = 0,
        bool memoryLow = false,
        int blockedRequests = 0,
        int runnableTasks = 0)
    {
        var snapshot = (ServerHealthSnapshot)CreateRecord(typeof(ServerHealthSnapshot), new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["registrationId"] = registrationId,
            ["collectedAtUtc"] = DateTimeOffset.Parse("2026-08-11T00:00:00Z"),
            ["databaseOnline"] = 1,
            ["databaseTotal"] = 1,
            ["backups"] = CreateRecord(typeof(BackupHealthSnapshot), new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["missingFullBackupLast24Hours"] = backupGaps }),
            ["memory"] = CreateRecord(typeof(MemoryHealthSnapshot), new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["isPhysicalMemoryLow"] = memoryLow, ["isVirtualMemoryLow"] = false }),
            ["blocking"] = CreateRecord(typeof(BlockingHealthSnapshot), new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["blockedRequests"] = blockedRequests }),
            ["performance"] = CreateRecord(typeof(PerformanceHealthSnapshot), new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["runnableTasks"] = runnableTasks })
        });
        return (SnapshotCacheResult)CreateRecord(typeof(SnapshotCacheResult), new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["snapshot"] = snapshot,
            ["freshness"] = freshness
        });
    }

    private static object CreateRecord(Type type, IReadOnlyDictionary<string, object?> overrides)
    {
        var constructor = type.GetConstructors().OrderByDescending(item => item.GetParameters().Length).First();
        var values = constructor.GetParameters().Select(parameter =>
        {
            if (overrides.TryGetValue(parameter.Name ?? string.Empty, out var value)) return value;
            var parameterType = parameter.ParameterType;
            if (parameter.HasDefaultValue) return parameter.DefaultValue;
            if (!parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) is not null) return null;
            return Activator.CreateInstance(parameterType);
        }).ToArray();
        return constructor.Invoke(values);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class RegistrationStore(IReadOnlyList<ServerRegistration> initial) : IServerRegistrationRepository
    {
        private readonly List<ServerRegistration> _items = initial.ToList();
        public IReadOnlyList<ServerRegistration> GetAll() => _items.ToArray();
        public ServerRegistration? GetById(Guid id) => _items.FirstOrDefault(item => item.Id == id);
        public void Upsert(ServerRegistration registration) { _items.RemoveAll(item => item.Id == registration.Id); _items.Add(registration); }
        public bool Remove(Guid id) => _items.RemoveAll(item => item.Id == id) > 0;
    }

    private sealed class PeekOnlyCache : IServerHealthSnapshotCache
    {
        private readonly Dictionary<Guid, SnapshotCacheResult> _items = [];
        public int PeekCalls { get; private set; }
        public int GetCalls { get; private set; }
        public int RefreshCalls { get; private set; }
        public void Set(Guid id, SnapshotCacheResult result) => _items[id] = result;
        public SnapshotCacheResult? Peek(Guid registrationId) { PeekCalls++; return _items.TryGetValue(registrationId, out var value) ? value : null; }
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) { GetCalls++; throw new InvalidOperationException("Fleet intelligence must not call GetAsync."); }
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default) { RefreshCalls++; throw new InvalidOperationException("Fleet intelligence must not call RefreshAsync."); }
    }

    private sealed class IncidentStore : IHealthIncidentRepository
    {
        private readonly List<HealthIncident> _items = [];
        public void Add(HealthIncident incident) => _items.Add(incident);
        public void Apply(IEnumerable<HealthFinding> findings) => throw new NotSupportedException();
        public void Reconcile(Guid registrationId, DateTimeOffset observedAtUtc, IEnumerable<HealthFinding> activeFindings, bool canResolve) => throw new NotSupportedException();
        public IReadOnlyList<HealthIncident> GetAll() => _items.ToArray();
        public HealthIncident? GetById(string id) => _items.FirstOrDefault(item => item.Id == id);
        public bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next) => false;
    }
}
