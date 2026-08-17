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
    FleetDecisionSupportSnapshot? DecisionSupport = null);

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
        var now = timeProvider.GetUtcNow();
        var servers = registrations.GetAll()
            .Where(item => item.IsEnabled)
            .OrderBy(item => item.Id)
            .Select(registration =>
            {
                var metadata = operatorMetadata.GetServer(registration.Id);
                var snapshot = cache.Peek(registration.Id);
                return new ServerProjection(
                    registration.Id,
                    metadata.Environment.ToString(),
                    metadata.Group ?? "Unassigned",
                    metadata.Tags,
                    snapshot,
                    EnterpriseOperatorPolicy.IsMaintenanceActive(metadata, now),
                    EnterpriseOperatorPolicy.IsAlertSuppressed(metadata, now));
            })
            .ToArray();

        var byEnvironment = Bucket(servers.Select(item => (item.Environment, item)));
        var byGroup = Bucket(servers.Select(item => (item.Group, item)));
        var byTag = Bucket(servers.SelectMany(item => item.Tags.Length == 0
            ? [(Key: "Untagged", Item: item)]
            : item.Tags.Select(tag => (Key: tag, Item: item))));

        var incidentRows = incidents.GetAll()
            .Where(item => item.Status != IncidentStatus.Resolved)
            .Select(incident =>
            {
                var server = servers.FirstOrDefault(item => item.Id == incident.RegistrationId);
                return (Incident: incident, Server: server, Suppressed: server?.Suppressed ?? false);
            })
            .ToArray();
        var hotspots = incidentRows
            .GroupBy(item => item.Incident.RuleId, StringComparer.Ordinal)
            .Select(group => new FleetRuleHotspot(
                group.Key,
                group.Count(),
                group.Count(item => item.Incident.Severity == FindingSeverity.Critical),
                group.Count(item => item.Suppressed)))
            .OrderByDescending(item => item.Critical)
            .ThenByDescending(item => item.Open)
            .ThenBy(item => item.RuleId, StringComparer.Ordinal)
            .Take(20)
            .ToArray();

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

        var decisionSupport = FleetDecisionSupport.Build(incidentRows
            .Where(item => item.Server is not null)
            .Select(item => new FleetDecisionIncident(
                item.Incident.Id,
                item.Incident.RegistrationId,
                item.Incident.RuleId,
                item.Incident.Severity,
                item.Incident.LastSeenUtc,
                item.Server!.Environment,
                item.Server.Suppressed,
                item.Server.Maintenance,
                ReadAssignee(item.Incident.Id))));

        return new(
            byEnvironment,
            byGroup,
            byTag,
            servers.Count(item => item.Snapshot?.Freshness == SnapshotFreshness.Fresh),
            servers.Count(item => item.Snapshot?.Freshness == SnapshotFreshness.Stale),
            servers.Count(item => item.Snapshot is null),
            servers.Count(item => item.Maintenance),
            servers.Count(item => item.Suppressed),
            hotspots,
            risks,
            advanced,
            decisionSupport);
    }

    private string? ReadAssignee(string incidentId)
    {
        try
        {
            return operatorMetadata.GetIncident(incidentId).Assignee;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (SharedStateStoreUnavailableException)
        {
            return null;
        }
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
                group.Count(item => item.Item.Maintenance),
                group.Count(item => item.Item.Suppressed)))
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private sealed record ServerProjection(
        Guid Id,
        string Environment,
        string Group,
        string[] Tags,
        SnapshotCacheResult? Snapshot,
        bool Maintenance,
        bool Suppressed);
}
