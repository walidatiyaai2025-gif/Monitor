using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum ChangeScopeKind
{
    Server,
    Group,
    Environment
}

public sealed record ChangeWindow(
    Guid Id,
    ChangeScopeKind ScopeKind,
    string ScopeValue,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Reason,
    bool Freeze,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy);

public static class ChangeCalendarValidation
{
    public static ChangeWindow Normalize(ChangeWindow item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Id == Guid.Empty) throw new ArgumentException("Change window ID is required.", nameof(item));
        if (item.StartUtc.Offset != TimeSpan.Zero || item.EndUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Change windows must use UTC offsets.", nameof(item));
        if (item.EndUtc <= item.StartUtc) throw new ArgumentException("Change window end must be after start.", nameof(item));
        if (item.EndUtc - item.StartUtc > TimeSpan.FromDays(31)) throw new ArgumentException("Change window cannot exceed 31 days.", nameof(item));
        var scope = SecurityInput.NormalizeAuditField(item.ScopeValue, 80);
        var reason = SecurityInput.NormalizeAuditField(item.Reason, 200);
        var actor = EnterpriseOperatorValidation.NormalizeActor(item.CreatedBy);
        if (string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Change scope and reason are required.", nameof(item));
        return item with { ScopeValue = scope, Reason = reason, CreatedBy = actor };
    }

    public static bool Overlaps(ChangeWindow left, ChangeWindow right) =>
        left.ScopeKind == right.ScopeKind &&
        string.Equals(left.ScopeValue, right.ScopeValue, StringComparison.OrdinalIgnoreCase) &&
        left.StartUtc < right.EndUtc && right.StartUtc < left.EndUtc;
}

public interface IChangeCalendarStore
{
    IReadOnlyList<ChangeWindow> Snapshot();
    bool TryAdd(ChangeWindow item);
    bool Remove(Guid id);
}

public sealed class InMemoryChangeCalendarStore : IChangeCalendarStore
{
    private const int MaxItems = 500;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, ChangeWindow> _items = [];

    public IReadOnlyList<ChangeWindow> Snapshot()
    {
        lock (_gate) return _items.Values.OrderBy(item => item.StartUtc).ThenBy(item => item.Id).ToArray();
    }

    public bool TryAdd(ChangeWindow item)
    {
        item = ChangeCalendarValidation.Normalize(item);
        lock (_gate)
        {
            if (_items.ContainsKey(item.Id)) return false;
            if (_items.Values.Any(existing => ChangeCalendarValidation.Overlaps(existing, item))) return false;
            _items[item.Id] = item;
            while (_items.Count > MaxItems)
            {
                var victim = _items.Values.OrderBy(current => current.EndUtc).ThenBy(current => current.Id).First();
                _items.Remove(victim.Id);
            }
            return true;
        }
    }

    public bool Remove(Guid id)
    {
        lock (_gate) return _items.Remove(id);
    }
}

public sealed class ChangeCalendarService(IChangeCalendarStore store, IAuditStore audit, TimeProvider timeProvider)
{
    public bool Add(ChangeWindow item)
    {
        item = ChangeCalendarValidation.Normalize(item);
        var applied = store.TryAdd(item);
        audit.Append(item.CreatedBy, "change-window.add", item.Id.ToString("D"), applied ? "applied" : "rejected:overlap-or-duplicate");
        return applied;
    }

    public bool Remove(Guid id, string actor)
    {
        actor = EnterpriseOperatorValidation.NormalizeActor(actor);
        var applied = store.Remove(id);
        audit.Append(actor, "change-window.remove", id.ToString("D"), applied ? "applied" : "not-found");
        return applied;
    }

    public IReadOnlyList<ChangeWindow> Active() => At(timeProvider.GetUtcNow());

    public IReadOnlyList<ChangeWindow> At(DateTimeOffset now) => store.Snapshot()
        .Where(item => item.StartUtc <= now && now < item.EndUtc)
        .OrderByDescending(item => item.Freeze)
        .ThenBy(item => item.EndUtc)
        .ThenBy(item => item.Id)
        .ToArray();

    public IReadOnlyList<ChangeWindow> Upcoming(TimeSpan horizon, int limit = 50)
    {
        if (horizon <= TimeSpan.Zero || horizon > TimeSpan.FromDays(31)) throw new ArgumentOutOfRangeException(nameof(horizon));
        var now = timeProvider.GetUtcNow();
        var end = now + horizon;
        return store.Snapshot().Where(item => item.StartUtc > now && item.StartUtc <= end)
            .OrderBy(item => item.StartUtc).ThenBy(item => item.Id).Take(Math.Clamp(limit, 1, 100)).ToArray();
    }

    public bool IsFrozen(Guid registrationId, ServerOperatorMetadata metadata, DateTimeOffset? atUtc = null)
    {
        if (registrationId == Guid.Empty) throw new ArgumentException("Registration ID is required.", nameof(registrationId));
        ArgumentNullException.ThrowIfNull(metadata);
        var at = atUtc ?? timeProvider.GetUtcNow();
        return At(at).Any(item => item.Freeze && Matches(item, registrationId, metadata));
    }

    public IReadOnlyList<ChangeWindow> ForServer(Guid registrationId, ServerOperatorMetadata metadata, DateTimeOffset? atUtc = null)
    {
        if (registrationId == Guid.Empty) throw new ArgumentException("Registration ID is required.", nameof(registrationId));
        ArgumentNullException.ThrowIfNull(metadata);
        var at = atUtc ?? timeProvider.GetUtcNow();
        return At(at).Where(item => Matches(item, registrationId, metadata)).ToArray();
    }

    private static bool Matches(ChangeWindow item, Guid registrationId, ServerOperatorMetadata metadata) => item.ScopeKind switch
    {
        ChangeScopeKind.Server => string.Equals(item.ScopeValue, registrationId.ToString("D"), StringComparison.OrdinalIgnoreCase),
        ChangeScopeKind.Group => !string.IsNullOrWhiteSpace(metadata.Group) && string.Equals(item.ScopeValue, metadata.Group, StringComparison.OrdinalIgnoreCase),
        ChangeScopeKind.Environment => string.Equals(item.ScopeValue, metadata.Environment.ToString(), StringComparison.OrdinalIgnoreCase),
        _ => false
    };
}
