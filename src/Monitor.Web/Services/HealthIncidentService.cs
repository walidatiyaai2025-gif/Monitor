using System.Collections.Concurrent;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface IHealthRuleEvaluator
{
    IReadOnlyList<HealthFinding> Evaluate(Guid registrationId, ServerHealthSnapshot snapshot, SnapshotFreshness freshness);
}

public sealed class HealthRuleEvaluator : IHealthRuleEvaluator
{
    public IReadOnlyList<HealthFinding> Evaluate(Guid registrationId, ServerHealthSnapshot snapshot, SnapshotFreshness freshness)
    {
        var findings = new List<HealthFinding>();
        void Add(string rule, FindingSeverity severity, string title, string evidence) =>
            findings.Add(new(registrationId, rule, severity, title, evidence, snapshot.CollectedAtUtc));

        if (freshness == SnapshotFreshness.Stale) Add("snapshot.stale", FindingSeverity.Warning, "Snapshot is stale", "The last known snapshot is outside the fresh window.");
        if (snapshot.DatabaseTotal > 0 && snapshot.DatabaseOnline < snapshot.DatabaseTotal) Add("database.unavailable", snapshot.DatabaseOnline == 0 ? FindingSeverity.Critical : FindingSeverity.Warning, "Databases unavailable", $"{snapshot.DatabaseTotal - snapshot.DatabaseOnline} database(s) are not online.");
        if (snapshot.Databases is { Suspect: > 0 }) Add("database.suspect", FindingSeverity.Critical, "Suspect databases", $"{snapshot.Databases.Suspect} database(s) report SUSPECT state.");
        if (snapshot.Backups is { MissingFullBackupLast24Hours: > 0 }) Add("backup.full-gap", FindingSeverity.Warning, "Full backup gap", $"{snapshot.Backups.MissingFullBackupLast24Hours} database(s) have no full backup in 24 hours.");
        if (snapshot.Jobs is { FailedLastRun: > 0 }) Add("agent.failed-job", FindingSeverity.Warning, "SQL Agent failures", $"{snapshot.Jobs.FailedLastRun} job(s) failed on their last run.");
        if (snapshot.Blocking is { BlockedRequests: > 0 } blocking) Add("blocking.active", blocking.MaxWaitMilliseconds >= 30000 ? FindingSeverity.Critical : FindingSeverity.Warning, "Active blocking", $"{blocking.BlockedRequests} request(s) blocked; max wait {blocking.MaxWaitMilliseconds} ms.");
        if (snapshot.Memory is { IsPhysicalMemoryLow: true } or { IsVirtualMemoryLow: true }) Add("memory.pressure", FindingSeverity.Warning, "SQL memory pressure", "SQL Server reports a low-memory signal.");
        if (snapshot.Performance is { RunnableTasks: >= 10 } performance) Add("performance.runnable", FindingSeverity.Warning, "Runnable task pressure", $"{performance.RunnableTasks} runnable task(s) observed.");
        return findings;
    }
}

public interface IHealthIncidentRepository
{
    void Apply(IEnumerable<HealthFinding> findings);
    void Reconcile(Guid registrationId, DateTimeOffset observedAtUtc, IEnumerable<HealthFinding> activeFindings, bool canResolve);
    IReadOnlyList<HealthIncident> GetAll();
}

public sealed class InMemoryHealthIncidentRepository : IHealthIncidentRepository
{
    private readonly ConcurrentDictionary<string, HealthIncident> _items = new(StringComparer.Ordinal);

    public void Apply(IEnumerable<HealthFinding> findings)
    {
        foreach (var finding in findings)
        {
            var key = $"{finding.RegistrationId:N}:{finding.RuleId}";
            _items.AddOrUpdate(key,
                _ => new(key, finding.RegistrationId, finding.RuleId, finding.Severity, finding.Title, finding.Evidence, finding.ObservedAtUtc, finding.ObservedAtUtc, 1, IncidentStatus.Open),
                (_, current) => finding.ObservedAtUtc < current.LastSeenUtc
                    ? current
                    : current with { Severity = finding.Severity, Title = finding.Title, Evidence = finding.Evidence, LastSeenUtc = finding.ObservedAtUtc, Occurrences = current.Occurrences + 1, Status = IncidentStatus.Open });
        }
    }

    public void Reconcile(Guid registrationId, DateTimeOffset observedAtUtc, IEnumerable<HealthFinding> activeFindings, bool canResolve)
    {
        var active = activeFindings.ToArray();
        Apply(active);
        if (!canResolve) return;
        var activeRules = active.Select(item => item.RuleId).ToHashSet(StringComparer.Ordinal);
        foreach (var pair in _items.Where(pair => pair.Value.RegistrationId == registrationId && pair.Value.Status != IncidentStatus.Resolved))
        {
            if (!activeRules.Contains(pair.Value.RuleId) && observedAtUtc >= pair.Value.LastSeenUtc)
            {
                _items.TryUpdate(pair.Key, pair.Value with { Status = IncidentStatus.Resolved, LastSeenUtc = observedAtUtc }, pair.Value);
            }
        }
    }

    public IReadOnlyList<HealthIncident> GetAll() => _items.Values
        .OrderByDescending(item => item.Severity)
        .ThenByDescending(item => item.LastSeenUtc)
        .ToArray();
}
