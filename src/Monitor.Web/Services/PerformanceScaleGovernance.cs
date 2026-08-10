using Microsoft.Data.SqlClient;

namespace Monitor.Web.Services;

public sealed class PerformanceScaleOptions
{
    public const string SectionName = "PerformanceScale";

    public int SnapshotCacheMaxEntries { get; set; } = 512;
    public int HistoryMaxReadPoints { get; set; } = 100;
    public int AuditMaxPageSize { get; set; } = 100;
    public int IncidentMaxPageSize { get; set; } = 100;
    public int ServerDefaultPageSize { get; set; } = 50;
    public int ServerMaxPageSize { get; set; } = 100;
    public int ManualRefreshMaxConcurrency { get; set; } = 4;
    public int SqlMaxPoolSize { get; set; } = 4;
    public int SqlPoolLifetimeSeconds { get; set; } = 300;

    public void Validate()
    {
        if (SnapshotCacheMaxEntries is < 16 or > 5000) throw new InvalidOperationException("PerformanceScale:SnapshotCacheMaxEntries must be between 16 and 5000.");
        if (HistoryMaxReadPoints is < 10 or > 288) throw new InvalidOperationException("PerformanceScale:HistoryMaxReadPoints must be between 10 and 288.");
        if (AuditMaxPageSize is < 10 or > 100) throw new InvalidOperationException("PerformanceScale:AuditMaxPageSize must be between 10 and 100.");
        if (IncidentMaxPageSize is < 10 or > 100) throw new InvalidOperationException("PerformanceScale:IncidentMaxPageSize must be between 10 and 100.");
        if (ServerDefaultPageSize is < 10 or > 100) throw new InvalidOperationException("PerformanceScale:ServerDefaultPageSize must be between 10 and 100.");
        if (ServerMaxPageSize < ServerDefaultPageSize || ServerMaxPageSize > 250) throw new InvalidOperationException("PerformanceScale:ServerMaxPageSize must be at least the default and no more than 250.");
        if (ManualRefreshMaxConcurrency is < 1 or > 16) throw new InvalidOperationException("PerformanceScale:ManualRefreshMaxConcurrency must be between 1 and 16.");
        if (SqlMaxPoolSize is < 1 or > 32) throw new InvalidOperationException("PerformanceScale:SqlMaxPoolSize must be between 1 and 32.");
        if (SqlPoolLifetimeSeconds is < 30 or > 3600) throw new InvalidOperationException("PerformanceScale:SqlPoolLifetimeSeconds must be between 30 and 3600 seconds.");
    }

    public int BoundHistoryLimit(int requested) => Math.Clamp(requested, 1, HistoryMaxReadPoints);
    public int BoundAuditLimit(int requested) => Math.Clamp(requested, 1, AuditMaxPageSize);
    public int BoundIncidentLimit(int requested) => Math.Clamp(requested, 1, IncidentMaxPageSize);
    public int BoundServerLimit(int requested) => Math.Clamp(requested <= 0 ? ServerDefaultPageSize : requested, 1, ServerMaxPageSize);
    public static int BoundOffset(int offset) => Math.Clamp(offset, 0, 1_000_000);
}

public sealed record ServerEstatePage(
    IReadOnlyList<Monitor.Web.Models.ServerCard> Items,
    int Offset,
    int Limit,
    int TotalCount)
{
    public bool HasPrevious => Offset > 0;
    public bool HasNext => Offset + Items.Count < TotalCount;
    public int PreviousOffset => Math.Max(0, Offset - Limit);
    public int NextOffset => Math.Min(TotalCount, Offset + Limit);
}

public sealed class ManualRefreshConcurrencyGate
{
    private readonly SemaphoreSlim _semaphore;

    public ManualRefreshConcurrencyGate(PerformanceScaleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _semaphore = new SemaphoreSlim(options.ManualRefreshMaxConcurrency, options.ManualRefreshMaxConcurrency);
    }

    public bool TryAcquire(out IDisposable? lease)
    {
        if (!_semaphore.Wait(0))
        {
            lease = null;
            return false;
        }

        lease = new Lease(_semaphore);
        return true;
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        private SemaphoreSlim? _semaphore = semaphore;

        public void Dispose()
        {
            Interlocked.Exchange(ref _semaphore, null)?.Release();
        }
    }
}

public sealed class PerformanceBoundedAuditStore(
    IAuditStore inner,
    PerformanceScaleOptions options) : IAuditStore
{
    public void Append(string actor, string action, string target, string outcome) =>
        inner.Append(actor, action, target, outcome);

    public IReadOnlyList<AuditEvent> Read(int offset, int limit) =>
        inner.Read(PerformanceScaleOptions.BoundOffset(offset), options.BoundAuditLimit(limit));
}

public static class SchedulerJitter
{
    public static TimeSpan Compute(string seed, long cycle, int maxSeconds)
    {
        if (maxSeconds <= 0) return TimeSpan.Zero;
        if (maxSeconds > 30) throw new ArgumentOutOfRangeException(nameof(maxSeconds));

        var value = $"{seed}:{cycle}";
        uint hash = 2166136261;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= 16777619;
        }

        var maxMilliseconds = checked(maxSeconds * 1000);
        return TimeSpan.FromMilliseconds(hash % (maxMilliseconds + 1u));
    }
}

public static class SqlConnectionPoolPolicy
{
    public static void Apply(SqlConnectionStringBuilder builder, PerformanceScaleOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        builder.Pooling = true;
        builder.MinPoolSize = 0;
        builder.MaxPoolSize = options.SqlMaxPoolSize;
        builder.LoadBalanceTimeout = options.SqlPoolLifetimeSeconds;
    }
}
