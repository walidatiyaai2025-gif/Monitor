using System.Collections.Concurrent;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface ISnapshotRefreshService
{
    Task<SnapshotRefreshResult> RefreshAsync(Guid registrationId, CancellationToken cancellationToken = default);
}

public sealed class SnapshotRefreshService(
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache,
    TimeProvider timeProvider,
    ISnapshotObserver? observer = null,
    IDistributedLeaseManager? leases = null,
    DistributedCoordinationOptions? coordination = null) : ISnapshotRefreshService
{
    internal static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(15);
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastAccepted = new();

    public async Task<SnapshotRefreshResult> RefreshAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var registration = registrations.GetById(registrationId);
        if (registration is null)
        {
            return new(SnapshotRefreshStatus.RegistrationNotFound, "Server registration was not found.");
        }

        if (!registration.IsEnabled)
        {
            return new(SnapshotRefreshStatus.Disabled, "Server registration is disabled.");
        }

        var coordinationPolicy = coordination ?? new DistributedCoordinationOptions();
        coordinationPolicy.Validate();

        DistributedLeaseHandle? lease = null;
        if (coordinationPolicy.Enabled)
        {
            if (leases is null)
            {
                return new(SnapshotRefreshStatus.Throttled, "Snapshot refresh coordination is unavailable.", RetryAfterSeconds: 5);
            }

            try
            {
                lease = await leases.TryAcquireAsync(
                    $"refresh:{registrationId:N}",
                    TimeSpan.FromSeconds(coordinationPolicy.RefreshLeaseSeconds),
                    cancellationToken);
            }
            catch (SharedStateStoreUnavailableException)
            {
                return new(SnapshotRefreshStatus.Throttled, "Snapshot refresh coordination is unavailable.", RetryAfterSeconds: 5);
            }

            if (lease is null)
            {
                return new(
                    SnapshotRefreshStatus.Throttled,
                    "A snapshot refresh is already in progress.",
                    RetryAfterSeconds: Math.Max(1, coordinationPolicy.RefreshLeaseSeconds / 3));
            }
        }

        try
        {
            var now = timeProvider.GetUtcNow();
            while (true)
            {
                if (_lastAccepted.TryGetValue(registrationId, out var previous))
                {
                    var remaining = MinimumInterval - (now - previous);
                    if (remaining > TimeSpan.Zero)
                    {
                        return new(
                            SnapshotRefreshStatus.Throttled,
                            "Snapshot refresh is throttled.",
                            Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds)));
                    }

                    if (!_lastAccepted.TryUpdate(registrationId, now, previous))
                    {
                        continue;
                    }
                }
                else if (!_lastAccepted.TryAdd(registrationId, now))
                {
                    continue;
                }

                break;
            }

            var result = await cache.RefreshAsync(registration, cancellationToken);
            observer?.Observe(result);
            return new(
                SnapshotRefreshStatus.Refreshed,
                result.Freshness == SnapshotFreshness.Fresh
                    ? "Snapshot refreshed."
                    : "Refresh failed; retained stale snapshot returned.",
                Freshness: result.Freshness);
        }
        finally
        {
            if (lease is not null && leases is not null)
            {
                try
                {
                    await leases.ReleaseAsync(lease, CancellationToken.None);
                }
                catch (SharedStateStoreUnavailableException)
                {
                    // Lease expiration is the safe fallback when the shared provider is unavailable.
                }
            }
        }
    }
}
