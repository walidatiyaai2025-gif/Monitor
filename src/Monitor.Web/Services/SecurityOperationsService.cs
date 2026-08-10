using System.Collections.Concurrent;

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
    internal static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private sealed record State(int Failures, DateTimeOffset WindowStart);
    private readonly ConcurrentDictionary<string, State> _states = new(StringComparer.Ordinal);

    public bool IsAllowed(string key) =>
        !_states.TryGetValue(key, out var state) ||
        timeProvider.GetUtcNow() - state.WindowStart >= Window ||
        state.Failures < FailureLimit;

    public void RecordFailure(string key) => _states.AddOrUpdate(
        key,
        _ => new(1, timeProvider.GetUtcNow()),
        (_, current) => timeProvider.GetUtcNow() - current.WindowStart >= Window
            ? new(1, timeProvider.GetUtcNow())
            : current with { Failures = current.Failures + 1 });

    public void RecordSuccess(string key) => _states.TryRemove(key, out _);
}
