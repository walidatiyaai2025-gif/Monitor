namespace Monitor.Web.Models;

public sealed record IncidentTransitionResult(
    bool Applied,
    IncidentStatus? PreviousStatus,
    IncidentStatus? NewStatus)
{
    public string AuditOutcome => Applied
        ? $"{PreviousStatus}->{NewStatus}"
        : PreviousStatus is null
            ? "rejected:not-found"
            : $"rejected:current={NewStatus ?? PreviousStatus}";
}
