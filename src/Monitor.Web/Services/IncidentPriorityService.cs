using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record IncidentPriorityProjection(
    HealthIncident Incident,
    string? Assignee,
    string RuleFamily,
    int Score,
    bool Suppressed,
    bool MaintenanceActive,
    bool Actionable,
    IncidentSlaBucket SlaBucket);

public static class IncidentPriorityScoring
{
    public static int Score(HealthIncident incident, IncidentSlaBucket sla, bool suppressed, bool maintenance)
    {
        ArgumentNullException.ThrowIfNull(incident);
        var severity = incident.Severity == FindingSeverity.Critical ? 40 : 20;
        var age = sla switch
        {
            IncidentSlaBucket.Breached => 30,
            IncidentSlaBucket.Aging => 18,
            IncidentSlaBucket.Fresh => 5,
            _ => 0
        };
        var occurrence = Math.Min(20, Math.Max(0, incident.Occurrences - 1) * 2);
        var state = incident.Status switch
        {
            IncidentStatus.Open => 10,
            IncidentStatus.Acknowledged => 5,
            _ => 0
        };
        var raw = severity + age + occurrence + state;
        if (incident.Status == IncidentStatus.Resolved) return 0;
        if (suppressed || maintenance) raw = Math.Min(raw, 35);
        return Math.Clamp(raw, 0, 100);
    }

    public static string RuleFamily(string ruleId)
    {
        if (string.IsNullOrWhiteSpace(ruleId)) return "unknown";
        var normalized = ruleId.Trim().ToLowerInvariant();
        var separator = normalized.IndexOfAny(['.', ':', '/']);
        var family = separator > 0 ? normalized[..separator] : normalized;
        return family.Length > 40 ? family[..40] : family;
    }
}

public sealed class IncidentPriorityService(
    IHealthIncidentRepository incidents,
    IOperatorMetadataStore metadata,
    TimeProvider timeProvider)
{
    public IReadOnlyList<IncidentPriorityProjection> Queue(string? assignee = null, int limit = 50)
    {
        var normalizedAssignee = EnterpriseOperatorValidation.NormalizeAssignee(assignee);
        var now = timeProvider.GetUtcNow();
        var collaboration = new IncidentCollaborationService(metadata, new NullAuditStore(), timeProvider);
        var rows = incidents.GetAll()
            .Where(item => item.Status != IncidentStatus.Resolved)
            .GroupBy(item => (item.RegistrationId, item.RuleId), new IncidentKeyComparer())
            .Select(group => Collapse(group))
            .Select(incident =>
            {
                var operatorIncident = metadata.GetIncident(incident.Id);
                var operatorServer = metadata.GetServer(incident.RegistrationId);
                var suppressed = EnterpriseOperatorPolicy.IsAlertSuppressed(operatorServer, now);
                var maintenance = EnterpriseOperatorPolicy.IsMaintenanceActive(operatorServer, now);
                var sla = collaboration.ClassifySla(incident);
                return new IncidentPriorityProjection(
                    incident,
                    operatorIncident.Assignee,
                    IncidentPriorityScoring.RuleFamily(incident.RuleId),
                    IncidentPriorityScoring.Score(incident, sla, suppressed, maintenance),
                    suppressed,
                    maintenance,
                    !suppressed && !maintenance,
                    sla);
            })
            .Where(item => normalizedAssignee is null || string.Equals(item.Assignee, normalizedAssignee, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Actionable)
            .ThenByDescending(item => item.Score)
            .ThenByDescending(item => item.Incident.LastSeenUtc)
            .ThenBy(item => item.Incident.Id, StringComparer.Ordinal)
            .Take(Math.Clamp(limit, 1, 100))
            .ToArray();
        return rows;
    }

    public IReadOnlyDictionary<string, int> GroupByRuleFamily(int limit = 100) =>
        Queue(null, limit)
            .GroupBy(item => item.RuleFamily, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    internal static HealthIncident Collapse(IEnumerable<HealthIncident> values)
    {
        var items = values.OrderByDescending(item => item.Severity).ThenByDescending(item => item.LastSeenUtc).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        if (items.Length == 0) throw new ArgumentException("Incident group cannot be empty.", nameof(values));
        var selected = items[0];
        return selected with
        {
            FirstSeenUtc = items.Min(item => item.FirstSeenUtc),
            LastSeenUtc = items.Max(item => item.LastSeenUtc),
            Occurrences = items.Sum(item => Math.Max(0, item.Occurrences))
        };
    }

    private sealed class IncidentKeyComparer : IEqualityComparer<(Guid RegistrationId, string RuleId)>
    {
        public bool Equals((Guid RegistrationId, string RuleId) x, (Guid RegistrationId, string RuleId) y) =>
            x.RegistrationId == y.RegistrationId && string.Equals(x.RuleId, y.RuleId, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((Guid RegistrationId, string RuleId) obj) => HashCode.Combine(obj.RegistrationId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.RuleId ?? string.Empty));
    }

    private sealed class NullAuditStore : IAuditStore
    {
        public void Append(string actor, string action, string target, string outcome) { }
        public IReadOnlyList<AuditEvent> Read(int offset, int limit) => [];
    }
}
