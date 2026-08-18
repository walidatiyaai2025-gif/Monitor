using System.Security.Cryptography;
using System.Text;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum IncidentSlaBucket
{
    Fresh,
    Aging,
    Breached,
    Resolved
}

public sealed class IncidentNoteRequestAmbiguousException : InvalidOperationException
{
    public IncidentNoteRequestAmbiguousException()
        : base("A prior incident-note request with the same request key has an unresolved outcome. Verify the incident notes before submitting a new request.")
    {
    }
}

public sealed record IncidentCollaborationProjection(
    HealthIncident Incident,
    string? Assignee,
    IncidentSlaBucket SlaBucket,
    IReadOnlyList<IncidentOperatorNote> Notes);

public interface IIncidentCollaborationService
{
    IReadOnlyList<IncidentCollaborationProjection> QueryByAssignee(IEnumerable<HealthIncident> incidents, string? assignee);
    IReadOnlyList<IncidentOperatorNote> ReadNotes(string incidentId, int offset, int limit);
    bool TryAddNote(string incidentId, string actor, string note, string requestKey);
    void Assign(string incidentId, string? assignee, string actor);
    IncidentSlaBucket ClassifySla(HealthIncident incident);
    bool RecordSeverityEscalation(HealthIncident before, HealthIncident after, string actor);
    void AddReopenReason(string incidentId, string actor, string reason);
    void AddResolutionNote(string incidentId, string actor, string note);
}

public sealed class IncidentCollaborationService(
    IOperatorMetadataStore metadata,
    IAuditStore audit,
    TimeProvider timeProvider) : IIncidentCollaborationService
{
    private const int MaxPageSize = 20;
    private static readonly TimeSpan AgingAfter = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan BreachAfter = TimeSpan.FromHours(2);

    public IReadOnlyList<IncidentCollaborationProjection> QueryByAssignee(IEnumerable<HealthIncident> incidents, string? assignee)
    {
        ArgumentNullException.ThrowIfNull(incidents);
        var normalized = EnterpriseOperatorValidation.NormalizeAssignee(assignee);
        return incidents
            .Where(incident => !HasPruneReceipt("governance.prune.incident", incident.Id))
            .Select(incident => new IncidentCollaborationProjection(
                incident,
                metadata.GetIncident(incident.Id).Assignee,
                ClassifySla(incident),
                ReadNotes(incident.Id, 0, 5)))
            .Where(item => normalized is null || string.Equals(item.Assignee, normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Incident.Status)
            .ThenByDescending(item => item.Incident.Severity)
            .ThenByDescending(item => item.Incident.LastSeenUtc)
            .ToArray();
    }

    public IReadOnlyList<IncidentOperatorNote> ReadNotes(string incidentId, int offset, int limit)
    {
        incidentId = EnterpriseOperatorValidation.NormalizeIncidentId(incidentId);
        return metadata.GetIncident(incidentId).Notes
            .Where(note => !HasPruneReceipt("governance.prune.note", note.Id.ToString("D")))
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip(Math.Max(0, offset))
            .Take(Math.Clamp(limit, 1, MaxPageSize))
            .ToArray();
    }

    public bool TryAddNote(string incidentId, string actor, string note, string requestKey)
    {
        incidentId = EnterpriseOperatorValidation.NormalizeIncidentId(incidentId);
        actor = EnterpriseOperatorValidation.NormalizeActor(actor);
        note = EnterpriseOperatorValidation.NormalizeNote(note);
        var receiptTarget = BuildReceiptTarget(incidentId, requestKey);
        if (AuditAny(item => item.Action == "incident.note.request" && item.Target == receiptTarget && item.Outcome == "applied"))
            return false;

        if (AuditAny(item => item.Action == "incident.note.write.request" && item.Target == receiptTarget && item.Outcome == "requested"))
            throw new IncidentNoteRequestAmbiguousException();

        audit.Append(actor, "incident.note.write.request", receiptTarget, "requested");
        metadata.AddIncidentNote(incidentId, actor, note);
        audit.Append(actor, "incident.note.request", receiptTarget, "applied");
        return true;
    }

    public void Assign(string incidentId, string? assignee, string actor)
    {
        incidentId = EnterpriseOperatorValidation.NormalizeIncidentId(incidentId);
        actor = EnterpriseOperatorValidation.NormalizeActor(actor);
        var before = metadata.GetIncident(incidentId).Assignee;
        var next = EnterpriseOperatorValidation.NormalizeAssignee(assignee);
        if (string.Equals(before, next, StringComparison.OrdinalIgnoreCase)) return;
        audit.Append(actor, "incident.owner.change.request", incidentId, "requested");
        metadata.AssignIncident(incidentId, next);
        audit.Append(actor, "incident.owner.change", incidentId, $"{DisplayOwner(before)}->{DisplayOwner(next)}");
    }

    public IncidentSlaBucket ClassifySla(HealthIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);
        if (incident.Status == IncidentStatus.Resolved) return IncidentSlaBucket.Resolved;
        var age = timeProvider.GetUtcNow() - incident.FirstSeenUtc;
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;
        if (age >= BreachAfter) return IncidentSlaBucket.Breached;
        return age >= AgingAfter ? IncidentSlaBucket.Aging : IncidentSlaBucket.Fresh;
    }

    public bool RecordSeverityEscalation(HealthIncident before, HealthIncident after, string actor)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (!string.Equals(before.Id, after.Id, StringComparison.Ordinal)) throw new ArgumentException("Incident identity must not change across severity comparison.", nameof(after));
        if (after.Severity <= before.Severity) return false;
        actor = EnterpriseOperatorValidation.NormalizeActor(actor);
        audit.Append(actor, "incident.severity.escalation", after.Id, $"{before.Severity}->{after.Severity}");
        return true;
    }

    public void AddReopenReason(string incidentId, string actor, string reason) => AddTransitionNote(incidentId, actor, reason, "REOPEN");
    public void AddResolutionNote(string incidentId, string actor, string note) => AddTransitionNote(incidentId, actor, note, "RESOLUTION");

    private void AddTransitionNote(string incidentId, string actor, string text, string category)
    {
        incidentId = EnterpriseOperatorValidation.NormalizeIncidentId(incidentId);
        actor = EnterpriseOperatorValidation.NormalizeActor(actor);
        text = EnterpriseOperatorValidation.NormalizeNote(text);
        var bounded = $"[{category}] {text}";
        if (bounded.Length > EnterpriseOperatorValidation.MaxNoteLength)
            bounded = bounded[..EnterpriseOperatorValidation.MaxNoteLength];
        audit.Append(actor, $"incident.{category.ToLowerInvariant()}.note.request", incidentId, "requested");
        metadata.AddIncidentNote(incidentId, actor, bounded);
        audit.Append(actor, $"incident.{category.ToLowerInvariant()}.note", incidentId, "added");
    }

    private bool HasPruneReceipt(string action, string target) =>
        AuditAny(item => item.Action == action && item.Target == target && item.Outcome == "applied");

    private bool AuditAny(Func<AuditEvent, bool> predicate)
    {
        const int pageSize = 100;
        for (var offset = 0; offset < 1000; offset += pageSize)
        {
            var page = audit.Read(offset, pageSize);
            if (page.Any(predicate)) return true;
            if (page.Count < pageSize) break;
        }
        return false;
    }

    private static string BuildReceiptTarget(string incidentId, string requestKey)
    {
        if (string.IsNullOrWhiteSpace(requestKey)) throw new ArgumentException("Request key is required.", nameof(requestKey));
        var normalized = requestKey.Trim();
        if (normalized.Length is < 8 or > 100 || normalized.Any(char.IsControl)) throw new ArgumentException("Request key is invalid.", nameof(requestKey));
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..24];
        var prefix = incidentId.Length <= 120 ? incidentId : incidentId[..120];
        return $"{prefix}:{digest}";
    }

    private static string DisplayOwner(string? value) => string.IsNullOrWhiteSpace(value) ? "unassigned" : value;
}