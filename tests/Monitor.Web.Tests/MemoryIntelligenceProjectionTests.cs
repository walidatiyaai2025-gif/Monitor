using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class MemoryIntelligenceProjectionTests
{
    [Fact]
    public void Build_UsesCollectedPressureSignalsWithoutAutomaticRemediation()
    {
        var memory = new MemoryHealthSnapshot(
            64_000_000, 6_000_000, 30_000_000, 91, false, false, "Available physical memory is high",
            49_152, 38_000_000, 42_000_000, 6_000, 3, "MEMORYCLERK_SQLBUFFERPOOL", 18_000_000);

        var result = MemoryIntelligenceProjection.Build(memory);

        Assert.Equal("warning", result.State);
        Assert.True(result.NeedsAttention);
        Assert.Equal(90, result.TargetAttainmentPercent);
        Assert.Contains("Memory grants are pending", result.Recommendation, StringComparison.Ordinal);
        Assert.Contains("MEMORYCLERK_SQLBUFFERPOOL", result.TopMemoryClerkLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("execute", result.Recommendation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_MissingEvidenceStaysUnknownInsteadOfHealthyZero()
    {
        var result = MemoryIntelligenceProjection.Build(null);

        Assert.Equal("unknown", result.State);
        Assert.False(result.NeedsAttention);
        Assert.Null(result.TargetAttainmentPercent);
        Assert.Equal("Not collected", result.TopMemoryClerkLabel);
        Assert.Contains("do not infer zero pressure", result.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_LowMemoryFlagsTakePriority()
    {
        var memory = new MemoryHealthSnapshot(
            64_000_000, 500_000, 40_000_000, 70, true, false, "Available physical memory is low",
            49_152, 40_000_000, 40_000_000, 10_000, 0, "MEMORYCLERK_SQLBUFFERPOOL", 20_000_000);

        var result = MemoryIntelligenceProjection.Build(memory);

        Assert.Equal("critical", result.State);
        Assert.Contains("low-memory pressure", result.Recommendation, StringComparison.Ordinal);
        Assert.Contains("do not apply an automatic configuration change", result.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public void MemoryHealthView_IsCacheOnlyDrillableAndHasNoPlannedDiagnostics()
    {
        var root = FindRoot();
        var view = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Views/Operations/MemoryHealth.cshtml"));
        var controller = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Controllers/OperationsController.cs"));

        Assert.Contains("@model HealthModulePageViewModel", view, StringComparison.Ordinal);
        Assert.Contains("_HealthSourceBadge", view, StringComparison.Ordinal);
        Assert.Contains("ServerDetails", view, StringComparison.Ordinal);
        Assert.Contains("Max server memory", view, StringComparison.Ordinal);
        Assert.Contains("Total / Target Server Memory", view, StringComparison.Ordinal);
        Assert.Contains("Page life expectancy", view, StringComparison.Ordinal);
        Assert.Contains("Memory grants pending", view, StringComparison.Ordinal);
        Assert.Contains("Dominant memory clerk", view, StringComparison.Ordinal);
        Assert.Contains("DBA RECOMMENDATION", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Planned", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqlConnection", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT ", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Memory Health\", \"Cached SQL/OS memory evidence", controller, StringComparison.Ordinal);
        Assert.Contains("GetHealthModulesAsync(cancellationToken)", controller, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
