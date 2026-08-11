using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum NotificationRoute
{
    AuditOnly,
    Operations,
    DbaOnCall,
    CriticalOnCall
}

public enum NotificationDeliveryState
{
    Pending,
    Delivered,
    DeadLetter
}

public sealed record MonitorNotificationEvent(
    string EventKey,
    Guid RegistrationId,
    string IncidentId,
    string RuleId,
    FindingSeverity Severity,
    ServerEnvironmentClass Environment,
    bool Suppressed,
    bool MaintenanceActive,
    DateTimeOffset OccurredAtUtc);

public sealed record NotificationOutboxItem(
    string IdempotencyKey,
    MonitorNotificationEvent Event,
    NotificationRoute Route,
    NotificationDeliveryState State,
    int Attempts,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? LastOutcome = null);

public static class NotificationRoutingPolicy
{
    public static NotificationRoute Route(MonitorNotificationEvent item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Suppressed || item.MaintenanceActive) return NotificationRoute.AuditOnly;
        if (item.Severity == FindingSeverity.Critical) return NotificationRoute.CriticalOnCall;
        return item.Environment == ServerEnvironmentClass.Production ? NotificationRoute.DbaOnCall : NotificationRoute.Operations;
    }

    public static string IdempotencyKey(MonitorNotificationEvent item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var canonical = string.Join('|',
            EnterpriseSecurityPolicy.NormalizeIncidentRouteId(item.IncidentId),
            item.RuleId.Trim().ToLowerInvariant(),
            item.Severity,
            item.Environment,
            item.OccurredAtUtc.ToUniversalTime().ToString("O"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public interface INotificationOutboxStore
{
    bool Enqueue(NotificationOutboxItem item);
    IReadOnlyList<NotificationOutboxItem> Read(NotificationDeliveryState? state = null, int limit = 100);
    bool TryMarkDelivered(string idempotencyKey, string outcome);
    bool TryRecordFailure(string idempotencyKey, string outcome, int maxAttempts = 5);
}

public sealed class InMemoryNotificationOutboxStore(TimeProvider timeProvider) : INotificationOutboxStore
{
    private const int MaxItems = 500;
    private readonly object _gate = new();
    private readonly Dictionary<string, NotificationOutboxItem> _items = new(StringComparer.Ordinal);

    public bool Enqueue(NotificationOutboxItem item)
    {
        Validate(item);
        lock (_gate)
        {
            if (_items.ContainsKey(item.IdempotencyKey)) return false;
            _items[item.IdempotencyKey] = item;
            Trim();
            return true;
        }
    }

    public IReadOnlyList<NotificationOutboxItem> Read(NotificationDeliveryState? state = null, int limit = 100)
    {
        lock (_gate)
        {
            return _items.Values
                .Where(item => state is null || item.State == state)
                .OrderBy(item => item.CreatedAtUtc)
                .ThenBy(item => item.IdempotencyKey, StringComparer.Ordinal)
                .Take(Math.Clamp(limit, 1, 100))
                .ToArray();
        }
    }

    public bool TryMarkDelivered(string idempotencyKey, string outcome) => Mutate(idempotencyKey, item =>
        item.State == NotificationDeliveryState.Delivered ? null : item with
        {
            State = NotificationDeliveryState.Delivered,
            Attempts = item.Attempts + 1,
            UpdatedAtUtc = timeProvider.GetUtcNow(),
            LastOutcome = BoundOutcome(outcome)
        });

    public bool TryRecordFailure(string idempotencyKey, string outcome, int maxAttempts = 5)
    {
        if (maxAttempts is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        return Mutate(idempotencyKey, item =>
        {
            if (item.State != NotificationDeliveryState.Pending) return null;
            var attempts = item.Attempts + 1;
            return item with
            {
                Attempts = attempts,
                State = attempts >= maxAttempts ? NotificationDeliveryState.DeadLetter : NotificationDeliveryState.Pending,
                UpdatedAtUtc = timeProvider.GetUtcNow(),
                LastOutcome = BoundOutcome(outcome)
            };
        });
    }

    private bool Mutate(string key, Func<NotificationOutboxItem, NotificationOutboxItem?> mutate)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        lock (_gate)
        {
            if (!_items.TryGetValue(key, out var current)) return false;
            var next = mutate(current);
            if (next is null) return false;
            _items[key] = next;
            return true;
        }
    }

    private void Trim()
    {
        while (_items.Count > MaxItems)
        {
            var victim = _items.Values.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.IdempotencyKey, StringComparer.Ordinal).First();
            _items.Remove(victim.IdempotencyKey);
        }
    }

    internal static void Validate(NotificationOutboxItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IdempotencyKey.Length != 64 || item.IdempotencyKey.Any(character => !char.IsAsciiHexDigit(character))) throw new ArgumentException("Notification idempotency key is invalid.", nameof(item));
        _ = EnterpriseSecurityPolicy.NormalizeIncidentRouteId(item.Event.IncidentId);
        if (item.Event.RegistrationId == Guid.Empty || string.IsNullOrWhiteSpace(item.Event.RuleId)) throw new ArgumentException("Notification event is incomplete.", nameof(item));
    }

    internal static string BoundOutcome(string value) => SecurityInput.NormalizeAuditField(value ?? string.Empty, 160);
}

public sealed class SharedNotificationOutboxStore(ISharedStateDocumentStore store, TimeProvider timeProvider) : INotificationOutboxStore
{
    private const string StateKey = "monitor:notification-outbox:v1";
    private const int MaxItems = 500;

    public bool Enqueue(NotificationOutboxItem item)
    {
        InMemoryNotificationOutboxStore.Validate(item);
        return Mutate(state =>
        {
            if (state.Any(current => current.IdempotencyKey == item.IdempotencyKey)) return Mutation.Unchanged(state, false);
            var next = state.Append(item).OrderBy(current => current.CreatedAtUtc).ThenBy(current => current.IdempotencyKey, StringComparer.Ordinal).TakeLast(MaxItems).ToList();
            return Mutation.Applied(next, true);
        });
    }

    public IReadOnlyList<NotificationOutboxItem> Read(NotificationDeliveryState? state = null, int limit = 100) =>
        Load().Where(item => state is null || item.State == state).OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.IdempotencyKey, StringComparer.Ordinal).Take(Math.Clamp(limit, 1, 100)).ToArray();

    public bool TryMarkDelivered(string idempotencyKey, string outcome) => Update(idempotencyKey, item =>
        item.State == NotificationDeliveryState.Delivered ? null : item with { State = NotificationDeliveryState.Delivered, Attempts = item.Attempts + 1, UpdatedAtUtc = timeProvider.GetUtcNow(), LastOutcome = InMemoryNotificationOutboxStore.BoundOutcome(outcome) });

    public bool TryRecordFailure(string idempotencyKey, string outcome, int maxAttempts = 5)
    {
        if (maxAttempts is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        return Update(idempotencyKey, item =>
        {
            if (item.State != NotificationDeliveryState.Pending) return null;
            var attempts = item.Attempts + 1;
            return item with { Attempts = attempts, State = attempts >= maxAttempts ? NotificationDeliveryState.DeadLetter : NotificationDeliveryState.Pending, UpdatedAtUtc = timeProvider.GetUtcNow(), LastOutcome = InMemoryNotificationOutboxStore.BoundOutcome(outcome) };
        });
    }

    private bool Update(string key, Func<NotificationOutboxItem, NotificationOutboxItem?> update) => Mutate(state =>
    {
        var index = state.FindIndex(item => item.IdempotencyKey == key);
        if (index < 0) return Mutation.Unchanged(state, false);
        var next = update(state[index]);
        if (next is null) return Mutation.Unchanged(state, false);
        state[index] = next;
        return Mutation.Applied(state, true);
    });

    private List<NotificationOutboxItem> Load()
    {
        var document = store.ReadAsync(StateKey).ConfigureAwait(false).GetAwaiter().GetResult();
        if (document is null) return [];
        try
        {
            return JsonSerializer.Deserialize<List<NotificationOutboxItem>>(document.PayloadJson, SharedStateDocumentMutation.JsonOptions) ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Notification outbox state is corrupt.", exception);
        }
    }

    private bool Mutate(Func<List<NotificationOutboxItem>, Mutation> mutation)
    {
        return SharedStateDocumentMutation.Mutate(
            store,
            StateKey,
            payload => payload is null ? [] : JsonSerializer.Deserialize<List<NotificationOutboxItem>>(payload, SharedStateDocumentMutation.JsonOptions) ?? [],
            state =>
            {
                var result = mutation(state);
                return result.Changed
                    ? SharedStateDocumentMutation.MutationResult<List<NotificationOutboxItem>, bool>.Applied(result.State, result.Result)
                    : SharedStateDocumentMutation.MutationResult<List<NotificationOutboxItem>, bool>.Unchanged(result.State, result.Result);
            },
            state => JsonSerializer.Serialize(state, SharedStateDocumentMutation.JsonOptions));
    }

    private sealed record Mutation(List<NotificationOutboxItem> State, bool Result, bool Changed)
    {
        public static Mutation Applied(List<NotificationOutboxItem> state, bool result) => new(state, result, true);
        public static Mutation Unchanged(List<NotificationOutboxItem> state, bool result) => new(state, result, false);
    }
}

public sealed class NotificationOutboxService(INotificationOutboxStore store, TimeProvider timeProvider)
{
    public bool Capture(MonitorNotificationEvent item)
    {
        var key = NotificationRoutingPolicy.IdempotencyKey(item);
        var now = timeProvider.GetUtcNow();
        return store.Enqueue(new(key, item, NotificationRoutingPolicy.Route(item), NotificationDeliveryState.Pending, 0, now, now));
    }
}
