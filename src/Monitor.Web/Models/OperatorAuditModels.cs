namespace Monitor.Web.Models;

public enum OperatorAuditAction
{
    IncidentAcknowledged,
    IncidentResolved,
    IncidentReopened
}

public sealed record OperatorAuditEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string Actor,
    OperatorAuditAction Action,
    string ResourceType,
    string ResourceId,
    string PreviousState,
    string NewState);

public sealed record AuditTrailViewModel(
    IReadOnlyList<OperatorAuditEvent> Events,
    int RetentionCapacity);
