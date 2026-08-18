namespace Monitor.Web.Services;

public static class MonitorRoles
{
    public const string Viewer = "Viewer";
    public const string Operator = "Operator";
    public const string Administrator = "Administrator";
}

public static class MonitorPolicies
{
    public const string Read = "Monitor.Read";
    public const string Operate = "Incident.Operate";
    public const string Manage = "Connections.Manage";
    public const string Advisor = "Advisor.Request";
}

public sealed record AuditEvent(Guid Id, DateTimeOffset OccurredAtUtc, string Actor, string Action, string Target, string Outcome);
public interface IAuditStore { void Append(string actor, string action, string target, string outcome); IReadOnlyList<AuditEvent> Read(int offset, int limit); }
public sealed class InMemoryAuditStore(TimeProvider timeProvider) : IAuditStore
{
    private const int MaxEvents = 1000;
    private readonly object _gate = new();
    private readonly List<AuditEvent> _events = [];
    public void Append(string actor, string action, string target, string outcome)
    {
        lock (_gate)
        {
            _events.Add(new(
                Guid.NewGuid(),
                timeProvider.GetUtcNow(),
                SecurityInput.NormalizeAuditField(actor, 100),
                SecurityInput.NormalizeAuditField(action, 80),
                SecurityInput.NormalizeAuditField(target, 160),
                SecurityInput.NormalizeAuditField(outcome, 40)));
            if (_events.Count > MaxEvents) _events.RemoveRange(0, _events.Count - MaxEvents);
        }
    }
    public IReadOnlyList<AuditEvent> Read(int offset, int limit)
    {
        lock (_gate) return _events.OrderByDescending(item => item.OccurredAtUtc).Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 100)).ToArray();
    }
}

public interface ILoginAttemptLimiter { bool IsAllowed(string key); void RecordFailure(string key); void RecordSuccess(string key); }
public sealed class LoginAttemptLimiter(TimeProvider timeProvider) : ILoginAttemptLimiter
{
    internal const int FailureLimit = 5;
    internal const int MaxTrackedKeys = 4096;
    internal static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan PruneInterval = TimeSpan.FromSeconds(30);

    private sealed record State(int Failures, DateTimeOffset WindowStart);
    private readonly object _gate = new();
    private readonly Dictionary<string, State> _states = new(StringComparer.Ordinal);
    private DateTimeOffset _nextPruneUtc = DateTimeOffset.MinValue;

    internal int TrackedKeyCount
    {
        get
        {
            lock (_gate) return _states.Count;
        }
    }

    public bool IsAllowed(string key)
    {
        var now = timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (_states.TryGetValue(key, out var state))
            {
                if (now - state.WindowStart >= Window)
                {
                    _states.Remove(key);
                    return true;
                }

                return state.Failures < FailureLimit;
            }

            PruneExpiredIfDue(now);
            return _states.Count < MaxTrackedKeys;
        }
    }

    public void RecordFailure(string key)
    {
        var now = timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (_states.TryGetValue(key, out var current))
            {
                _states[key] = now - current.WindowStart >= Window
                    ? new(1, now)
                    : current with { Failures = current.Failures + 1 };
                return;
            }

            PruneExpiredIfDue(now);
            if (_states.Count >= MaxTrackedKeys)
            {
                return;
            }

            _states[key] = new(1, now);
        }
    }

    public void RecordSuccess(string key)
    {
        lock (_gate) _states.Remove(key);
    }

    private void PruneExpiredIfDue(DateTimeOffset now)
    {
        if (now < _nextPruneUtc)
        {
            return;
        }

        _nextPruneUtc = now + PruneInterval;
        foreach (var key in _states
                     .Where(pair => now - pair.Value.WindowStart >= Window)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _states.Remove(key);
        }
    }
}
