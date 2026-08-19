namespace Monitor.Web.Services;

internal sealed class LazyLegacyGovernancePruneStateStore : IGovernancePruneStateStore
{
    private readonly object _gate = new();
    private readonly IAuditStore _audit;
    private readonly IOperatorMetadataStore _metadata;
    private readonly InMemoryGovernancePruneStateStore _inner = new();
    private bool _loaded;

    public LazyLegacyGovernancePruneStateStore(IAuditStore audit, IOperatorMetadataStore metadata)
    {
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public bool Contains(GovernancePruneKind kind, string target)
    {
        EnsureLoaded();
        return _inner.Contains(kind, target);
    }

    public void MarkPruned(GovernancePruneKind kind, string target)
    {
        EnsureLoaded();
        _inner.MarkPruned(kind, target);
    }

    public void Synchronize(EnterpriseOperatorSnapshot metadata, IEnumerable<GovernancePruneMarker> retainedLegacyMarkers)
    {
        EnsureLoaded();
        _inner.Synchronize(metadata, retainedLegacyMarkers);
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        lock (_gate)
        {
            if (_loaded)
            {
                return;
            }

            GovernancePruneStateMigration.MaterializeRetainedAuditReceipts(_inner, _audit, _metadata);
            _loaded = true;
        }
    }
}
