using System.Text.Json;

namespace Monitor.Web.Services;

public enum IncidentNoteClaimResult
{
    Claimed,
    AlreadyApplied,
    Ambiguous
}

public interface IIncidentNoteClaimAuditStore
{
    IncidentNoteClaimResult TryClaimIncidentNote(string actor, string receiptTarget);
}

public sealed class CoordinatedIncidentNoteAuditStore : IAuditStore, IIncidentNoteClaimAuditStore
{
    private const string SharedAuditDocumentKey = "monitor:audit:v1";
    private const int SharedAuditFormatVersion = 1;
    private const int MaxAuditEvents = 1000;
    private const int AuditPageSize = 100;
    private readonly object _localGate = new();
    private readonly IAuditStore _inner;
    private readonly ISharedStateDocumentStore _sharedState;
    private readonly TimeProvider _timeProvider;
    private readonly bool _useSharedOperationalState;
    private readonly IIncidentNoteRequestStateStore? _requestState;

    public CoordinatedIncidentNoteAuditStore(
        IAuditStore inner,
        ISharedStateDocumentStore sharedState,
        TimeProvider timeProvider,
        bool useSharedOperationalState,
        IIncidentNoteRequestStateStore? requestState = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _useSharedOperationalState = useSharedOperationalState;
        _requestState = requestState;
    }

    public void Append(string actor, string action, string target, string outcome)
    {
        if (_requestState is not null &&
            action == "incident.note.request" &&
            outcome == "applied")
        {
            _requestState.MarkApplied(target);
        }

        _inner.Append(actor, action, target, outcome);
    }

    public IReadOnlyList<AuditEvent> Read(int offset, int limit) =>
        _inner.Read(offset, limit);

    public IncidentNoteClaimResult TryClaimIncidentNote(string actor, string receiptTarget)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptTarget);

        if (_requestState is not null)
        {
            var claim = _requestState.TryClaim(receiptTarget);
            if (claim == IncidentNoteClaimResult.Claimed)
            {
                _inner.Append(actor, "incident.note.write.commit", receiptTarget, "armed");
            }
            return claim;
        }

        if (_useSharedOperationalState)
        {
            return TryClaimShared(actor, receiptTarget);
        }

        lock (_localGate)
        {
            var observed = ObserveLocal(receiptTarget);
            if (observed != IncidentNoteClaimResult.Claimed)
            {
                return observed;
            }

            _inner.Append(actor, "incident.note.write.commit", receiptTarget, "armed");
            return IncidentNoteClaimResult.Claimed;
        }
    }

    private IncidentNoteClaimResult TryClaimShared(string actor, string receiptTarget)
    {
        var armedEvent = CreateAuditEvent(actor, "incident.note.write.commit", receiptTarget, "armed");
        return SharedStateDocumentMutation.Mutate(
            _sharedState,
            SharedAuditDocumentKey,
            DeserializeSharedAudit,
            state =>
            {
                var observed = ObserveState(state, receiptTarget);
                if (observed != IncidentNoteClaimResult.Claimed)
                {
                    return SharedStateDocumentMutation.MutationResult<List<AuditEvent>, IncidentNoteClaimResult>.Unchanged(state, observed);
                }

                state.Add(armedEvent);
                if (state.Count > MaxAuditEvents)
                {
                    state.RemoveRange(0, state.Count - MaxAuditEvents);
                }

                return SharedStateDocumentMutation.MutationResult<List<AuditEvent>, IncidentNoteClaimResult>.Applied(
                    state,
                    IncidentNoteClaimResult.Claimed);
            },
            SerializeSharedAudit);
    }

    private IncidentNoteClaimResult ObserveLocal(string receiptTarget)
    {
        var armed = false;
        for (var offset = 0; offset < MaxAuditEvents; offset += AuditPageSize)
        {
            var page = _inner.Read(offset, AuditPageSize);
            foreach (var item in page)
            {
                var observed = ObserveEvent(item, receiptTarget);
                if (observed == IncidentNoteClaimResult.AlreadyApplied)
                {
                    return observed;
                }

                armed |= observed == IncidentNoteClaimResult.Ambiguous;
            }

            if (page.Count < AuditPageSize)
            {
                break;
            }
        }

        return armed ? IncidentNoteClaimResult.Ambiguous : IncidentNoteClaimResult.Claimed;
    }

    private static IncidentNoteClaimResult ObserveState(IEnumerable<AuditEvent> state, string receiptTarget)
    {
        var armed = false;
        foreach (var item in state)
        {
            var observed = ObserveEvent(item, receiptTarget);
            if (observed == IncidentNoteClaimResult.AlreadyApplied)
            {
                return observed;
            }

            armed |= observed == IncidentNoteClaimResult.Ambiguous;
        }

        return armed ? IncidentNoteClaimResult.Ambiguous : IncidentNoteClaimResult.Claimed;
    }

    private static IncidentNoteClaimResult ObserveEvent(AuditEvent item, string receiptTarget)
    {
        if (!string.Equals(item.Target, receiptTarget, StringComparison.Ordinal))
        {
            return IncidentNoteClaimResult.Claimed;
        }

        if (item.Action == "incident.note.request" && item.Outcome == "applied")
        {
            return IncidentNoteClaimResult.AlreadyApplied;
        }

        return item.Action == "incident.note.write.commit" && item.Outcome == "armed"
            ? IncidentNoteClaimResult.Ambiguous
            : IncidentNoteClaimResult.Claimed;
    }

    private AuditEvent CreateAuditEvent(string actor, string action, string target, string outcome) =>
        new(
            Guid.NewGuid(),
            _timeProvider.GetUtcNow(),
            SecurityInput.NormalizeAuditField(actor, 100),
            SecurityInput.NormalizeAuditField(action, 80),
            SecurityInput.NormalizeAuditField(target, 160),
            SecurityInput.NormalizeAuditField(outcome, 40));

    private static List<AuditEvent> DeserializeSharedAudit(string? payload)
    {
        if (payload is null)
        {
            return [];
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<AuditEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared audit state is invalid.");
            if (envelope.Version != SharedAuditFormatVersion || envelope.Events is null || envelope.Events.Length > MaxAuditEvents)
            {
                throw new InvalidDataException("Shared audit state format or event count is invalid.");
            }

            var ids = new HashSet<Guid>();
            foreach (var item in envelope.Events)
            {
                if (item.Id == Guid.Empty || !ids.Add(item.Id) || item.OccurredAtUtc == default ||
                    item.Actor is null || item.Action is null || item.Target is null || item.Outcome is null ||
                    item.Actor.Length > 100 || item.Action.Length > 80 || item.Target.Length > 160 || item.Outcome.Length > 40)
                {
                    throw new InvalidDataException("Shared audit state contains invalid bounded metadata.");
                }
            }

            return envelope.Events.OrderBy(item => item.OccurredAtUtc).ToList();
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Shared audit state is corrupt.", exception);
        }
    }

    private static string SerializeSharedAudit(List<AuditEvent> state) =>
        JsonSerializer.Serialize(
            new AuditEnvelope(SharedAuditFormatVersion, state.OrderBy(item => item.OccurredAtUtc).ToArray()),
            SharedStateDocumentMutation.JsonOptions);

    private sealed record AuditEnvelope(int Version, AuditEvent[]? Events);
}
