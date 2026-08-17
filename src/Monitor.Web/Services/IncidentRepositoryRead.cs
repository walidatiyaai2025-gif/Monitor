using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record IncidentRepositoryQuery(
    IReadOnlyCollection<Guid>? RegistrationIds = null,
    IncidentStatus? Status = null,
    FindingSeverity? Severity = null,
    string? RuleId = null,
    bool ExcludeResolved = false,
    int Offset = 0,
    int Limit = 50);

public sealed record IncidentRepositoryReadResult(
    IReadOnlyList<HealthIncident> Items,
    IncidentSummary Summary,
    int TotalMatched,
    int Offset,
    int Limit)
{
    public bool HasMore => Offset + Items.Count < TotalMatched;
}

public static class IncidentRepositoryRead
{
    public const int MaximumLimit = 1000;
    public const int MaximumOffset = 1_000_000;

    public static IncidentRepositoryReadResult Project(
        IEnumerable<HealthIncident> source,
        IncidentRepositoryQuery query)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(query);
        Validate(query);

        var registrationIds = query.RegistrationIds?.Where(id => id != Guid.Empty).ToHashSet();
        var boundedRuleId = string.IsNullOrWhiteSpace(query.RuleId) ? null : query.RuleId.Trim();
        var window = checked(query.Offset + query.Limit);
        var ordered = new SortedSet<HealthIncident>(IncidentOrderComparer.Instance);

        var open = 0;
        var acknowledged = 0;
        var resolved = 0;
        var critical = 0;
        var warning = 0;
        var matched = 0;

        foreach (var incident in source)
        {
            switch (incident.Status)
            {
                case IncidentStatus.Open: open++; break;
                case IncidentStatus.Acknowledged: acknowledged++; break;
                case IncidentStatus.Resolved: resolved++; break;
            }

            switch (incident.Severity)
            {
                case FindingSeverity.Critical: critical++; break;
                case FindingSeverity.Warning: warning++; break;
            }

            if (registrationIds is not null && !registrationIds.Contains(incident.RegistrationId)) continue;
            if (query.ExcludeResolved && incident.Status == IncidentStatus.Resolved) continue;
            if (query.Status is not null && incident.Status != query.Status) continue;
            if (query.Severity is not null && incident.Severity != query.Severity) continue;
            if (boundedRuleId is not null && !string.Equals(incident.RuleId, boundedRuleId, StringComparison.Ordinal)) continue;

            matched++;
            if (window == 0) continue;

            ordered.Add(incident);
            if (ordered.Count > window)
            {
                ordered.Remove(ordered.Max!);
            }
        }

        var page = ordered.Skip(query.Offset).Take(query.Limit).ToArray();
        return new(
            page,
            new(open, acknowledged, resolved, critical, warning),
            matched,
            query.Offset,
            query.Limit);
    }

    private static void Validate(IncidentRepositoryQuery query)
    {
        if (query.Offset is < 0 or > MaximumOffset)
            throw new ArgumentOutOfRangeException(nameof(query), $"Incident query offset must be between 0 and {MaximumOffset}.");
        if (query.Limit is < 1 or > MaximumLimit)
            throw new ArgumentOutOfRangeException(nameof(query), $"Incident query limit must be between 1 and {MaximumLimit}.");
        if (query.RuleId is { Length: > 80 })
            throw new ArgumentOutOfRangeException(nameof(query), "Incident rule ID exceeds the 80-character bound.");
        if (query.RegistrationIds?.Any(id => id == Guid.Empty) == true)
            throw new ArgumentException("Incident registration scope cannot contain an empty ID.", nameof(query));
        if (query.Status is not null && !Enum.IsDefined(query.Status.Value))
            throw new ArgumentOutOfRangeException(nameof(query), "Incident status is invalid.");
        if (query.Severity is not null && !Enum.IsDefined(query.Severity.Value))
            throw new ArgumentOutOfRangeException(nameof(query), "Incident severity is invalid.");
    }

    private sealed class IncidentOrderComparer : IComparer<HealthIncident>
    {
        public static readonly IncidentOrderComparer Instance = new();

        public int Compare(HealthIncident? left, HealthIncident? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return 1;
            if (right is null) return -1;

            var severity = right.Severity.CompareTo(left.Severity);
            if (severity != 0) return severity;
            var observed = right.LastSeenUtc.CompareTo(left.LastSeenUtc);
            if (observed != 0) return observed;
            var rule = string.Compare(left.RuleId, right.RuleId, StringComparison.Ordinal);
            if (rule != 0) return rule;
            return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
        }
    }
}
