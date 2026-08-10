using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class IncidentTransitionAuditEnrichmentTests
{
    private static readonly Guid RegistrationId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly DateTimeOffset Observed = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AuditTime = new(2026, 8, 10, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void SuccessfulAcknowledge_AuditsAuthenticatedActorAndBeforeAfterState()
    {
        var repository = RepositoryWithOpenIncident();
        var audit = new InMemoryAuditStore(new FixedTimeProvider(AuditTime));
        var controller = Controller(repository, audit, "DOMAIN\\operator.one");
        var incident = Assert.Single(repository.GetAll());

        var response = controller.AcknowledgeIncident(incident.Id);

        Assert.IsType<RedirectToActionResult>(response);
        Assert.Equal(IncidentStatus.Acknowledged, repository.GetById(incident.Id)!.Status);
        var auditEvent = Assert.Single(audit.Read());
        Assert.Equal(AuditTime, auditEvent.TimestampUtc);
        Assert.Equal("DOMAIN\\operator.one", auditEvent.Actor);
        Assert.Equal("incident.acknowledge", auditEvent.Action);
        Assert.Equal(incident.Id, auditEvent.Target);
        Assert.Equal("Open->Acknowledged", auditEvent.Outcome);
    }

    [Fact]
    public void MissingActor_FailsClosedBeforeMutationAndDoesNotAudit()
    {
        var repository = RepositoryWithOpenIncident();
        var audit = new InMemoryAuditStore(new FixedTimeProvider(AuditTime));
        var controller = Controller(repository, audit, actor: null);
        var incident = Assert.Single(repository.GetAll());

        var response = controller.ResolveIncident(incident.Id);

        Assert.IsType<ForbidResult>(response);
        Assert.Equal(IncidentStatus.Open, repository.GetById(incident.Id)!.Status);
        Assert.Empty(audit.Read());
    }

    [Fact]
    public void RejectedTransition_AuditsBoundedCurrentStateContext()
    {
        var repository = RepositoryWithOpenIncident();
        var audit = new InMemoryAuditStore(new FixedTimeProvider(AuditTime));
        var controller = Controller(repository, audit, "operator.two");
        var incident = Assert.Single(repository.GetAll());

        var response = controller.ReopenIncident(incident.Id);

        Assert.IsType<ConflictObjectResult>(response);
        Assert.Equal(IncidentStatus.Open, repository.GetById(incident.Id)!.Status);
        var auditEvent = Assert.Single(audit.Read());
        Assert.Equal("operator.two", auditEvent.Actor);
        Assert.Equal("incident.reopen", auditEvent.Action);
        Assert.Equal("rejected:current=Open", auditEvent.Outcome);
        Assert.DoesNotContain("bounded evidence", auditEvent.Outcome, StringComparison.Ordinal);
    }

    private static InMemoryHealthIncidentRepository RepositoryWithOpenIncident()
    {
        var repository = new InMemoryHealthIncidentRepository();
        repository.Apply([
            new HealthFinding(
                RegistrationId,
                "backup.full-gap",
                FindingSeverity.Warning,
                "Backup gap",
                "bounded evidence",
                Observed)
        ]);
        return repository;
    }

    private static OperationsController Controller(
        IHealthIncidentRepository repository,
        IAuditStore audit,
        string? actor)
    {
        var workflow = new IncidentWorkflowService(
            repository,
            new RecommendationEngine(),
            new AdvisorContextBuilder(),
            new DisabledAdvisorProvider());
        var controller = new OperationsController(
            null!,
            null!,
            workflow,
            null,
            audit,
            null);
        var identity = actor is null
            ? new ClaimsIdentity(authenticationType: "test")
            : new ClaimsIdentity([new Claim(ClaimTypes.Name, actor)], "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
        return controller;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
