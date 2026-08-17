using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record AgentReliabilityViewModel(
    string JobKey,
    string Owner,
    double SuccessRatePercent,
    int FailureStreak,
    TimeSpan P95Duration,
    double DurationRegressionPercent,
    double Score,
    B400Severity Severity,
    bool AlertWorthy,
    int RunsEvaluated,
    bool ScheduleLatenessEvaluated = false);

public static class AgentReliabilityProjection
{
    public static IReadOnlyList<AgentReliabilityViewModel> Build(SqlAgentHealthSnapshot? jobs, int limit = 10)
    {
        if (jobs?.RecentRuns is not { Count: > 0 } runs) return [];

        var bounded = Math.Clamp(limit, 1, 20);
        return runs
            .GroupBy(run => run.JobKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildOne(group.OrderByDescending(run => run.RunOrder).Take(20).ToArray()))
            .Where(item => item is not null)
            .Select(item => item!)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.FailureStreak)
            .ThenBy(item => item.JobKey, StringComparer.OrdinalIgnoreCase)
            .Take(bounded)
            .ToArray();
    }

    private static AgentReliabilityViewModel? BuildOne(IReadOnlyList<AgentJobRunSnapshot> snapshots)
    {
        if (snapshots.Count == 0) return null;

        var materialized = new List<(AgentJobRunSnapshot Snapshot, AgentJobRun Run)>();
        foreach (var snapshot in snapshots)
        {
            if (ToRun(snapshot, out var run) && run is not null)
            {
                materialized.Add((snapshot, run));
            }
        }
        if (materialized.Count == 0) return null;

        var runs = materialized.Select(item => item.Run).ToArray();
        var current = runs[0].Duration;
        var baseline = runs.Length > 1 ? Batch400AgentReliability.P95Duration(runs.Skip(1)) : current;
        var regression = Batch400AgentReliability.DurationRegression(baseline, current);
        var successRate = Batch400AgentReliability.SuccessRate(runs);
        var streak = Batch400AgentReliability.FailureStreak(runs);
        var p95 = Batch400AgentReliability.P95Duration(runs);
        var score = Batch400AgentReliability.ReliabilityScore(runs, TimeSpan.Zero, regression);
        var severity = Batch400AgentReliability.Severity(score);

        return new(
            materialized[0].Snapshot.JobKey,
            Batch400AgentReliability.NormalizeOwner(materialized[0].Snapshot.Owner),
            successRate,
            streak,
            p95,
            regression,
            score,
            severity,
            Batch400AgentReliability.AlertWorthy(score, streak, TimeSpan.Zero),
            runs.Length);
    }

    private static bool ToRun(AgentJobRunSnapshot snapshot, out AgentJobRun? run)
    {
        run = null;
        var date = snapshot.RunOrder / 1_000_000;
        var time = snapshot.RunOrder % 1_000_000;
        var year = (int)(date / 10_000);
        var month = (int)((date / 100) % 100);
        var day = (int)(date % 100);
        var hour = (int)(time / 10_000);
        var minute = (int)((time / 100) % 100);
        var second = (int)(time % 100);

        try
        {
            var normalizedWallClock = new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero);
            run = new AgentJobRun(snapshot.Succeeded, normalizedWallClock, TimeSpan.FromSeconds(snapshot.DurationSeconds));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
