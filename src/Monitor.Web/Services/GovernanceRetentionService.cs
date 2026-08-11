using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed class GovernanceRetentionOptions
{
    public const string SectionName = "GovernanceRetention";
    public int ResolvedIncidentMetadataDays { get; init; } = 30;
    public int OperatorNoteRetentionDays { get; init; } = 30;
    public int AuditMaxVisibleEvents { get; init; } = 500;
    public int BackupRetentionCount { get; init; } = 10;
    public int HistoryRetentionHours { get; init; } = 24;

    public void Validate()
    {
        if (ResolvedIncidentMetadataDays is < 1 or > 365) throw new InvalidOperationException("Resolved incident metadata retention must be 1..365 days.");
        if (OperatorNoteRetentionDays is < 1 or > 365) throw new InvalidOperationException("Operator note retention must be 1..365 days.");
        if (AuditMaxVisibleEvents is < 100 or > 1000) throw new InvalidOperationException("Audit visible retention must be 100..1000 events.");
        if (BackupRetentionCount is < 1 or > 50) throw new InvalidOperationException("Backup retention must be 1..50 backups.");
        if (HistoryRetentionHours is < 1 or > 24) throw new InvalidOperationException("History retention must be 1..24 hours.");
    }
}

public sealed record GovernanceCleanupCandidate(string Kind, string Key, string Reason);
public sealed record GovernanceCleanupPlan(DateTimeOffset EvaluatedAtUtc, IReadOnlyList<GovernanceCleanupCandidate> Candidates)
{
    public int OrphanServers => Candidates.Count(item => item.Kind == "server");
    public int IncidentMetadata => Candidates.Count(item => item.Kind == "incident");
    public int ExpiredNotes => Candidates.Count(item => item.Kind == "note");
}

public interface IGovernanceRetentionService
{
    GovernanceCleanupPlan DryRun();
    int Apply(string actor);
    IReadOnlyList<AuditEvent> ReadGovernedAudit(int offset, int limit);
    bool IsIncidentPruned(string incidentId);
    bool IsNotePruned(Guid noteId);
}

public sealed class GovernanceRetentionService(
    IServerRegistrationRepository registrations,
    IHealthIncidentRepository incidents,
    IOperatorMetadataStore metadata,
    IAuditStore audit,
    TimeProvider timeProvider,
    GovernanceRetentionOptions? options = null) : IGovernanceRetentionService
{
    private readonly GovernanceRetentionOptions _options = Validate(options ?? new GovernanceRetentionOptions());

    public GovernanceCleanupPlan DryRun()
    {
        var now = timeProvider.GetUtcNow();
        var receiptIndex = audit.Read(0, 1000)
            .Where(item => item.Action.StartsWith("governance.prune.", StringComparison.Ordinal) && item.Outcome == "applied")
            .Select(item => $"{item.Action}:{item.Target}")
            .ToHashSet(StringComparer.Ordinal);
        var activeRegistrationIds = registrations.GetAll().Select(item => item.Id).ToHashSet();
        var incidentById = incidents.GetAll().ToDictionary(item => item.Id, StringComparer.Ordinal);
        var candidates = new List<GovernanceCleanupCandidate>();

        foreach (var server in metadata.Snapshot().Servers)
        {
            var key = server.RegistrationId.ToString("D");
            if (!activeRegistrationIds.Contains(server.RegistrationId) && !receiptIndex.Contains($"governance.prune.server:{key}"))
                candidates.Add(new("server", key, "Operator metadata has no active registration."));
        }

        var incidentCutoff = now.AddDays(-_options.ResolvedIncidentMetadataDays);
        var noteCutoff = now.AddDays(-_options.OperatorNoteRetentionDays);
        foreach (var item in metadata.Snapshot().Incidents)
        {
            var shouldPruneIncident = !incidentById.TryGetValue(item.IncidentId, out var incident) ||
                incident.Status == IncidentStatus.Resolved && incident.LastSeenUtc < incidentCutoff;
            if (shouldPruneIncident && !receiptIndex.Contains($"governance.prune.incident:{item.IncidentId}"))
                candidates.Add(new("incident", item.IncidentId, "Incident metadata is orphaned or beyond resolved retention."));

            foreach (var note in item.Notes.Where(note => note.OccurredAtUtc < noteCutoff))
            {
                var key = note.Id.ToString("D");
                if (!receiptIndex.Contains($"governance.prune.note:{key}"))
                    candidates.Add(new("note", key, "Operator note exceeded retention."));
            }
        }

        return new(now, candidates
            .OrderBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .Take(1000)
            .ToArray());
    }

    public int Apply(string actor)
    {
        actor = EnterpriseOperatorValidation.NormalizeActor(actor);
        var plan = DryRun();
        foreach (var candidate in plan.Candidates)
        {
            audit.Append(actor, $"governance.prune.{candidate.Kind}", SecurityInput.NormalizeAuditField(candidate.Key, 160), "applied");
        }
        audit.Append(actor, "governance.cleanup", "operator-metadata", $"applied:{plan.Candidates.Count}");
        return plan.Candidates.Count;
    }

    public IReadOnlyList<AuditEvent> ReadGovernedAudit(int offset, int limit) =>
        audit.Read(Math.Max(0, offset), Math.Min(Math.Clamp(limit, 1, 100), _options.AuditMaxVisibleEvents));

    public bool IsIncidentPruned(string incidentId)
    {
        incidentId = EnterpriseOperatorValidation.NormalizeIncidentId(incidentId);
        return HasReceipt("governance.prune.incident", incidentId);
    }

    public bool IsNotePruned(Guid noteId) => noteId != Guid.Empty && HasReceipt("governance.prune.note", noteId.ToString("D"));

    private bool HasReceipt(string action, string target) =>
        audit.Read(0, 1000).Any(item => item.Action == action && item.Target == target && item.Outcome == "applied");

    private static GovernanceRetentionOptions Validate(GovernanceRetentionOptions options)
    {
        options.Validate();
        return options;
    }
}
