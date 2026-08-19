using System.Collections.Concurrent;
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
        if (id == Guid.Empty) return null;
        if (!ReadIndex().Contains(id)) return null;
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

    private static HashSet<Guid> DeserializeIndex(string? payload)
    {
        if (payload is null) return [];
        try
        {
            var envelope = JsonSerializer.Deserialize<TargetIndexEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared website target index is invalid.");
            if (envelope.Version != FormatVersion || envelope.TargetIds is null || envelope.TargetIds.Length > FileWebsiteTargetStore.MaxTargets ||
                envelope.TargetIds.Any(id => id == Guid.Empty) || envelope.TargetIds.Distinct().Count() != envelope.TargetIds.Length)
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
        var key = WebsiteSharedStateKeys.History(result.TargetId);
        SharedStateDocumentMutation.Mutate(
            _store,
            key,
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
        var boundedWindow = window > MaxRetention ? MaxRetention : window;
        var cutoff = _timeProvider.GetUtcNow() - boundedWindow;
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
            if (envelope.Version != FormatVersion || envelope.Points is null || envelope.Points.Length > FileWebsiteProbeHistoryStore.MaxPerTarget)
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
        if (point.TargetId != expectedTargetId || point.TargetId == Guid.Empty || point.CompletedAtUtc == default || !Enum.IsDefined(point.State) ||
            point.RuleId.Length is < 1 or > 80 || point.ProbableLayer.Length is < 1 or > 120 || point.Confidence.Length is < 1 or > 16 ||
            point.HttpStatusCode is < 100 or > 599 || point.ElapsedMilliseconds is < 0 || point.FinalHost.Length is < 1 or > 253 ||
            point.RedirectCount is < 0 or > WebsiteProbeEngine.MaxRedirects + 1 || point.EvidenceSummary.Length > 500)
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
                var next = state with { LeaseToken = token, LeaseUntilUtc = claim.LeaseUntilUtc };
                return SharedStateDocumentMutation.MutationResult<ScheduleState, WebsiteProbeClaim?>.Applied(next, claim);
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
                var next = new ScheduleState(claim.TargetId, completedAtUtc + interval, null, null);
                return SharedStateDocumentMutation.MutationResult<ScheduleState, bool>.Applied(next, true);
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

public sealed class SharedWebsiteNotificationGroupStore : IWebsiteNotificationGroupStore
{
    private const int FormatVersion = 1;
    private readonly ISharedStateDocumentStore _store;

    public SharedWebsiteNotificationGroupStore(ISharedStateDocumentStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public IReadOnlyList<WebsiteNotificationGroup> GetAll() =>
        ReadIndex().Select(ReadIndexedGroup).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    public WebsiteNotificationGroup? Get(string id)
    {
        var normalized = id?.Trim() ?? string.Empty;
        if (normalized.Length == 0) return null;
        var indexed = ReadIndex().FirstOrDefault(value => string.Equals(value, normalized, StringComparison.OrdinalIgnoreCase));
        return indexed is null ? null : ReadIndexedGroup(indexed);
    }

    public void Upsert(WebsiteNotificationGroup group)
    {
        WebsiteNotificationValidation.ValidateGroup(group);
        var normalized = InMemoryWebsiteNotificationGroupStore.Normalize(group);
        WriteGroup(normalized);
        SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.GroupIndex,
            DeserializeIndex,
            ids =>
            {
                var existing = ids.FirstOrDefault(value => string.Equals(value, normalized.Id, StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                {
                    if (string.Equals(existing, normalized.Id, StringComparison.Ordinal))
                        return SharedStateDocumentMutation.MutationResult<List<string>, bool>.Unchanged(ids, true);
                    ids[ids.IndexOf(existing)] = normalized.Id;
                    return SharedStateDocumentMutation.MutationResult<List<string>, bool>.Applied(ids, true);
                }
                if (ids.Count >= WebsiteNotificationValidation.MaxGroups)
                    throw new InvalidOperationException("Website notification group capacity has been reached.");
                ids.Add(normalized.Id);
                return SharedStateDocumentMutation.MutationResult<List<string>, bool>.Applied(ids, true);
            },
            SerializeIndex);
    }

    public bool Remove(string id)
    {
        var normalized = id?.Trim() ?? string.Empty;
        if (normalized.Length == 0) return false;
        return SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.GroupIndex,
            DeserializeIndex,
            ids =>
            {
                var existing = ids.FirstOrDefault(value => string.Equals(value, normalized, StringComparison.OrdinalIgnoreCase));
                if (existing is null) return SharedStateDocumentMutation.MutationResult<List<string>, bool>.Unchanged(ids, false);
                ids.Remove(existing);
                return SharedStateDocumentMutation.MutationResult<List<string>, bool>.Applied(ids, true);
            },
            SerializeIndex);
    }

    private WebsiteNotificationGroup ReadIndexedGroup(string id)
    {
        var document = SharedStateDocumentMutation.Read(_store, WebsiteSharedStateKeys.Group(id))
            ?? throw new InvalidDataException("Shared website notification-group index references missing metadata.");
        return DeserializeGroup(document.PayloadJson, id);
    }

    private void WriteGroup(WebsiteNotificationGroup group) =>
        SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.Group(group.Id),
            payload => payload is null ? null : DeserializeGroup(payload, group.Id),
            _ => SharedStateDocumentMutation.MutationResult<WebsiteNotificationGroup?, bool>.Applied(group, true),
            value => JsonSerializer.Serialize(new GroupEnvelope(FormatVersion, value!), SharedStateDocumentMutation.JsonOptions));

    private static WebsiteNotificationGroup DeserializeGroup(string payload, string expectedId)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<GroupEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared website notification group is invalid.");
            if (envelope.Version != FormatVersion || !string.Equals(envelope.Group.Id, expectedId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Shared website notification-group identity or format is invalid.");
            WebsiteNotificationValidation.ValidateGroup(envelope.Group);
            return InMemoryWebsiteNotificationGroupStore.Normalize(envelope.Group);
        }
        catch (InvalidDataException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Shared website notification group is corrupt.", exception);
        }
    }

    private static List<string> DeserializeIndex(string? payload)
    {
        if (payload is null) return [];
        try
        {
            var envelope = JsonSerializer.Deserialize<GroupIndexEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared website notification-group index is invalid.");
            if (envelope.Version != FormatVersion || envelope.GroupIds is null || envelope.GroupIds.Length > WebsiteNotificationValidation.MaxGroups ||
                envelope.GroupIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 80) ||
                envelope.GroupIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != envelope.GroupIds.Length)
                throw new InvalidDataException("Shared website notification-group index is outside its bounded contract.");
            return envelope.GroupIds.ToList();
        }
        catch (InvalidDataException) { throw; }
        catch (JsonException exception) { throw new InvalidDataException("Shared website notification-group index is corrupt.", exception); }
    }

    private static string SerializeIndex(List<string> ids) =>
        JsonSerializer.Serialize(new GroupIndexEnvelope(FormatVersion, ids.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray()), SharedStateDocumentMutation.JsonOptions);

    private sealed record GroupEnvelope(int Version, WebsiteNotificationGroup Group);
    private sealed record GroupIndexEnvelope(int Version, string[]? GroupIds);
}

public sealed class SharedWebsiteNotificationOutbox : IWebsiteNotificationOutbox
{
    private const int FormatVersion = 1;
    private readonly ISharedStateDocumentStore _store;

    public SharedWebsiteNotificationOutbox(ISharedStateDocumentStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public bool Enqueue(WebsiteNotificationOutboxItem item)
    {
        ValidateItem(item);
        WritePayload(item);
        return SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.OutboxIndex,
            DeserializeIndex,
            entries =>
            {
                if (entries.Any(existing => string.Equals(existing.DedupKey, item.DedupKey, StringComparison.Ordinal)))
                    return SharedStateDocumentMutation.MutationResult<List<OutboxIndexEntry>, bool>.Unchanged(entries, false);
                if (entries.Any(existing => string.Equals(existing.Id, item.Id, StringComparison.Ordinal)))
                    throw new InvalidDataException("Shared website notification outbox contains duplicate item identities.");
                if (entries.Count >= FileWebsiteNotificationOutbox.MaxEntries)
                {
                    var removable = entries.Where(existing => existing.Status != WebsiteNotificationDeliveryStatus.Pending)
                        .OrderBy(existing => existing.CreatedAtUtc).FirstOrDefault();
                    if (removable is null) throw new InvalidOperationException("Website notification outbox capacity has been reached.");
                    entries.Remove(removable);
                }
                entries.Add(ToIndexEntry(item));
                return SharedStateDocumentMutation.MutationResult<List<OutboxIndexEntry>, bool>.Applied(entries, true);
            },
            SerializeIndex);
    }

    public WebsiteNotificationClaim? TryClaimDue(DateTimeOffset nowUtc, TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        var claimed = SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.OutboxIndex,
            DeserializeIndex,
            entries =>
            {
                var index = entries.FindIndex(item => item.Status == WebsiteNotificationDeliveryStatus.Pending &&
                    item.NextAttemptUtc <= nowUtc && (item.LeaseUntilUtc is null || item.LeaseUntilUtc <= nowUtc));
                if (index < 0)
                    return SharedStateDocumentMutation.MutationResult<List<OutboxIndexEntry>, OutboxIndexEntry?>.Unchanged(entries, null);
                var token = Guid.NewGuid().ToString("N");
                var next = entries[index] with { LeaseToken = token, LeaseUntilUtc = nowUtc + leaseDuration };
                entries[index] = next;
                return SharedStateDocumentMutation.MutationResult<List<OutboxIndexEntry>, OutboxIndexEntry?>.Applied(entries, next);
            },
            SerializeIndex);
        if (claimed is null) return null;
        var payload = ReadPayload(claimed.Id);
        return new WebsiteNotificationClaim(claimed.Id, claimed.LeaseToken!, Combine(payload, claimed));
    }

    public bool MarkSent(WebsiteNotificationClaim claim, DateTimeOffset sentAtUtc)
    {
        var changed = MutateClaim(claim, current => current with
        {
            Status = WebsiteNotificationDeliveryStatus.Sent,
            LeaseToken = null,
            LeaseUntilUtc = null,
            NextAttemptUtc = sentAtUtc,
            LastError = null
        });
        if (changed) SetPayloadError(claim.ItemId, null);
        return changed;
    }

    public bool MarkFailed(WebsiteNotificationClaim claim, DateTimeOffset nowUtc, int maxAttempts, string error)
    {
        if (maxAttempts is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        var boundedError = Bound(error, 300);
        var changed = MutateClaim(claim, current =>
        {
            var attempts = Math.Min(10, current.Attempts + 1);
            var dead = attempts >= maxAttempts;
            var retryDelay = TimeSpan.FromSeconds(Math.Min(900, 15 * (1 << Math.Min(5, attempts - 1))));
            return current with
            {
                Attempts = attempts,
                Status = dead ? WebsiteNotificationDeliveryStatus.DeadLetter : WebsiteNotificationDeliveryStatus.Pending,
                NextAttemptUtc = dead ? nowUtc : nowUtc + retryDelay,
                LeaseToken = null,
                LeaseUntilUtc = null,
                LastError = boundedError
            };
        });
        if (changed) SetPayloadError(claim.ItemId, boundedError);
        return changed;
    }

    public IReadOnlyList<WebsiteNotificationOutboxItem> Snapshot() =>
        ReadIndex().OrderByDescending(item => item.CreatedAtUtc)
            .Select(entry => Combine(ReadPayload(entry.Id), entry))
            .ToArray();

    private bool MutateClaim(WebsiteNotificationClaim claim, Func<OutboxIndexEntry, OutboxIndexEntry> mutation)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.OutboxIndex,
            DeserializeIndex,
            entries =>
            {
                var index = entries.FindIndex(item => string.Equals(item.Id, claim.ItemId, StringComparison.Ordinal));
                if (index < 0 || !string.Equals(entries[index].LeaseToken, claim.Token, StringComparison.Ordinal))
                    return SharedStateDocumentMutation.MutationResult<List<OutboxIndexEntry>, bool>.Unchanged(entries, false);
                entries[index] = mutation(entries[index]);
                return SharedStateDocumentMutation.MutationResult<List<OutboxIndexEntry>, bool>.Applied(entries, true);
            },
            SerializeIndex);
    }

    private void WritePayload(WebsiteNotificationOutboxItem item)
    {
        var payload = new OutboxPayload(item.Id, item.TargetId, item.IncidentId, item.Kind, item.Recipients, item.Subject, item.Body, item.LastError);
        SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.OutboxItem(item.Id),
            raw => raw is null ? null : DeserializePayload(raw, item.Id),
            current =>
            {
                if (current is not null && !PayloadEquivalent(current, payload))
                    throw new InvalidDataException("Shared website notification outbox item identity is already bound to different payload.");
                return current is null
                    ? SharedStateDocumentMutation.MutationResult<OutboxPayload?, bool>.Applied(payload, true)
                    : SharedStateDocumentMutation.MutationResult<OutboxPayload?, bool>.Unchanged(current, true);
            },
            value => JsonSerializer.Serialize(new PayloadEnvelope(FormatVersion, value!), SharedStateDocumentMutation.JsonOptions));
    }

    private OutboxPayload ReadPayload(string id)
    {
        var document = SharedStateDocumentMutation.Read(_store, WebsiteSharedStateKeys.OutboxItem(id))
            ?? throw new InvalidDataException("Shared website notification outbox index references missing payload.");
        return DeserializePayload(document.PayloadJson, id);
    }

    private void SetPayloadError(string id, string? error)
    {
        SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.OutboxItem(id),
            raw => raw is null ? null : DeserializePayload(raw, id),
            payload => payload is null
                ? throw new InvalidDataException("Shared website notification payload is missing during delivery-state mutation.")
                : SharedStateDocumentMutation.MutationResult<OutboxPayload?, bool>.Applied(payload with { LastError = error }, true),
            value => JsonSerializer.Serialize(new PayloadEnvelope(FormatVersion, value!), SharedStateDocumentMutation.JsonOptions));
    }

    private static WebsiteNotificationOutboxItem Combine(OutboxPayload payload, OutboxIndexEntry entry) => new(
        entry.Id, entry.DedupKey, payload.TargetId, payload.IncidentId, payload.Kind, payload.Recipients,
        payload.Subject, payload.Body, entry.CreatedAtUtc, entry.NextAttemptUtc, entry.Attempts, entry.Status,
        entry.LeaseToken, entry.LeaseUntilUtc, payload.LastError);

    private static OutboxIndexEntry ToIndexEntry(WebsiteNotificationOutboxItem item) => new(
        item.Id, item.DedupKey, item.CreatedAtUtc, item.NextAttemptUtc, item.Attempts, item.Status, item.LeaseToken, item.LeaseUntilUtc, null);

    private List<OutboxIndexEntry> ReadIndex() =>
        DeserializeIndex(SharedStateDocumentMutation.Read(_store, WebsiteSharedStateKeys.OutboxIndex)?.PayloadJson);

    private static List<OutboxIndexEntry> DeserializeIndex(string? payload)
    {
        if (payload is null) return [];
        try
        {
            var envelope = JsonSerializer.Deserialize<OutboxIndexEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared website notification outbox index is invalid.");
            if (envelope.Version != FormatVersion || envelope.Items is null || envelope.Items.Length > FileWebsiteNotificationOutbox.MaxEntries)
                throw new InvalidDataException("Shared website notification outbox index format or capacity is invalid.");
            foreach (var item in envelope.Items) ValidateIndexEntry(item);
            if (envelope.Items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != envelope.Items.Length ||
                envelope.Items.Select(item => item.DedupKey).Distinct(StringComparer.Ordinal).Count() != envelope.Items.Length)
                throw new InvalidDataException("Shared website notification outbox index contains duplicate identities.");
            return envelope.Items.OrderBy(item => item.CreatedAtUtc).ToList();
        }
        catch (InvalidDataException) { throw; }
        catch (JsonException exception) { throw new InvalidDataException("Shared website notification outbox index is corrupt.", exception); }
    }

    private static string SerializeIndex(List<OutboxIndexEntry> entries)
    {
        var payload = JsonSerializer.Serialize(new OutboxIndexEnvelope(FormatVersion, entries.OrderBy(item => item.CreatedAtUtc).ToArray()), SharedStateDocumentMutation.JsonOptions);
        if (Encoding.UTF8.GetByteCount(payload) > SqlServerSharedStateDocumentStore.MaximumPayloadBytes)
            throw new InvalidOperationException("Website notification outbox index exceeds the shared-state payload bound before its entry-count capacity.");
        return payload;
    }

    private static OutboxPayload DeserializePayload(string payload, string expectedId)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<PayloadEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared website notification payload is invalid.");
            if (envelope.Version != FormatVersion || !string.Equals(envelope.Payload.Id, expectedId, StringComparison.Ordinal))
                throw new InvalidDataException("Shared website notification payload identity or format is invalid.");
            ValidatePayload(envelope.Payload);
            return envelope.Payload;
        }
        catch (InvalidDataException) { throw; }
        catch (JsonException exception) { throw new InvalidDataException("Shared website notification payload is corrupt.", exception); }
    }

    private static void ValidateItem(WebsiteNotificationOutboxItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 80 || string.IsNullOrWhiteSpace(item.DedupKey) || item.DedupKey.Length > 80 ||
            item.TargetId == Guid.Empty || string.IsNullOrWhiteSpace(item.IncidentId) || item.IncidentId.Length > 180 || !Enum.IsDefined(item.Kind) ||
            item.Recipients is null || item.Recipients.Length is < 1 or > 100 || item.Recipients.Any(address => !WebsiteNotificationValidation.IsEmail(address)) ||
            item.Subject.Length is < 1 or > 200 || item.Body.Length is < 1 or > 4000 || item.CreatedAtUtc == default || item.NextAttemptUtc == default ||
            item.Attempts is < 0 or > 10 || !Enum.IsDefined(item.Status) || item.LeaseToken is { Length: > 64 } ||
            (item.LeaseToken is null) != (item.LeaseUntilUtc is null) || item.LastError is { Length: > 300 })
            throw new InvalidDataException("Shared website notification outbox item is outside its bounded contract.");
    }

    private static void ValidateIndexEntry(OutboxIndexEntry item)
    {
        if (string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 80 || string.IsNullOrWhiteSpace(item.DedupKey) || item.DedupKey.Length > 80 ||
            item.CreatedAtUtc == default || item.NextAttemptUtc == default || item.Attempts is < 0 or > 10 || !Enum.IsDefined(item.Status) ||
            item.LeaseToken is { Length: > 64 } || (item.LeaseToken is null) != (item.LeaseUntilUtc is null))
            throw new InvalidDataException("Shared website notification outbox index contains invalid bounded metadata.");
    }

    private static void ValidatePayload(OutboxPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Id) || payload.Id.Length > 80 || payload.TargetId == Guid.Empty ||
            string.IsNullOrWhiteSpace(payload.IncidentId) || payload.IncidentId.Length > 180 || !Enum.IsDefined(payload.Kind) ||
            payload.Recipients is null || payload.Recipients.Length is < 1 or > 100 || payload.Recipients.Any(address => !WebsiteNotificationValidation.IsEmail(address)) ||
            payload.Subject.Length is < 1 or > 200 || payload.Body.Length is < 1 or > 4000 || payload.LastError is { Length: > 300 })
            throw new InvalidDataException("Shared website notification payload contains invalid bounded metadata.");
    }

    private static bool PayloadEquivalent(OutboxPayload left, OutboxPayload right) =>
        left.Id == right.Id && left.TargetId == right.TargetId && left.IncidentId == right.IncidentId && left.Kind == right.Kind &&
        left.Recipients.SequenceEqual(right.Recipients, StringComparer.OrdinalIgnoreCase) && left.Subject == right.Subject && left.Body == right.Body;

    private static string Bound(string value, int max) => value.Length <= max ? value : value[..max];
    private sealed record OutboxIndexEnvelope(int Version, OutboxIndexEntry[]? Items);
    private sealed record PayloadEnvelope(int Version, OutboxPayload Payload);
    private sealed record OutboxIndexEntry(string Id, string DedupKey, DateTimeOffset CreatedAtUtc, DateTimeOffset NextAttemptUtc, int Attempts,
        WebsiteNotificationDeliveryStatus Status, string? LeaseToken, DateTimeOffset? LeaseUntilUtc, string? LastError);
    private sealed record OutboxPayload(string Id, Guid TargetId, string IncidentId, WebsiteNotificationKind Kind, string[] Recipients, string Subject, string Body, string? LastError);
}

public sealed record WebsiteProbeExecutionAttempt(bool Executed, WebsiteProbeResult? Result, WebsiteIncidentObservation Observation)
{
    public static WebsiteProbeExecutionAttempt Busy { get; } = new(false, null, WebsiteIncidentObservation.None);
}

public interface IWebsiteProbeExecutionService
{
    Task<WebsiteProbeExecutionAttempt> TryExecuteAsync(WebsiteTargetDefinition target, CancellationToken cancellationToken = default);
}

public sealed class WebsiteProbeExecutionService(
    DistributedCoordinationOptions coordination,
    IDistributedLeaseManager distributedLeases,
    IWebsiteProbeEngine probe,
    IWebsiteProbeHistoryStore history,
    IWebsiteIncidentCoordinator incidentCoordinator,
    IWebsiteNotificationPlanner notificationPlanner,
    ILogger<WebsiteProbeExecutionService> logger) : IWebsiteProbeExecutionService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> LocalGates = new();
    private static readonly TimeSpan DistributedProbeLease = TimeSpan.FromSeconds(120);

    public async Task<WebsiteProbeExecutionAttempt> TryExecuteAsync(WebsiteTargetDefinition target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        var validation = WebsiteTargetValidator.Validate(target);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors), nameof(target));

        var local = LocalGates.GetOrAdd(target.Id, _ => new SemaphoreSlim(1, 1));
        if (!await local.WaitAsync(0, cancellationToken)) return WebsiteProbeExecutionAttempt.Busy;

        DistributedLeaseHandle? distributed = null;
        try
        {
            if (coordination.Enabled)
            {
                distributed = await distributedLeases.TryAcquireAsync($"website.probe.{target.Id:N}", DistributedProbeLease, cancellationToken);
                if (distributed is null) return WebsiteProbeExecutionAttempt.Busy;
            }

            var result = await probe.ProbeAsync(target, cancellationToken);
            history.Append(result);
            var observation = incidentCoordinator.Observe(target, result);
            _ = notificationPlanner.Queue(target, result, observation);
            return new WebsiteProbeExecutionAttempt(true, result, observation);
        }
        finally
        {
            if (distributed is not null)
            {
                try
                {
                    if (!await distributedLeases.ReleaseAsync(distributed, CancellationToken.None))
                        logger.LogWarning("Website distributed probe lease for {TargetId} could not be released because persisted ownership changed.", target.Id);
                }
                catch (Exception exception) when (exception is SharedStateStoreUnavailableException or InvalidDataException or InvalidOperationException)
                {
                    logger.LogWarning(exception, "Website distributed probe lease release failed for {TargetId}; expiry remains the safety boundary.", target.Id);
                }
            }
            local.Release();
        }
    }
}
