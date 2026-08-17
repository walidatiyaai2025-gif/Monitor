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

        var ids = registrationIds.ToHashSet();
        if (ids.Contains(Guid.Empty))
        {
            throw new ArgumentException("Incident registration scope cannot contain an empty ID.", nameof(registrationIds));
        }
        if (ids.Count == 0)
        {
            return new([], true, limit);
        }

        var read = repository.Read(new IncidentRepositoryQuery(
            RegistrationIds: ids,
            ExcludeResolved: true,
            Offset: 0,
            Limit: limit));
        return new(read.Items, !read.HasMore, limit);
    }
}
