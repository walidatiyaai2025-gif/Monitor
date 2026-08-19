namespace Monitor.Web.Models;

internal static class IncidentRetentionPolicy
{
    public static bool ShouldPruneOperatorMetadata(
        HealthIncident? incident,
        DateTimeOffset now,
        int resolvedIncidentMetadataDays)
    {
        if (resolvedIncidentMetadataDays is < 1 or > 365)
            throw new ArgumentOutOfRangeException(nameof(resolvedIncidentMetadataDays));
        if (incident is null) return true;
        if (incident.Status != IncidentStatus.Resolved) return false;
        return incident.LastSeenUtc < now.AddDays(-resolvedIncidentMetadataDays);
    }
}
