using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800IncidentRepositoryQueryTests
{
    private static readonly Guid ServerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ServerB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Project_ReturnsBoundedDeterministicPageWithExactGlobalSummary()
    {
        var source = new[]
        {
            Incident("warning-new", ServerA, FindingSeverity.Warning, IncidentStatus.Open, Now),
            Incident("critical-old", ServerA, FindingSeverity.Critical, IncidentStatus.Open, Now.AddMinutes(-2)),
            Incident("critical-new", ServerA, FindingSeverity.Critical, IncidentStatus.Acknowledged, Now.AddMinutes(-1)),
            Incident("resolved", ServerA, FindingSeverity.Warning, IncidentStatus.Resolved, Now.AddMinutes(1)),
            Incident("other", ServerB, FindingSeverity.Critical, IncidentStatus.Open, Now.AddMinutes(2))
        };

        var result = IncidentRepositoryRead.Project(source, new IncidentRepositoryQuery(
            RegistrationIds: new[] { ServerA },
            ExcludeResolved: true,
            Offset: 1,
            Limit: 1));

        Assert.Equal(4, result.Summary.Open);
        Assert.Equal(1, result.Summary.Acknowledged);
        Assert.Equal(1, result.Summary.Resolved);
        Assert.Equal(3, result.Summary.Critical);
        Assert.Equal(2, result.Summary.Warning);
        Assert.Equal(3, result.TotalMatched);
        Assert.True(result.HasMore);
        Assert.Equal("critical-old", Assert.Single(result.Items).Id);
    }

    [Fact]
    public void Project_AppliesStatusSeverityAndRuleFiltersWithoutChangingGlobalSummary()
    {
        var source = new[]
        {
            Incident("one", ServerA, FindingSeverity.Critical, IncidentStatus.Open, Now, "rule.target"),
            Incident("two", ServerA, FindingSeverity.Critical, IncidentStatus.Acknowledged, Now, "rule.target"),
            Incident("three", ServerA, FindingSeverity.Warning, IncidentStatus.Open, Now, "rule.target"),
            Incident("four", ServerA, FindingSeverity.Critical, IncidentStatus.Open, Now, "rule.other")
        };

        var result = IncidentRepositoryRead.Project(source, new IncidentRepositoryQuery(
            Status: IncidentStatus.Open,
            Severity: FindingSeverity.Critical,
            RuleId: " rule.target ",
            Limit: 10));

        Assert.Equal("one", Assert.Single(result.Items).Id);
        Assert.Equal(1, result.TotalMatched);
        Assert.False(result.HasMore);
        Assert.Equal(3, result.Summary.Open);
        Assert.Equal(1, result.Summary.Acknowledged);
    }

    [Fact]
    public void IncidentWorkflow_UsesRepositoryReadInsteadOfGetAll()
    {
        var repository = new ReadOnlyQueryRepository(Incident("one", ServerA, FindingSeverity.Warning, IncidentStatus.Open, Now));
        var workflow = new IncidentWorkflowService(repository, new RecommendationEngine(), new AdvisorContextBuilder(), new DisabledAdvisorProvider());

        var result = workflow.Query(new IncidentQuery(Limit: 50));

        Assert.Equal(1, repository.ReadCount);
        Assert.Equal("one", Assert.Single(result.Items).Id);
        Assert.Equal(1, result.Summary.Open);
    }

    [Fact]
    public void InMemoryRepository_OverridesBoundedReadContract()
    {
        var repository = new InMemoryHealthIncidentRepository();
        repository.Apply([
            new HealthFinding(ServerA, "rule.a", FindingSeverity.Warning, "A", "evidence", Now),
            new HealthFinding(ServerB, "rule.b", FindingSeverity.Critical, "B", "evidence", Now)
        ]);

        var result = repository.Read(new IncidentRepositoryQuery(RegistrationIds: new[] { ServerB }, Limit: 10));

        Assert.Equal(1, result.TotalMatched);
        Assert.Equal(ServerB, Assert.Single(result.Items).RegistrationId);
        Assert.Equal(2, result.Summary.Open);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1000001, 1)]
    [InlineData(0, 0)]
    [InlineData(0, 1001)]
    public void Project_InvalidBoundsFailClosed(int offset, int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IncidentRepositoryRead.Project(Array.Empty<HealthIncident>(), new IncidentRepositoryQuery(Offset: offset, Limit: limit)));
    }

    private static HealthIncident Incident(
        string id,
        Guid registrationId,
        FindingSeverity severity,
        IncidentStatus status,
        DateTimeOffset lastSeen,
        string? ruleId = null) =>
        new(id, registrationId, ruleId ?? $"rule.{id}", severity, "Title", "Evidence", lastSeen.AddMinutes(-1), lastSeen, 1, status);

    private sealed class ReadOnlyQueryRepository(HealthIncident incident) : IHealthIncidentRepository
    {
        public int ReadCount { get; private set; }
        public void Apply(IEnumerable<HealthFinding> findings) => throw new NotSupportedException();
        public void Reconcile(Guid registrationId, DateTimeOffset observedAtUtc, IEnumerable<HealthFinding> activeFindings, bool canResolve) => throw new NotSupportedException();
        public IReadOnlyList<HealthIncident> GetAll() => throw new InvalidOperationException("Operator query must not use GetAll().");
        public IncidentRepositoryReadResult Read(IncidentRepositoryQuery query)
        {
            ReadCount++;
            return IncidentRepositoryRead.Project(new[] { incident }, query);
        }
        public HealthIncident? GetById(string id) => string.Equals(id, incident.Id, StringComparison.Ordinal) ? incident : null;
        public bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next) => false;
    }
}
