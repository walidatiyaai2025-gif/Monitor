namespace Monitor.Web.Services;

public sealed record QueryCumulativeEvidence(
    string QueryHash,
    string? PlanHash,
    long CompileEpoch,
    long PlanGeneration,
    long ExecutionCount,
    long TotalElapsedMicroseconds,
    long TotalWorkerMicroseconds,
    long TotalLogicalReads);

public enum QueryIntervalStatus
{
    Ready,
    InvalidEvidence,
    DifferentQuery,
    CacheEpochChanged,
    CounterReset,
    NoExecutions
}

public sealed record QueryIntervalEvidence(
    QueryIntervalStatus Status,
    QueryMetric? Metric,
    string Reason)
{
    public bool IsReady => Status == QueryIntervalStatus.Ready && Metric is not null;
}

public static class QueryRegressionEvidenceContract
{
    public static QueryIntervalEvidence Evaluate(
        QueryCumulativeEvidence previous,
        QueryCumulativeEvidence current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        if (!TryNormalizeHash(previous.QueryHash, out var previousQueryHash) ||
            !TryNormalizeHash(current.QueryHash, out var currentQueryHash) ||
            !TryNormalizeOptionalHash(previous.PlanHash, out var previousPlanHash) ||
            !TryNormalizeOptionalHash(current.PlanHash, out var currentPlanHash) ||
            !HasValidCounters(previous) ||
            !HasValidCounters(current))
        {
            return Unavailable(QueryIntervalStatus.InvalidEvidence, "Query counter evidence is invalid.");
        }

        if (!string.Equals(previousQueryHash, currentQueryHash, StringComparison.Ordinal))
        {
            return Unavailable(QueryIntervalStatus.DifferentQuery, "Query hashes differ; interval comparison is not valid.");
        }

        if (previous.CompileEpoch != current.CompileEpoch ||
            previous.PlanGeneration != current.PlanGeneration ||
            !string.Equals(previousPlanHash, currentPlanHash, StringComparison.Ordinal))
        {
            return Unavailable(QueryIntervalStatus.CacheEpochChanged, "Plan cache epoch or plan generation changed; establish a new baseline interval.");
        }

        if (current.ExecutionCount < previous.ExecutionCount ||
            current.TotalElapsedMicroseconds < previous.TotalElapsedMicroseconds ||
            current.TotalWorkerMicroseconds < previous.TotalWorkerMicroseconds ||
            current.TotalLogicalReads < previous.TotalLogicalReads)
        {
            return Unavailable(QueryIntervalStatus.CounterReset, "Cumulative query counters moved backwards; establish a new baseline interval.");
        }

        var executions = current.ExecutionCount - previous.ExecutionCount;
        if (executions <= 0)
        {
            return Unavailable(QueryIntervalStatus.NoExecutions, "No completed executions occurred in this evidence interval.");
        }

        var elapsedMicroseconds = current.TotalElapsedMicroseconds - previous.TotalElapsedMicroseconds;
        var workerMicroseconds = current.TotalWorkerMicroseconds - previous.TotalWorkerMicroseconds;
        var logicalReads = current.TotalLogicalReads - previous.TotalLogicalReads;
        var metric = new QueryMetric(
            $"QH:{currentQueryHash}",
            elapsedMicroseconds / 1000d / executions,
            workerMicroseconds / 1000d / executions,
            logicalReads / (double)executions,
            executions,
            currentPlanHash is null ? null : $"PH:{currentPlanHash}");

        return new QueryIntervalEvidence(
            QueryIntervalStatus.Ready,
            metric,
            "Interval metrics were derived from monotonic cumulative counters within one cache epoch.");
    }

    public static bool TryNormalizeHash(string? value, out string normalized) =>
        TryNormalizeHashCore(value, required: true, out normalized);

    private static bool TryNormalizeOptionalHash(string? value, out string? normalized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalized = null;
            return true;
        }

        var valid = TryNormalizeHashCore(value, required: false, out var result);
        normalized = valid ? result : null;
        return valid;
    }

    private static bool TryNormalizeHashCore(string? value, bool required, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return !required;

        var candidate = value.Trim();
        if (candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) candidate = candidate[2..];
        if (candidate.Length != 16 || candidate.Any(character => !Uri.IsHexDigit(character))) return false;

        normalized = candidate.ToUpperInvariant();
        return true;
    }

    private static bool HasValidCounters(QueryCumulativeEvidence evidence) =>
        evidence.CompileEpoch >= 0 &&
        evidence.PlanGeneration >= 0 &&
        evidence.ExecutionCount >= 0 &&
        evidence.TotalElapsedMicroseconds >= 0 &&
        evidence.TotalWorkerMicroseconds >= 0 &&
        evidence.TotalLogicalReads >= 0;

    private static QueryIntervalEvidence Unavailable(QueryIntervalStatus status, string reason) =>
        new(status, null, reason);
}
