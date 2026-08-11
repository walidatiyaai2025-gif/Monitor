using System.Collections.Concurrent;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record SnapshotCacheResult(
    ServerHealthSnapshot Snapshot,
    SnapshotFreshness Freshness,
    TimeSpan Age);

public interface IServerHealthSnapshotCache
{
    SnapshotCacheResult? Peek(Guid registrationId) => null;
    void Evict(Guid registrationId) { }
    Task<SnapshotCacheResult> GetAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken = default);

    Task<SnapshotCacheResult> RefreshAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken = default);
}

public sealed class ServerHealthSnapshotCache(
    ISqlServerSnapshotCollector collector,
    TimeProvider timeProvider,
    PerformanceScaleOptions? performance = null,
    ILatestSnapshotStore? latestSnapshotStore = null) : IServerHealthSnapshotCache
{
    internal static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan RetainStaleFor = TimeSpan.FromMinutes(5);

    private readonly ILatestSnapshotStore _latestSnapshotStore = latestSnapshotStore ?? NullLatestSnapshotStore.Instance;
    private readonly ConcurrentDictionary<Guid, ServerHealthSnapshot> _snapshots = new(
        (latestSnapshotStore ?? NullLatestSnapshotStore.Instance)
            .LoadAll()
            .ToDictionary(item => item.RegistrationId));
    private readonly ConcurrentDictionary<Guid, Lazy<Task<ServerHealthSnapshot>>> _inflight = new();
    private readonly ConcurrentDictionary<Guid, long> _generations = new();
    private readonly object _trimGate = new();
    private readonly int _maxEntries = ResolveCapacity(performance);

    public SnapshotCacheResult? Peek(Guid registrationId)
    {
        if (!_snapshots.TryGetValue(registrationId, out var snapshot)) return null;
        var age = Age(snapshot);
        if (age > RetainStaleFor)
        {
            _snapshots.TryRemove(registrationId, out _);
            _latestSnapshotStore.Remove(registrationId);
            return null;
        }
        return new(snapshot, age <= FreshFor ? SnapshotFreshness.Fresh : SnapshotFreshness.Stale, age);
    }

    public void Evict(Guid registrationId)
    {
        _generations.AddOrUpdate(registrationId, 1, (_, current) => checked(current + 1));
        _snapshots.TryRemove(registrationId, out _);
        _latestSnapshotStore.Remove(registrationId);
    }

    public async Task<SnapshotCacheResult> GetAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken = default) =>
        await GetCoreAsync(registration, forceRefresh: false, cancellationToken);

    public async Task<SnapshotCacheResult> RefreshAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken = default) =>
        await GetCoreAsync(registration, forceRefresh: true, cancellationToken);

    private async Task<SnapshotCacheResult> GetCoreAsync(
        ServerRegistration registration,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        cancellationToken.ThrowIfCancellationRequested();

        if (!registration.IsEnabled)
        {
            throw new SnapshotCollectionException(
                SnapshotCollectionFailure.Disabled,
                "Server registration is disabled.");
        }

        _snapshots.TryGetValue(registration.Id, out var existing);
        var existingAge = existing is null ? TimeSpan.MaxValue : Age(existing);
        if (!forceRefresh && existing is not null && existingAge <= FreshFor)
        {
            return new SnapshotCacheResult(existing, SnapshotFreshness.Fresh, existingAge);
        }

        var flight = _inflight.GetOrAdd(
            registration.Id,
            _ => new Lazy<Task<ServerHealthSnapshot>>(
                () => CollectAndStoreAsync(registration, _generations.GetOrAdd(registration.Id, 0)),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            var snapshot = await flight.Value.WaitAsync(cancellationToken);
            return new SnapshotCacheResult(snapshot, SnapshotFreshness.Fresh, Age(snapshot));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SnapshotCollectionException) when (existing is not null && existingAge <= RetainStaleFor)
        {
            return new SnapshotCacheResult(existing, SnapshotFreshness.Stale, existingAge);
        }
    }

    private async Task<ServerHealthSnapshot> CollectAndStoreAsync(ServerRegistration registration, long generation)
    {
        try
        {
            var snapshot = await collector.CollectAsync(registration, CancellationToken.None);
            if (_generations.GetOrAdd(registration.Id, 0) != generation)
            {
                throw new SnapshotCollectionException(
                    SnapshotCollectionFailure.Disabled,
                    "The collected snapshot was discarded because monitoring state changed.");
            }

            var retained = _snapshots.AddOrUpdate(
                registration.Id,
                snapshot,
                (_, current) => snapshot.CollectedAtUtc > current.CollectedAtUtc ? snapshot : current);
            try
            {
                _latestSnapshotStore.Upsert(retained);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or SharedStateStoreUnavailableException or SharedStateConcurrencyException)
            {
                _snapshots.TryRemove(registration.Id, out _);
                throw new SnapshotCollectionException(
                    SnapshotCollectionFailure.Failed,
                    "Snapshot persistence failed.");
            }

            TrimToCapacity();
            return retained;
        }
        finally
        {
            _inflight.TryRemove(registration.Id, out _);
        }
    }

    private void TrimToCapacity()
    {
        if (_snapshots.Count <= _maxEntries) return;
        lock (_trimGate)
        {
            while (_snapshots.Count > _maxEntries)
            {
                var victim = _snapshots
                    .OrderBy(pair => pair.Value.CollectedAtUtc)
                    .ThenBy(pair => pair.Key)
                    .FirstOrDefault();
                if (victim.Equals(default(KeyValuePair<Guid, ServerHealthSnapshot>))) return;
                if (_snapshots.TryRemove(victim.Key, out _))
                    _latestSnapshotStore.Remove(victim.Key);
            }
        }
    }

    private TimeSpan Age(ServerHealthSnapshot snapshot)
    {
        var age = timeProvider.GetUtcNow() - snapshot.CollectedAtUtc;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private static int ResolveCapacity(PerformanceScaleOptions? options)
    {
        var value = options ?? new PerformanceScaleOptions();
        value.Validate();
        return value.SnapshotCacheMaxEntries;
    }
}