namespace Monitor.Web.Services;

public sealed record EnterprisePersistenceReadiness(
    string Status,
    int ServerMetadataRecords,
    int IncidentMetadataRecords,
    DateTimeOffset CheckedAtUtc,
    string Message);

public interface IEnterprisePersistenceReadinessService
{
    EnterprisePersistenceReadiness Read();
}

public sealed class EnterprisePersistenceReadinessService(
    IOperatorMetadataStore metadata,
    TimeProvider timeProvider) : IEnterprisePersistenceReadinessService
{
    public EnterprisePersistenceReadiness Read()
    {
        try
        {
            var snapshot = metadata.Snapshot();
            return new(
                "ready",
                snapshot.Servers.Length,
                snapshot.Incidents.Length,
                timeProvider.GetUtcNow(),
                "Operator metadata persistence is readable. Counts are Monitor-owned control-plane state only.");
        }
        catch (Exception exception) when (exception is InvalidDataException or SharedStateStoreUnavailableException or IOException)
        {
            return new(
                "degraded",
                0,
                0,
                timeProvider.GetUtcNow(),
                "Operator metadata persistence is unavailable or invalid. Mutating enterprise workflows should be treated as unavailable until readiness is restored.");
        }
    }
}
