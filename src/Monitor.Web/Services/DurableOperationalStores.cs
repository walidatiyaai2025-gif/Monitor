using System.Text.Json;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum OperationalStoreMode
{
    File,
    InMemory
}

public sealed class OperationalStoreOptions
{
    public const string SectionName = "OperationalStore";

    public OperationalStoreMode Mode { get; set; } = OperationalStoreMode.File;
    public string RootPath { get; set; } = "App_Data/operational";

    public void Validate()
    {
        if (!Enum.IsDefined(Mode))
        {
            throw new InvalidOperationException("OperationalStore:Mode is not supported.");
        }

        if (Mode == OperationalStoreMode.File && string.IsNullOrWhiteSpace(RootPath))
        {
            throw new InvalidOperationException("OperationalStore:RootPath is required when file persistence is enabled.");
        }
    }
}

public static class OperationalStorePath
{
    public static string ResolveOutsideWebRoot(
        string configuredPath,
        string contentRootPath,
        string? webRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException("Operational store path is required.");
        }

        var root = Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
        var webRoot = Path.GetFullPath(webRootPath ?? Path.Combine(contentRootPath, "wwwroot"));
        var relativeToWebRoot = Path.GetRelativePath(webRoot, root);
        if (relativeToWebRoot == "." ||
            (!relativeToWebRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
             !string.Equals(relativeToWebRoot, "..", StringComparison.Ordinal) &&
             !Path.IsPathRooted(relativeToWebRoot)))
        {
            throw new InvalidOperationException("OperationalStore:RootPath must be outside wwwroot.");
        }

        return root;
    }
}

internal static class AtomicJsonFile
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static T? Load<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, Options)
                ?? throw new InvalidDataException("Operational state file is empty or invalid.");
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or IOException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException("Operational state file is corrupt or unreadable.", exception);
        }
    }

    public static void Save<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Operational state directory could not be resolved.");
        Directory.CreateDirectory(directory);

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value, Options);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}

public sealed class FileAuditStore : IAuditStore
{
    private const int CurrentFormatVersion = 1;
    private const int MaxEvents = 1000;
    private readonly object _gate = new();
    private readonly string _path;
    private readonly TimeProvider _timeProvider;
    private List<AuditEvent> _events;

    public FileAuditStore(string path, TimeProvider timeProvider)
    {
        _path = Path.GetFullPath(path);
        _timeProvider = timeProvider;
        _events = Load(_path);
    }

    public void Append(string actor, string action, string target, string outcome)
    {
        static string Bound(string value, int max) => value.Length <= max ? value : value[..max];

        lock (_gate)
        {
            var candidate = new List<AuditEvent>(_events)
            {
                new(Guid.NewGuid(), _timeProvider.GetUtcNow(), Bound(actor, 100), Bound(action, 80), Bound(target, 160), Bound(outcome, 40))
            };
            if (candidate.Count > MaxEvents)
            {
                candidate.RemoveRange(0, candidate.Count - MaxEvents);
            }

            Persist(candidate);
            _events = candidate;
        }
    }

    public IReadOnlyList<AuditEvent> Read(int offset, int limit)
    {
        lock (_gate)
        {
            return _events
                .OrderByDescending(item => item.OccurredAtUtc)
                .Skip(Math.Max(0, offset))
                .Take(Math.Clamp(limit, 1, 100))
                .ToArray();
        }
    }

    private void Persist(IReadOnlyList<AuditEvent> events) =>
        AtomicJsonFile.Save(_path, new AuditEnvelope(CurrentFormatVersion, events.ToArray()));

    private static List<AuditEvent> Load(string path)
    {
        var envelope = AtomicJsonFile.Load<AuditEnvelope>(path);
        if (envelope is null)
        {
            return [];
        }
        if (envelope.Version != CurrentFormatVersion || envelope.Events is null || envelope.Events.Length > MaxEvents)
        {
            throw new InvalidDataException("Audit store format or event count is invalid.");
        }

        var ids = new HashSet<Guid>();
        foreach (var item in envelope.Events)
        {
            if (item.Id == Guid.Empty || !ids.Add(item.Id) || item.OccurredAtUtc == default ||
                item.Actor.Length > 100 || item.Action.Length > 80 || item.Target.Length > 160 || item.Outcome.Length > 40)
            {
                throw new InvalidDataException("Audit store contains invalid bounded event metadata.");
            }
        }

        return envelope.Events.OrderBy(item => item.OccurredAtUtc).ToList();
    }

    private sealed record AuditEnvelope(int Version, AuditEvent[]? Events);
}

public sealed class FileSnapshotHistoryStore : ISnapshotHistoryStore
{
    private const int CurrentFormatVersion = 1;
    private const int MaxPerServer = 288;
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private readonly object _gate = new();
    private readonly string _path;
    private readonly TimeProvider _timeProvider;
    private List<SnapshotHistoryPoint> _points;

    public FileSnapshotHistoryStore(string path, TimeProvider timeProvider)
    {
        _path = Path.GetFullPath(path);
        _timeProvider = timeProvider;
        _points = Load(_path);
    }

    public void Append(SnapshotCacheResult result)
    {
        var snapshot = result.Snapshot;
        var point = new SnapshotHistoryPoint(
            snapshot.RegistrationId,
            snapshot.CollectedAtUtc,
            snapshot.DatabaseOnline,
            snapshot.DatabaseTotal,
            snapshot.Memory?.SqlProcessMemoryUtilizationPercent,
            snapshot.Blocking?.BlockedRequests,
            snapshot.Performance?.RunnableTasks,
            result.Freshness);

        lock (_gate)
        {
            var cutoff = _timeProvider.GetUtcNow() - Retention;
            var candidate = _points.Where(item => item.CollectedAtUtc >= cutoff).ToList();
            if (!candidate.Any(item => item.RegistrationId == point.RegistrationId && item.CollectedAtUtc == point.CollectedAtUtc))
            {
                candidate.Add(point);
            }

            candidate = candidate
                .GroupBy(item => item.RegistrationId)
                .SelectMany(group => group.OrderBy(item => item.CollectedAtUtc).TakeLast(MaxPerServer))
                .OrderBy(item => item.RegistrationId)
                .ThenBy(item => item.CollectedAtUtc)
                .ToList();

            Persist(candidate);
            _points = candidate;
        }
    }

    public IReadOnlyList<SnapshotHistoryPoint> Read(Guid registrationId, TimeSpan window)
    {
        lock (_gate)
        {
            var cutoff = _timeProvider.GetUtcNow() - window;
            return _points
                .Where(item => item.RegistrationId == registrationId && item.CollectedAtUtc >= cutoff)
                .OrderBy(item => item.CollectedAtUtc)
                .ToArray();
        }
    }

    private void Persist(IReadOnlyList<SnapshotHistoryPoint> points) =>
        AtomicJsonFile.Save(_path, new HistoryEnvelope(CurrentFormatVersion, points.ToArray()));

    private static List<SnapshotHistoryPoint> Load(string path)
    {
        var envelope = AtomicJsonFile.Load<HistoryEnvelope>(path);
        if (envelope is null)
        {
            return [];
        }
        if (envelope.Version != CurrentFormatVersion || envelope.Points is null)
        {
            throw new InvalidDataException("History store format is invalid.");
        }

        var keys = new HashSet<(Guid RegistrationId, DateTimeOffset CollectedAtUtc)>();
        foreach (var point in envelope.Points)
        {
            if (point.RegistrationId == Guid.Empty || point.CollectedAtUtc == default ||
                point.DatabaseTotal < 0 || point.DatabaseOnline < 0 || point.DatabaseOnline > point.DatabaseTotal ||
                point.MemoryPercent is < 0 or > 100 || point.BlockedRequests is < 0 || point.RunnableTasks is < 0 ||
                !Enum.IsDefined(point.Freshness) || !keys.Add((point.RegistrationId, point.CollectedAtUtc)))
            {
                throw new InvalidDataException("History store contains invalid aggregate state.");
            }
        }

        if (envelope.Points.GroupBy(item => item.RegistrationId).Any(group => group.Count() > MaxPerServer))
        {
            throw new InvalidDataException("History store exceeds the per-server retention bound.");
        }

        return envelope.Points
            .OrderBy(item => item.RegistrationId)
            .ThenBy(item => item.CollectedAtUtc)
            .ToList();
    }

    private sealed record HistoryEnvelope(int Version, SnapshotHistoryPoint[]? Points);
}

public sealed partial class FileHealthIncidentRepository : IHealthIncidentRepository
{
    private const int CurrentFormatVersion = 1;
    private const int MaxRuleIdLength = 80;
    private const int MaxTitleLength = 160;
    private const int MaxEvidenceLength = 500;
    private readonly object _gate = new();
    private readonly string _path;
    private Dictionary<string, HealthIncident> _items;

    public FileHealthIncidentRepository(string path)
    {
        _path = Path.GetFullPath(path);
        _items = Load(_path);
    }

    public void Apply(IEnumerable<HealthFinding> findings)
    {
        var materialized = findings.ToArray();
        lock (_gate)
        {
            var candidate = new Dictionary<string, HealthIncident>(_items, StringComparer.Ordinal);
            ApplyTo(candidate, materialized);
            Commit(candidate);
        }
    }

    public void Reconcile(
        Guid registrationId,
        DateTimeOffset observedAtUtc,
        IEnumerable<HealthFinding> activeFindings,
        bool canResolve)
    {
        var active = activeFindings.ToArray();
        lock (_gate)
        {
            var candidate = new Dictionary<string, HealthIncident>(_items, StringComparer.Ordinal);
            ApplyTo(candidate, active);
            if (canResolve)
            {
                var activeRules = active.Select(item => item.RuleId).ToHashSet(StringComparer.Ordinal);
                foreach (var pair in candidate
                    .Where(pair => pair.Value.RegistrationId == registrationId && pair.Value.Status != IncidentStatus.Resolved)
                    .ToArray())
                {
                    if (!activeRules.Contains(pair.Value.RuleId) && observedAtUtc >= pair.Value.LastSeenUtc)
                    {
                        candidate[pair.Key] = pair.Value with { Status = IncidentStatus.Resolved, LastSeenUtc = observedAtUtc };
                    }
                }
            }

            Commit(candidate);
        }
    }

    public IReadOnlyList<HealthIncident> GetAll()
    {
        lock (_gate)
        {
            return _items.Values
                .OrderByDescending(item => item.Severity)
                .ThenByDescending(item => item.LastSeenUtc)
                .ToArray();
        }
    }

    public HealthIncident? GetById(string id)
    {
        lock (_gate)
        {
            return _items.TryGetValue(id, out var value) ? value : null;
        }
    }

    public bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(id, out var current) || current.Status != expected)
            {
                return false;
            }

            var candidate = new Dictionary<string, HealthIncident>(_items, StringComparer.Ordinal)
            {
                [id] = current with { Status = next }
            };
            Commit(candidate);
            return true;
        }
    }

    private void Commit(Dictionary<string, HealthIncident> candidate)
    {
        ValidateItems(candidate.Values);
        AtomicJsonFile.Save(
            _path,
            new IncidentEnvelope(CurrentFormatVersion, candidate.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray()));
        _items = candidate;
    }

    private static void ApplyTo(Dictionary<string, HealthIncident> items, IEnumerable<HealthFinding> findings)
    {
        foreach (var finding in findings)
        {
            ValidateFinding(finding);
            var key = $"{finding.RegistrationId:N}:{finding.RuleId}";
            if (!items.TryGetValue(key, out var current))
            {
                items[key] = new(
                    key,
                    finding.RegistrationId,
                    finding.RuleId,
                    finding.Severity,
                    finding.Title,
                    finding.Evidence,
                    finding.ObservedAtUtc,
                    finding.ObservedAtUtc,
                    1,
                    IncidentStatus.Open);
                continue;
            }

            if (finding.ObservedAtUtc <= current.LastSeenUtc)
            {
                continue;
            }

            items[key] = current with
            {
                Severity = finding.Severity,
                Title = finding.Title,
                Evidence = finding.Evidence,
                LastSeenUtc = finding.ObservedAtUtc,
                Occurrences = current.Occurrences + 1,
                Status = current.Status == IncidentStatus.Acknowledged ? IncidentStatus.Acknowledged : IncidentStatus.Open
            };
        }
    }

    private static void ValidateFinding(HealthFinding finding)
    {
        if (finding.RegistrationId == Guid.Empty || finding.ObservedAtUtc == default ||
            string.IsNullOrWhiteSpace(finding.RuleId) || finding.RuleId.Length > MaxRuleIdLength ||
            string.IsNullOrWhiteSpace(finding.Title) || finding.Title.Length > MaxTitleLength ||
            finding.Evidence.Length > MaxEvidenceLength || !Enum.IsDefined(finding.Severity))
        {
            throw new InvalidDataException("Finding is outside the durable incident bounds.");
        }
    }

    private static void ValidateItems(IEnumerable<HealthIncident> incidents)
    {
        foreach (var item in incidents)
        {
            var expectedId = $"{item.RegistrationId:N}:{item.RuleId}";
            if (item.RegistrationId == Guid.Empty || item.FirstSeenUtc == default || item.LastSeenUtc == default ||
                item.FirstSeenUtc > item.LastSeenUtc || item.Occurrences < 1 ||
                string.IsNullOrWhiteSpace(item.RuleId) || item.RuleId.Length > MaxRuleIdLength ||
                string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > MaxTitleLength ||
                item.Evidence.Length > MaxEvidenceLength ||
                !string.Equals(item.Id, expectedId, StringComparison.Ordinal) ||
                !Enum.IsDefined(item.Severity) || !Enum.IsDefined(item.Status))
            {
                throw new InvalidDataException("Incident store contains invalid bounded state.");
            }
        }
    }

    private static Dictionary<string, HealthIncident> Load(string path)
    {
        var envelope = AtomicJsonFile.Load<IncidentEnvelope>(path);
        if (envelope is null)
        {
            return new(StringComparer.Ordinal);
        }
        if (envelope.Version != CurrentFormatVersion || envelope.Incidents is null)
        {
            throw new InvalidDataException("Incident store format is invalid.");
        }

        ValidateItems(envelope.Incidents);
        var result = new Dictionary<string, HealthIncident>(StringComparer.Ordinal);
        foreach (var incident in envelope.Incidents)
        {
            if (!result.TryAdd(incident.Id, incident))
            {
                throw new InvalidDataException("Incident store contains duplicate incident IDs.");
            }
        }

        return result;
    }

    private sealed record IncidentEnvelope(int Version, HealthIncident[]? Incidents);
}
