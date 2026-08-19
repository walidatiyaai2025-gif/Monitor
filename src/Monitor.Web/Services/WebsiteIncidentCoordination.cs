using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record WebsiteCheckState(
    Guid TargetId,
    WebsiteProbeState LastState,
    string? ActiveRuleId,
    int ConsecutiveFailures,
    int ConsecutiveSuccesses,
    DateTimeOffset LastObservedAtUtc,
    DateTimeOffset? LastSuccessAtUtc,
    DateTimeOffset? LastFailureAtUtc);

public interface IWebsiteCheckStateStore
{
    WebsiteCheckState? Get(Guid targetId);
    void Upsert(WebsiteCheckState state);
}

public sealed class InMemoryWebsiteCheckStateStore : IWebsiteCheckStateStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, WebsiteCheckState> _states = [];

    public WebsiteCheckState? Get(Guid targetId)
    {
        lock (_gate) return _states.TryGetValue(targetId, out var state) ? state : null;
    }

    public void Upsert(WebsiteCheckState state)
    {
        Validate(state);
        lock (_gate) _states[state.TargetId] = state;
    }

    internal static void Validate(WebsiteCheckState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.TargetId == Guid.Empty || !Enum.IsDefined(state.LastState) ||
            state.ActiveRuleId is { Length: > 80 } || state.ConsecutiveFailures is < 0 or > 10 ||
            state.ConsecutiveSuccesses is < 0 or > 10 || state.LastObservedAtUtc == default)
            throw new InvalidDataException("Website check state contains invalid bounded metadata.");
    }
}

public sealed class FileWebsiteCheckStateStore : IWebsiteCheckStateStore
{
    private const int CurrentFormatVersion = 1;
    private const int MaxDocumentBytes = 4 * 1024 * 1024;
    private const int MaxTargets = FileWebsiteTargetStore.MaxTargets;
    private readonly object _gate = new();
    private readonly string _path;
    private readonly string _leasePath;

    public FileWebsiteCheckStateStore(string path)
    {
        _path = Path.GetFullPath(path);
        _leasePath = $"{_path}.lock";
        using var lease = AcquireLease();
        _ = Load();
    }

    public WebsiteCheckState? Get(Guid targetId)
    {
        lock (_gate)
        {
            using var lease = AcquireLease();
            return Load().TryGetValue(targetId, out var state) ? state : null;
        }
    }

    public void Upsert(WebsiteCheckState state)
    {
        InMemoryWebsiteCheckStateStore.Validate(state);
        lock (_gate)
        {
            using var lease = AcquireLease();
            var states = Load();
            if (!states.ContainsKey(state.TargetId) && states.Count >= MaxTargets)
                throw new InvalidOperationException("Website check state capacity has been reached.");
            states[state.TargetId] = state;
            Persist(states.Values);
        }
    }

    private Dictionary<Guid, WebsiteCheckState> Load()
    {
        var envelope = AtomicJsonFile.Load<StateEnvelope>(_path, MaxDocumentBytes);
        if (envelope is null) return [];
        if (envelope.Version != CurrentFormatVersion || envelope.States is null || envelope.States.Length > MaxTargets)
            throw new InvalidDataException("Website check state format or capacity is invalid.");
        var states = new Dictionary<Guid, WebsiteCheckState>();
        foreach (var state in envelope.States)
        {
            InMemoryWebsiteCheckStateStore.Validate(state);
            if (!states.TryAdd(state.TargetId, state))
                throw new InvalidDataException("Website check state contains duplicate target ids.");
        }
        return states;
    }

    private void Persist(IEnumerable<WebsiteCheckState> states) =>
        AtomicJsonFile.Save(_path, new StateEnvelope(CurrentFormatVersion, states.OrderBy(item => item.TargetId).ToArray()), MaxDocumentBytes);
    private FileStream AcquireLease() => CrossProcessFileLease.Acquire(_leasePath, "Website check state store");
    private sealed record StateEnvelope(int Version, WebsiteCheckState[]? States);
}

public interface IWebsiteIncidentCoordinator
{
    void Observe(WebsiteTargetDefinition target, WebsiteProbeResult result);
}

public sealed class WebsiteIncidentCoordinator(
    IWebsiteCheckStateStore stateStore,
    IHealthIncidentRepository incidents) : IWebsiteIncidentCoordinator
{
    public void Observe(WebsiteTargetDefinition target, WebsiteProbeResult result)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(result);
        if (target.Id != result.TargetId) throw new ArgumentException("Probe result target does not match target definition.", nameof(result));

        var previous = stateStore.Get(target.Id) ?? new WebsiteCheckState(
            target.Id, WebsiteProbeState.Unknown, null, 0, 0, result.CompletedAtUtc, null, null);

        switch (result.Classification.State)
        {
            case WebsiteProbeState.Down:
            case WebsiteProbeState.Degraded:
                ObserveFailure(target, result, previous);
                break;
            case WebsiteProbeState.Up:
                ObserveSuccess(target, result, previous);
                break;
            default:
                stateStore.Upsert(previous with
                {
                    LastState = WebsiteProbeState.Unknown,
                    LastObservedAtUtc = result.CompletedAtUtc
                });
                incidents.Reconcile(target.Id, result.CompletedAtUtc, Array.Empty<HealthFinding>(), canResolve: false);
                break;
        }
    }

    private void ObserveFailure(WebsiteTargetDefinition target, WebsiteProbeResult result, WebsiteCheckState previous)
    {
        var sameRule = string.Equals(previous.ActiveRuleId, result.Classification.RuleId, StringComparison.Ordinal);
        var failures = previous.LastState is WebsiteProbeState.Down or WebsiteProbeState.Degraded && sameRule
            ? Math.Min(10, previous.ConsecutiveFailures + 1)
            : 1;
        var confirmed = failures >= target.FailureConfirmationCount;
        var activeRule = confirmed ? result.Classification.RuleId : previous.ActiveRuleId;

        stateStore.Upsert(previous with
        {
            LastState = result.Classification.State,
            ActiveRuleId = activeRule,
            ConsecutiveFailures = failures,
            ConsecutiveSuccesses = 0,
            LastObservedAtUtc = result.CompletedAtUtc,
            LastFailureAtUtc = result.CompletedAtUtc
        });

        if (!confirmed)
        {
            incidents.Reconcile(target.Id, result.CompletedAtUtc, Array.Empty<HealthFinding>(), canResolve: false);
            return;
        }

        var finding = new HealthFinding(
            target.Id,
            result.Classification.RuleId,
            Severity(target, result.Classification.State),
            Title(result.Classification.RuleId, target.Name),
            Evidence(result),
            result.CompletedAtUtc);
        incidents.Reconcile(target.Id, result.CompletedAtUtc, [finding], canResolve: true);
    }

    private void ObserveSuccess(WebsiteTargetDefinition target, WebsiteProbeResult result, WebsiteCheckState previous)
    {
        var successes = previous.LastState == WebsiteProbeState.Up
            ? Math.Min(10, previous.ConsecutiveSuccesses + 1)
            : 1;
        var recovered = successes >= target.RecoveryConfirmationCount;

        stateStore.Upsert(previous with
        {
            LastState = WebsiteProbeState.Up,
            ActiveRuleId = recovered ? null : previous.ActiveRuleId,
            ConsecutiveFailures = 0,
            ConsecutiveSuccesses = successes,
            LastObservedAtUtc = result.CompletedAtUtc,
            LastSuccessAtUtc = result.CompletedAtUtc
        });

        incidents.Reconcile(target.Id, result.CompletedAtUtc, Array.Empty<HealthFinding>(), canResolve: recovered);
    }

    private static FindingSeverity Severity(WebsiteTargetDefinition target, WebsiteProbeState state)
    {
        if (state == WebsiteProbeState.Degraded) return FindingSeverity.Warning;
        return string.Equals(target.Environment, "production", StringComparison.OrdinalIgnoreCase)
            ? FindingSeverity.Critical
            : FindingSeverity.Warning;
    }

    private static string Title(string ruleId, string targetName) => ruleId switch
    {
        "dns.failure" => $"Website DNS failure: {targetName}",
        "network.connect-failure" => $"Website connection failure: {targetName}",
        "network.timeout" => $"Website timeout: {targetName}",
        "tls.invalid" => $"Website TLS failure: {targetName}",
        "tls.expiring" => $"Website certificate expiring: {targetName}",
        "http.4xx" => $"Website HTTP 4xx: {targetName}",
        "http.5xx" => $"Website HTTP 5xx: {targetName}",
        "content.mismatch" => $"Website content contract failed: {targetName}",
        "performance.slow" => $"Website response is slow: {targetName}",
        _ => $"Website check failed: {targetName}"
    };

    private static string Evidence(WebsiteProbeResult result)
    {
        var status = result.Evidence.HttpStatusCode is int value ? $" HTTP={value}." : string.Empty;
        var elapsed = result.Evidence.ElapsedMilliseconds is long ms ? $" Elapsed={ms}ms." : string.Empty;
        var text = $"Observed rule={result.Classification.RuleId}; probable layer={result.Classification.ProbableLayer}; confidence={result.Classification.Confidence}.{status}{elapsed} {result.Classification.EvidenceSummary}";
        return text.Length <= 500 ? text : text[..500];
    }
}
