using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class IncidentWorkflowServiceTests
{
    private static readonly Guid RegistrationId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTimeOffset Observed = new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OperatorTime = new(2026, 8, 10, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Workflow_FiltersTransitionsBuildsDisabledAdvisorDetailAndAuditsOperator()
    {
        var repository = new InMemoryHealthIncidentRepository();
        repository.Apply([Finding("backup.full-gap", FindingSeverity.Warning), Finding("database.suspect", FindingSeverity.Critical)]);
        var audit = new InMemoryOperatorAuditTrail();
        var service = Service(repository, audit);

        var page = service.Query(new(Severity: FindingSeverity.Critical));
        var incident = Assert.Single(page.Items);
        Assert.Equal(2, page.Summary.Open);
        Assert.True(service.Acknowledge(incident.Id, "DOMAIN\\dba.operator"));
        Assert.True(service.Resolve(incident.Id, "DOMAIN\\dba.operator"));
        Assert.True(service.Reopen(incident.Id, "DOMAIN\\dba.operator"));

        var events = audit.GetRecent();
        Assert.Equal(3, events.Count);
        Assert.Equal(OperatorAuditAction.IncidentReopened, events[0].Action);
        Assert.Equal("Resolved", events[0].PreviousState);
        Assert.Equal("Open", events[0].NewState);
        Assert.Equal(OperatorAuditAction.IncidentResolved, events[1].Action);
        Assert.Equal("Acknowledged", events[1].PreviousState);
        Assert.Equal("Resolved", events[1].NewState);
        Assert.Equal(OperatorAuditAction.IncidentAcknowledged, events[2].Action);
        Assert.Equal("Open", events[2].PreviousState);
        Assert.Equal("Acknowledged", events[2].NewState);
        Assert.All(events, item => Assert.Equal("DOMAIN\\dba.operator", item.Actor));
        Assert.All(events, item => Assert.Equal(OperatorTime, item.OccurredAtUtc));
        Assert.All(events, item => Assert.Equal(incident.Id, item.ResourceId));

        var details = await service.GetDetailsAsync(incident.Id, default);
        Assert.NotNull(details);
        Assert.NotNull(details.Recommendation);
        Assert.Equal(AdvisorStatus.Disabled, details.Advisor.Status);
    }

    [Fact]
    public void MissingActor_FailsClosedAndCreatesNoAuditEvent()
    {
        var repository = new InMemoryHealthIncidentRepository();
        repository.Apply([Finding("backup.full-gap", FindingSeverity.Warning)]);
        var audit = new InMemoryOperatorAuditTrail();
        var service = Service(repository, audit);
        var incident = Assert.Single(repository.GetAll());

        Assert.False(service.Acknowledge(incident.Id, "  "));

        Assert.Equal(IncidentStatus.Open, repository.GetById(incident.Id)!.Status);
        Assert.Empty(audit.GetRecent());
    }

    [Fact]
    public void RejectedTransition_CreatesNoSuccessAuditEvent()
    {
        var repository = new InMemoryHealthIncidentRepository();
        repository.Apply([Finding("backup.full-gap", FindingSeverity.Warning)]);
        var audit = new InMemoryOperatorAuditTrail();
        var service = Service(repository, audit);
        var incident = Assert.Single(repository.GetAll());

        Assert.False(service.Reopen(incident.Id, "Administrator"));

        Assert.Equal(IncidentStatus.Open, repository.GetById(incident.Id)!.Status);
        Assert.Empty(audit.GetRecent());
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

    private static IncidentWorkflowService Service(
        IHealthIncidentRepository repository,
        IOperatorAuditTrail? audit = null) => new(
        repository,
        new RecommendationEngine(),
        new AdvisorContextBuilder(),
        new DisabledAdvisorProvider(),
        audit ?? new InMemoryOperatorAuditTrail(),
        new FixedTimeProvider(OperatorTime));

    private static HealthFinding Finding(string rule, FindingSeverity severity) =>
        new(RegistrationId, rule, severity, rule, "bounded evidence", Observed);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
