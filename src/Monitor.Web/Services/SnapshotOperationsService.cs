using System.Collections.Concurrent;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface ISnapshotHistoryStore
{
    void Append(SnapshotCacheResult result);
    IReadOnlyList<SnapshotHistoryPoint> Read(Guid registrationId, TimeSpan window);

    IReadOnlyList<SnapshotHistoryPoint> Read(Guid registrationId, TimeSpan window, int offset, int limit) =>
        Read(registrationId, window)
            .Skip(PerformanceScaleOptions.BoundOffset(offset))
            .Take(Math.Clamp(limit, 1, 288))
            .ToArray();
}

public sealed class InMemorySnapshotHistoryStore(TimeProvider timeProvider) : ISnapshotHistoryStore
{
    private const int MaxPerServer = 288;
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private readonly ConcurrentDictionary<Guid, SortedDictionary<DateTimeOffset, SnapshotHistoryPoint>> _points = new();

    public void Append(SnapshotCacheResult result)
    {
        var snapshot = result.Snapshot;
        var point = new SnapshotHistoryPoint(snapshot.RegistrationId, snapshot.CollectedAtUtc, snapshot.DatabaseOnline, snapshot.DatabaseTotal,
            snapshot.Memory?.SqlProcessMemoryUtilizationPercent, snapshot.Blocking?.BlockedRequests, snapshot.Performance?.RunnableTasks, result.Freshness);
        var series = _points.GetOrAdd(snapshot.RegistrationId, _ => new());
        lock (series)
        {
            series.TryAdd(point.CollectedAtUtc, point);
            var cutoff = timeProvider.GetUtcNow() - Retention;
            foreach (var key in series.Keys.Where(key => key < cutoff).ToArray()) series.Remove(key);
            while (series.Count > MaxPerServer) series.Remove(series.Keys.First());
        }
    }

    public IReadOnlyList<SnapshotHistoryPoint> Read(Guid registrationId, TimeSpan window) =>
        Read(registrationId, window, 0, MaxPerServer);

    public IReadOnlyList<SnapshotHistoryPoint> Read(Guid registrationId, TimeSpan window, int offset, int limit)
    {
        ValidateWindow(window);
        if (!_points.TryGetValue(registrationId, out var series)) return [];
        lock (series)
        {
            var cutoff = timeProvider.GetUtcNow() - window;
            return series.Values
                .Where(point => point.CollectedAtUtc >= cutoff)
                .Skip(PerformanceScaleOptions.BoundOffset(offset))
                .Take(Math.Clamp(limit, 1, MaxPerServer))
                .ToArray();
        }
    }

    private static void ValidateWindow(TimeSpan window)
    {
        if (window <= TimeSpan.Zero || window > Retention) throw new ArgumentOutOfRangeException(nameof(window));
    }
}

public interface ISnapshotObserver { void Observe(SnapshotCacheResult result); }
public sealed class SnapshotObserver(ISnapshotHistoryStore history, IHealthRuleEvaluator evaluator, IHealthIncidentRepository incidents) : ISnapshotObserver
{
    public void Observe(SnapshotCacheResult result)
    {
        history.Append(result);
        var snapshot = result.Snapshot;
        var findings = evaluator.Evaluate(snapshot.RegistrationId, snapshot, result.Freshness);
        incidents.Reconcile(snapshot.RegistrationId, snapshot.CollectedAtUtc, findings, result.Freshness == SnapshotFreshness.Fresh);
    }
}

public sealed class SnapshotScheduleOptions
{
    public const string SectionName = "Monitor:Schedule";
    public bool Enabled { get; init; }
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(1);
    public int MaxConcurrency { get; init; } = 2;
    public int MaxTargetsPerCycle { get; init; } = 100;
    public int MaxJitterSeconds { get; init; } = 5;

    public void Validate()
    {
        if (Interval < TimeSpan.FromSeconds(30) || Interval > TimeSpan.FromHours(1)) throw new InvalidOperationException("Schedule interval is outside the allowed range.");
        if (MaxConcurrency is < 1 or > 8) throw new InvalidOperationException("Schedule concurrency is outside the allowed range.");
        if (MaxTargetsPerCycle is < 1 or > 500) throw new InvalidOperationException("Schedule targets-per-cycle is outside the allowed range.");
        if (MaxJitterSeconds is < 0 or > 30 || TimeSpan.FromSeconds(MaxJitterSeconds) >= Interval) throw new InvalidOperationException("Schedule jitter is outside the allowed range.");
    }
}

public sealed record SchedulerStatus(bool Enabled, bool Running, DateTimeOffset? LastStartedUtc, DateTimeOffset? LastCompletedUtc, int Attempted, int Succeeded, int Failed, int SkippedBackoff);
public interface ISchedulerStatusStore { SchedulerStatus Get(); void Set(SchedulerStatus value); }
public sealed class SchedulerStatusStore : ISchedulerStatusStore
{
    private SchedulerStatus _value = new(false, false, null, null, 0, 0, 0, 0);
    public SchedulerStatus Get() => Volatile.Read(ref _value);
    public void Set(SchedulerStatus value) => Volatile.Write(ref _value, value);
}

public interface ICollectionBackoffPolicy { bool IsEligible(Guid id); void Success(Guid id); void Failure(Guid id); }
public sealed class CollectionBackoffPolicy(TimeProvider timeProvider, IOperatorMetadataStore? operatorMetadata = null) : ICollectionBackoffPolicy
{
    private sealed record State(int Failures, DateTimeOffset NextEligibleUtc);
    private readonly ConcurrentDictionary<Guid, State> _states = new();

    public bool IsEligible(Guid id)
    {
        if (operatorMetadata is not null)
        {
            try
            {
                var metadata = operatorMetadata.GetServer(id);
                if (EnterpriseOperatorPolicy.IsMaintenanceActive(metadata, timeProvider.GetUtcNow())) return false;
            }
            catch (InvalidDataException)
            {
                return false;
            }
            catch (SharedStateStoreUnavailableException)
            {
                return false;
            }
        }

        return !_states.TryGetValue(id, out var state) || timeProvider.GetUtcNow() >= state.NextEligibleUtc;
    }

    public void Success(Guid id) => _states.TryRemove(id, out _);
    public void Failure(Guid id) => _states.AddOrUpdate(id,
        _ => new(1, timeProvider.GetUtcNow().AddSeconds(30)),
        (_, current) => new(current.Failures + 1, timeProvider.GetUtcNow().AddSeconds(Math.Min(300, 30 * Math.Pow(2, current.Failures)))));
}

public interface ISnapshotCollectionCycle { Task RunOnceAsync(CancellationToken cancellationToken); }
public sealed class SnapshotCollectionCycle(
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache,
    ISnapshotObserver observer,
    SnapshotScheduleOptions? options = null,
    ICollectionBackoffPolicy? backoff = null,
    ISchedulerStatusStore? status = null,
    TimeProvider? timeProvider = null) : ISnapshotCollectionCycle
{
    private readonly object _batchGate = new();
    private int _nextOffset;

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var policy = options ?? new SnapshotScheduleOptions();
        policy.Validate();
        var clock = timeProvider ?? TimeProvider.System;
        var allTargets = registrations.GetAll().Where(item => item.IsEnabled).OrderBy(item => item.Id).ToArray();
        var targets = SelectBatch(allTargets, policy.MaxTargetsPerCycle);
        var succeeded = 0;
        var failed = 0;
        var skipped = 0;
        var startedAt = clock.GetUtcNow();
        status?.Set(new(policy.Enabled, true, startedAt, null, targets.Length, 0, 0, 0));
        try
        {
            await Parallel.ForEachAsync(targets, new ParallelOptions { MaxDegreeOfParallelism = policy.MaxConcurrency, CancellationToken = cancellationToken }, async (registration, token) =>
            {
                if (backoff is not null && !backoff.IsEligible(registration.Id)) { Interlocked.Increment(ref skipped); return; }
                try
                {
                    observer.Observe(await cache.RefreshAsync(registration, token));
                    backoff?.Success(registration.Id);
                    Interlocked.Increment(ref succeeded);
                }
                catch (SnapshotCollectionException)
                {
                    backoff?.Failure(registration.Id);
                    Interlocked.Increment(ref failed);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    backoff?.Failure(registration.Id);
                    Interlocked.Increment(ref failed);
                    throw;
                }
            });
        }
        finally
        {
            status?.Set(new(policy.Enabled, false, startedAt, clock.GetUtcNow(), targets.Length, succeeded, failed, skipped));
        }
    }

    private ServerRegistration[] SelectBatch(ServerRegistration[] targets, int maxTargets)
    {
        if (targets.Length <= maxTargets)
        {
            lock (_batchGate) _nextOffset = 0;
            return targets;
        }

        lock (_batchGate)
        {
            var start = _nextOffset % targets.Length;
            var count = Math.Min(maxTargets, targets.Length);
            var selected = new ServerRegistration[count];
            for (var index = 0; index < count; index++)
            {
                selected[index] = targets[(start + index) % targets.Length];
            }
            _nextOffset = (start + count) % targets.Length;
            return selected;
        }
    }
}

public sealed class SnapshotSchedulerService(
    SnapshotScheduleOptions options,
    ISnapshotCollectionCycle cycle,
    ISchedulerStatusStore status,
    IDistributedLeaseManager? leases = null,
    DistributedCoordinationOptions? coordination = null) : BackgroundService
{
    private long _cycleSequence;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        options.Validate();
        var coordinationPolicy = coordination ?? new DistributedCoordinationOptions();
        coordinationPolicy.Validate();

        if (!options.Enabled)
        {
            status.Set(new(false, false, null, null, 0, 0, 0, 0));
            return;
        }

        if (!coordinationPolicy.Enabled)
        {
            status.Set(new(true, false, null, null, 0, 0, 0, 0));
        }

        var jitterSeed = ResolveJitterSeed(coordinationPolicy);
        using var timer = new PeriodicTimer(options.Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var jitter = SchedulerJitter.Compute(jitterSeed, Interlocked.Increment(ref _cycleSequence), options.MaxJitterSeconds);
            if (jitter > TimeSpan.Zero) await Task.Delay(jitter, stoppingToken);

            if (!coordinationPolicy.Enabled)
            {
                await cycle.RunOnceAsync(stoppingToken);
                continue;
            }

            if (leases is null)
            {
                continue;
            }

            DistributedLeaseHandle? lease;
            try
            {
                lease = await leases.TryAcquireAsync("scheduler", TimeSpan.FromSeconds(coordinationPolicy.SchedulerLeaseSeconds), stoppingToken);
            }
            catch (SharedStateStoreUnavailableException)
            {
                continue;
            }

            if (lease is null)
            {
                continue;
            }

            await RunLeaderCycleAsync(lease, stoppingToken);
        }
    }

    private async Task RunLeaderCycleAsync(DistributedLeaseHandle initialLease, CancellationToken stoppingToken)
    {
        if (leases is null)
        {
            return;
        }

        using var leaseCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var holder = new LeaseHolder(initialLease);
        var renewal = RenewWhileRunningAsync(holder, leaseCancellation, stoppingToken);

        try
        {
            await cycle.RunOnceAsync(leaseCancellation.Token);
        }
        finally
        {
            leaseCancellation.Cancel();
            try
            {
                await renewal;
            }
            catch (OperationCanceledException)
            {
            }

            try
            {
                await leases.ReleaseAsync(holder.Current, CancellationToken.None);
            }
            catch (SharedStateStoreUnavailableException)
            {
            }
        }
    }

    private async Task RenewWhileRunningAsync(LeaseHolder holder, CancellationTokenSource leaseCancellation, CancellationToken stoppingToken)
    {
        if (leases is null)
        {
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(5, holder.Current.Duration.TotalSeconds / 3));
        while (!leaseCancellation.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, leaseCancellation.Token);
                var renewed = await leases.RenewAsync(holder.Current, leaseCancellation.Token);
                if (renewed is null)
                {
                    leaseCancellation.Cancel();
                    return;
                }

                holder.Current = renewed;
            }
            catch (SharedStateStoreUnavailableException)
            {
                leaseCancellation.Cancel();
                return;
            }
            catch (OperationCanceledException) when (leaseCancellation.IsCancellationRequested || stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private static string ResolveJitterSeed(DistributedCoordinationOptions coordinationPolicy)
    {
        if (!string.IsNullOrWhiteSpace(coordinationPolicy.NodeIdEnvironmentVariable))
        {
            var configured = Environment.GetEnvironmentVariable(coordinationPolicy.NodeIdEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured)) return configured;
        }
        return Environment.MachineName;
    }

    private sealed class LeaseHolder(DistributedLeaseHandle current)
    {
        private readonly object _gate = new();
        private DistributedLeaseHandle _current = current;

        public DistributedLeaseHandle Current
        {
            get { lock (_gate) return _current; }
            set { lock (_gate) _current = value; }
        }
    }
}

public interface ITrendReadService
{
    SnapshotTrendViewModel? Read(Guid registrationId, string window, int offset = 0, int limit = 100);
}

public sealed class TrendReadService(
    IServerRegistrationRepository registrations,
    ISnapshotHistoryStore history,
    PerformanceScaleOptions? performance = null) : ITrendReadService
{
    private static readonly IReadOnlyDictionary<string, TimeSpan> Windows = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
    { ["1h"] = TimeSpan.FromHours(1), ["6h"] = TimeSpan.FromHours(6), ["24h"] = TimeSpan.FromHours(24) };

    public SnapshotTrendViewModel? Read(Guid registrationId, string window, int offset = 0, int limit = 100)
    {
        if (registrations.GetById(registrationId) is null || !Windows.TryGetValue(window, out var duration)) return null;
        var policy = performance ?? new PerformanceScaleOptions();
        policy.Validate();
        return new(registrationId, window.ToLowerInvariant(), history.Read(registrationId, duration, PerformanceScaleOptions.BoundOffset(offset), policy.BoundHistoryLimit(limit)));
    }
}
