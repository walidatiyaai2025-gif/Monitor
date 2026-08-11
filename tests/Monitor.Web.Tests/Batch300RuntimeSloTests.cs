using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300RuntimeSloTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-11T06:00:00Z");

    [Fact]
    public void B300_081_ReadPathLatencyHistogramExposesBoundedPercentiles()
    {
        var service = Service();
        foreach (var value in new[] { 10, 20, 30, 40, 50 }) service.RecordReadPath(TimeSpan.FromMilliseconds(value));
        var snapshot = service.Snapshot().ReadPath;
        Assert.Equal(5, snapshot.Samples);
        Assert.Equal(30, snapshot.P50Milliseconds);
        Assert.Equal(50, snapshot.P95Milliseconds);
        Assert.Equal(50, snapshot.P99Milliseconds);
    }

    [Fact]
    public void B300_082_CollectionDurationModelIsIndependentFromReadPath()
    {
        var service = Service();
        service.RecordReadPath(TimeSpan.FromMilliseconds(10));
        service.RecordCollectionCycle(TimeSpan.FromMilliseconds(2000));
        var snapshot = service.Snapshot();
        Assert.Equal(10, snapshot.ReadPath.P95Milliseconds);
        Assert.Equal(2000, snapshot.CollectionCycle.P95Milliseconds);
    }

    [Fact]
    public void B300_083_CacheHitRatioUsesAllCacheReads()
    {
        var service = Service();
        service.RecordCacheRead(hit: true, stale: false);
        service.RecordCacheRead(hit: true, stale: true);
        service.RecordCacheRead(hit: false, stale: false);
        service.RecordCacheRead(hit: true, stale: false);
        Assert.Equal(75, service.Snapshot().Ratios.CacheHitPercent);
    }

    [Fact]
    public void B300_084_StaleReadRatioIsProjectedSeparatelyFromHitRatio()
    {
        var service = Service();
        for (var i = 0; i < 10; i++) service.RecordCacheRead(hit: true, stale: i < 2);
        var ratios = service.Snapshot().Ratios;
        Assert.Equal(100, ratios.CacheHitPercent);
        Assert.Equal(20, ratios.StaleReadPercent);
    }

    [Fact]
    public void B300_085_IncidentTransitionSuccessRatioUsesAttemptDenominator()
    {
        var service = Service();
        for (var i = 0; i < 20; i++) service.RecordIncidentTransition(success: i < 19);
        Assert.Equal(95, service.Snapshot().Ratios.IncidentTransitionSuccessPercent);
    }

    [Fact]
    public void B300_086_CasConflictRatioUsesAttemptDenominator()
    {
        var service = Service();
        for (var i = 0; i < 20; i++) service.RecordCasAttempt(conflict: i < 2);
        Assert.Equal(10, service.Snapshot().Ratios.CasConflictPercent);
    }

    [Fact]
    public void B300_087_SloThresholdOptionsRejectInvalidBounds()
    {
        new RuntimeSloThresholdOptions().Validate();
        Assert.Throws<InvalidOperationException>(() => new RuntimeSloThresholdOptions { ReadPathP95Milliseconds = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new RuntimeSloThresholdOptions { CollectionP95Milliseconds = 120001 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new RuntimeSloThresholdOptions { MinimumCacheHitPercent = 101 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new RuntimeSloThresholdOptions { MaximumCasConflictPercent = -1 }.Validate());
    }

    [Fact]
    public void B300_088_SloHealthClassifiesHealthyDegradedAndBreached()
    {
        var healthy = Service();
        healthy.RecordReadPath(TimeSpan.FromMilliseconds(100));
        healthy.RecordCollectionCycle(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(RuntimeSloHealth.Healthy, healthy.Snapshot().Health);

        var degraded = Service(new RuntimeSloThresholdOptions { ReadPathP95Milliseconds = 100 });
        degraded.RecordReadPath(TimeSpan.FromMilliseconds(120));
        Assert.Equal(RuntimeSloHealth.Degraded, degraded.Snapshot().Health);

        var breached = Service(new RuntimeSloThresholdOptions { ReadPathP95Milliseconds = 100 });
        breached.RecordReadPath(TimeSpan.FromMilliseconds(200));
        Assert.Equal(RuntimeSloHealth.Breached, breached.Snapshot().Health);
    }

    [Fact]
    public void B300_089_ObservabilitySnapshotIsBoundedAndTimestamped()
    {
        var service = Service();
        for (var i = 0; i < BoundedDurationHistogram.MaxSamples + 100; i++) service.RecordReadPath(TimeSpan.FromMilliseconds(i));
        var snapshot = service.Snapshot();
        Assert.Equal(BoundedDurationHistogram.MaxSamples, snapshot.ReadPath.Samples);
        Assert.Equal(Now, snapshot.CapturedAtUtc);
    }

    [Fact]
    public async Task B300_090_ConcurrentRuntimeRecordingRemainsBoundedAndConsistent()
    {
        var service = Service();
        var tasks = Enumerable.Range(0, 20).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                service.RecordReadPath(TimeSpan.FromMilliseconds(worker + i));
                service.RecordCollectionCycle(TimeSpan.FromMilliseconds(1000 + worker));
                service.RecordCacheRead(hit: i % 4 != 0, stale: i % 10 == 0);
                service.RecordIncidentTransition(success: i % 20 != 0);
                service.RecordCasAttempt(conflict: i % 10 == 0);
            }
        }));
        await Task.WhenAll(tasks);
        var snapshot = service.Snapshot();
        Assert.Equal(BoundedDurationHistogram.MaxSamples, snapshot.ReadPath.Samples);
        Assert.Equal(BoundedDurationHistogram.MaxSamples, snapshot.CollectionCycle.Samples);
        Assert.InRange(snapshot.Ratios.CacheHitPercent, 0, 100);
        Assert.InRange(snapshot.Ratios.StaleReadPercent, 0, 100);
        Assert.InRange(snapshot.Ratios.IncidentTransitionSuccessPercent, 0, 100);
        Assert.InRange(snapshot.Ratios.CasConflictPercent, 0, 100);
    }

    private static RuntimeSloService Service(RuntimeSloThresholdOptions? options = null) =>
        new(new FixedTimeProvider(Now), options);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
