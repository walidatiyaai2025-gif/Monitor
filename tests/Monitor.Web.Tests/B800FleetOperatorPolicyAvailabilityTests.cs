using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800FleetOperatorPolicyAvailabilityTests
{
    [Fact]
    public void ServerPolicyFailure_KeepsBaseFleetEvidenceAndDoesNotFabricatePolicyFacts()
    {
        var registration = Registration("SQL-POLICY-DOWN");
        var registrations = Registrations(registration);
        var metadata = new InMemoryOperatorMetadataStore(TimeProvider.System);
        var incidents = new InMemoryHealthIncidentRepository();
        var cache = new PeekOnlyEmptyCache();
        var service = new FleetIntelligenceService(
            registrations,
            cache,
            new FailingReadOperatorMetadataStore(metadata, failServerReads: true),
            incidents,
            TimeProvider.System);

        var snapshot = service.Read();

        Assert.Equal(1, snapshot.Unavailable);
        Assert.False(snapshot.ServerPolicyEvidenceComplete);
        Assert.Equal(1, snapshot.OperatorPolicyUnavailable);
        Assert.Empty(snapshot.ByEnvironment);
        Assert.Empty(snapshot.ByGroup);
        Assert.Empty(snapshot.ByTag);
        Assert.Equal(0, snapshot.Maintenance);
        Assert.Equal(0, snapshot.Suppressed);
        Assert.Equal(1, cache.PeekCalls);
        Assert.Equal(0, cache.CollectionCalls);
    }

    [Fact]
    public void IncidentPolicyFailure_WithholdsHotspotsAndDecisionSupportInsteadOfTreatingItAsUnassigned()
    {
        var registration = Registration("SQL-INCIDENT-POLICY-DOWN");
        var registrations = Registrations(registration);
        var metadata = new InMemoryOperatorMetadataStore(TimeProvider.System);
        metadata.UpsertServer(ServerMetadata(registration.Id));
        var incidents = Incidents(registration.Id);
        var service = new FleetIntelligenceService(
            registrations,
            new PeekOnlyEmptyCache(),
            new FailingReadOperatorMetadataStore(metadata, failIncidentReads: true),
            incidents,
            TimeProvider.System);

        var snapshot = service.Read();

        Assert.True(snapshot.ServerPolicyEvidenceComplete);
        Assert.True(snapshot.IncidentEvidenceComplete);
        Assert.False(snapshot.IncidentPolicyEvidenceComplete);
        Assert.Equal(1, snapshot.OperatorPolicyUnavailable);
        Assert.Empty(snapshot.RuleHotspots);
        Assert.Null(snapshot.DecisionSupport);
    }

    [Fact]
    public void ReadableIncidentWithNullAssignee_RemainsValidUnassignedEvidence()
    {
        var registration = Registration("SQL-UNASSIGNED");
        var registrations = Registrations(registration);
        var metadata = new InMemoryOperatorMetadataStore(TimeProvider.System);
        metadata.UpsertServer(ServerMetadata(registration.Id));
        var incidents = Incidents(registration.Id);
        var service = new FleetIntelligenceService(
            registrations,
            new PeekOnlyEmptyCache(),
            metadata,
            incidents,
            TimeProvider.System);

        var snapshot = service.Read();

        Assert.True(snapshot.ServerPolicyEvidenceComplete);
        Assert.True(snapshot.IncidentEvidenceComplete);
        Assert.True(snapshot.IncidentPolicyEvidenceComplete);
        Assert.Equal(0, snapshot.OperatorPolicyUnavailable);
        Assert.Single(snapshot.RuleHotspots);
        Assert.NotNull(snapshot.DecisionSupport);
        Assert.Single(snapshot.DecisionSupport!.RoutingSuggestions);
    }

    private static InMemoryServerRegistrationRepository Registrations(ServerRegistration registration)
    {
        var registrations = new InMemoryServerRegistrationRepository();
        registrations.Upsert(registration);
        return registrations;
    }

    private static InMemoryHealthIncidentRepository Incidents(Guid registrationId)
    {
        var incidents = new InMemoryHealthIncidentRepository();
        var observedAt = DateTimeOffset.UtcNow;
        incidents.Apply([
            new HealthFinding(
                registrationId,
                "memory.pressure",
                FindingSeverity.Warning,
                "Memory pressure",
                "Cached evidence",
                observedAt)
        ]);
        return incidents;
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

    private sealed class PeekOnlyEmptyCache : IServerHealthSnapshotCache
    {
        public int PeekCalls { get; private set; }
        public int CollectionCalls { get; private set; }

        public SnapshotCacheResult? Peek(Guid registrationId)
        {
            PeekCalls++;
            return null;
        }

        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            CollectionCalls++;
            throw new InvalidOperationException("Fleet GET must not collect monitored SQL.");
        }

        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            CollectionCalls++;
            throw new InvalidOperationException("Fleet GET must not collect monitored SQL.");
        }
    }

    private sealed class FailingReadOperatorMetadataStore(
        IOperatorMetadataStore inner,
        bool failServerReads = false,
        bool failIncidentReads = false) : IOperatorMetadataStore
    {
        public ServerOperatorMetadata GetServer(Guid registrationId) => failServerReads
            ? throw new InvalidDataException("Operator server metadata is unavailable.")
            : inner.GetServer(registrationId);

        public void UpsertServer(ServerOperatorMetadata metadata) => inner.UpsertServer(metadata);

        public IncidentOperatorMetadata GetIncident(string incidentId) => failIncidentReads
            ? throw new InvalidDataException("Operator incident metadata is unavailable.")
            : inner.GetIncident(incidentId);

        public void AssignIncident(string incidentId, string? assignee) => inner.AssignIncident(incidentId, assignee);
        public void AddIncidentNote(string incidentId, string actor, string note) => inner.AddIncidentNote(incidentId, actor, note);
        public void SetRecommendationAcknowledged(string incidentId, string recommendationKey, bool acknowledged) => inner.SetRecommendationAcknowledged(incidentId, recommendationKey, acknowledged);
        public EnterpriseOperatorSnapshot Snapshot() => inner.Snapshot();
    }
}
