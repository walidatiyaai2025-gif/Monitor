using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class WaitIntelligenceProjectionTests
{
    [Fact]
    public void Build_UsesUptimeToNormalizeCumulativeWaitCounters()
    {
        var performance = new PerformanceHealthSnapshot(
            4,
            2,
            1,
            [
                new WaitStatSnapshot("PAGEIOLATCH_SH", 36_000, 3_600, 100),
                new WaitStatSnapshot("SOS_SCHEDULER_YIELD", 18_000, 9_000, 200)
            ]);

        var result = WaitIntelligenceProjection.Build(performance, 3_600, 8);

        Assert.Equal(2, result.Count);
        Assert.Equal("PAGEIOLATCH_SH", result[0].WaitType);
        Assert.Equal(WaitCategory.Io, result[0].Category);
        Assert.Equal(10d, result[0].WaitMsPerSecond);
        Assert.Equal(66.67d, result[0].SharePercent);
        Assert.Equal(10d, result[0].SignalPercent);
    }

    [Fact]
    public void Build_MissingWaitsOrUptimeReturnsNoSyntheticHealthyRows()
    {
        Assert.Empty(WaitIntelligenceProjection.Build(new PerformanceHealthSnapshot(1, 0, 0), 3_600));
        Assert.Empty(WaitIntelligenceProjection.Build(new PerformanceHealthSnapshot(1, 0, 0, [new WaitStatSnapshot("WRITELOG", 100, 10, 2)]), null));
        Assert.Empty(WaitIntelligenceProjection.Build(new PerformanceHealthSnapshot(1, 0, 0, [new WaitStatSnapshot("WRITELOG", 100, 10, 2)]), 0));
    }

    [Fact]
    public void PerformanceView_WiresCachedB400WaitIntelligenceWithTruthfulBoundary()
    {
        var root = FindRoot();
        var view = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Views/Portal/Performance.cshtml"));
        var controller = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Controllers/PortalController.cs"));

        Assert.Contains("WaitIntelligenceProjection.Build", view, StringComparison.Ordinal);
        Assert.Contains("B400 WAIT INTELLIGENCE", view, StringComparison.Ordinal);
        Assert.Contains("Cumulative waits since SQL Server start", view, StringComparison.Ordinal);
        Assert.Contains("It is not an interval delta", view, StringComparison.Ordinal);
        Assert.Contains("SQL text, query plans", view, StringComparison.Ordinal);
        Assert.Contains("GetHealthModulesAsync(cancellationToken)", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT ", view, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
