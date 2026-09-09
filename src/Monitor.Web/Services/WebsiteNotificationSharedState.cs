using System.Text.Json;

namespace Monitor.Web.Services;

public sealed class SharedWebsiteNotificationGroupStore : IWebsiteNotificationGroupStore
{
    private const int FormatVersion = 1;
    private readonly ISharedStateDocumentStore _store;

    public SharedWebsiteNotificationGroupStore(ISharedStateDocumentStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public IReadOnlyList<WebsiteNotificationGroup> GetAll() =>
        ReadIndex().Select(ReadIndexedGroup)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

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
                if (existing is null)
                    return SharedStateDocumentMutation.MutationResult<List<string>, bool>.Unchanged(ids, false);
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

    private List<string> ReadIndex() =>
        DeserializeIndex(SharedStateDocumentMutation.Read(_store, WebsiteSharedStateKeys.GroupIndex)?.PayloadJson);

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
            if (envelope.Version != FormatVersion || envelope.GroupIds is null ||
                envelope.GroupIds.Length > WebsiteNotificationValidation.MaxGroups ||
                envelope.GroupIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 80) ||
                envelope.GroupIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != envelope.GroupIds.Length)
                throw new InvalidDataException("Shared website notification-group index is outside its bounded contract.");
            return envelope.GroupIds.ToList();
        }
        catch (InvalidDataException) { throw; }
        catch (JsonException exception) { throw new InvalidDataException("Shared website notification-group index is corrupt.", exception); }
    }

    private static string SerializeIndex(List<string> ids) =>
        JsonSerializer.Serialize(
            new GroupIndexEnvelope(FormatVersion, ids.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray()),
            SharedStateDocumentMutation.JsonOptions);

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
        WriteInitialItem(item);
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
                    var removable = entries
                        .OrderBy(existing => existing.CreatedAtUtc)
                        .FirstOrDefault(existing => ReadItem(existing.Id).Status != WebsiteNotificationDeliveryStatus.Pending);
                    if (removable is null)
                        throw new InvalidOperationException("Website notification outbox capacity has been reached.");
                    entries.Remove(removable);
                }

                entries.Add(new OutboxIndexEntry(item.Id, item.DedupKey, item.CreatedAtUtc));
                return SharedStateDocumentMutation.MutationResult<List<OutboxIndexEntry>, bool>.Applied(entries, true);
            },
            SerializeIndex);
    }

    public WebsiteNotificationClaim? TryClaimDue(DateTimeOffset nowUtc, TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        foreach (var entry in ReadIndex().OrderBy(item => item.CreatedAtUtc))
        {
            var claim = TryClaimItem(entry.Id, nowUtc, leaseDuration);
            if (claim is not null) return claim;
        }
        return null;
    }

    public bool MarkSent(WebsiteNotificationClaim claim, DateTimeOffset sentAtUtc) =>
        MutateClaim(claim, current => current with
        {
            Status = WebsiteNotificationDeliveryStatus.Sent,
            LeaseToken = null,
            LeaseUntilUtc = null,
            LastError = null,
            NextAttemptUtc = sentAtUtc
        });

    public bool MarkFailed(WebsiteNotificationClaim claim, DateTimeOffset nowUtc, int maxAttempts, string error)
    {
        if (maxAttempts is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        var boundedError = Bound(error, 300);
        return MutateClaim(claim, current =>
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
    }

    public IReadOnlyList<WebsiteNotificationOutboxItem> Snapshot() =>
        ReadIndex().OrderByDescending(item => item.CreatedAtUtc).Select(entry => ReadItem(entry.Id)).ToArray();

    private WebsiteNotificationClaim? TryClaimItem(string id, DateTimeOffset nowUtc, TimeSpan leaseDuration)
    {
        var item = SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.OutboxItem(id),
            payload => DeserializeItemRequired(payload, id),
            current =>
            {
                if (current.Status != WebsiteNotificationDeliveryStatus.Pending || current.NextAttemptUtc > nowUtc ||
                    (current.LeaseUntilUtc is DateTimeOffset leaseUntil && leaseUntil > nowUtc))
                    return SharedStateDocumentMutation.MutationResult<WebsiteNotificationOutboxItem, WebsiteNotificationOutboxItem?>.Unchanged(current, null);

                var claimed = current with
                {
                    LeaseToken = Guid.NewGuid().ToString("N"),
                    LeaseUntilUtc = nowUtc + leaseDuration
                };
                return SharedStateDocumentMutation.MutationResult<WebsiteNotificationOutboxItem, WebsiteNotificationOutboxItem?>.Applied(claimed, claimed);
            },
            SerializeItem);

        return item is null ? null : new WebsiteNotificationClaim(item.Id, item.LeaseToken!, item);
    }

    private bool MutateClaim(WebsiteNotificationClaim claim, Func<WebsiteNotificationOutboxItem, WebsiteNotificationOutboxItem> mutation)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.OutboxItem(claim.ItemId),
            payload => DeserializeItemRequired(payload, claim.ItemId),
            current =>
            {
                if (!string.Equals(current.LeaseToken, claim.Token, StringComparison.Ordinal))
                    return SharedStateDocumentMutation.MutationResult<WebsiteNotificationOutboxItem, bool>.Unchanged(current, false);
                var next = mutation(current);
                ValidateItem(next);
                return SharedStateDocumentMutation.MutationResult<WebsiteNotificationOutboxItem, bool>.Applied(next, true);
            },
            SerializeItem);
    }

    private void WriteInitialItem(WebsiteNotificationOutboxItem item) =>
        SharedStateDocumentMutation.Mutate(
            _store,
            WebsiteSharedStateKeys.OutboxItem(item.Id),
            payload => payload is null ? null : DeserializeItem(payload, item.Id),
            current =>
            {
                if (current is not null && !EquivalentIdentityAndPayload(current, item))
                    throw new InvalidDataException("Shared website notification outbox item identity is already bound to different payload.");
                return current is null
                    ? SharedStateDocumentMutation.MutationResult<WebsiteNotificationOutboxItem?, bool>.Applied(item, true)
                    : SharedStateDocumentMutation.MutationResult<WebsiteNotificationOutboxItem?, bool>.Unchanged(current, true);
            },
            value => SerializeItem(value!));

    private WebsiteNotificationOutboxItem ReadItem(string id)
    {
        var document = SharedStateDocumentMutation.Read(_store, WebsiteSharedStateKeys.OutboxItem(id))
            ?? throw new InvalidDataException("Shared website notification outbox index references missing payload.");
        return DeserializeItem(document.PayloadJson, id);
    }

    private List<OutboxIndexEntry> ReadIndex() =>
        DeserializeIndex(SharedStateDocumentMutation.Read(_store, WebsiteSharedStateKeys.OutboxIndex)?.PayloadJson);

    private static WebsiteNotificationOutboxItem DeserializeItemRequired(string? payload, string expectedId) =>
        payload is null
            ? throw new InvalidDataException("Shared website notification payload is missing.")
            : DeserializeItem(payload, expectedId);

    private static WebsiteNotificationOutboxItem DeserializeItem(string payload, string expectedId)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<OutboxItemEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared website notification payload is invalid.");
            if (envelope.Version != FormatVersion || !string.Equals(envelope.Item.Id, expectedId, StringComparison.Ordinal))
                throw new InvalidDataException("Shared website notification payload identity or format is invalid.");
            ValidateItem(envelope.Item);
            return envelope.Item;
        }
        catch (InvalidDataException) { throw; }
        catch (JsonException exception) { throw new InvalidDataException("Shared website notification payload is corrupt.", exception); }
    }

    private static string SerializeItem(WebsiteNotificationOutboxItem item)
    {
        ValidateItem(item);
        return JsonSerializer.Serialize(new OutboxItemEnvelope(FormatVersion, item), SharedStateDocumentMutation.JsonOptions);
    }

    private static List<OutboxIndexEntry> DeserializeIndex(string? payload)
    {
        if (payload is null) return [];
        try
        {
            var envelope = JsonSerializer.Deserialize<OutboxIndexEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared website notification outbox index is invalid.");
            if (envelope.Version != FormatVersion || envelope.Items is null ||
                envelope.Items.Length > FileWebsiteNotificationOutbox.MaxEntries)
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
        var payload = JsonSerializer.Serialize(
            new OutboxIndexEnvelope(FormatVersion, entries.OrderBy(item => item.CreatedAtUtc).ToArray()),
            SharedStateDocumentMutation.JsonOptions);
        if (System.Text.Encoding.UTF8.GetByteCount(payload) > SqlServerSharedStateDocumentStore.MaximumPayloadBytes)
            throw new InvalidOperationException("Website notification outbox index exceeds the shared-state payload bound.");
        return payload;
    }

    private static void ValidateItem(WebsiteNotificationOutboxItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 80 ||
            string.IsNullOrWhiteSpace(item.DedupKey) || item.DedupKey.Length > 80 ||
            item.TargetId == Guid.Empty || string.IsNullOrWhiteSpace(item.IncidentId) || item.IncidentId.Length > 180 ||
            !Enum.IsDefined(item.Kind) || item.Recipients is null || item.Recipients.Length is < 1 or > 100 ||
            item.Recipients.Any(address => !WebsiteNotificationValidation.IsEmail(address)) ||
            item.Subject.Length is < 1 or > 200 || item.Body.Length is < 1 or > 4000 ||
            item.CreatedAtUtc == default || item.NextAttemptUtc == default || item.Attempts is < 0 or > 10 ||
            !Enum.IsDefined(item.Status) || item.LeaseToken is { Length: > 64 } ||
            (item.LeaseToken is null) != (item.LeaseUntilUtc is null) || item.LastError is { Length: > 300 })
            throw new InvalidDataException("Shared website notification outbox item is outside its bounded contract.");
    }

    private static void ValidateIndexEntry(OutboxIndexEntry item)
    {
        if (string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 80 ||
            string.IsNullOrWhiteSpace(item.DedupKey) || item.DedupKey.Length > 80 || item.CreatedAtUtc == default)
            throw new InvalidDataException("Shared website notification outbox index contains invalid bounded metadata.");
    }

    private static bool EquivalentIdentityAndPayload(WebsiteNotificationOutboxItem left, WebsiteNotificationOutboxItem right) =>
        left.Id == right.Id && left.DedupKey == right.DedupKey && left.TargetId == right.TargetId &&
        left.IncidentId == right.IncidentId && left.Kind == right.Kind &&
        left.Recipients.SequenceEqual(right.Recipients, StringComparer.OrdinalIgnoreCase) &&
        left.Subject == right.Subject && left.Body == right.Body && left.CreatedAtUtc == right.CreatedAtUtc;

    private static string Bound(string value, int max) => value.Length <= max ? value : value[..max];
    private sealed record OutboxItemEnvelope(int Version, WebsiteNotificationOutboxItem Item);
    private sealed record OutboxIndexEnvelope(int Version, OutboxIndexEntry[]? Items);
    private sealed record OutboxIndexEntry(string Id, string DedupKey, DateTimeOffset CreatedAtUtc);
}
