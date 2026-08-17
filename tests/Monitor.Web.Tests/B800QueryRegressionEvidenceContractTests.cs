using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800QueryRegressionEvidenceContractTests
{
    [Fact]
    public void Evaluate_DerivesPerExecutionIntervalMetricsFromMonotonicCounters()
    {
        var previous = S("0011223344556677", "1111222233334444", 10, 2, 10, 100_000, 50_000, 1_000);
        var current = S("0011223344556677", "1111222233334444", 10, 2, 15, 175_000, 80_000, 1_300);

        var result = QueryRegressionEvidenceContract.Evaluate(previous, current);

        Assert.True(result.IsReady);
        Assert.Equal(QueryIntervalStatus.Ready, result.Status);
        Assert.NotNull(result.Metric);
        Assert.Equal("QH:0011223344556677", result.Metric!.QueryKey);
        Assert.Equal("PH:1111222233334444", result.Metric.PlanHash);
        Assert.Equal(5, result.Metric.Executions);
        Assert.Equal(15, result.Metric.DurationMs, 5);
        Assert.Equal(6, result.Metric.CpuMs, 5);
        Assert.Equal(60, result.Metric.LogicalReads, 5);
    }

    [Theory]
    [InlineData("0011")]
    [InlineData("not-a-hash-value")]
    [InlineData("001122334455667Z")]
    public void Evaluate_RejectsInvalidOpaqueHashes(string queryHash)
    {
        var result = QueryRegressionEvidenceContract.Evaluate(
            S(queryHash, null, 1, 0, 1, 1, 1, 1),
            S(queryHash, null, 1, 0, 2, 2, 2, 2));

        Assert.Equal(QueryIntervalStatus.InvalidEvidence, result.Status);
        Assert.Null(result.Metric);
    }

    [Fact]
    public void Evaluate_FailsClosedAcrossRecompileOrCacheEpochChange()
    {
        var baseline = S("0011223344556677", "1111222233334444", 10, 2, 10, 100, 100, 100);

        var generationChanged = QueryRegressionEvidenceContract.Evaluate(
            baseline,
            S("0011223344556677", "1111222233334444", 10, 3, 20, 300, 300, 300));
        var epochChanged = QueryRegressionEvidenceContract.Evaluate(
            baseline,
            S("0011223344556677", "1111222233334444", 11, 2, 20, 300, 300, 300));
        var planChangedInsideInterval = QueryRegressionEvidenceContract.Evaluate(
            baseline,
            S("0011223344556677", "AAAABBBBCCCCDDDD", 10, 2, 20, 300, 300, 300));

        Assert.Equal(QueryIntervalStatus.CacheEpochChanged, generationChanged.Status);
        Assert.Equal(QueryIntervalStatus.CacheEpochChanged, epochChanged.Status);
        Assert.Equal(QueryIntervalStatus.CacheEpochChanged, planChangedInsideInterval.Status);
        Assert.Null(generationChanged.Metric);
        Assert.Null(epochChanged.Metric);
        Assert.Null(planChangedInsideInterval.Metric);
    }

    [Fact]
    public void Evaluate_FailsClosedWhenCountersResetOrNoExecutionsComplete()
    {
        var previous = S("0011223344556677", null, 10, 2, 10, 100, 100, 100);
        var reset = QueryRegressionEvidenceContract.Evaluate(
            previous,
            S("0011223344556677", null, 10, 2, 9, 90, 90, 90));
        var idle = QueryRegressionEvidenceContract.Evaluate(
            previous,
            S("0011223344556677", null, 10, 2, 10, 100, 100, 100));

        Assert.Equal(QueryIntervalStatus.CounterReset, reset.Status);
        Assert.Equal(QueryIntervalStatus.NoExecutions, idle.Status);
        Assert.Null(reset.Metric);
        Assert.Null(idle.Metric);
    }

    [Fact]
    public void SeparateStableIntervals_CanFeedB400WithoutCrossingPlanReset()
    {
        var baseline = QueryRegressionEvidenceContract.Evaluate(
            S("0011223344556677", "1111222233334444", 10, 2, 10, 100_000, 50_000, 1_000),
            S("0011223344556677", "1111222233334444", 10, 2, 20, 200_000, 100_000, 2_000));
        var current = QueryRegressionEvidenceContract.Evaluate(
            S("0011223344556677", "AAAABBBBCCCCDDDD", 20, 0, 5, 100_000, 50_000, 500),
            S("0011223344556677", "AAAABBBBCCCCDDDD", 20, 0, 10, 300_000, 150_000, 1_500));

        Assert.True(baseline.IsReady);
        Assert.True(current.IsReady);
        Assert.True(Batch400QueryRegression.PlanChanged(baseline.Metric!, current.Metric!));
        Assert.True(Batch400QueryRegression.Score(baseline.Metric!, current.Metric!) > 0);
    }

    [Fact]
    public void NormalizeHash_AcceptsOnlyBoundedEngineHashValue()
    {
        Assert.True(QueryRegressionEvidenceContract.TryNormalizeHash("0xabcdef0123456789", out var normalized));
        Assert.Equal("ABCDEF0123456789", normalized);
        Assert.False(QueryRegressionEvidenceContract.TryNormalizeHash("abcdef", out _));
    }

    private static QueryCumulativeEvidence S(
        string queryHash,
        string? planHash,
        long compileEpoch,
        long generation,
        long executions,
        long elapsedUs,
        long workerUs,
        long reads) =>
        new(queryHash, planHash, compileEpoch, generation, executions, elapsedUs, workerUs, reads);
}
