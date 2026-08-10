using System.Collections.Concurrent;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface ISnapshotHistoryStore
{
    void Append(SnapshotCacheResult result);
    IReadOnlyList<SnapshotHistoryPoint> Read(Guid registrationId, TimeSpan window);
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

    public IReadOnlyList<SnapshotHistoryPoint> Read(Guid registrationId, TimeSpan window)
    {
        if (!_points.TryGetValue(registrationId, out var series)) return [];
        lock (series)
        {
            var cutoff = timeProvider.GetUtcNow() - window;
            return series.Values.Where(point => point.CollectedAtUtc >= cutoff).ToArray();
        }
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
    public void Validate()
    {
        if (Interval < TimeSpan.FromSeconds(30) || Interval > TimeSpan.FromHours(1)) throw new InvalidOperationException("Schedule interval is outside the allowed range.");
        if (MaxConcurrency is < 1 or > 8) throw new InvalidOperationException("Schedule concurrency is outside the allowed range.");
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
public sealed class CollectionBackoffPolicy(TimeProvider timeProvider) : ICollectionBackoffPolicy
{
    private sealed record State(int Failures, DateTimeOffset NextEligibleUtc);
    private readonly ConcurrentDictionary<Guid, State> _states = new();
    public bool IsEligible(Guid id) => !_states.TryGetValue(id, out var state) || timeProvider.GetUtcNow() >= state.NextEligibleUtc;
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
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var policy = options ?? new SnapshotScheduleOptions();
        policy.Validate();
        var clock = timeProvider ?? TimeProvider.System;
        var targets = registrations.GetAll().Where(item => item.IsEnabled).OrderBy(item => item.Id).ToArray();
        var succeeded = 0;
        var failed = 0;
        var skipped = 0;
        status?.Set(new(policy.Enabled, true, clock.GetUtcNow(), null, targets.Length, 0, 0, 0));
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
        });
        status?.Set(new(policy.Enabled, false, status.Get().LastStartedUtc, clock.GetUtcNow(), targets.Length, succeeded, failed, skipped));
    }
}

public sealed class SnapshotSchedulerService(
    SnapshotScheduleOptions options,
    ISnapshotCollectionCycle cycle,
    ISchedulerStatusStore status) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        options.Validate();
        status.Set(new(options.Enabled, false, null, null, 0, 0, 0, 0));
        if (!options.Enabled) return;
        using var timer = new PeriodicTimer(options.Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken)) await cycle.RunOnceAsync(stoppingToken);
    }
}

public interface ITrendReadService { SnapshotTrendViewModel? Read(Guid registrationId, string window); }
public sealed class TrendReadService(IServerRegistrationRepository registrations, ISnapshotHistoryStore history) : ITrendReadService
{
    private static readonly IReadOnlyDictionary<string, TimeSpan> Windows = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
    { ["1h"] = TimeSpan.FromHours(1), ["6h"] = TimeSpan.FromHours(6), ["24h"] = TimeSpan.FromHours(24) };

    public SnapshotTrendViewModel? Read(Guid registrationId, string window)
    {
        if (registrations.GetById(registrationId) is null || !Windows.TryGetValue(window, out var duration)) return null;
        return new(registrationId, window.ToLowerInvariant(), history.Read(registrationId, duration));
    }
}
