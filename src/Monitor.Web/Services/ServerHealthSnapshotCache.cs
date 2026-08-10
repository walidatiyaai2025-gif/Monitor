using System.Collections.Concurrent;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum SnapshotFreshness
{
    Fresh,
    Stale
}

public sealed record SnapshotCacheResult(
    ServerHealthSnapshot Snapshot,
    SnapshotFreshness Freshness,
    TimeSpan Age);

public interface IServerHealthSnapshotCache
{
    Task<SnapshotCacheResult> GetAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken = default);
}

public sealed class ServerHealthSnapshotCache(
    ISqlServerSnapshotCollector collector,
    TimeProvider timeProvider) : IServerHealthSnapshotCache
{
    internal static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan RetainStaleFor = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<Guid, ServerHealthSnapshot> _snapshots = new();
    private readonly ConcurrentDictionary<Guid, Lazy<Task<ServerHealthSnapshot>>> _inflight = new();

    public async Task<SnapshotCacheResult> GetAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken = default)
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
        if (existing is not null && existingAge <= FreshFor)
        {
            return new SnapshotCacheResult(existing, SnapshotFreshness.Fresh, existingAge);
        }

        var flight = _inflight.GetOrAdd(
            registration.Id,
            _ => new Lazy<Task<ServerHealthSnapshot>>(
                () => CollectAndStoreAsync(registration),
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

    private async Task<ServerHealthSnapshot> CollectAndStoreAsync(ServerRegistration registration)
    {
        try
        {
            var snapshot = await collector.CollectAsync(registration, CancellationToken.None);
            _snapshots.AddOrUpdate(
                registration.Id,
                snapshot,
                (_, current) => snapshot.CollectedAtUtc > current.CollectedAtUtc ? snapshot : current);
            return _snapshots[registration.Id];
        }
        finally
        {
            _inflight.TryRemove(registration.Id, out _);
        }
    }

    private TimeSpan Age(ServerHealthSnapshot snapshot)
    {
        var age = timeProvider.GetUtcNow() - snapshot.CollectedAtUtc;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }
}
