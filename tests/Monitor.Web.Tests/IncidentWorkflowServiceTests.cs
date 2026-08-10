using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class IncidentWorkflowServiceTests
{
    private static readonly Guid RegistrationId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTimeOffset Observed = new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Workflow_FiltersTransitionsAndBuildsDisabledAdvisorDetail()
    {
        var repository = new InMemoryHealthIncidentRepository();
        repository.Apply([Finding("backup.full-gap", FindingSeverity.Warning), Finding("database.suspect", FindingSeverity.Critical)]);
        var service = Service(repository);

        var page = service.Query(new(Severity: FindingSeverity.Critical));
        var incident = Assert.Single(page.Items);
        Assert.Equal(2, page.Summary.Open);

        var acknowledged = service.Acknowledge(incident.Id);
        Assert.True(acknowledged.Applied);
        Assert.Equal(IncidentStatus.Open, acknowledged.PreviousStatus);
        Assert.Equal(IncidentStatus.Acknowledged, acknowledged.NewStatus);

        var resolved = service.Resolve(incident.Id);
        Assert.True(resolved.Applied);
        Assert.Equal(IncidentStatus.Acknowledged, resolved.PreviousStatus);
        Assert.Equal(IncidentStatus.Resolved, resolved.NewStatus);

        var reopened = service.Reopen(incident.Id);
        Assert.True(reopened.Applied);
        Assert.Equal(IncidentStatus.Resolved, reopened.PreviousStatus);
        Assert.Equal(IncidentStatus.Open, reopened.NewStatus);

        var details = await service.GetDetailsAsync(incident.Id, default);
        Assert.NotNull(details);
        Assert.NotNull(details.Recommendation);
        Assert.Equal(AdvisorStatus.Disabled, details.Advisor.Status);
    }

    [Fact]
    public void RejectedTransition_ReturnsCurrentStateContext()
    {
        var repository = new InMemoryHealthIncidentRepository();
        repository.Apply([Finding("backup.full-gap", FindingSeverity.Warning)]);
        var service = Service(repository);
        var incident = Assert.Single(repository.GetAll());

        var result = service.Reopen(incident.Id);

        Assert.False(result.Applied);
        Assert.Equal(IncidentStatus.Open, result.PreviousStatus);
        Assert.Equal(IncidentStatus.Open, result.NewStatus);
        Assert.Equal("rejected:current=Open", result.AuditOutcome);
    }

    [Fact]
    public void MissingIncident_ReturnsBoundedNotFoundResult()
    {
        var result = Service(new InMemoryHealthIncidentRepository()).Resolve("missing");

        Assert.False(result.Applied);
        Assert.Null(result.PreviousStatus);
        Assert.Null(result.NewStatus);
        Assert.Equal("rejected:not-found", result.AuditOutcome);
    }

    [Fact]
    public void DuplicateObservation_IsIdempotent()
    {
        var repository = new InMemoryHealthIncidentRepository();
        var finding = Finding("agent.failed-job", FindingSeverity.Warning);
        repository.Apply([finding, finding]);
        Assert.Equal(1, Assert.Single(repository.GetAll()).Occurrences);
    }

    [Fact]
    public void Query_ClampsPagingAndUnknownRecommendationFailsClosed()
    {
        var repository = new InMemoryHealthIncidentRepository();
        repository.Apply([Finding("unknown.rule", FindingSeverity.Warning)]);
        var service = Service(repository);
        var page = service.Query(new(Offset: -10, Limit: 500));
        Assert.Equal(0, page.Query.Offset);
        Assert.Equal(100, page.Query.Limit);
        Assert.Null(new RecommendationEngine().Build(Assert.Single(page.Items)));
    }

    [Fact]
    public void NewerObservation_PreservesAcknowledgedState()
    {
        var repository = new InMemoryHealthIncidentRepository();
        var finding = Finding("backup.full-gap", FindingSeverity.Warning);
        repository.Apply([finding]);
        var incident = Assert.Single(repository.GetAll());
        Assert.True(repository.TrySetStatus(incident.Id, IncidentStatus.Open, IncidentStatus.Acknowledged));
        repository.Apply([finding with { ObservedAtUtc = Observed.AddMinutes(1) }]);
        Assert.Equal(IncidentStatus.Acknowledged, Assert.Single(repository.GetAll()).Status);
    }

    [Fact]
    public void AdvisorContext_IsStrictlyBounded()
    {
        var incident = new HealthIncident("id", RegistrationId, new string('r', 100), FindingSeverity.Warning, "title", new string('e', 800), Observed, Observed, 1, IncidentStatus.Open);
        var context = new AdvisorContextBuilder().Build(incident, null);
        Assert.Equal(80, context.RuleId.Length);
        Assert.Equal(500, context.Evidence.Length);
    }

    private static IncidentWorkflowService Service(IHealthIncidentRepository repository) => new(
        repository, new RecommendationEngine(), new AdvisorContextBuilder(), new DisabledAdvisorProvider());

    private static HealthFinding Finding(string rule, FindingSeverity severity) =>
        new(RegistrationId, rule, severity, rule, "bounded evidence", Observed);
}
