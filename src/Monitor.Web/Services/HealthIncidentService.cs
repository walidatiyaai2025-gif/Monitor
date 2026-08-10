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
    HealthIncident? GetById(string id);
    bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next);
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
                (_, current) => finding.ObservedAtUtc <= current.LastSeenUtc
                    ? current
                    : current with { Severity = finding.Severity, Title = finding.Title, Evidence = finding.Evidence, LastSeenUtc = finding.ObservedAtUtc, Occurrences = current.Occurrences + 1, Status = current.Status == IncidentStatus.Acknowledged ? IncidentStatus.Acknowledged : IncidentStatus.Open });
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

    public HealthIncident? GetById(string id) => _items.TryGetValue(id, out var value) ? value : null;

    public bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next)
    {
        if (!_items.TryGetValue(id, out var current) || current.Status != expected) return false;
        return _items.TryUpdate(id, current with { Status = next }, current);
    }
}

public interface IRecommendationEngine
{
    RecommendationPlan? Build(HealthIncident incident);
}

public sealed class RecommendationEngine : IRecommendationEngine
{
    private static readonly IReadOnlyDictionary<string, (string Explanation, string[] Steps)> Catalog =
        new Dictionary<string, (string, string[])>(StringComparer.Ordinal)
        {
            ["snapshot.stale"] = ("Restore trustworthy monitoring freshness before acting on other signals.", ["Verify connectivity and collector permissions.", "Request one throttled backend refresh."]),
            ["database.unavailable"] = ("Confirm database state and recovery progress before failover decisions.", ["Review database state in SQL Server.", "Check recovery and storage dependencies."]),
            ["database.suspect"] = ("SUSPECT is a critical integrity signal requiring controlled DBA investigation.", ["Preserve evidence and verify storage health.", "Follow the approved recovery runbook."]),
            ["backup.full-gap"] = ("Restore policy-compliant full backup coverage.", ["Verify the backup schedule and destination availability.", "Run an approved backup job outside Monitor."]),
            ["agent.failed-job"] = ("Investigate failed SQL Agent executions without exposing job commands.", ["Review the job history in the approved admin tool.", "Correct the dependency and rerun under change control."]),
            ["blocking.active"] = ("Identify the blocking chain and protect transaction safety.", ["Inspect active sessions with a read-only diagnostic.", "Coordinate any session termination outside Monitor."]),
            ["memory.pressure"] = ("Validate sustained memory pressure before configuration changes.", ["Compare OS and SQL memory signals.", "Review max server memory under change control."]),
            ["performance.runnable"] = ("Runnable tasks can indicate scheduler pressure.", ["Correlate runnable tasks with workload and CPU telemetry.", "Review expensive workload using approved diagnostics."])
        };

    public RecommendationPlan? Build(HealthIncident incident) => Catalog.TryGetValue(incident.RuleId, out var template)
        ? new(incident.RuleId, template.Explanation, template.Steps.Select((step, index) => new RecommendationStep(index + 1, step, "Advisory only")).ToArray())
        : null;
}

public interface IAdvisorContextBuilder { AdvisorContext Build(HealthIncident incident, RecommendationPlan? recommendation); }
public sealed class AdvisorContextBuilder : IAdvisorContextBuilder
{
    public AdvisorContext Build(HealthIncident incident, RecommendationPlan? recommendation) =>
        new(Bounded(incident.RuleId, 80), incident.Severity, Bounded(incident.Evidence, 500), Bounded(recommendation?.Explanation ?? "No recommendation available.", 500));

    private static string Bounded(string value, int length) => value.Length <= length ? value : value[..length];
}

public interface IAdvisorProvider { Task<AdvisorResult> AdviseAsync(AdvisorContext context, CancellationToken cancellationToken); }
public sealed class DisabledAdvisorProvider : IAdvisorProvider
{
    public Task<AdvisorResult> AdviseAsync(AdvisorContext context, CancellationToken cancellationToken) =>
        Task.FromResult(new AdvisorResult(AdvisorStatus.Disabled, "AI Advisor is disabled until an approved provider is configured."));
}

public interface IIncidentWorkflowService
{
    IncidentCenterViewModel Query(IncidentQuery query);
    Task<IncidentDetailsViewModel?> GetDetailsAsync(string id, CancellationToken cancellationToken);
    bool Acknowledge(string id);
    bool Resolve(string id);
    bool Reopen(string id);
}

public sealed class IncidentWorkflowService(
    IHealthIncidentRepository repository,
    IRecommendationEngine recommendations,
    IAdvisorContextBuilder contextBuilder,
    IAdvisorProvider advisor) : IIncidentWorkflowService
{
    public IncidentCenterViewModel Query(IncidentQuery query)
    {
        var all = repository.GetAll();
        var filtered = all.Where(item => query.Status is null || item.Status == query.Status)
            .Where(item => query.Severity is null || item.Severity == query.Severity)
            .Where(item => string.IsNullOrWhiteSpace(query.RuleId) || string.Equals(item.RuleId, query.RuleId, StringComparison.Ordinal))
            .Skip(Math.Max(0, query.Offset)).Take(Math.Clamp(query.Limit, 1, 100)).ToArray();
        var summary = new IncidentSummary(
            all.Count(item => item.Status == IncidentStatus.Open), all.Count(item => item.Status == IncidentStatus.Acknowledged),
            all.Count(item => item.Status == IncidentStatus.Resolved), all.Count(item => item.Severity == FindingSeverity.Critical),
            all.Count(item => item.Severity == FindingSeverity.Warning));
        return new(filtered, summary, query with { Offset = Math.Max(0, query.Offset), Limit = Math.Clamp(query.Limit, 1, 100) });
    }

    public async Task<IncidentDetailsViewModel?> GetDetailsAsync(string id, CancellationToken cancellationToken)
    {
        var incident = repository.GetById(id);
        if (incident is null) return null;
        var plan = recommendations.Build(incident);
        var result = await advisor.AdviseAsync(contextBuilder.Build(incident, plan), cancellationToken);
        return new(incident, plan, result);
    }

    public bool Acknowledge(string id) => repository.TrySetStatus(id, IncidentStatus.Open, IncidentStatus.Acknowledged);
    public bool Resolve(string id) => repository.TrySetStatus(id, IncidentStatus.Acknowledged, IncidentStatus.Resolved) || repository.TrySetStatus(id, IncidentStatus.Open, IncidentStatus.Resolved);
    public bool Reopen(string id) => repository.TrySetStatus(id, IncidentStatus.Resolved, IncidentStatus.Open);
}

public interface IAdvisorRequestService { Task<AdvisorResult> RequestAsync(string incidentId, string actor, CancellationToken cancellationToken); }
public sealed class AdvisorRequestService(
    IHealthIncidentRepository incidents,
    IRecommendationEngine recommendations,
    IAdvisorContextBuilder contextBuilder,
    IAdvisorProvider provider,
    IAuditStore audit,
    TimeProvider timeProvider) : IAdvisorRequestService
{
    private sealed record CacheEntry(DateTimeOffset EvidenceAtUtc, DateTimeOffset ExpiresAtUtc, AdvisorResult Result);
    private readonly ConcurrentDictionary<string, Lazy<Task<AdvisorResult>>> _flights = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private int _failures;
    private DateTimeOffset _circuitUntilUtc;

    public async Task<AdvisorResult> RequestAsync(string incidentId, string actor, CancellationToken cancellationToken)
    {
        var incident = incidents.GetById(incidentId);
        if (incident is null) return new(AdvisorStatus.Unavailable, "Incident was not found.");
        var now = timeProvider.GetUtcNow();
        if (_cache.TryGetValue(incidentId, out var cached) && cached.EvidenceAtUtc == incident.LastSeenUtc && cached.ExpiresAtUtc > now) return cached.Result;
        if (now < _circuitUntilUtc) return new(AdvisorStatus.Unavailable, "AI Advisor is temporarily unavailable.");

        var flight = _flights.GetOrAdd(incidentId, _ => new(() => InvokeAsync(incident, actor, CancellationToken.None)));
        try { return await flight.Value.WaitAsync(cancellationToken); }
        finally { _flights.TryRemove(new KeyValuePair<string, Lazy<Task<AdvisorResult>>>(incidentId, flight)); }
    }

    private async Task<AdvisorResult> InvokeAsync(HealthIncident incident, string actor, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var context = contextBuilder.Build(incident, recommendations.Build(incident));
            var result = await provider.AdviseAsync(context, timeout.Token);
            _failures = 0;
            _cache[incident.Id] = new(incident.LastSeenUtc, timeProvider.GetUtcNow().AddMinutes(5), result);
            audit.Append(actor, "advisor.request", incident.Id, result.Status.ToString());
            return result;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (Interlocked.Increment(ref _failures) >= 3) _circuitUntilUtc = timeProvider.GetUtcNow().AddMinutes(1);
            audit.Append(actor, "advisor.request", incident.Id, "unavailable");
            return new(AdvisorStatus.Unavailable, "AI Advisor is temporarily unavailable.");
        }
    }
}
