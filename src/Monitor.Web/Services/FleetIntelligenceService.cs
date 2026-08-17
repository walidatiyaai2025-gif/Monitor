using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record FleetHealthBucket(string Key, int Servers, int Fresh, int Stale, int Unavailable, int Maintenance, int Suppressed);
public sealed record FleetRuleHotspot(string RuleId, int Open, int Critical, int Suppressed);
public sealed record FleetRiskSummary(int BackupGaps, int MemoryPressure, int BlockingRisk, int RunnableRisk);
public sealed record FleetAdvancedEvidenceSummary(
    int TempDbServers,
    int TransactionLogServers,
    int HaServers,
    int HaEnabledServers,
    int LogReuseBlockedDatabases,
    int SuspendedHaDatabases,
    int StaleEvidenceServers);
public sealed record FleetIntelligenceSnapshot(
    IReadOnlyList<FleetHealthBucket> ByEnvironment,
    IReadOnlyList<FleetHealthBucket> ByGroup,
    IReadOnlyList<FleetHealthBucket> ByTag,
    int Fresh,
    int Stale,
    int Unavailable,
    int Maintenance,
    int Suppressed,
    IReadOnlyList<FleetRuleHotspot> RuleHotspots,
    FleetRiskSummary Risks,
    FleetAdvancedEvidenceSummary? Advanced = null,
    FleetDecisionSupportSnapshot? DecisionSupport = null,
    bool IncidentEvidenceComplete = true,
    int IncidentEvidenceLimit = BoundedIncidentReadModel.DefaultLimit,
    bool ServerPolicyEvidenceComplete = true,
    bool IncidentPolicyEvidenceComplete = true,
    int OperatorPolicyUnavailable = 0);

public interface IFleetIntelligenceService
{
    FleetIntelligenceSnapshot Read();
}

public sealed class FleetIntelligenceService(
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache,
    IOperatorMetadataStore operatorMetadata,
    IHealthIncidentRepository incidents,
    TimeProvider timeProvider) : IFleetIntelligenceService
{
    public FleetIntelligenceSnapshot Read()
    {
        var operatorPolicy = new OperatorPolicyReadService(operatorMetadata, timeProvider);
        var enabledRegistrations = registrations.GetAll()
            .Where(item => item.IsEnabled)
            .OrderBy(item => item.Id)
            .ToArray();
        var serverPolicies = operatorPolicy.GetServers(enabledRegistrations.Select(item => item.Id));
        var servers = enabledRegistrations
            .Select(registration => new ServerProjection(
                registration.Id,
                serverPolicies[registration.Id],
                cache.Peek(registration.Id)))
            .ToArray();
        var readableServers = servers.Where(item => item.Policy.PolicyReadable).ToArray();
        var serverPolicyEvidenceComplete = readableServers.Length == servers.Length;

        var byEnvironment = Bucket(readableServers.Select(item => (item.Policy.Environment.ToString(), item)));
        var byGroup = Bucket(readableServers.Select(item => (item.Policy.Group ?? "Unassigned", item)));
        var byTag = Bucket(readableServers.SelectMany(item => item.Policy.Tags.Count == 0
            ? [(Key: "Untagged", Item: item)]
            : item.Policy.Tags.Select(tag => (Key: tag, Item: item))));

        var incidentRead = BoundedIncidentReadModel.ActiveForRegistrations(
            incidents,
            servers.Select(item => item.Id));
        IReadOnlyDictionary<string, IncidentOperatorPolicyState> incidentPolicies = incidentRead.IsComplete
            ? operatorPolicy.GetIncidents(incidentRead.Incidents)
            : new Dictionary<string, IncidentOperatorPolicyState>(StringComparer.Ordinal);
        var incidentRows = incidentRead.Incidents
            .Select(incident =>
            {
                var server = servers.FirstOrDefault(item => item.Id == incident.RegistrationId);
                incidentPolicies.TryGetValue(incident.Id, out var policy);
                return (Incident: incident, Server: server, Policy: policy);
            })
            .ToArray();
        var incidentPolicyEvidenceComplete = incidentRead.IsComplete
            && incidentRows.All(item => item.Server?.Policy.PolicyReadable == true && item.Policy?.PolicyReadable == true);
        var hotspots = incidentRead.IsComplete && incidentPolicyEvidenceComplete
            ? incidentRows
                .GroupBy(item => item.Incident.RuleId, StringComparer.Ordinal)
                .Select(group => new FleetRuleHotspot(
                    group.Key,
                    group.Count(),
                    group.Count(item => item.Incident.Severity == FindingSeverity.Critical),
                    group.Count(item => item.Policy!.AlertSuppressed)))
                .OrderByDescending(item => item.Critical)
                .ThenByDescending(item => item.Open)
                .ThenBy(item => item.RuleId, StringComparer.Ordinal)
                .Take(20)
                .ToArray()
            : [];

        var risks = new FleetRiskSummary(
            servers.Sum(item => item.Snapshot?.Snapshot.Backups?.MissingFullBackupLast24Hours ?? 0),
            servers.Count(item => item.Snapshot?.Snapshot.Memory is { IsPhysicalMemoryLow: true } or { IsVirtualMemoryLow: true }),
            servers.Count(item => item.Snapshot?.Snapshot.Blocking is { BlockedRequests: > 0 }),
            servers.Count(item => item.Snapshot?.Snapshot.Performance is { RunnableTasks: >= 10 }));

        var advanced = new FleetAdvancedEvidenceSummary(
            servers.Count(item => item.Snapshot?.Snapshot.TempDb is not null),
            servers.Count(item => item.Snapshot?.Snapshot.TransactionLogs is not null),
            servers.Count(item => item.Snapshot?.Snapshot.Ha is not null),
            servers.Count(item => item.Snapshot?.Snapshot.Ha?.IsHadrEnabled == true),
            servers.Sum(item => AdvancedEvidenceProjection.BuildTransactionLogs(item.Snapshot?.Snapshot.TransactionLogs).Count(database => database.TruncationBlocked == true)),
            servers.Sum(item => item.Snapshot?.Snapshot.Ha?.DatabaseReplicas?.Count(database => database.IsSuspended == true) ?? 0),
            servers.Count(item => item.Snapshot?.Freshness == SnapshotFreshness.Stale && HasAdvancedEvidence(item.Snapshot.Snapshot)));

        var decisionSupport = incidentRead.IsComplete && incidentPolicyEvidenceComplete
            ? FleetDecisionSupport.Build(incidentRows.Select(item => new FleetDecisionIncident(
                item.Incident.Id,
                item.Incident.RegistrationId,
                item.Incident.RuleId,
                item.Incident.Severity,
                item.Incident.LastSeenUtc,
                item.Server!.Policy.Environment.ToString(),
                item.Policy!.AlertSuppressed,
                item.Server.Policy.MaintenanceActive,
                item.Policy.Assignee)))
            : null;
        var operatorPolicyUnavailable = servers.Count(item => !item.Policy.PolicyReadable)
            + incidentRows.Count(item => incidentRead.IsComplete && item.Policy?.PolicyReadable != true);

        return new(
            byEnvironment,
            byGroup,
            byTag,
            servers.Count(item => item.Snapshot?.Freshness == SnapshotFreshness.Fresh),
            servers.Count(item => item.Snapshot?.Freshness == SnapshotFreshness.Stale),
            servers.Count(item => item.Snapshot is null),
            readableServers.Count(item => item.Policy.MaintenanceActive),
            readableServers.Count(item => item.Policy.AlertSuppressed),
            hotspots,
            risks,
            advanced,
            decisionSupport,
            incidentRead.IsComplete,
            incidentRead.Limit,
            serverPolicyEvidenceComplete,
            incidentPolicyEvidenceComplete,
            operatorPolicyUnavailable);
    }

    private static bool HasAdvancedEvidence(ServerHealthSnapshot snapshot) =>
        snapshot.TempDb is not null || snapshot.TransactionLogs is not null || snapshot.Ha is not null;

    private static FleetHealthBucket[] Bucket(IEnumerable<(string Key, ServerProjection Item)> source) =>
        source.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FleetHealthBucket(
                group.Key,
                group.Select(item => item.Item.Id).Distinct().Count(),
                group.Count(item => item.Item.Snapshot?.Freshness == SnapshotFreshness.Fresh),
                group.Count(item => item.Item.Snapshot?.Freshness == SnapshotFreshness.Stale),
                group.Count(item => item.Item.Snapshot is null),
                group.Count(item => item.Item.Policy.MaintenanceActive),
                group.Count(item => item.Item.Policy.AlertSuppressed)))
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private sealed record ServerProjection(
        Guid Id,
        ServerOperatorPolicyState Policy,
        SnapshotCacheResult? Snapshot);
}
