using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch400AgentReliabilityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-11T06:00:00Z");
    [Fact] public void B400_061_AgentOwnerNormalizationHasFallback() => Assert.Equal("UNASSIGNED", Batch400AgentReliability.NormalizeOwner(" "));
    [Fact] public void B400_062_AgentSuccessRateUsesHistory() => Assert.Equal(50, Batch400AgentReliability.SuccessRate([Run(true, 0, 1), Run(false, 1, 1)]));
    [Fact] public void B400_063_AgentFailureStreakStopsAtSuccess() => Assert.Equal(2, Batch400AgentReliability.FailureStreak([Run(false, 2, 1), Run(false, 1, 1), Run(true, 0, 1)]));
    [Fact] public void B400_064_AgentP95DurationIsDeterministic() { var runs = Enumerable.Range(1, 20).Select(i => Run(true, i, i)); Assert.Equal(TimeSpan.FromSeconds(19), Batch400AgentReliability.P95Duration(runs)); }
    [Fact] public void B400_065_AgentLatenessNeverGoesNegative() => Assert.Equal(TimeSpan.Zero, Batch400AgentReliability.Lateness(Now, Now.AddMinutes(5)));
    [Fact] public void B400_066_AgentDurationRegressionUsesBaseline() => Assert.Equal(100, Batch400AgentReliability.DurationRegression(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)));
    [Fact] public void B400_067_AgentReliabilityScoreCombinesFailures() { var score = Batch400AgentReliability.ReliabilityScore([Run(false, 2, 1), Run(false, 1, 1)], TimeSpan.FromHours(1), 100); Assert.True(score > 40); }
    [Fact] public void B400_068_AgentSeverityUsesRiskBands() => Assert.Equal(B400Severity.Critical, Batch400AgentReliability.Severity(80));
    [Fact] public void B400_069_AgentAlertWorthyDetectsFailureStreak() => Assert.True(Batch400AgentReliability.AlertWorthy(0, 2, TimeSpan.Zero));
    [Fact] public void B400_070_AgentSummaryBoundsHistory() { var runs = Enumerable.Range(0, 120).Select(i => Run(true, i, 1)); var result = Batch400AgentReliability.Summarize("DBA", runs, Now.AddHours(200), Now, TimeSpan.FromSeconds(1)); Assert.Equal(100, result.RunsEvaluated); }
    private static AgentJobRun Run(bool ok, int minute, int durationSeconds) => new(ok, Now.AddMinutes(minute), TimeSpan.FromSeconds(durationSeconds));
}
