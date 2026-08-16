using Xunit;

namespace Monitor.Web.Tests;

public sealed class RoadmapCurrentStateTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Roadmap_DoesNotRegressToHistoricalBatchPlanningState()
    {
        var roadmap = Read("docs/ROADMAP.md");

        Assert.DoesNotContain("60/100 CI VERIFIED", roadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("Batch 7 / B100-061..070", roadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("Batches 8–10 / B100-071..100", roadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("— NEXT", roadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("— PLANNED", roadmap, StringComparison.Ordinal);

        Assert.Contains("BATCH-100", roadmap, StringComparison.Ordinal);
        Assert.Contains("100/100 COMPLETE", roadmap, StringComparison.Ordinal);
        Assert.Contains("BATCH-700", roadmap, StringComparison.Ordinal);
        Assert.Contains("50/50 COMPLETE", roadmap, StringComparison.Ordinal);
        Assert.Contains("660", roadmap, StringComparison.Ordinal);
    }

    [Fact]
    public void Roadmap_PreservesCurrentP05AndSafetyBoundaries()
    {
        var roadmap = Read("docs/ROADMAP.md");

        Assert.Contains("P0.1 through P0.4 are COMPLETE", roadmap, StringComparison.Ordinal);
        Assert.Contains("complete through PR #219", roadmap, StringComparison.Ordinal);
        Assert.Contains("#162 — RC.61 durable retention", roadmap, StringComparison.Ordinal);
        Assert.Contains("verify-durable-release", roadmap, StringComparison.Ordinal);
        Assert.Contains("#116 — real Windows/IIS acceptance", roadmap, StringComparison.Ordinal);
        Assert.Contains("#111 — umbrella closure", roadmap, StringComparison.Ordinal);
        Assert.Contains("SingleNode", roadmap, StringComparison.Ordinal);
        Assert.Contains("MultiNode", roadmap, StringComparison.Ordinal);
        Assert.Contains("No autonomous remediation or AI-generated SQL execution", roadmap, StringComparison.Ordinal);
        Assert.Contains("GETs remain cache/control-plane only", roadmap, StringComparison.Ordinal);
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Root, relative)).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
