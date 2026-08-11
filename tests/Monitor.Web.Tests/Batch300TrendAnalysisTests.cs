using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300TrendAnalysisTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-08-11T00:00:00Z");
    private static readonly Guid ServerId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void B300_011_TrendSeriesIsOrderedDeduplicatedAndBounded()
    {
        var samples = Enumerable.Range(0, 320).Select(i => new DbaTrendSample(Start.AddMinutes(i), i)).Append(new(Start.AddMinutes(319), 999));
        var bounded = DbaTrendAnalysis.Bound(samples);
        Assert.Equal(DbaTrendAnalysis.MaxSamples, bounded.Count);
        Assert.Equal(999, bounded[^1].Value);
        Assert.True(bounded.Zip(bounded.Skip(1)).All(pair => pair.First.AtUtc < pair.Second.AtUtc));
    }

    [Fact]
    public void B300_012_MovingAverageUsesRequestedTrailingWindow()
    {
        var samples = Enumerable.Range(1, 10).Select(i => new DbaTrendSample(Start.AddMinutes(i), i));
        Assert.Equal(8d, DbaTrendAnalysis.MovingAverage(samples, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => DbaTrendAnalysis.MovingAverage(samples, 0));
    }

    [Fact]
    public void B300_013_TrendSlopeClassifiesRisingFallingAndStable()
    {
        Assert.Equal(DbaTrendDirection.Rising, DbaTrendAnalysis.Analyze(Samples(10, 20, 30)).Direction);
        Assert.Equal(DbaTrendDirection.Falling, DbaTrendAnalysis.Analyze(Samples(30, 20, 10)).Direction);
        Assert.Equal(DbaTrendDirection.Stable, DbaTrendAnalysis.Analyze(Samples(20, 20.1, 20.2)).Direction);
    }

    [Fact]
    public void B300_014_MemoryTrendProjectsOnlyAvailableMemoryPoints()
    {
        var points = new[] { History(0, memory: 50), History(1, memory: null), History(2, memory: 80) };
        var trend = DbaTrendAnalysis.Memory(points);
        Assert.Equal(2, trend.Samples);
        Assert.Equal(80, trend.Current);
    }

    [Fact]
    public void B300_015_BlockingTrendProjectsBlockedRequests()
    {
        var trend = DbaTrendAnalysis.Blocking([History(0, blocking: 0), History(1, blocking: 2), History(2, blocking: 7)]);
        Assert.Equal(7, trend.Current);
        Assert.Equal(DbaTrendDirection.Rising, trend.Direction);
    }

    [Fact]
    public void B300_016_RunnableTrendProjectsSchedulerPressure()
    {
        var trend = DbaTrendAnalysis.Runnable([History(0, runnable: 7), History(1, runnable: 4), History(2, runnable: 1)]);
        Assert.Equal(1, trend.Current);
        Assert.Equal(DbaTrendDirection.Falling, trend.Direction);
    }

    [Fact]
    public void B300_017_DatabaseAvailabilityTrendUsesPercentageNotRawDatabaseCount()
    {
        var trend = DbaTrendAnalysis.DatabaseAvailability([
            History(0, online: 8, total: 10),
            History(1, online: 9, total: 10),
            History(2, online: 10, total: 10)]);
        Assert.Equal(100, trend.Current);
        Assert.Equal(DbaTrendDirection.Rising, trend.Direction);
    }

    [Fact]
    public void B300_018_BackupTrendProjectsCompliancePercentage()
    {
        var trend = DbaTrendAnalysis.BackupCompliance([
            new(Start, 5, 5),
            new(Start.AddHours(1), 8, 2),
            new(Start.AddHours(2), 10, 0)]);
        Assert.Equal(100, trend.Current);
        Assert.Equal(DbaTrendDirection.Rising, trend.Direction);
    }

    [Fact]
    public void B300_019_SparseAndStaleHistoryLowersConfidence()
    {
        Assert.Equal(DbaTrendConfidence.None, DbaTrendAnalysis.Confidence(Samples(1, 2)));
        Assert.Equal(DbaTrendConfidence.Low, DbaTrendAnalysis.Confidence(Enumerable.Range(0, 6).Select(i => new DbaTrendSample(Start.AddMinutes(i), i, Stale: i < 4))));
        Assert.Equal(DbaTrendConfidence.High, DbaTrendAnalysis.Confidence(Enumerable.Range(0, 20).Select(i => new DbaTrendSample(Start.AddMinutes(i), i))));
    }

    [Fact]
    public void B300_020_TrendAcceptanceKeepsInvalidValuesOutAndEmptySeriesSafe()
    {
        var bounded = DbaTrendAnalysis.Bound([
            new(Start, double.NaN),
            new(Start.AddMinutes(1), double.PositiveInfinity),
            new(Start.AddMinutes(2), 42)]);
        Assert.Single(bounded);
        Assert.Equal(42, bounded[0].Value);
        var empty = DbaTrendAnalysis.Analyze([]);
        Assert.Equal(DbaTrendDirection.Insufficient, empty.Direction);
        Assert.Equal(DbaTrendConfidence.None, empty.Confidence);
    }

    private static IEnumerable<DbaTrendSample> Samples(params double[] values) =>
        values.Select((value, index) => new DbaTrendSample(Start.AddHours(index), value));

    private static SnapshotHistoryPoint History(int hour, int? memory = 50, int? blocking = 0, int? runnable = 0, int online = 10, int total = 10, SnapshotFreshness freshness = SnapshotFreshness.Fresh) =>
        new(ServerId, Start.AddHours(hour), online, total, memory, blocking, runnable, freshness);
}
