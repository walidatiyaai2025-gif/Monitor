namespace Monitor.Web.Services;

public enum RuntimeSloHealth
{
    Healthy,
    Degraded,
    Breached
}

public sealed class RuntimeSloThresholdOptions
{
    public double ReadPathP95Milliseconds { get; init; } = 250;
    public double CollectionP95Milliseconds { get; init; } = 5000;
    public double MinimumCacheHitPercent { get; init; } = 80;
    public double MaximumStaleReadPercent { get; init; } = 20;
    public double MinimumIncidentTransitionSuccessPercent { get; init; } = 95;
    public double MaximumCasConflictPercent { get; init; } = 10;

    public void Validate()
    {
        if (ReadPathP95Milliseconds is <= 0 or > 60_000) throw new InvalidOperationException("Read-path P95 threshold is invalid.");
        if (CollectionP95Milliseconds is <= 0 or > 120_000) throw new InvalidOperationException("Collection P95 threshold is invalid.");
        ValidatePercent(MinimumCacheHitPercent, nameof(MinimumCacheHitPercent));
        ValidatePercent(MaximumStaleReadPercent, nameof(MaximumStaleReadPercent));
        ValidatePercent(MinimumIncidentTransitionSuccessPercent, nameof(MinimumIncidentTransitionSuccessPercent));
        ValidatePercent(MaximumCasConflictPercent, nameof(MaximumCasConflictPercent));
    }

    private static void ValidatePercent(double value, string name)
    {
        if (!double.IsFinite(value) || value is < 0 or > 100) throw new InvalidOperationException($"{name} must be 0..100.");
    }
}

public sealed record DurationHistogramSnapshot(int Samples, double P50Milliseconds, double P95Milliseconds, double P99Milliseconds, double MaxMilliseconds);
public sealed record RuntimeSloRatios(double CacheHitPercent, double StaleReadPercent, double IncidentTransitionSuccessPercent, double CasConflictPercent);
public sealed record RuntimeSloSnapshot(
    DurationHistogramSnapshot ReadPath,
    DurationHistogramSnapshot CollectionCycle,
    RuntimeSloRatios Ratios,
    RuntimeSloHealth Health,
    DateTimeOffset CapturedAtUtc);

public sealed class BoundedDurationHistogram
{
    public const int MaxSamples = 1000;
    private readonly object _gate = new();
    private readonly Queue<double> _values = new();

    public void Record(TimeSpan duration)
    {
        var milliseconds = Math.Max(0, duration.TotalMilliseconds);
        if (!double.IsFinite(milliseconds)) return;
        lock (_gate)
        {
            _values.Enqueue(milliseconds);
            while (_values.Count > MaxSamples) _values.Dequeue();
        }
    }

    public DurationHistogramSnapshot Snapshot()
    {
        double[] values;
        lock (_gate) values = _values.OrderBy(value => value).ToArray();
        if (values.Length == 0) return new(0, 0, 0, 0, 0);
        return new(values.Length, Percentile(values, 0.50), Percentile(values, 0.95), Percentile(values, 0.99), values[^1]);
    }

    private static double Percentile(IReadOnlyList<double> ordered, double percentile)
    {
        if (ordered.Count == 0) return 0;
        var index = (int)Math.Ceiling(percentile * ordered.Count) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Count - 1)];
    }
}

public sealed class RuntimeSloService
{
    private readonly BoundedDurationHistogram _readPath = new();
    private readonly BoundedDurationHistogram _collection = new();
    private readonly RuntimeSloThresholdOptions _thresholds;
    private readonly TimeProvider _timeProvider;
    private long _cacheReads;
    private long _cacheHits;
    private long _staleReads;
    private long _incidentTransitions;
    private long _incidentTransitionSuccesses;
    private long _casAttempts;
    private long _casConflicts;

    public RuntimeSloService(TimeProvider timeProvider, RuntimeSloThresholdOptions? thresholds = null)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _thresholds = thresholds ?? new RuntimeSloThresholdOptions();
        _thresholds.Validate();
    }

    public void RecordReadPath(TimeSpan duration) => _readPath.Record(duration);
    public void RecordCollectionCycle(TimeSpan duration) => _collection.Record(duration);

    public void RecordCacheRead(bool hit, bool stale)
    {
        Interlocked.Increment(ref _cacheReads);
        if (hit) Interlocked.Increment(ref _cacheHits);
        if (stale) Interlocked.Increment(ref _staleReads);
    }

    public void RecordIncidentTransition(bool success)
    {
        Interlocked.Increment(ref _incidentTransitions);
        if (success) Interlocked.Increment(ref _incidentTransitionSuccesses);
    }

    public void RecordCasAttempt(bool conflict)
    {
        Interlocked.Increment(ref _casAttempts);
        if (conflict) Interlocked.Increment(ref _casConflicts);
    }

    public RuntimeSloSnapshot Snapshot()
    {
        var read = _readPath.Snapshot();
        var collection = _collection.Snapshot();
        var ratios = new RuntimeSloRatios(
            Ratio(Interlocked.Read(ref _cacheHits), Interlocked.Read(ref _cacheReads), emptyValue: 100),
            Ratio(Interlocked.Read(ref _staleReads), Interlocked.Read(ref _cacheReads), emptyValue: 0),
            Ratio(Interlocked.Read(ref _incidentTransitionSuccesses), Interlocked.Read(ref _incidentTransitions), emptyValue: 100),
            Ratio(Interlocked.Read(ref _casConflicts), Interlocked.Read(ref _casAttempts), emptyValue: 0));
        return new(read, collection, ratios, Classify(read, collection, ratios), _timeProvider.GetUtcNow());
    }

    private RuntimeSloHealth Classify(DurationHistogramSnapshot read, DurationHistogramSnapshot collection, RuntimeSloRatios ratios)
    {
        var breaches = 0;
        var degradations = 0;
        EvaluateUpper(read.P95Milliseconds, _thresholds.ReadPathP95Milliseconds, read.Samples > 0, ref breaches, ref degradations);
        EvaluateUpper(collection.P95Milliseconds, _thresholds.CollectionP95Milliseconds, collection.Samples > 0, ref breaches, ref degradations);
        EvaluateLower(ratios.CacheHitPercent, _thresholds.MinimumCacheHitPercent, ref breaches, ref degradations);
        EvaluateUpper(ratios.StaleReadPercent, _thresholds.MaximumStaleReadPercent, true, ref breaches, ref degradations);
        EvaluateLower(ratios.IncidentTransitionSuccessPercent, _thresholds.MinimumIncidentTransitionSuccessPercent, ref breaches, ref degradations);
        EvaluateUpper(ratios.CasConflictPercent, _thresholds.MaximumCasConflictPercent, true, ref breaches, ref degradations);
        if (breaches > 0) return RuntimeSloHealth.Breached;
        return degradations > 0 ? RuntimeSloHealth.Degraded : RuntimeSloHealth.Healthy;
    }

    private static void EvaluateUpper(double value, double threshold, bool hasData, ref int breaches, ref int degradations)
    {
        if (!hasData) return;
        if (value > threshold * 1.5) breaches++;
        else if (value > threshold) degradations++;
    }

    private static void EvaluateLower(double value, double threshold, ref int breaches, ref int degradations)
    {
        if (value < threshold * 0.5) breaches++;
        else if (value < threshold) degradations++;
    }

    private static double Ratio(long numerator, long denominator, double emptyValue) =>
        denominator <= 0 ? emptyValue : Math.Round(100d * Math.Clamp(numerator, 0, denominator) / denominator, 2, MidpointRounding.AwayFromZero);
}
