using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record BoundedIncidentReadResult(
    IReadOnlyList<HealthIncident> Incidents,
    bool IsComplete,
    int Limit)
{
    public bool IsTruncated => !IsComplete;
}

public static class BoundedIncidentReadModel
{
    public const int DefaultLimit = 100;
    public const int MaximumLimit = 1000;

    public static BoundedIncidentReadResult ActiveForServer(
        IHealthIncidentRepository repository,
        Guid registrationId,
        int limit = DefaultLimit) =>
        ActiveForRegistrations(repository, [registrationId], limit);

    public static BoundedIncidentReadResult ActiveForRegistrations(
        IHealthIncidentRepository repository,
        IEnumerable<Guid> registrationIds,
        int limit = DefaultLimit)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(registrationIds);
        if (limit is < 1 or > MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), $"Incident read limit must be between 1 and {MaximumLimit}.");
        }

        var ids = registrationIds.Where(id => id != Guid.Empty).ToHashSet();
        if (ids.Count == 0)
        {
            return new([], true, limit);
        }

        // IHealthIncidentRepository.GetAll() is a legacy storage contract. This projection
        // deliberately bounds the evidence admitted into operator decisions and makes
        // overflow explicit. Storage-level server-scoped querying is a separate follow-up.
        var rows = repository.GetAll()
            .Where(incident => ids.Contains(incident.RegistrationId) && incident.Status != IncidentStatus.Resolved)
            .OrderByDescending(incident => incident.Severity)
            .ThenByDescending(incident => incident.LastSeenUtc)
            .ThenBy(incident => incident.RuleId, StringComparer.Ordinal)
            .ThenBy(incident => incident.Id, StringComparer.Ordinal)
            .Take(limit + 1)
            .ToArray();

        var complete = rows.Length <= limit;
        return new(rows.Take(limit).ToArray(), complete, limit);
    }
}
