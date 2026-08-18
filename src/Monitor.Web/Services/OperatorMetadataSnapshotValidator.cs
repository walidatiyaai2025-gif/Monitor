namespace Monitor.Web.Services;

internal static class OperatorMetadataSnapshotValidator
{
    internal const int MaxServers = 5000;
    internal const int MaxIncidents = 1000;
    internal const int MaxRecommendationKeys = 20;

    public static EnterpriseOperatorSnapshot Validate(EnterpriseOperatorSnapshot? state)
    {
        if (state is null || state.Servers is null || state.Incidents is null)
        {
            throw new InvalidDataException("Operator metadata contains invalid state.");
        }

        if (state.Servers.Length > MaxServers || state.Incidents.Length > MaxIncidents)
        {
            throw new InvalidDataException("Operator metadata store exceeds bounded capacity.");
        }

        var serverIds = new HashSet<Guid>();
        foreach (var server in state.Servers)
        {
            if (server is null || !serverIds.Add(server.RegistrationId))
            {
                throw new InvalidDataException("Operator metadata contains duplicate or invalid server records.");
            }

            try
            {
                EnterpriseOperatorValidation.NormalizeServer(server, server.UpdatedAtUtc);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Operator metadata contains invalid server state.", exception);
            }
        }

        var incidentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var incident in state.Incidents)
        {
            if (incident is null || incident.Notes is null || incident.AcknowledgedRecommendationKeys is null)
            {
                throw new InvalidDataException("Operator metadata contains invalid incident state.");
            }

            string incidentId;
            try
            {
                incidentId = EnterpriseOperatorValidation.NormalizeIncidentId(incident.IncidentId);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Operator metadata contains invalid incident state.", exception);
            }

            if (!incidentIds.Add(incidentId) ||
                incident.Notes.Length > EnterpriseOperatorValidation.MaxNotesPerIncident ||
                incident.AcknowledgedRecommendationKeys.Length > MaxRecommendationKeys)
            {
                throw new InvalidDataException("Operator metadata contains invalid incident state.");
            }

            try
            {
                _ = EnterpriseOperatorValidation.NormalizeAssignee(incident.Assignee);
                foreach (var note in incident.Notes)
                {
                    if (note is null || note.Id == Guid.Empty || note.OccurredAtUtc == default)
                    {
                        throw new InvalidDataException("Operator note metadata is invalid.");
                    }

                    EnterpriseOperatorValidation.NormalizeActor(note.Actor);
                    EnterpriseOperatorValidation.NormalizeNote(note.Text);
                }

                foreach (var key in incident.AcknowledgedRecommendationKeys)
                {
                    EnterpriseOperatorValidation.NormalizeRecommendationKey(key);
                }
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Operator metadata contains invalid incident state.", exception);
            }
        }

        return state;
    }
}
