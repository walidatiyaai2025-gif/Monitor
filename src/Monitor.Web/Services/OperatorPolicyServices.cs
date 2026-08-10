using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record ServerOperatorPolicyState(Guid RegistrationId, bool PolicyReadable, bool MaintenanceActive, bool AlertSuppressed, ServerEnvironmentClass Environment, string? Group, IReadOnlyList<string> Tags);
public sealed record IncidentOperatorPolicyState(string IncidentId, Guid RegistrationId, bool PolicyReadable, bool AlertSuppressed, string? Assignee);
public sealed record OperatorPolicySummary(int ServersInMaintenance, int IncidentsActionable, int IncidentsSuppressed, int PolicyUnavailable);

public interface IOperatorPolicyReadService
{
    ServerOperatorPolicyState GetServer(Guid registrationId);
    bool IsScheduledCollectionAllowed(Guid registrationId);
    IReadOnlyDictionary<Guid, ServerOperatorPolicyState> GetServers(IEnumerable<Guid> registrationIds);
    IReadOnlyDictionary<string, IncidentOperatorPolicyState> GetIncidents(IEnumerable<HealthIncident> incidents);
    OperatorPolicySummary Summarize(IEnumerable<Guid> registrationIds, IEnumerable<HealthIncident> incidents);
}

public sealed class OperatorPolicyReadService(IOperatorMetadataStore metadata, TimeProvider timeProvider) : IOperatorPolicyReadService
{
    public ServerOperatorPolicyState GetServer(Guid registrationId)
    {
        if (registrationId == Guid.Empty) return Unavailable(registrationId);
        try
        {
            var item = metadata.GetServer(registrationId);
            var now = timeProvider.GetUtcNow();
            return new(registrationId, true, EnterpriseOperatorPolicy.IsMaintenanceActive(item, now), EnterpriseOperatorPolicy.IsAlertSuppressed(item, now), item.Environment, item.Group, item.Tags.ToArray());
        }
        catch (InvalidDataException)
        {
            return Unavailable(registrationId);
        }
        catch (SharedStateStoreUnavailableException)
        {
            return Unavailable(registrationId);
        }
    }

    public bool IsScheduledCollectionAllowed(Guid registrationId)
    {
        var state = GetServer(registrationId);
        return state.PolicyReadable && !state.MaintenanceActive;
    }

    public IReadOnlyDictionary<Guid, ServerOperatorPolicyState> GetServers(IEnumerable<Guid> registrationIds)
    {
        ArgumentNullException.ThrowIfNull(registrationIds);
        return registrationIds.Where(id => id != Guid.Empty).Distinct().ToDictionary(id => id, GetServer);
    }

    public IReadOnlyDictionary<string, IncidentOperatorPolicyState> GetIncidents(IEnumerable<HealthIncident> incidents)
    {
        ArgumentNullException.ThrowIfNull(incidents);
        var result = new Dictionary<string, IncidentOperatorPolicyState>(StringComparer.Ordinal);
        foreach (var incident in incidents)
        {
            var server = GetServer(incident.RegistrationId);
            string? assignee = null;
            var readable = server.PolicyReadable;
            try
            {
                assignee = metadata.GetIncident(incident.Id).Assignee;
            }
            catch (InvalidDataException)
            {
                readable = false;
            }
            catch (SharedStateStoreUnavailableException)
            {
                readable = false;
            }
            result[incident.Id] = new(incident.Id, incident.RegistrationId, readable, server.AlertSuppressed, assignee);
        }
        return result;
    }

    public OperatorPolicySummary Summarize(IEnumerable<Guid> registrationIds, IEnumerable<HealthIncident> incidents)
    {
        var servers = GetServers(registrationIds);
        var incidentStates = GetIncidents(incidents);
        return new(
            servers.Values.Count(item => item.MaintenanceActive),
            incidentStates.Values.Count(item => item.PolicyReadable && !item.AlertSuppressed),
            incidentStates.Values.Count(item => item.PolicyReadable && item.AlertSuppressed),
            servers.Values.Count(item => !item.PolicyReadable) + incidentStates.Values.Count(item => !item.PolicyReadable));
    }

    private static ServerOperatorPolicyState Unavailable(Guid id) => new(id, false, true, false, ServerEnvironmentClass.Unspecified, null, Array.Empty<string>());
}
