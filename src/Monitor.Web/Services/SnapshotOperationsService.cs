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
    public bool Enabled { get; init; }
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(1);
    public int MaxConcurrency { get; init; } = 2;
    public void Validate()
    {
        if (Interval < TimeSpan.FromSeconds(30) || Interval > TimeSpan.FromHours(1)) throw new InvalidOperationException("Schedule interval is outside the allowed range.");
        if (MaxConcurrency is < 1 or > 8) throw new InvalidOperationException("Schedule concurrency is outside the allowed range.");
    }
}

public interface ISnapshotCollectionCycle { Task RunOnceAsync(CancellationToken cancellationToken); }
public sealed class SnapshotCollectionCycle(
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache,
    ISnapshotObserver observer) : ISnapshotCollectionCycle
{
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        foreach (var registration in registrations.GetAll().Where(item => item.IsEnabled).OrderBy(item => item.Id))
        {
            try { observer.Observe(await cache.RefreshAsync(registration, cancellationToken)); }
            catch (SnapshotCollectionException) { }
        }
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
