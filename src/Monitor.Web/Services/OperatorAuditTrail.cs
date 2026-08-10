using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface IOperatorAuditTrail
{
    int Capacity { get; }
    void Record(OperatorAuditEvent auditEvent);
    IReadOnlyList<OperatorAuditEvent> GetRecent(int limit = 100);
}

public sealed class InMemoryOperatorAuditTrail : IOperatorAuditTrail
{
    public const int DefaultCapacity = 1000;

    private readonly object _sync = new();
    private readonly List<OperatorAuditEvent> _events = [];

    public InMemoryOperatorAuditTrail() : this(DefaultCapacity) { }

    internal InMemoryOperatorAuditTrail(int capacity)
    {
        if (capacity is < 1 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        Capacity = capacity;
    }

    public int Capacity { get; }

    public void Record(OperatorAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        Validate(auditEvent);

        lock (_sync)
        {
            _events.Add(auditEvent);
            var overflow = _events.Count - Capacity;
            if (overflow > 0)
            {
                _events.RemoveRange(0, overflow);
            }
        }
    }

    public IReadOnlyList<OperatorAuditEvent> GetRecent(int limit = 100)
    {
        var bounded = Math.Clamp(limit, 1, Math.Min(Capacity, 500));
        lock (_sync)
        {
            return _events
                .OrderByDescending(item => item.OccurredAtUtc)
                .ThenByDescending(item => item.EventId)
                .Take(bounded)
                .ToArray();
        }
    }

    private static void Validate(OperatorAuditEvent auditEvent)
    {
        if (auditEvent.EventId == Guid.Empty ||
            string.IsNullOrWhiteSpace(auditEvent.Actor) ||
            string.IsNullOrWhiteSpace(auditEvent.ResourceType) ||
            string.IsNullOrWhiteSpace(auditEvent.ResourceId) ||
            string.IsNullOrWhiteSpace(auditEvent.PreviousState) ||
            string.IsNullOrWhiteSpace(auditEvent.NewState))
        {
            throw new ArgumentException("Audit event contains an invalid required field.", nameof(auditEvent));
        }

        if (auditEvent.Actor.Length > 128 ||
            auditEvent.ResourceType.Length > 40 ||
            auditEvent.ResourceId.Length > 200 ||
            auditEvent.PreviousState.Length > 40 ||
            auditEvent.NewState.Length > 40)
        {
            throw new ArgumentException("Audit event exceeds a bounded field length.", nameof(auditEvent));
        }
    }
}
