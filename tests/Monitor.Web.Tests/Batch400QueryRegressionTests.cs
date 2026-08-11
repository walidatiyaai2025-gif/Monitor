using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch400QueryRegressionTests
{
    [Fact] public void B400_021_QueryKeyNormalizationIsBounded() => Assert.Equal(96, Batch400QueryRegression.NormalizeQueryKey(new string('x', 200)).Length);
    [Fact] public void B400_022_PercentDeltaHandlesBaseline() => Assert.Equal(100, Batch400QueryRegression.PercentDelta(10, 20));
    [Fact] public void B400_023_DurationDeltaUsesMetrics() => Assert.Equal(50, Batch400QueryRegression.DurationDelta(Q(10, 5, 100), Q(15, 5, 100)));
    [Fact] public void B400_024_CpuDeltaUsesMetrics() => Assert.Equal(100, Batch400QueryRegression.CpuDelta(Q(10, 5, 100), Q(10, 10, 100)));
    [Fact] public void B400_025_ReadDeltaUsesMetrics() => Assert.Equal(50, Batch400QueryRegression.ReadDelta(Q(10, 5, 100), Q(10, 5, 150)));
    [Fact] public void B400_026_PlanChangeRequiresTwoKnownHashes() => Assert.True(Batch400QueryRegression.PlanChanged(Q(10, 5, 100, "A"), Q(10, 5, 100, "B")));
    [Fact] public void B400_027_RegressionScoreIsBounded() => Assert.Equal(100, Batch400QueryRegression.Score(Q(10, 10, 10, "A"), Q(100, 100, 100, "B")));
    [Fact] public void B400_028_QuerySeverityUsesRiskBands() => Assert.Equal(B400Severity.Critical, Batch400QueryRegression.Severity(90));
    [Fact] public void B400_029_QueryCandidateRequiresExecutions() => Assert.True(Batch400QueryRegression.IsRegressionCandidate(Q(10, 10, 10), Q(100, 100, 100)));
    [Fact] public void B400_030_TopRegressionsAreBoundedAndOrdered() { var rows = Batch400QueryRegression.TopRegressions([(Q(10, 10, 10), Q(100, 100, 100)), (Q(10, 10, 10), Q(20, 20, 20))], 1); Assert.Single(rows); Assert.True(rows[0].Score > 0); }

    private static QueryMetric Q(double duration, double cpu, double reads, string? plan = "A") => new("Q1", duration, cpu, reads, 10, plan);
}
