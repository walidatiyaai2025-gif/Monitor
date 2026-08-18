using System.Collections.Concurrent;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface ISnapshotRefreshService
{
    Task<SnapshotRefreshResult> RefreshAsync(Guid registrationId, CancellationToken cancellationToken = default);
}

public sealed class SnapshotRefreshService : ISnapshotRefreshService
{
    internal static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(15);

    private readonly IServerRegistrationRepository _registrations;
    private readonly IServerHealthSnapshotCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ISnapshotObserver? _observer;
    private readonly IDistributedLeaseManager? _leases;
    private readonly DistributedCoordinationOptions? _coordination;
    private readonly ManualRefreshConcurrencyGate? _concurrencyGate;
    private readonly Func<TimeSpan, CancellationToken, Task> _leaseRenewalDelay;
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastAccepted = new();

    public SnapshotRefreshService(
        IServerRegistrationRepository registrations,
        IServerHealthSnapshotCache cache,
        TimeProvider timeProvider,
        ISnapshotObserver? observer = null,
        IDistributedLeaseManager? leases = null,
        DistributedCoordinationOptions? coordination = null,
        ManualRefreshConcurrencyGate? concurrencyGate = null)
        : this(
            registrations,
            cache,
            timeProvider,
            observer,
            leases,
            coordination,
            concurrencyGate,
            leaseRenewalDelay: null)
    {
    }

    internal SnapshotRefreshService(
        IServerRegistrationRepository registrations,
        IServerHealthSnapshotCache cache,
        TimeProvider timeProvider,
        ISnapshotObserver? observer,
        IDistributedLeaseManager? leases,
        DistributedCoordinationOptions? coordination,
        ManualRefreshConcurrencyGate? concurrencyGate,
        Func<TimeSpan, CancellationToken, Task>? leaseRenewalDelay)
    {
        _registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _observer = observer;
        _leases = leases;
        _coordination = coordination;
        _concurrencyGate = concurrencyGate;
        _leaseRenewalDelay = leaseRenewalDelay ?? ((delay, token) => Task.Delay(delay, _timeProvider, token));
    }

    public async Task<SnapshotRefreshResult> RefreshAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var registration = _registrations.GetById(registrationId);
        if (registration is null)
        {
            return new(SnapshotRefreshStatus.RegistrationNotFound, "Server registration was not found.");
        }

        if (!registration.IsEnabled)
        {
            return new(SnapshotRefreshStatus.Disabled, "Server registration is disabled.");
        }

        var coordinationPolicy = _coordination ?? new DistributedCoordinationOptions();
        coordinationPolicy.Validate();

        DistributedLeaseHandle? lease = null;
        CancellationTokenSource? leaseHeartbeatStop = null;
        Task<LeaseHeartbeatResult>? leaseHeartbeat = null;
        IDisposable? concurrencyLease = null;
        if (coordinationPolicy.Enabled)
        {
            if (_leases is null)
            {
                return new(SnapshotRefreshStatus.Throttled, "Snapshot refresh coordination is unavailable.", RetryAfterSeconds: 5);
            }

            try
            {
                lease = await _leases.TryAcquireAsync(
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
            if (_concurrencyGate is not null && !_concurrencyGate.TryAcquire(out concurrencyLease))
            {
                return new(
                    SnapshotRefreshStatus.Throttled,
                    "Manual refresh capacity is busy. Try again shortly.",
                    RetryAfterSeconds: 2);
            }

            var now = _timeProvider.GetUtcNow();
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

            if (lease is not null && _leases is not null)
            {
                leaseHeartbeatStop = new CancellationTokenSource();
                leaseHeartbeat = MaintainLeaseAsync(lease, _leases, leaseHeartbeatStop.Token);
            }

            var refreshTask = _cache.RefreshAsync(registration, CancellationToken.None);
            SnapshotCacheResult result;
            try
            {
                result = await refreshTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await refreshTask;
                }
                catch
                {
                    // The caller cancellation remains authoritative, but the underlying shared
                    // cache flight must settle before this node gives up distributed ownership.
                }

                throw;
            }

            if (leaseHeartbeat is not null && leaseHeartbeatStop is not null)
            {
                var heartbeat = await StopHeartbeatAsync(leaseHeartbeatStop, leaseHeartbeat);
                lease = heartbeat.Lease;
                leaseHeartbeat = null;
                leaseHeartbeatStop = null;
                if (!heartbeat.AuthorityMaintained)
                {
                    return new(
                        SnapshotRefreshStatus.Throttled,
                        "Snapshot refresh coordination was lost before completion.",
                        RetryAfterSeconds: Math.Max(1, coordinationPolicy.RefreshLeaseSeconds / 3));
                }
            }

            _observer?.Observe(result);
            return result.Freshness == SnapshotFreshness.Fresh
                ? new(
                    SnapshotRefreshStatus.Refreshed,
                    "Snapshot refreshed.",
                    Freshness: result.Freshness)
                : new(
                    SnapshotRefreshStatus.RetainedStale,
                    "Refresh failed; retained stale snapshot returned.",
                    Freshness: result.Freshness);
        }
        finally
        {
            concurrencyLease?.Dispose();

            if (leaseHeartbeat is not null && leaseHeartbeatStop is not null)
            {
                var heartbeat = await StopHeartbeatAsync(leaseHeartbeatStop, leaseHeartbeat);
                lease = heartbeat.Lease;
            }

            if (lease is not null && _leases is not null)
            {
                try
                {
                    await _leases.ReleaseAsync(lease, CancellationToken.None);
                }
                catch (SharedStateStoreUnavailableException)
                {
                    // Lease expiration is the safe fallback when the shared provider is unavailable.
                }
            }
        }
    }

    private async Task<LeaseHeartbeatResult> MaintainLeaseAsync(
        DistributedLeaseHandle initialLease,
        IDistributedLeaseManager leases,
        CancellationToken stopToken)
    {
        var current = initialLease;
        while (true)
        {
            var remaining = current.ExpiresAtUtc - _timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                return new(current, AuthorityMaintained: false);
            }

            var delay = RenewalDelay(current.Duration, remaining);
            try
            {
                await _leaseRenewalDelay(delay, stopToken);
            }
            catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
            {
                return new(current, AuthorityMaintained: true);
            }

            if (stopToken.IsCancellationRequested)
            {
                return new(current, AuthorityMaintained: true);
            }

            try
            {
                var renewed = await leases.RenewAsync(current, stopToken);
                if (renewed is null)
                {
                    return new(current, AuthorityMaintained: false);
                }

                current = renewed;
            }
            catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
            {
                return new(current, AuthorityMaintained: true);
            }
            catch (SharedStateStoreUnavailableException)
            {
                return new(current, AuthorityMaintained: false);
            }
        }
    }

    private static TimeSpan RenewalDelay(TimeSpan duration, TimeSpan remaining)
    {
        var cadence = TimeSpan.FromTicks(Math.Max(1, duration.Ticks / 3));
        var expiryMargin = TimeSpan.FromTicks(Math.Max(1, remaining.Ticks / 2));
        return cadence <= expiryMargin ? cadence : expiryMargin;
    }

    private static async Task<LeaseHeartbeatResult> StopHeartbeatAsync(
        CancellationTokenSource stop,
        Task<LeaseHeartbeatResult> heartbeat)
    {
        stop.Cancel();
        try
        {
            return await heartbeat;
        }
        finally
        {
            stop.Dispose();
        }
    }

    private sealed record LeaseHeartbeatResult(
        DistributedLeaseHandle Lease,
        bool AuthorityMaintained);
}
