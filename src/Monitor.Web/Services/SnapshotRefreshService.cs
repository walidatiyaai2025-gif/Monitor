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
    ISnapshotObserver? observer = null) : ISnapshotRefreshService
{
    internal static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(15);
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastAccepted = new();

    public async Task<SnapshotRefreshResult> RefreshAsync(
        Guid registrationId,
        CancellationToken cancellationToken = default)
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
}
