using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record DbaIntelligenceOptions
{
    public long StorageCapacityBytes { get; init; }
    public int HistoryHours { get; init; } = 6;
    public int HistoryPoints { get; init; } = 48;

    public void Validate()
    {
        if (StorageCapacityBytes < 0) throw new InvalidOperationException("Storage capacity cannot be negative.");
        if (HistoryHours is < 1 or > 24) throw new InvalidOperationException("History window must be 1..24 hours.");
        if (HistoryPoints is < 3 or > 288) throw new InvalidOperationException("History point limit must be 3..288.");
    }
}

public sealed record DbaRiskFleetCard(
    Guid RegistrationId,
    string DisplayName,
    ServerEnvironmentClass Environment,
    int Score,
    DbaRiskLevel Level,
    bool Actionable,
    string Freshness);

public sealed record DbaTrendSummaryCard(
    Guid RegistrationId,
    string DisplayName,
    DbaTrendProjection Memory,
    DbaTrendProjection Blocking,
    DbaTrendProjection Runnable,
    DbaTrendProjection DatabaseAvailability);

public sealed record DbaPriorityIncidentCard(
    string IncidentId,
    string RuleId,
    FindingSeverity Severity,
    int Score,
    bool Actionable,
    string? Assignee,
    IncidentSlaBucket SlaBucket);

public sealed record DbaCapacityComplianceCard(
    Guid RegistrationId,
    string DisplayName,
    bool Available,
    CapacityComplianceProjection? Projection,
    string Message);

public sealed record DbaEstateLifecycleCard(
    Guid RegistrationId,
    string DisplayName,
    SqlMajorGeneration Generation,
    SqlEditionClass Edition,
    EncryptionPosture Encryption,
    RegistrationLifecycleState Lifecycle,
    bool UpgradeCandidate);

public sealed record DbaIntelligenceDashboardViewModel(
    IReadOnlyList<DbaRiskFleetCard> RiskFleet,
    IReadOnlyList<DbaTrendSummaryCard> Trends,
    IReadOnlyList<DbaPriorityIncidentCard> PriorityIncidents,
    IReadOnlyList<DbaCapacityComplianceCard> CapacityCompliance,
    IReadOnlyList<DbaEstateLifecycleCard> EstateLifecycle,
    string State,
    string StateMessage,
    DateTimeOffset GeneratedAtUtc)
{
    public bool IsEmpty => RiskFleet.Count == 0;
    public bool IsDegraded => string.Equals(State, "degraded", StringComparison.Ordinal);
}

public sealed class DbaIntelligenceDashboardService(
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache,
    ISnapshotHistoryStore history,
    IHealthIncidentRepository incidents,
    IOperatorMetadataStore operatorMetadata,
    TimeProvider timeProvider,
    DbaIntelligenceOptions? options = null)
{
    private readonly DbaIntelligenceOptions _options = Validate(options ?? new DbaIntelligenceOptions());

    public DbaIntelligenceDashboardViewModel Read()
    {
        var now = timeProvider.GetUtcNow();
        var registrationSnapshot = registrations.GetAll()
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .ToArray();
        var incidentSnapshot = incidents.GetAll();
        var rows = registrationSnapshot.Select(registration =>
        {
            var cached = cache.Peek(registration.Id);
            var metadata = operatorMetadata.GetServer(registration.Id);
            var risk = DbaRiskScoring.Evaluate(registration.Id, cached, incidentSnapshot, metadata, now);
            return (Registration: registration, Cached: cached, Metadata: metadata, Risk: risk);
        }).ToArray();

        var risks = rows
            .Select(item => new DbaRiskFleetCard(
                item.Registration.Id,
                item.Registration.DisplayName,
                item.Metadata.Environment,
                item.Risk.Score,
                item.Risk.Level,
                item.Risk.Actionable,
                item.Cached?.Freshness.ToString() ?? "Unavailable"))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var trends = rows.Select(item =>
        {
            var points = history.Read(item.Registration.Id, TimeSpan.FromHours(_options.HistoryHours), 0, _options.HistoryPoints);
            return new DbaTrendSummaryCard(
                item.Registration.Id,
                item.Registration.DisplayName,
                DbaTrendAnalysis.Memory(points),
                DbaTrendAnalysis.Blocking(points),
                DbaTrendAnalysis.Runnable(points),
                DbaTrendAnalysis.DatabaseAvailability(points));
        }).ToArray();

        var priority = new IncidentPriorityService(incidents, operatorMetadata, timeProvider).Queue(limit: 25)
            .Select(item => new DbaPriorityIncidentCard(
                item.Incident.Id,
                item.Incident.RuleId,
                item.Incident.Severity,
                item.Score,
                item.Actionable,
                item.Assignee,
                item.SlaBucket))
            .ToArray();

        var capacity = rows.Select(item => CapacityCard(item.Registration, item.Cached, now)).ToArray();
        var estatePolicy = new EstateLifecyclePolicy();
        var estate = rows.Select(item =>
        {
            var projected = EstateInventory.Project(item.Registration, item.Cached, item.Metadata, estatePolicy);
            return new DbaEstateLifecycleCard(
                item.Registration.Id,
                item.Registration.DisplayName,
                projected.Generation,
                projected.Edition,
                projected.Encryption,
                projected.Lifecycle,
                projected.UpgradeCandidate);
        }).ToArray();

        var unavailable = rows.Count(item => item.Cached is null);
        var state = rows.Length == 0 ? "empty" : unavailable > 0 ? "degraded" : "ready";
        var message = state switch
        {
            "empty" => "No registered SQL Server targets are available in Monitor control-plane state.",
            "degraded" => $"{unavailable} registered server(s) have no retained snapshot. Dashboard remains read-only and does not trigger collection.",
            _ => "DBA intelligence is derived from retained Monitor snapshots, history and operator state."
        };
        return new(risks, trends, priority, capacity, estate, state, message, now);
    }

    private DbaCapacityComplianceCard CapacityCard(ServerRegistration registration, SnapshotCacheResult? cached, DateTimeOffset now)
    {
        if (_options.StorageCapacityBytes <= 0)
            return new(registration.Id, registration.DisplayName, false, null, "Storage capacity policy is not configured.");
        if (cached is null)
            return new(registration.Id, registration.DisplayName, false, null, "No retained snapshot is available.");
        var policy = new CapacityPolicy(_options.StorageCapacityBytes);
        var projection = CapacityCompliance.Evaluate(cached.Snapshot, policy, now);
        return new(registration.Id, registration.DisplayName, true, projection, "Capacity/compliance projection available.");
    }

    private static DbaIntelligenceOptions Validate(DbaIntelligenceOptions options)
    {
        options.Validate();
        return options;
    }
}
