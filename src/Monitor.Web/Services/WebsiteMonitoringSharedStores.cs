using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Monitor.Web.Services;

internal static class WebsiteSharedStateKeys
{
    public const string TargetIndex = "monitor:website:targets:v1";
    public const string GroupIndex = "monitor:website:groups:v1";
    public const string OutboxIndex = "monitor:website:outbox:v1";

    public static string Target(Guid id) => $"monitor:website:target:v1:{id:N}";
    public static string History(Guid id) => $"monitor:website:history:v1:{id:N}";
    public static string Schedule(Guid id) => $"monitor:website:schedule:v1:{id:N}";
    public static string CheckState(Guid id) => $"monitor:website:check:v1:{id:N}";
    public static string Group(string id) => $"monitor:website:group:v1:{Hash(id.Trim().ToLowerInvariant())}";
    public static string OutboxItem(string id) => $"monitor:website:outbox-item:v1:{Hash(id)}";

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..40];
}

public sealed class SharedWebsiteTargetStore : IWebsiteTargetStore
{
    private const int FormatVersion = 1;
    private readonly ISharedStateDocumentStore _store;

    public SharedWebsiteTargetStore(ISharedStateDocumentStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public IReadOnlyList<WebsiteTargetDefinition> GetAll() =>
        ReadIndex().Select(ReadIndexedTarget)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public WebsiteTargetDefinition? Get(Guid id)
    {
        if (id == Guid.Empty || !ReadIndex().Contains(id)) return null;
        return ReadIndexedTarget(id);
    }

    public void Upsert(WebsiteTargetDefinition target)
    {
        InMemoryWebsiteTargetStore.ValidateTarget(target);
        WriteTarget(target);
        SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.TargetIndex,
            DeserializeIndex,
            ids =>
            {
                if (ids.Contains(target.Id))
                    return SharedStateDocumentMutation.MutationResult<HashSet<Guid>, bool>.Unchanged(ids, true);
                if (ids.Count >= FileWebsiteTargetStore.MaxTargets)
                    throw new InvalidOperationException($"Website target capacity of {FileWebsiteTargetStore.MaxTargets} has been reached.");
                ids.Add(target.Id);
                return SharedStateDocumentMutation.MutationResult<HashSet<Guid>, bool>.Applied(ids, true);
            },
            SerializeIndex);
    }

    public bool Remove(Guid id)
    {
        if (id == Guid.Empty) return false;
        return SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.TargetIndex,
            DeserializeIndex,
            ids =>
            {
                if (!ids.Remove(id))
                    return SharedStateDocumentMutation.MutationResult<HashSet<Guid>, bool>.Unchanged(ids, false);
                return SharedStateDocumentMutation.MutationResult<HashSet<Guid>, bool>.Applied(ids, true);
            },
            SerializeIndex);
    }

    private WebsiteTargetDefinition ReadIndexedTarget(Guid id)
    {
        var document = SharedStateDocumentMutation.Read(_store, WebsiteSharedStateKeys.Target(id))
            ?? throw new InvalidDataException("Shared website target index references missing target metadata.");
        return DeserializeTarget(document.PayloadJson, id);
    }

    private void WriteTarget(WebsiteTargetDefinition target) =>
        SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.Target(target.Id),
            payload => payload is null ? null : DeserializeTarget(payload, target.Id),
            _ => SharedStateDocumentMutation.MutationResult<WebsiteTargetDefinition?, bool>.Applied(target, true),
            value => JsonSerializer.Serialize(new TargetEnvelope(FormatVersion, value!), SharedStateDocumentMutation.JsonOptions));

    private static WebsiteTargetDefinition DeserializeTarget(string payload, Guid expectedId)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<TargetEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared website target metadata is invalid.");
            if (envelope.Version != FormatVersion || envelope.Target.Id != expectedId)
                throw new InvalidDataException("Shared website target identity or format is invalid.");
            InMemoryWebsiteTargetStore.ValidateTarget(envelope.Target);
            return envelope.Target;
        }
        catch (InvalidDataException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Shared website target metadata is corrupt.", exception);
        }
    }

    private HashSet<Guid> ReadIndex() =>
        DeserializeIndex(SharedStateDocumentMutation.Read(_store, WebsiteSharedStateKeys.TargetIndex)?.PayloadJson);

    private static HashSet<Guid> DeserializeIndex(string? payload)
    {
        if (payload is null) return [];
        try
        {
            var envelope = JsonSerializer.Deserialize<TargetIndexEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared website target index is invalid.");
            if (envelope.Version != FormatVersion || envelope.TargetIds is null ||
                envelope.TargetIds.Length > FileWebsiteTargetStore.MaxTargets ||
                envelope.TargetIds.Any(id => id == Guid.Empty) ||
                envelope.TargetIds.Distinct().Count() != envelope.TargetIds.Length)
                throw new InvalidDataException("Shared website target index is outside its bounded contract.");
            return envelope.TargetIds.ToHashSet();
        }
        catch (InvalidDataException) { throw; }
        catch (JsonException exception) { throw new InvalidDataException("Shared website target index is corrupt.", exception); }
    }

    private static string SerializeIndex(HashSet<Guid> ids) =>
        JsonSerializer.Serialize(new TargetIndexEnvelope(FormatVersion, ids.OrderBy(id => id).ToArray()), SharedStateDocumentMutation.JsonOptions);

    private sealed record TargetEnvelope(int Version, WebsiteTargetDefinition Target);
    private sealed record TargetIndexEnvelope(int Version, Guid[]? TargetIds);
}

public sealed class SharedWebsiteProbeHistoryStore : IWebsiteProbeHistoryStore
{
    private const int FormatVersion = 1;
    private static readonly TimeSpan MaxRetention = TimeSpan.FromDays(30);
    private readonly ISharedStateDocumentStore _store;
    private readonly TimeProvider _timeProvider;

    public SharedWebsiteProbeHistoryStore(ISharedStateDocumentStore store, TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public void Append(WebsiteProbeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var point = ToPoint(result);
        ValidatePoint(point, result.TargetId);
        SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.History(result.TargetId),
            payload => Deserialize(payload, result.TargetId),
            points =>
            {
                var cutoff = _timeProvider.GetUtcNow() - MaxRetention;
                points.RemoveAll(item => item.CompletedAtUtc < cutoff);
                if (!points.Any(item => item.CompletedAtUtc == point.CompletedAtUtc)) points.Add(point);
                points = points.OrderBy(item => item.CompletedAtUtc).TakeLast(FileWebsiteProbeHistoryStore.MaxPerTarget).ToList();
                return SharedStateDocumentMutation.MutationResult<List<WebsiteProbeHistoryPoint>, bool>.Applied(points, true);
            },
            SerializeBounded);
    }

    public IReadOnlyList<WebsiteProbeHistoryPoint> Read(Guid targetId, TimeSpan window)
    {
        if (targetId == Guid.Empty || window <= TimeSpan.Zero) return [];
        var bounded = window > MaxRetention ? MaxRetention : window;
        var cutoff = _timeProvider.GetUtcNow() - bounded;
        return Deserialize(SharedStateDocumentMutation.Read(_store, WebsiteSharedStateKeys.History(targetId))?.PayloadJson, targetId)
            .Where(item => item.CompletedAtUtc >= cutoff)
            .OrderBy(item => item.CompletedAtUtc)
            .ToArray();
    }

    private static WebsiteProbeHistoryPoint ToPoint(WebsiteProbeResult result) => new(
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

    private static List<WebsiteProbeHistoryPoint> Deserialize(string? payload, Guid expectedTargetId)
    {
        if (payload is null) return [];
        try
        {
            var envelope = JsonSerializer.Deserialize<HistoryEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared website history is invalid.");
            if (envelope.Version != FormatVersion || envelope.Points is null ||
                envelope.Points.Length > FileWebsiteProbeHistoryStore.MaxPerTarget)
                throw new InvalidDataException("Shared website history format or capacity is invalid.");
            foreach (var point in envelope.Points) ValidatePoint(point, expectedTargetId);
            if (envelope.Points.Select(item => item.CompletedAtUtc).Distinct().Count() != envelope.Points.Length)
                throw new InvalidDataException("Shared website history contains duplicate observation timestamps.");
            return envelope.Points.OrderBy(item => item.CompletedAtUtc).ToList();
        }
        catch (InvalidDataException) { throw; }
        catch (JsonException exception) { throw new InvalidDataException("Shared website history is corrupt.", exception); }
    }

    private static string SerializeBounded(List<WebsiteProbeHistoryPoint> points)
    {
        points = points.OrderBy(item => item.CompletedAtUtc).TakeLast(FileWebsiteProbeHistoryStore.MaxPerTarget).ToList();
        while (true)
        {
            var payload = JsonSerializer.Serialize(new HistoryEnvelope(FormatVersion, points.ToArray()), SharedStateDocumentMutation.JsonOptions);
            if (Encoding.UTF8.GetByteCount(payload) <= SqlServerSharedStateDocumentStore.MaximumPayloadBytes) return payload;
            if (points.Count <= 1) throw new InvalidDataException("A website history point exceeds the shared-state payload bound.");
            points.RemoveRange(0, Math.Max(1, points.Count / 8));
        }
    }

    private static void ValidatePoint(WebsiteProbeHistoryPoint point, Guid expectedTargetId)
    {
        if (point.TargetId != expectedTargetId || point.TargetId == Guid.Empty || point.CompletedAtUtc == default ||
            !Enum.IsDefined(point.State) || point.RuleId.Length is < 1 or > 80 ||
            point.ProbableLayer.Length is < 1 or > 120 || point.Confidence.Length is < 1 or > 16 ||
            point.HttpStatusCode is < 100 or > 599 || point.ElapsedMilliseconds is < 0 ||
            point.FinalHost.Length is < 1 or > 253 || point.RedirectCount is < 0 or > WebsiteProbeEngine.MaxRedirects + 1 ||
            point.EvidenceSummary.Length > 500)
            throw new InvalidDataException("Shared website history contains invalid bounded probe evidence.");
    }

    private static string Bound(string value, int max) => value.Length <= max ? value : value[..max];
    private sealed record HistoryEnvelope(int Version, WebsiteProbeHistoryPoint[]? Points);
}

public sealed class SharedWebsiteScheduleStateStore : IWebsiteScheduleStateStore
{
    private const int FormatVersion = 1;
    private readonly ISharedStateDocumentStore _store;

    public SharedWebsiteScheduleStateStore(ISharedStateDocumentStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public WebsiteProbeClaim? TryClaim(Guid targetId, DateTimeOffset nowUtc, TimeSpan interval, TimeSpan leaseDuration)
    {
        if (targetId == Guid.Empty) throw new ArgumentException("Target id is required.", nameof(targetId));
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        return SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.Schedule(targetId),
            payload => Deserialize(payload, targetId, nowUtc),
            state =>
            {
                if (state.LeaseUntilUtc is DateTimeOffset leaseUntil && leaseUntil > nowUtc)
                    return SharedStateDocumentMutation.MutationResult<ScheduleState, WebsiteProbeClaim?>.Unchanged(state, null);
                if (state.NextDueUtc > nowUtc)
                    return SharedStateDocumentMutation.MutationResult<ScheduleState, WebsiteProbeClaim?>.Unchanged(state, null);
                var token = Guid.NewGuid().ToString("N");
                var claim = new WebsiteProbeClaim(targetId, token, nowUtc + leaseDuration);
                return SharedStateDocumentMutation.MutationResult<ScheduleState, WebsiteProbeClaim?>.Applied(
                    state with { LeaseToken = token, LeaseUntilUtc = claim.LeaseUntilUtc }, claim);
            },
            Serialize);
    }

    public bool Complete(WebsiteProbeClaim claim, DateTimeOffset completedAtUtc, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        return SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.Schedule(claim.TargetId),
            payload => Deserialize(payload, claim.TargetId, completedAtUtc),
            state =>
            {
                if (!string.Equals(state.LeaseToken, claim.Token, StringComparison.Ordinal))
                    return SharedStateDocumentMutation.MutationResult<ScheduleState, bool>.Unchanged(state, false);
                return SharedStateDocumentMutation.MutationResult<ScheduleState, bool>.Applied(
                    new ScheduleState(claim.TargetId, completedAtUtc + interval, null, null), true);
            },
            Serialize);
    }

    private static ScheduleState Deserialize(string? payload, Guid targetId, DateTimeOffset initialDue)
    {
        if (payload is null) return new ScheduleState(targetId, initialDue, null, null);
        try
        {
            var envelope = JsonSerializer.Deserialize<ScheduleEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared website scheduler state is invalid.");
            var state = envelope.State;
            if (envelope.Version != FormatVersion || state.TargetId != targetId || state.NextDueUtc == default ||
                state.LeaseToken is { Length: > 64 } || (state.LeaseToken is null) != (state.LeaseUntilUtc is null))
                throw new InvalidDataException("Shared website scheduler state is outside its bounded contract.");
            return state;
        }
        catch (InvalidDataException) { throw; }
        catch (JsonException exception) { throw new InvalidDataException("Shared website scheduler state is corrupt.", exception); }
    }

    private static string Serialize(ScheduleState state) =>
        JsonSerializer.Serialize(new ScheduleEnvelope(FormatVersion, state), SharedStateDocumentMutation.JsonOptions);

    private sealed record ScheduleEnvelope(int Version, ScheduleState State);
    private sealed record ScheduleState(Guid TargetId, DateTimeOffset NextDueUtc, string? LeaseToken, DateTimeOffset? LeaseUntilUtc);
}

public sealed class SharedWebsiteCheckStateStore : IWebsiteCheckStateStore
{
    private const int FormatVersion = 1;
    private readonly ISharedStateDocumentStore _store;

    public SharedWebsiteCheckStateStore(ISharedStateDocumentStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public WebsiteCheckState? Get(Guid targetId)
    {
        if (targetId == Guid.Empty) return null;
        var document = SharedStateDocumentMutation.Read(_store, WebsiteSharedStateKeys.CheckState(targetId));
        return document is null ? null : Deserialize(document.PayloadJson, targetId);
    }

    public void Upsert(WebsiteCheckState state)
    {
        InMemoryWebsiteCheckStateStore.Validate(state);
        SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.CheckState(state.TargetId),
            payload => payload is null ? null : Deserialize(payload, state.TargetId),
            _ => SharedStateDocumentMutation.MutationResult<WebsiteCheckState?, bool>.Applied(state, true),
            value => JsonSerializer.Serialize(new StateEnvelope(FormatVersion, value!), SharedStateDocumentMutation.JsonOptions));
    }

    private static WebsiteCheckState Deserialize(string payload, Guid expectedTargetId)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<StateEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared website check state is invalid.");
            if (envelope.Version != FormatVersion || envelope.State.TargetId != expectedTargetId)
                throw new InvalidDataException("Shared website check state identity or format is invalid.");
            InMemoryWebsiteCheckStateStore.Validate(envelope.State);
            return envelope.State;
        }
        catch (InvalidDataException) { throw; }
        catch (JsonException exception) { throw new InvalidDataException("Shared website check state is corrupt.", exception); }
    }

    private sealed record StateEnvelope(int Version, WebsiteCheckState State);
}
