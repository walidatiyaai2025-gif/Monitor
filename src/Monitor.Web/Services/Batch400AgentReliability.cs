namespace Monitor.Web.Services;

public sealed record AgentJobRun(bool Succeeded, DateTimeOffset StartedAtUtc, TimeSpan Duration);
public sealed record AgentReliabilitySummary(string Owner, double SuccessRatePercent, int FailureStreak, TimeSpan P95Duration, TimeSpan Lateness, double DurationRegressionPercent, double Score, B400Severity Severity, bool AlertWorthy, int RunsEvaluated);

public static class Batch400AgentReliability
{
    public static string NormalizeOwner(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "UNASSIGNED";
        var trimmed = value.Trim();
        return trimmed[..Math.Min(trimmed.Length, 64)];
    }

    public static double SuccessRate(IEnumerable<AgentJobRun> runs)
    {
        var materialized = runs.ToArray();
        return materialized.Length == 0 ? 100 : Math.Round(materialized.Count(item => item.Succeeded) * 100d / materialized.Length, 2);
    }

    public static int FailureStreak(IEnumerable<AgentJobRun> runs)
    {
        var ordered = runs.OrderByDescending(item => item.StartedAtUtc).ToArray();
        var streak = 0;
        foreach (var run in ordered)
        {
            if (run.Succeeded) break;
            streak++;
        }
        return streak;
    }

    public static TimeSpan P95Duration(IEnumerable<AgentJobRun> runs)
    {
        var values = runs.Select(item => Math.Max(0, item.Duration.TotalMilliseconds)).OrderBy(value => value).ToArray();
        if (values.Length == 0) return TimeSpan.Zero;
        var index = Math.Clamp((int)Math.Ceiling(values.Length * 0.95) - 1, 0, values.Length - 1);
        return TimeSpan.FromMilliseconds(values[index]);
    }

    public static TimeSpan Lateness(DateTimeOffset nowUtc, DateTimeOffset expectedStartUtc)
    {
        var late = nowUtc - expectedStartUtc;
        return late <= TimeSpan.Zero ? TimeSpan.Zero : late > TimeSpan.FromDays(30) ? TimeSpan.FromDays(30) : late;
    }

    public static double DurationRegression(TimeSpan baseline, TimeSpan current)
    {
        var oldMs = Math.Max(0, baseline.TotalMilliseconds);
        var newMs = Math.Max(0, current.TotalMilliseconds);
        if (oldMs <= 0) return newMs > 0 ? 100 : 0;
        return Math.Round(Math.Clamp((newMs - oldMs) * 100d / oldMs, -100, 10_000), 2);
    }

    public static double ReliabilityScore(IEnumerable<AgentJobRun> runs, TimeSpan lateness, double durationRegressionPercent)
    {
        var successPenalty = 100 - SuccessRate(runs);
        var streakPenalty = Math.Min(100, FailureStreak(runs) * 25d);
        var latePenalty = Math.Min(100, Math.Max(0, lateness.TotalMinutes) / 1.2d);
        var durationPenalty = Math.Min(100, Math.Max(0, durationRegressionPercent) / 2d);
        return Math.Round(Math.Clamp(successPenalty * 0.4 + streakPenalty * 0.3 + latePenalty * 0.2 + durationPenalty * 0.1, 0, 100), 2);
    }

    public static B400Severity Severity(double score) => score switch
    {
        >= 75 => B400Severity.Critical,
        >= 40 => B400Severity.Warning,
        > 0 => B400Severity.Info,
        _ => B400Severity.None
    };

    public static bool AlertWorthy(double score, int failureStreak, TimeSpan lateness) => Severity(score) is B400Severity.Warning or B400Severity.Critical || failureStreak >= 2 || lateness >= TimeSpan.FromMinutes(30);

    public static AgentReliabilitySummary Summarize(string? owner, IEnumerable<AgentJobRun> runs, DateTimeOffset nowUtc, DateTimeOffset expectedStartUtc, TimeSpan baselineDuration)
    {
        var materialized = runs.OrderByDescending(item => item.StartedAtUtc).Take(100).ToArray();
        var p95 = P95Duration(materialized);
        var lateness = Lateness(nowUtc, expectedStartUtc);
        var regression = DurationRegression(baselineDuration, p95);
        var score = ReliabilityScore(materialized, lateness, regression);
        var streak = FailureStreak(materialized);
        return new(NormalizeOwner(owner), SuccessRate(materialized), streak, p95, lateness, regression, score, Severity(score), AlertWorthy(score, streak, lateness), materialized.Length);
    }
}
