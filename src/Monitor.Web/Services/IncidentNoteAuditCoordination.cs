using System.Security.Cryptography;
using System.Text;

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
    private const int AuditScanLimit = 1000;
    private const int AuditPageSize = 100;
    private readonly object _localGate = new();
    private readonly IAuditStore _inner;
    private readonly IDistributedLeaseManager _leases;
    private readonly DistributedCoordinationOptions _coordination;

    public CoordinatedIncidentNoteAuditStore(
        IAuditStore inner,
        IDistributedLeaseManager leases,
        DistributedCoordinationOptions coordination)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _leases = leases ?? throw new ArgumentNullException(nameof(leases));
        _coordination = coordination ?? throw new ArgumentNullException(nameof(coordination));
        _coordination.Validate();
    }

    public void Append(string actor, string action, string target, string outcome) =>
        _inner.Append(actor, action, target, outcome);

    public IReadOnlyList<AuditEvent> Read(int offset, int limit) =>
        _inner.Read(offset, limit);

    public IncidentNoteClaimResult TryClaimIncidentNote(string actor, string receiptTarget)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptTarget);

        if (!_coordination.Enabled)
        {
            lock (_localGate)
            {
                return EvaluateAndArm(actor, receiptTarget);
            }
        }

        var resource = BuildLeaseResource(receiptTarget);
        var duration = TimeSpan.FromSeconds(_coordination.RefreshLeaseSeconds);
        var lease = _leases.TryAcquireAsync(resource, duration, CancellationToken.None)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        if (lease is null)
        {
            return Observe(receiptTarget) switch
            {
                IncidentNoteClaimResult.AlreadyApplied => IncidentNoteClaimResult.AlreadyApplied,
                _ => IncidentNoteClaimResult.Ambiguous
            };
        }

        try
        {
            return EvaluateAndArm(actor, receiptTarget);
        }
        finally
        {
            try
            {
                _leases.ReleaseAsync(lease, CancellationToken.None)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (SharedStateStoreUnavailableException)
            {
                // The durable armed receipt is the safety boundary. A stale lease can expire naturally.
            }
        }
    }

    private IncidentNoteClaimResult EvaluateAndArm(string actor, string receiptTarget)
    {
        var observed = Observe(receiptTarget);
        if (observed != IncidentNoteClaimResult.Claimed)
        {
            return observed;
        }

        _inner.Append(actor, "incident.note.write.commit", receiptTarget, "armed");
        return IncidentNoteClaimResult.Claimed;
    }

    private IncidentNoteClaimResult Observe(string receiptTarget)
    {
        var armed = false;
        for (var offset = 0; offset < AuditScanLimit; offset += AuditPageSize)
        {
            var page = _inner.Read(offset, AuditPageSize);
            foreach (var item in page)
            {
                if (!string.Equals(item.Target, receiptTarget, StringComparison.Ordinal))
                {
                    continue;
                }

                if (item.Action == "incident.note.request" && item.Outcome == "applied")
                {
                    return IncidentNoteClaimResult.AlreadyApplied;
                }

                if (item.Action == "incident.note.write.commit" && item.Outcome == "armed")
                {
                    armed = true;
                }
            }

            if (page.Count < AuditPageSize)
            {
                break;
            }
        }

        return armed ? IncidentNoteClaimResult.Ambiguous : IncidentNoteClaimResult.Claimed;
    }

    private static string BuildLeaseResource(string receiptTarget)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(receiptTarget)))[..32];
        return $"incident-note:{digest}";
    }
}
