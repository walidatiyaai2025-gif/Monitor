using System.Security.Cryptography;
using System.Text;

namespace Monitor.Web.Services;

public sealed record QueryMetric(string QueryKey, double DurationMs, double CpuMs, double LogicalReads, long Executions, string? PlanHash);
public sealed record QueryRegressionResult(string QueryKey, double DurationDeltaPercent, double CpuDeltaPercent, double ReadDeltaPercent, bool PlanChanged, double Score, B400Severity Severity, string Fingerprint);

public static class Batch400QueryRegression
{
    public static string NormalizeQueryKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "UNKNOWN";
        var trimmed = value.Trim();
        return trimmed[..Math.Min(trimmed.Length, 96)];
    }

    public static double PercentDelta(double baseline, double current)
    {
        if (!double.IsFinite(baseline) || !double.IsFinite(current)) return 0;
        if (baseline <= 0) return current > 0 ? 100 : 0;
        return Math.Round(Math.Clamp((current - baseline) * 100d / baseline, -100, 10_000), 2);
    }

    public static double DurationDelta(QueryMetric baseline, QueryMetric current) => PercentDelta(baseline.DurationMs, current.DurationMs);
    public static double CpuDelta(QueryMetric baseline, QueryMetric current) => PercentDelta(baseline.CpuMs, current.CpuMs);
    public static double ReadDelta(QueryMetric baseline, QueryMetric current) => PercentDelta(baseline.LogicalReads, current.LogicalReads);

    public static bool PlanChanged(QueryMetric baseline, QueryMetric current)
    {
        var left = baseline.PlanHash?.Trim();
        var right = current.PlanHash?.Trim();
        return !string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right) && !string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    public static double Score(QueryMetric baseline, QueryMetric current)
    {
        var duration = Math.Max(0, DurationDelta(baseline, current));
        var cpu = Math.Max(0, CpuDelta(baseline, current));
        var reads = Math.Max(0, ReadDelta(baseline, current));
        var weighted = Math.Min(100, duration / 2d) * 0.5 + Math.Min(100, cpu / 2d) * 0.3 + Math.Min(100, reads / 2d) * 0.2;
        if (PlanChanged(baseline, current)) weighted += 10;
        return Math.Round(Math.Clamp(weighted, 0, 100), 2);
    }

    public static B400Severity Severity(double score) => score switch
    {
        >= 75 => B400Severity.Critical,
        >= 45 => B400Severity.Warning,
        >= 15 => B400Severity.Info,
        _ => B400Severity.None
    };

    public static bool IsRegressionCandidate(QueryMetric baseline, QueryMetric current, double threshold = 25)
    {
        if (threshold < 0 || threshold > 100) throw new ArgumentOutOfRangeException(nameof(threshold));
        return Score(baseline, current) >= threshold && Math.Max(0, current.Executions) > 0;
    }

    public static string Fingerprint(string? queryKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeQueryKey(queryKey)));
        return Convert.ToHexString(bytes.AsSpan(0, 8));
    }

    public static IReadOnlyList<QueryRegressionResult> TopRegressions(IEnumerable<(QueryMetric Baseline, QueryMetric Current)> pairs, int limit = 25)
    {
        var bounded = Math.Clamp(limit, 1, 100);
        return pairs.Select(pair =>
        {
            var score = Score(pair.Baseline, pair.Current);
            return new QueryRegressionResult(NormalizeQueryKey(pair.Current.QueryKey), DurationDelta(pair.Baseline, pair.Current), CpuDelta(pair.Baseline, pair.Current), ReadDelta(pair.Baseline, pair.Current), PlanChanged(pair.Baseline, pair.Current), score, Severity(score), Fingerprint(pair.Current.QueryKey));
        }).Where(item => item.Score > 0).OrderByDescending(item => item.Score).ThenBy(item => item.QueryKey, StringComparer.Ordinal).Take(bounded).ToArray();
    }
}
