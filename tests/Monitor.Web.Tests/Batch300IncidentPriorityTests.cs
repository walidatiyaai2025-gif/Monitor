using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300IncidentPriorityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-11T06:00:00Z");

    [Fact]
    public void B300_021_PriorityScoreIsDeterministicAndBounded()
    {
        var incident = Incident(Guid.NewGuid(), "memory.pressure", FindingSeverity.Critical, IncidentStatus.Open, 8, Now.AddHours(-3));
        var first = IncidentPriorityScoring.Score(incident, IncidentSlaBucket.Breached, false, false);
        var second = IncidentPriorityScoring.Score(incident, IncidentSlaBucket.Breached, false, false);
        Assert.Equal(first, second);
        Assert.InRange(first, 0, 100);
    }

    [Fact]
    public void B300_022_CriticalSeverityOutranksWarning()
    {
        var id = Guid.NewGuid();
        var warning = Incident(id, "rule.warning", FindingSeverity.Warning, IncidentStatus.Open, 1, Now);
        var critical = warning with { Severity = FindingSeverity.Critical, Id = warning.Id + ":critical" };
        Assert.True(IncidentPriorityScoring.Score(critical, IncidentSlaBucket.Fresh, false, false) > IncidentPriorityScoring.Score(warning, IncidentSlaBucket.Fresh, false, false));
    }

    [Fact]
    public void B300_023_BreachedSlaOutranksFreshSla()
    {
        var incident = Incident(Guid.NewGuid(), "blocking.wait", FindingSeverity.Warning, IncidentStatus.Open, 1, Now);
        Assert.True(IncidentPriorityScoring.Score(incident, IncidentSlaBucket.Breached, false, false) > IncidentPriorityScoring.Score(incident, IncidentSlaBucket.Fresh, false, false));
    }

    [Fact]
    public void B300_024_OccurrenceFrequencyAddsBoundedWeight()
    {
        var id = Guid.NewGuid();
        var once = Incident(id, "jobs.failed", FindingSeverity.Warning, IncidentStatus.Open, 1, Now);
        var frequent = once with { Occurrences = 50 };
        var onceScore = IncidentPriorityScoring.Score(once, IncidentSlaBucket.Fresh, false, false);
        var frequentScore = IncidentPriorityScoring.Score(frequent, IncidentSlaBucket.Fresh, false, false);
        Assert.Equal(20, frequentScore - onceScore);
    }

    [Fact]
    public void B300_025_SuppressionAndMaintenanceCapPriorityWithoutChangingIncidentEvidence()
    {
        var incident = Incident(Guid.NewGuid(), "memory.pressure", FindingSeverity.Critical, IncidentStatus.Open, 10, Now.AddHours(-5));
        var normal = IncidentPriorityScoring.Score(incident, IncidentSlaBucket.Breached, false, false);
        var suppressed = IncidentPriorityScoring.Score(incident, IncidentSlaBucket.Breached, true, false);
        var maintenance = IncidentPriorityScoring.Score(incident, IncidentSlaBucket.Breached, false, true);
        Assert.True(normal > suppressed);
        Assert.Equal(35, suppressed);
        Assert.Equal(35, maintenance);
        Assert.Equal("cached evidence", incident.Evidence);
    }

    [Fact]
    public void B300_026_AssigneeFilterReturnsOnlyMatchingQueueRows()
    {
        var id = Guid.NewGuid();
        var incident = Incident(id, "memory.pressure", FindingSeverity.Warning, IncidentStatus.Open, 1, Now);
        var metadata = new TestMetadataStore(id, Now);
        metadata.AssignIncident(incident.Id, "DBA-A");
        var service = Service([incident], metadata);
        Assert.Single(service.Queue("DBA-A"));
        Assert.Empty(service.Queue("DBA-B"));
    }

    [Theory]
    [InlineData("memory.pressure", "memory")]
    [InlineData("backup:missing", "backup")]
    [InlineData("jobs/failed", "jobs")]
    [InlineData("custom", "custom")]
    public void B300_027_RuleFamilyGroupingIsDeterministic(string rule, string expected)
    {
        Assert.Equal(expected, IncidentPriorityScoring.RuleFamily(rule));
    }

    [Fact]
    public void B300_028_DuplicateProjectionCollapsesSameServerAndRule()
    {
        var id = Guid.NewGuid();
        var first = Incident(id, "memory.pressure", FindingSeverity.Warning, IncidentStatus.Open, 2, Now.AddMinutes(-30));
        var second = first with { Id = first.Id + ":2", Severity = FindingSeverity.Critical, Occurrences = 3, FirstSeenUtc = Now.AddHours(-2), LastSeenUtc = Now };
        var queue = Service([first, second], new TestMetadataStore(id, Now)).Queue();
        var row = Assert.Single(queue);
        Assert.Equal(5, row.Incident.Occurrences);
        Assert.Equal(FindingSeverity.Critical, row.Incident.Severity);
        Assert.Equal(Now.AddHours(-2), row.Incident.FirstSeenUtc);
    }

    [Fact]
    public void B300_029_PriorityQueueIsTopNBoundedAndActionableFirst()
    {
        var id = Guid.NewGuid();
        var metadata = new TestMetadataStore(id, Now);
        var items = Enumerable.Range(0, 150).Select(i => Incident(id, $"rule.{i:000}", i % 2 == 0 ? FindingSeverity.Critical : FindingSeverity.Warning, IncidentStatus.Open, i + 1, Now.AddMinutes(-i))).ToArray();
        var queue = Service(items, metadata).Queue(limit: 500);
        Assert.Equal(100, queue.Count);
        Assert.True(queue.Zip(queue.Skip(1)).All(pair => pair.First.Score >= pair.Second.Score));
    }

    [Fact]
    public void B300_030_ResolvedIncidentsAreExcludedAndFamilyRollupIsBounded()
    {
        var id = Guid.NewGuid();
        var items = new[]
        {
            Incident(id, "memory.pressure", FindingSeverity.Warning, IncidentStatus.Open, 1, Now),
            Incident(id, "memory.low", FindingSeverity.Critical, IncidentStatus.Open, 1, Now),
            Incident(id, "backup.missing", FindingSeverity.Critical, IncidentStatus.Resolved, 1, Now)
        };
        var service = Service(items, new TestMetadataStore(id, Now));
        Assert.Equal(2, service.Queue().Count);
        var families = service.GroupByRuleFamily();
        Assert.Equal(2, families["memory"]);
        Assert.DoesNotContain("backup", families.Keys);
    }

    private static IncidentPriorityService Service(IReadOnlyList<HealthIncident> incidents, TestMetadataStore metadata) =>
        new(new IncidentStore(incidents), metadata, new FixedTimeProvider(Now));

    private static HealthIncident Incident(Guid id, string rule, FindingSeverity severity, IncidentStatus status, int occurrences, DateTimeOffset firstSeen) =>
        new($"{id:N}:{rule}:{occurrences}", id, rule, severity, rule, "cached evidence", firstSeen, Now, occurrences, status);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class IncidentStore(IReadOnlyList<HealthIncident> values) : IHealthIncidentRepository
    {
        public void Apply(IEnumerable<HealthFinding> findings) => throw new NotSupportedException();
        public void Reconcile(Guid registrationId, DateTimeOffset observedAtUtc, IEnumerable<HealthFinding> activeFindings, bool canResolve) => throw new NotSupportedException();
        public IReadOnlyList<HealthIncident> GetAll() => values;
        public HealthIncident? GetById(string id) => values.FirstOrDefault(item => item.Id == id);
        public bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next) => false;
    }
    private sealed class TestMetadataStore(Guid serverId, DateTimeOffset now) : IOperatorMetadataStore
    {
        private readonly Dictionary<string, IncidentOperatorMetadata> _incidents = new(StringComparer.Ordinal);
        private ServerOperatorMetadata _server = new(serverId, ServerEnvironmentClass.Production, "core", [], null, null, now);
        public ServerOperatorMetadata GetServer(Guid registrationId) => _server;
        public void UpsertServer(ServerOperatorMetadata metadata) => _server = metadata;
        public IncidentOperatorMetadata GetIncident(string incidentId) => _incidents.TryGetValue(incidentId, out var value) ? value : InMemoryOperatorMetadataStore.EmptyIncident(incidentId, now);
        public void AssignIncident(string incidentId, string? assignee)
        {
            var current = GetIncident(incidentId);
            _incidents[incidentId] = current with { Assignee = assignee, UpdatedAtUtc = now };
        }
        public void AddIncidentNote(string incidentId, string actor, string note) => throw new NotSupportedException();
        public void SetRecommendationAcknowledged(string incidentId, string recommendationKey, bool acknowledged) => throw new NotSupportedException();
        public EnterpriseOperatorSnapshot Snapshot() => new([_server], _incidents.Values.ToArray());
    }
}
