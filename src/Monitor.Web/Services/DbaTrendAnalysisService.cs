using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum DbaTrendDirection
{
    Insufficient,
    Falling,
    Stable,
    Rising
}

public enum DbaTrendConfidence
{
    None,
    Low,
    Medium,
    High
}

public sealed record DbaTrendSample(DateTimeOffset AtUtc, double Value, bool Stale = false);
public sealed record BackupComplianceTrendPoint(DateTimeOffset AtUtc, int BackedUp, int Missing, bool Stale = false);
public sealed record DbaTrendProjection(
    int Samples,
    double Current,
    double Baseline,
    double Delta,
    double SlopePerHour,
    DbaTrendDirection Direction,
    DbaTrendConfidence Confidence);

public static class DbaTrendAnalysis
{
    public const int MaxSamples = 288;

    public static IReadOnlyList<DbaTrendSample> Bound(IEnumerable<DbaTrendSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        return samples
            .Where(item => double.IsFinite(item.Value))
            .OrderBy(item => item.AtUtc)
            .GroupBy(item => item.AtUtc)
            .Select(group => group.Last())
            .TakeLast(MaxSamples)
            .ToArray();
    }

    public static double MovingAverage(IEnumerable<DbaTrendSample> samples, int window = 5)
    {
        if (window is < 1 or > 60) throw new ArgumentOutOfRangeException(nameof(window));
        var bounded = Bound(samples).TakeLast(window).ToArray();
        return bounded.Length == 0 ? 0 : bounded.Average(item => item.Value);
    }

    public static DbaTrendProjection Analyze(IEnumerable<DbaTrendSample> samples)
    {
        var bounded = Bound(samples).ToArray();
        if (bounded.Length == 0) return new(0, 0, 0, 0, 0, DbaTrendDirection.Insufficient, DbaTrendConfidence.None);
        var current = bounded[^1].Value;
        var baselineWindow = bounded.Length <= 1 ? bounded : bounded[..^1];
        var baseline = baselineWindow.TakeLast(Math.Min(12, baselineWindow.Length)).Average(item => item.Value);
        var delta = current - baseline;
        var slope = SlopePerHour(bounded);
        var confidence = Confidence(bounded);
        var direction = bounded.Length < 3 ? DbaTrendDirection.Insufficient : Direction(slope, baseline);
        return new(bounded.Length, current, baseline, delta, slope, direction, confidence);
    }

    public static DbaTrendProjection Memory(IEnumerable<SnapshotHistoryPoint> points) =>
        Analyze(points.Where(item => item.MemoryPercent.HasValue).Select(item => new DbaTrendSample(item.CollectedAtUtc, item.MemoryPercent!.Value, item.Freshness == SnapshotFreshness.Stale)));

    public static DbaTrendProjection Blocking(IEnumerable<SnapshotHistoryPoint> points) =>
        Analyze(points.Where(item => item.BlockedRequests.HasValue).Select(item => new DbaTrendSample(item.CollectedAtUtc, item.BlockedRequests!.Value, item.Freshness == SnapshotFreshness.Stale)));

    public static DbaTrendProjection Runnable(IEnumerable<SnapshotHistoryPoint> points) =>
        Analyze(points.Where(item => item.RunnableTasks.HasValue).Select(item => new DbaTrendSample(item.CollectedAtUtc, item.RunnableTasks!.Value, item.Freshness == SnapshotFreshness.Stale)));

    public static DbaTrendProjection DatabaseAvailability(IEnumerable<SnapshotHistoryPoint> points) =>
        Analyze(points.Where(item => item.DatabaseTotal > 0).Select(item => new DbaTrendSample(
            item.CollectedAtUtc,
            100d * Math.Clamp(item.DatabaseOnline, 0, item.DatabaseTotal) / item.DatabaseTotal,
            item.Freshness == SnapshotFreshness.Stale)));

    public static DbaTrendProjection BackupCompliance(IEnumerable<BackupComplianceTrendPoint> points) =>
        Analyze(points.Select(item =>
        {
            var total = Math.Max(0, item.BackedUp) + Math.Max(0, item.Missing);
            var value = total == 0 ? 100d : 100d * Math.Max(0, item.BackedUp) / total;
            return new DbaTrendSample(item.AtUtc, value, item.Stale);
        }));

    public static DbaTrendConfidence Confidence(IEnumerable<DbaTrendSample> samples)
    {
        var bounded = Bound(samples).ToArray();
        if (bounded.Length < 3) return DbaTrendConfidence.None;
        var staleRatio = bounded.Count(item => item.Stale) / (double)bounded.Length;
        if (bounded.Length < 5 || staleRatio > 0.50) return DbaTrendConfidence.Low;
        if (bounded.Length < 12 || staleRatio > 0.20) return DbaTrendConfidence.Medium;
        return DbaTrendConfidence.High;
    }

    private static double SlopePerHour(IReadOnlyList<DbaTrendSample> samples)
    {
        if (samples.Count < 2) return 0;
        var first = samples[0];
        var last = samples[^1];
        var hours = (last.AtUtc - first.AtUtc).TotalHours;
        if (hours <= 0) return 0;
        return (last.Value - first.Value) / hours;
    }

    private static DbaTrendDirection Direction(double slopePerHour, double baseline)
    {
        var threshold = Math.Max(0.25, Math.Abs(baseline) * 0.01);
        if (slopePerHour > threshold) return DbaTrendDirection.Rising;
        return slopePerHour < -threshold ? DbaTrendDirection.Falling : DbaTrendDirection.Stable;
    }
}
