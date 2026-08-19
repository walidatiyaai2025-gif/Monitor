namespace Monitor.Web.Services;

public sealed class WebsiteMonitoringOptions
{
    public const string SectionName = "WebsiteMonitoring";
    public bool Enabled { get; set; }
    public int MaxConcurrency { get; set; } = 4;
    public int SchedulerTickSeconds { get; set; } = 5;

    public void Validate()
    {
        if (MaxConcurrency is < 1 or > 16)
            throw new InvalidOperationException("WebsiteMonitoring:MaxConcurrency must be between 1 and 16.");
        if (SchedulerTickSeconds is < 1 or > 60)
            throw new InvalidOperationException("WebsiteMonitoring:SchedulerTickSeconds must be between 1 and 60.");
    }
}

public interface IWebsiteTargetStore
{
    IReadOnlyList<WebsiteTargetDefinition> GetAll();
    WebsiteTargetDefinition? Get(Guid id);
    void Upsert(WebsiteTargetDefinition target);
    bool Remove(Guid id);
}

public sealed class InMemoryWebsiteTargetStore : IWebsiteTargetStore
{
    private const int MaxTargets = FileWebsiteTargetStore.MaxTargets;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, WebsiteTargetDefinition> _targets = [];

    public IReadOnlyList<WebsiteTargetDefinition> GetAll()
    {
        lock (_gate) return _targets.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public WebsiteTargetDefinition? Get(Guid id)
    {
        lock (_gate) return _targets.TryGetValue(id, out var target) ? target : null;
    }

    public void Upsert(WebsiteTargetDefinition target)
    {
        ValidateTarget(target);
        lock (_gate)
        {
            if (!_targets.ContainsKey(target.Id) && _targets.Count >= MaxTargets)
                throw new InvalidOperationException($"Website target capacity of {MaxTargets} has been reached.");
            _targets[target.Id] = target;
        }
    }

    public bool Remove(Guid id)
    {
        lock (_gate) return _targets.Remove(id);
    }

    internal static void ValidateTarget(WebsiteTargetDefinition target)
    {
        var validation = WebsiteTargetValidator.Validate(target);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors), nameof(target));
    }
}

public sealed class FileWebsiteTargetStore : IWebsiteTargetStore
{
    private const int CurrentFormatVersion = 1;
    private const int MaxDocumentBytes = 4 * 1024 * 1024;
    public const int MaxTargets = 500;
    private readonly object _gate = new();
    private readonly string _path;
    private readonly string _leasePath;

    public FileWebsiteTargetStore(string path)
    {
        _path = Path.GetFullPath(path);
        _leasePath = $"{_path}.lock";
        using var lease = AcquireLease();
        _ = Load();
    }

    public IReadOnlyList<WebsiteTargetDefinition> GetAll()
    {
        lock (_gate)
        {
            using var lease = AcquireLease();
            return Load().Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public WebsiteTargetDefinition? Get(Guid id)
    {
        lock (_gate)
        {
            using var lease = AcquireLease();
            return Load().TryGetValue(id, out var target) ? target : null;
        }
    }

    public void Upsert(WebsiteTargetDefinition target)
    {
        InMemoryWebsiteTargetStore.ValidateTarget(target);
        lock (_gate)
        {
            using var lease = AcquireLease();
            var targets = Load();
            if (!targets.ContainsKey(target.Id) && targets.Count >= MaxTargets)
                throw new InvalidOperationException($"Website target capacity of {MaxTargets} has been reached.");
            targets[target.Id] = target;
            Persist(targets.Values);
        }
    }

    public bool Remove(Guid id)
    {
        lock (_gate)
        {
            using var lease = AcquireLease();
            var targets = Load();
            if (!targets.Remove(id)) return false;
            Persist(targets.Values);
            return true;
        }
    }

    private Dictionary<Guid, WebsiteTargetDefinition> Load()
    {
        var envelope = AtomicJsonFile.Load<TargetEnvelope>(_path, MaxDocumentBytes);
        if (envelope is null) return [];
        if (envelope.Version != CurrentFormatVersion || envelope.Targets is null || envelope.Targets.Length > MaxTargets)
            throw new InvalidDataException("Website target store format or capacity is invalid.");

        var targets = new Dictionary<Guid, WebsiteTargetDefinition>();
        foreach (var target in envelope.Targets)
        {
            try { InMemoryWebsiteTargetStore.ValidateTarget(target); }
            catch (ArgumentException exception) { throw new InvalidDataException("Website target store contains invalid target metadata.", exception); }
            if (!targets.TryAdd(target.Id, target))
                throw new InvalidDataException("Website target store contains duplicate target ids.");
        }
        return targets;
    }

    private void Persist(IEnumerable<WebsiteTargetDefinition> targets) =>
        AtomicJsonFile.Save(_path,
            new TargetEnvelope(CurrentFormatVersion, targets.OrderBy(item => item.Id).ToArray()),
            MaxDocumentBytes);

    private FileStream AcquireLease() => CrossProcessFileLease.Acquire(_leasePath, "Website target store");
    private sealed record TargetEnvelope(int Version, WebsiteTargetDefinition[]? Targets);
}

public sealed record WebsiteProbeHistoryPoint(
    Guid TargetId,
    DateTimeOffset CompletedAtUtc,
    WebsiteProbeState State,
    string RuleId,
    string ProbableLayer,
    string Confidence,
    int? HttpStatusCode,
    long? ElapsedMilliseconds,
    DateTimeOffset? CertificateNotAfterUtc,
    string FinalHost,
    int RedirectCount,
    string EvidenceSummary);

public interface IWebsiteProbeHistoryStore
{
    void Append(WebsiteProbeResult result);
    IReadOnlyList<WebsiteProbeHistoryPoint> Read(Guid targetId, TimeSpan window);
}

public sealed class FileWebsiteProbeHistoryStore : IWebsiteProbeHistoryStore
{
    private const int CurrentFormatVersion = 1;
    private const int MaxDocumentBytes = 32 * 1024 * 1024;
    public const int MaxPerTarget = 2880;
    private static readonly TimeSpan MaxRetention = TimeSpan.FromDays(30);
    private readonly object _gate = new();
    private readonly string _path;
    private readonly string _leasePath;
    private readonly TimeProvider _timeProvider;

    public FileWebsiteProbeHistoryStore(string path, TimeProvider timeProvider)
    {
        _path = Path.GetFullPath(path);
        _leasePath = $"{_path}.lock";
        _timeProvider = timeProvider;
        using var lease = AcquireLease();
        _ = Load();
    }

    public void Append(WebsiteProbeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var point = new WebsiteProbeHistoryPoint(
            result.TargetId,
            result.CompletedAtUtc,
            result.Classification.State,
            Bound(result.Classification.RuleId, 80),
            Bound(result.Classification.ProbableLayer, 120),
            Bound(result.Classification.Confidence, 16),
            result.Evidence.HttpStatusCode,
            result.Evidence.ElapsedMilliseconds,
            result.CertificateNotAfterUtc,
            Bound(result.FinalUri.DnsSafeHost, 253),
            result.RedirectCount,
            Bound(result.Classification.EvidenceSummary, 500));

        ValidatePoint(point);
        lock (_gate)
        {
            using var lease = AcquireLease();
            var cutoff = _timeProvider.GetUtcNow() - MaxRetention;
            var points = Load().Where(item => item.CompletedAtUtc >= cutoff).ToList();
            if (!points.Any(item => item.TargetId == point.TargetId && item.CompletedAtUtc == point.CompletedAtUtc))
                points.Add(point);
            points = points
                .GroupBy(item => item.TargetId)
                .SelectMany(group => group.OrderBy(item => item.CompletedAtUtc).TakeLast(MaxPerTarget))
                .OrderBy(item => item.TargetId)
                .ThenBy(item => item.CompletedAtUtc)
                .ToList();
            Persist(points);
        }
    }

    public IReadOnlyList<WebsiteProbeHistoryPoint> Read(Guid targetId, TimeSpan window)
    {
        if (targetId == Guid.Empty) return [];
        if (window <= TimeSpan.Zero) return [];
        var boundedWindow = window > MaxRetention ? MaxRetention : window;
        lock (_gate)
        {
            using var lease = AcquireLease();
            var cutoff = _timeProvider.GetUtcNow() - boundedWindow;
            return Load()
                .Where(item => item.TargetId == targetId && item.CompletedAtUtc >= cutoff)
                .OrderBy(item => item.CompletedAtUtc)
                .ToArray();
        }
    }

    private List<WebsiteProbeHistoryPoint> Load()
    {
        var envelope = AtomicJsonFile.Load<HistoryEnvelope>(_path, MaxDocumentBytes);
        if (envelope is null) return [];
        if (envelope.Version != CurrentFormatVersion || envelope.Points is null)
            throw new InvalidDataException("Website history store format is invalid.");
        if (envelope.Points.GroupBy(item => item.TargetId).Any(group => group.Count() > MaxPerTarget))
            throw new InvalidDataException("Website history store exceeds the per-target bound.");
        foreach (var point in envelope.Points) ValidatePoint(point);
        return envelope.Points.OrderBy(item => item.TargetId).ThenBy(item => item.CompletedAtUtc).ToList();
    }

    private static void ValidatePoint(WebsiteProbeHistoryPoint point)
    {
        if (point.TargetId == Guid.Empty || point.CompletedAtUtc == default || !Enum.IsDefined(point.State) ||
            point.RuleId.Length is < 1 or > 80 || point.ProbableLayer.Length is < 1 or > 120 ||
            point.Confidence.Length is < 1 or > 16 || point.HttpStatusCode is < 100 or > 599 ||
            point.ElapsedMilliseconds is < 0 || point.FinalHost.Length is < 1 or > 253 ||
            point.RedirectCount is < 0 or > WebsiteProbeEngine.MaxRedirects + 1 || point.EvidenceSummary.Length > 500)
            throw new InvalidDataException("Website history contains invalid bounded probe evidence.");
    }

    private void Persist(IEnumerable<WebsiteProbeHistoryPoint> points) =>
        AtomicJsonFile.Save(_path, new HistoryEnvelope(CurrentFormatVersion, points.ToArray()), MaxDocumentBytes);
    private FileStream AcquireLease() => CrossProcessFileLease.Acquire(_leasePath, "Website probe history store");
    private static string Bound(string value, int max) => value.Length <= max ? value : value[..max];
    private sealed record HistoryEnvelope(int Version, WebsiteProbeHistoryPoint[]? Points);
}

public sealed record WebsiteProbeClaim(Guid TargetId, string Token, DateTimeOffset LeaseUntilUtc);

public interface IWebsiteScheduleStateStore
{
    WebsiteProbeClaim? TryClaim(Guid targetId, DateTimeOffset nowUtc, TimeSpan interval, TimeSpan leaseDuration);
    bool Complete(WebsiteProbeClaim claim, DateTimeOffset completedAtUtc, TimeSpan interval);
}

public sealed class FileWebsiteScheduleStateStore : IWebsiteScheduleStateStore
{
    private const int CurrentFormatVersion = 1;
    private const int MaxDocumentBytes = 4 * 1024 * 1024;
    private const int MaxTargets = FileWebsiteTargetStore.MaxTargets;
    private readonly object _gate = new();
    private readonly string _path;
    private readonly string _leasePath;

    public FileWebsiteScheduleStateStore(string path)
    {
        _path = Path.GetFullPath(path);
        _leasePath = $"{_path}.lock";
        using var lease = AcquireLease();
        _ = Load();
    }

    public WebsiteProbeClaim? TryClaim(Guid targetId, DateTimeOffset nowUtc, TimeSpan interval, TimeSpan leaseDuration)
    {
        if (targetId == Guid.Empty) throw new ArgumentException("Target id is required.", nameof(targetId));
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        lock (_gate)
        {
            using var lease = AcquireLease();
            var states = Load();
            if (!states.TryGetValue(targetId, out var state))
            {
                if (states.Count >= MaxTargets) throw new InvalidOperationException("Website scheduler state capacity has been reached.");
                state = new ScheduleState(targetId, nowUtc, null, null);
            }

            if (state.LeaseUntilUtc is DateTimeOffset leaseUntil && leaseUntil > nowUtc) return null;
            if (state.NextDueUtc > nowUtc) return null;

            var token = Guid.NewGuid().ToString("N");
            var claim = new WebsiteProbeClaim(targetId, token, nowUtc + leaseDuration);
            states[targetId] = state with { LeaseToken = token, LeaseUntilUtc = claim.LeaseUntilUtc };
            Persist(states.Values);
            return claim;
        }
    }

    public bool Complete(WebsiteProbeClaim claim, DateTimeOffset completedAtUtc, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        lock (_gate)
        {
            using var lease = AcquireLease();
            var states = Load();
            if (!states.TryGetValue(claim.TargetId, out var state) ||
                !string.Equals(state.LeaseToken, claim.Token, StringComparison.Ordinal)) return false;
            states[claim.TargetId] = new ScheduleState(claim.TargetId, completedAtUtc + interval, null, null);
            Persist(states.Values);
            return true;
        }
    }

    private Dictionary<Guid, ScheduleState> Load()
    {
        var envelope = AtomicJsonFile.Load<ScheduleEnvelope>(_path, MaxDocumentBytes);
        if (envelope is null) return [];
        if (envelope.Version != CurrentFormatVersion || envelope.States is null || envelope.States.Length > MaxTargets)
            throw new InvalidDataException("Website scheduler state format or capacity is invalid.");
        var states = new Dictionary<Guid, ScheduleState>();
        foreach (var state in envelope.States)
        {
            if (state.TargetId == Guid.Empty || state.NextDueUtc == default ||
                state.LeaseToken is { Length: > 64 } ||
                (state.LeaseToken is null) != (state.LeaseUntilUtc is null) ||
                !states.TryAdd(state.TargetId, state))
                throw new InvalidDataException("Website scheduler state contains invalid claim metadata.");
        }
        return states;
    }

    private void Persist(IEnumerable<ScheduleState> states) =>
        AtomicJsonFile.Save(_path, new ScheduleEnvelope(CurrentFormatVersion, states.OrderBy(item => item.TargetId).ToArray()), MaxDocumentBytes);
    private FileStream AcquireLease() => CrossProcessFileLease.Acquire(_leasePath, "Website scheduler state store");
    private sealed record ScheduleEnvelope(int Version, ScheduleState[]? States);
    private sealed record ScheduleState(Guid TargetId, DateTimeOffset NextDueUtc, string? LeaseToken, DateTimeOffset? LeaseUntilUtc);
}
