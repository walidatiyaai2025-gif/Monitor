using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800MaintenancePolicyAvailabilityTests
{
    [Fact]
    public void UnreadableServerPolicy_RendersNotEvaluatedInsteadOfAssumingNonProduction()
    {
        var registration = Registration("SQL-MAINT-POLICY-DOWN");
        var registrations = new InMemoryServerRegistrationRepository();
        registrations.Upsert(registration);
        var metadata = new InMemoryOperatorMetadataStore(TimeProvider.System);
        var policy = new OperatorPolicyReadService(
            new FailingReadOperatorMetadataStore(metadata),
            TimeProvider.System);
        var controller = new MaintenanceDecisionSupportController(
            registrations,
            policy,
            new InMemoryHealthIncidentRepository());

        var view = Assert.IsType<ViewResult>(controller.Index(registration.Id, "StatisticsUpdate"));
        var model = Assert.IsType<MaintenanceDecisionSupportPageViewModel>(view.Model);

        Assert.False(model.Policy.PolicyReadable);
        Assert.Null(model.Policy.Metadata);
        Assert.Null(model.MaintenanceWindowActive);
        Assert.True(model.IncidentEvidenceComplete);
        Assert.Equal(0, model.ActiveCriticalIncidents);
        Assert.Equal(MaintenanceDecisionSupportStatus.NotEvaluated, model.Result.Status);
        Assert.Null(model.Result.Decision);
        Assert.Contains("environment-class", model.Result.MissingInputs);
    }

    [Fact]
    public void ReadableServerPolicy_RetainsOwnedMetadataWithoutSecondStoreRead()
    {
        var registration = Registration("SQL-MAINT-POLICY-OK");
        var metadata = new CountingOperatorMetadataStore(new InMemoryOperatorMetadataStore(TimeProvider.System));
        metadata.UpsertServer(ServerMetadata(registration.Id));
        var policy = new OperatorPolicyReadService(metadata, TimeProvider.System);

        var state = policy.GetServer(registration.Id);

        Assert.True(state.PolicyReadable);
        Assert.NotNull(state.Metadata);
        Assert.Equal(ServerEnvironmentClass.Production, state.Metadata!.Environment);
        Assert.Equal(1, metadata.ServerReads);
    }

    private static ServerRegistration Registration(string displayName) => new(
        Guid.NewGuid(),
        displayName,
        new SqlServerEndpoint("sql.internal", 1433),
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        DateTimeOffset.UtcNow);

    private static ServerOperatorMetadata ServerMetadata(Guid registrationId) => new(
        registrationId,
        ServerEnvironmentClass.Production,
        "core",
        ["tier-1"],
        null,
        null,
        DateTimeOffset.UtcNow);

    private sealed class FailingReadOperatorMetadataStore(IOperatorMetadataStore inner) : IOperatorMetadataStore
    {
        public ServerOperatorMetadata GetServer(Guid registrationId) =>
            throw new InvalidDataException("Operator server metadata is unavailable.");

        public void UpsertServer(ServerOperatorMetadata metadata) => inner.UpsertServer(metadata);
        public IncidentOperatorMetadata GetIncident(string incidentId) => inner.GetIncident(incidentId);
        public void AssignIncident(string incidentId, string? assignee) => inner.AssignIncident(incidentId, assignee);
        public void AddIncidentNote(string incidentId, string actor, string note) => inner.AddIncidentNote(incidentId, actor, note);
        public void SetRecommendationAcknowledged(string incidentId, string recommendationKey, bool acknowledged) => inner.SetRecommendationAcknowledged(incidentId, recommendationKey, acknowledged);
        public EnterpriseOperatorSnapshot Snapshot() => inner.Snapshot();
    }

    private sealed class CountingOperatorMetadataStore(IOperatorMetadataStore inner) : IOperatorMetadataStore
    {
        public int ServerReads { get; private set; }

        public ServerOperatorMetadata GetServer(Guid registrationId)
        {
            ServerReads++;
            return inner.GetServer(registrationId);
        }

        public void UpsertServer(ServerOperatorMetadata metadata) => inner.UpsertServer(metadata);
        public IncidentOperatorMetadata GetIncident(string incidentId) => inner.GetIncident(incidentId);
        public void AssignIncident(string incidentId, string? assignee) => inner.AssignIncident(incidentId, assignee);
        public void AddIncidentNote(string incidentId, string actor, string note) => inner.AddIncidentNote(incidentId, actor, note);
        public void SetRecommendationAcknowledged(string incidentId, string recommendationKey, bool acknowledged) => inner.SetRecommendationAcknowledged(incidentId, recommendationKey, acknowledged);
        public EnterpriseOperatorSnapshot Snapshot() => inner.Snapshot();
    }
}
