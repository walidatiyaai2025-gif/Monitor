using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class HealthRecommendationServiceTests
{
    private static readonly Guid RegistrationId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly DateTimeOffset Observed = new(2026, 8, 10, 9, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("snapshot.stale")]
    [InlineData("database.unavailable")]
    [InlineData("database.suspect")]
    [InlineData("backup.full-gap")]
    [InlineData("agent.failed-job")]
    [InlineData("blocking.active")]
    [InlineData("memory.pressure")]
    [InlineData("performance.runnable")]
    public void CurrentAllowlistedRules_HaveDeterministicRecommendations(string ruleId)
    {
        var incident = Incident(ruleId, "bounded evidence");
        var recommendation = new HealthRecommendationService().Create(incident);

        Assert.NotNull(recommendation);
        Assert.Equal(ruleId, recommendation.RuleId);
        Assert.Equal(incident.Severity, recommendation.Severity);
        Assert.Equal("bounded evidence", recommendation.Evidence);
        Assert.NotEmpty(recommendation.Problem);
        Assert.NotEmpty(recommendation.Rationale);
        Assert.NotEmpty(recommendation.Steps);
        Assert.Equal(Enumerable.Range(1, recommendation.Steps.Count), recommendation.Steps.Select(step => step.Order));
    }

    [Fact]
    public void UnsupportedRule_FailsClosed()
    {
        var recommendation = new HealthRecommendationService().Create(Incident("future.unknown-rule", "evidence"));

        Assert.Null(recommendation);
    }

    [Theory]
    [InlineData("database.unavailable")]
    [InlineData("database.suspect")]
    [InlineData("backup.full-gap")]
    [InlineData("agent.failed-job")]
    [InlineData("blocking.active")]
    [InlineData("memory.pressure")]
    [InlineData("performance.runnable")]
    public void DiagnosticSql_IsFixedReadOnlyAndDoesNotInterpolateEvidence(string ruleId)
    {
        const string hostileEvidence = "DROP TABLE sensitive_marker; Password=secret";
        var recommendation = Assert.IsType<HealthRecommendation>(new HealthRecommendationService().Create(Incident(ruleId, hostileEvidence)));
        var proposal = Assert.IsType<DiagnosticSqlProposal>(recommendation.DiagnosticSql);
        var sql = proposal.Sql;

        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(hostileEvidence, sql, StringComparison.Ordinal);
        foreach (var forbidden in new[] { " INSERT ", " UPDATE ", " DELETE ", " DROP ", " ALTER ", " RESTORE ", " BACKUP ", " DBCC ", " KILL " })
        {
            Assert.DoesNotContain(forbidden, $" {sql} ", StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void StaleSnapshotRecommendation_DoesNotInventDiagnosticSql()
    {
        var recommendation = Assert.IsType<HealthRecommendation>(new HealthRecommendationService().Create(Incident("snapshot.stale", "stale")));

        Assert.Null(recommendation.DiagnosticSql);
        Assert.All(recommendation.Steps, step => Assert.DoesNotContain("execute automatically", step.Detail, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadService_ReturnsRecommendationFromExistingIncidentWithoutSqlCollection()
    {
        var repository = new InMemoryHealthIncidentRepository();
        repository.Apply([new HealthFinding(RegistrationId, "blocking.active", FindingSeverity.Critical, "Active blocking", "2 blocked", Observed)]);
        var cache = new ThrowingCache();
        var readService = new MonitorReadService(
            new DemoMonitorService(),
            new InMemoryServerRegistrationRepository(),
            cache,
            new HealthRuleEvaluator(),
            repository,
            new HealthRecommendationService());
        var incidentId = Assert.Single(repository.GetAll()).Id;

        var result = await readService.GetRecommendationAsync(incidentId);

        Assert.NotNull(result);
        Assert.Equal("blocking.active", result.Recommendation.RuleId);
        Assert.Equal(0, cache.CallCount);
    }

    private static HealthIncident Incident(string ruleId, string evidence) => new(
        $"{RegistrationId:N}:{ruleId}",
        RegistrationId,
        ruleId,
        FindingSeverity.Warning,
        "Finding",
        evidence,
        Observed,
        Observed,
        1,
        IncidentStatus.Open);

    private sealed class ThrowingCache : IServerHealthSnapshotCache
    {
        public int CallCount { get; private set; }

        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("Recommendation lookup must not create a collection target.");
        }

        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("Recommendation lookup must not refresh SQL.");
        }
    }
}
