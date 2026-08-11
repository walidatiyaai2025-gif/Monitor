using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300RiskScoringTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-11T06:00:00Z");

    [Fact]
    public void B300_001_WeightedServerRiskScoreIsBoundedAndClassified()
    {
        var id = Guid.NewGuid();
        var risk = DbaRiskScoring.Evaluate(id, Cached(Snapshot(id)), [], Metadata(id), Now);
        Assert.InRange(risk.Score, 0, 100);
        Assert.Equal(DbaRiskScoring.Classify(risk.Score), risk.Level);
        Assert.Equal(risk.Score, risk.Components.Total);
    }

    [Fact]
    public void B300_002_FreshnessRiskDistinguishesUnavailableStaleAndFresh()
    {
        var id = Guid.NewGuid();
        Assert.Equal(25, DbaRiskScoring.FreshnessRisk(null));
        Assert.Equal(15, DbaRiskScoring.FreshnessRisk(Cached(Snapshot(id), SnapshotFreshness.Stale)));
        Assert.Equal(0, DbaRiskScoring.FreshnessRisk(Cached(Snapshot(id))));
    }

    [Fact]
    public void B300_003_DatabaseAvailabilityRiskScalesWithOfflineRatio()
    {
        var id = Guid.NewGuid();
        Assert.Equal(0, DbaRiskScoring.DatabaseAvailabilityRisk(Snapshot(id, databaseTotal: 10, databaseOnline: 10)));
        Assert.Equal(10, DbaRiskScoring.DatabaseAvailabilityRisk(Snapshot(id, databaseTotal: 10, databaseOnline: 5)));
        Assert.Equal(20, DbaRiskScoring.DatabaseAvailabilityRisk(Snapshot(id, databaseTotal: 10, databaseOnline: 0)));
    }

    [Fact]
    public void B300_004_BackupComplianceRiskUsesMissingBackupRatio()
    {
        var id = Guid.NewGuid();
        Assert.Equal(0, DbaRiskScoring.BackupComplianceRisk(Snapshot(id, backups: new(10, 0, Now))));
        Assert.Equal(8, DbaRiskScoring.BackupComplianceRisk(Snapshot(id, backups: new(5, 5, Now))));
        Assert.Equal(15, DbaRiskScoring.BackupComplianceRisk(Snapshot(id, backups: new(0, 10, null))));
    }

    [Fact]
    public void B300_005_MemoryPressureRiskUsesUtilizationAndPressureFlags()
    {
        var id = Guid.NewGuid();
        Assert.Equal(5, DbaRiskScoring.MemoryPressureRisk(Snapshot(id, memory: Memory(70))));
        Assert.Equal(10, DbaRiskScoring.MemoryPressureRisk(Snapshot(id, memory: Memory(80))));
        Assert.Equal(15, DbaRiskScoring.MemoryPressureRisk(Snapshot(id, memory: Memory(50, physicalLow: true))));
    }

    [Fact]
    public void B300_006_BlockingRiskUsesRequestCountAndWaitDuration()
    {
        var id = Guid.NewGuid();
        Assert.Equal(0, DbaRiskScoring.BlockingRisk(Snapshot(id, blocking: new(0, 0))));
        Assert.Equal(4, DbaRiskScoring.BlockingRisk(Snapshot(id, blocking: new(1, 500))));
        Assert.Equal(7, DbaRiskScoring.BlockingRisk(Snapshot(id, blocking: new(3, 1_000))));
        Assert.Equal(10, DbaRiskScoring.BlockingRisk(Snapshot(id, blocking: new(10, 500))));
    }

    [Fact]
    public void B300_007_RunnableSchedulerPressureIsBounded()
    {
        var id = Guid.NewGuid();
        Assert.Equal(0, DbaRiskScoring.RunnablePressureRisk(Snapshot(id, performance: new(1, 1, 0))));
        Assert.Equal(4, DbaRiskScoring.RunnablePressureRisk(Snapshot(id, performance: new(1, 2, 0))));
        Assert.Equal(7, DbaRiskScoring.RunnablePressureRisk(Snapshot(id, performance: new(1, 4, 0))));
        Assert.Equal(10, DbaRiskScoring.RunnablePressureRisk(Snapshot(id, performance: new(1, 8, 0))));
    }

    [Fact]
    public void B300_008_IncidentRiskCountsOnlyActiveIncidentsForTargetServer()
    {
        var id = Guid.NewGuid();
        var other = Guid.NewGuid();
        var items = new[]
        {
            Incident(id, "critical", FindingSeverity.Critical, IncidentStatus.Open),
            Incident(id, "warning", FindingSeverity.Warning, IncidentStatus.Acknowledged),
            Incident(id, "resolved", FindingSeverity.Critical, IncidentStatus.Resolved),
            Incident(other, "other", FindingSeverity.Critical, IncidentStatus.Open)
        };
        Assert.Equal(11, DbaRiskScoring.IncidentRisk(id, items));
    }

    [Fact]
    public void B300_009_MaintenanceAndSuppressionChangeActionabilityWithoutChangingScore()
    {
        var id = Guid.NewGuid();
        var cached = Cached(Snapshot(id, blocking: new(10, 60_000)));
        var plain = DbaRiskScoring.Evaluate(id, cached, [], Metadata(id), Now);
        var maintenance = DbaRiskScoring.Evaluate(id, cached, [], Metadata(id, maintenance: Window("maintenance")), Now);
        var suppressed = DbaRiskScoring.Evaluate(id, cached, [], Metadata(id, suppression: Window("suppression")), Now);
        Assert.True(plain.Actionable);
        Assert.False(maintenance.Actionable);
        Assert.False(suppressed.Actionable);
        Assert.Equal(plain.Score, maintenance.Score);
        Assert.Equal(plain.Score, suppressed.Score);
    }

    [Fact]
    public void B300_010_FleetRiskReadUsesCachePeekAndNeverCollects()
    {
        var clock = new FixedTimeProvider(Now);
        var id = Guid.NewGuid();
        var cache = new PeekOnlyCache(id, Cached(Snapshot(id)));
        var service = new DbaFleetRiskService(
            new RegistrationStore([Registration(id)]),
            cache,
            new IncidentStore([]),
            new MetadataStore(Metadata(id)),
            clock);
        var result = Assert.Single(service.Read());
        Assert.Equal(id, result.RegistrationId);
        Assert.Equal(1, cache.PeekCalls);
        Assert.Equal(0, cache.CollectionCalls);
    }

    private static SnapshotCacheResult Cached(ServerHealthSnapshot snapshot, SnapshotFreshness freshness = SnapshotFreshness.Fresh) =>
        new(snapshot, freshness, freshness == SnapshotFreshness.Fresh ? TimeSpan.FromSeconds(5) : TimeSpan.FromMinutes(1));

    private static ServerHealthSnapshot Snapshot(
        Guid id,
        int databaseTotal = 10,
        int databaseOnline = 10,
        MemoryHealthSnapshot? memory = null,
        BackupHealthSnapshot? backups = null,
        BlockingHealthSnapshot? blocking = null,
        PerformanceHealthSnapshot? performance = null) =>
        new(id, "SQL", "17.0", "Enterprise", null, 3600, databaseTotal, databaseOnline, Now,
            memory, null, backups, null, null, blocking, performance);

    private static MemoryHealthSnapshot Memory(int percent, bool physicalLow = false) =>
        new(1_000_000, 200_000, 700_000, percent, physicalLow, false, "Available physical memory is high");

    private static OperatorWindow Window(string reason) => new(Now.AddMinutes(-10), Now.AddMinutes(10), reason);

    private static ServerOperatorMetadata Metadata(Guid id, OperatorWindow? maintenance = null, OperatorWindow? suppression = null) =>
        new(id, ServerEnvironmentClass.Production, "core", ["tier-1"], maintenance, suppression, Now);

    private static HealthIncident Incident(Guid id, string rule, FindingSeverity severity, IncidentStatus status) =>
        new($"{id:N}:{rule}", id, rule, severity, rule, "cached evidence", Now.AddMinutes(-10), Now, 1, status);

    private static ServerRegistration Registration(Guid id) => new(
        id, "SQL", new SqlServerEndpoint("sql.internal", 1433), SqlAuthenticationMode.IntegratedSecurity, null, true, Now);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class RegistrationStore(IReadOnlyList<ServerRegistration> values) : IServerRegistrationRepository
    {
        public IReadOnlyList<ServerRegistration> GetAll() => values;
        public ServerRegistration? GetById(Guid id) => values.FirstOrDefault(item => item.Id == id);
        public void Upsert(ServerRegistration registration) => throw new NotSupportedException();
        public bool Remove(Guid id) => false;
    }
    private sealed class IncidentStore(IReadOnlyList<HealthIncident> values) : IHealthIncidentRepository
    {
        public void Apply(IEnumerable<HealthFinding> findings) => throw new NotSupportedException();
        public void Reconcile(Guid registrationId, DateTimeOffset observedAtUtc, IEnumerable<HealthFinding> activeFindings, bool canResolve) => throw new NotSupportedException();
        public IReadOnlyList<HealthIncident> GetAll() => values;
        public HealthIncident? GetById(string id) => values.FirstOrDefault(item => item.Id == id);
        public bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next) => false;
    }
    private sealed class MetadataStore(ServerOperatorMetadata value) : IOperatorMetadataStore
    {
        public ServerOperatorMetadata GetServer(Guid registrationId) => registrationId == value.RegistrationId ? value : throw new KeyNotFoundException();
        public void UpsertServer(ServerOperatorMetadata metadata) => throw new NotSupportedException();
        public IncidentOperatorMetadata GetIncident(string incidentId) => InMemoryOperatorMetadataStore.EmptyIncident(incidentId, Now);
        public void AssignIncident(string incidentId, string? assignee) => throw new NotSupportedException();
        public void AddIncidentNote(string incidentId, string actor, string note) => throw new NotSupportedException();
        public void SetRecommendationAcknowledged(string incidentId, string recommendationKey, bool acknowledged) => throw new NotSupportedException();
        public EnterpriseOperatorSnapshot Snapshot() => new([value], []);
    }
    private sealed class PeekOnlyCache(Guid id, SnapshotCacheResult result) : IServerHealthSnapshotCache
    {
        public int PeekCalls { get; private set; }
        public int CollectionCalls { get; private set; }
        public SnapshotCacheResult? Peek(Guid registrationId) { PeekCalls++; return registrationId == id ? result : null; }
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) { CollectionCalls++; throw new InvalidOperationException("Collection is forbidden on risk read."); }
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default) { CollectionCalls++; throw new InvalidOperationException("Collection is forbidden on risk read."); }
    }
}
