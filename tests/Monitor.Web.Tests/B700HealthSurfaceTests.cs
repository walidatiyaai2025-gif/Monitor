using Xunit;

namespace Monitor.Web.Tests;

public sealed class B700HealthSurfaceTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void HealthRoutes_UseDedicatedViewsInsteadOfGenericHealthModules()
    {
        var controller = Read("src/Monitor.Web/Controllers/OperationsController.cs");

        Assert.Contains("IActionResult> DatabaseHealth", controller, StringComparison.Ordinal);
        Assert.Contains("IActionResult> Backups", controller, StringComparison.Ordinal);
        Assert.Contains("IActionResult> Jobs", controller, StringComparison.Ordinal);
        Assert.Contains("IActionResult> Storage", controller, StringComparison.Ordinal);
        Assert.Contains("IActionResult> Blocking", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("View(\"HealthModules\"", controller, StringComparison.Ordinal);

        foreach (var view in new[] { "DatabaseHealth.cshtml", "Backups.cshtml", "Jobs.cshtml", "Storage.cshtml", "Blocking.cshtml" })
            Assert.True(File.Exists(Path.Combine(Root, "src/Monitor.Web/Views/Operations", view)), $"Dedicated health view missing: {view}");
    }

    [Fact]
    public void DedicatedHealthViews_AreCacheOnlyTruthfulAndDrillable()
    {
        foreach (var relative in new[]
        {
            "src/Monitor.Web/Views/Operations/DatabaseHealth.cshtml",
            "src/Monitor.Web/Views/Operations/Backups.cshtml",
            "src/Monitor.Web/Views/Operations/Jobs.cshtml",
            "src/Monitor.Web/Views/Operations/Storage.cshtml",
            "src/Monitor.Web/Views/Operations/Blocking.cshtml",
            "src/Monitor.Web/Views/Portal/Performance.cshtml"
        })
        {
            var view = Read(relative);
            Assert.Contains("Not collected", view, StringComparison.Ordinal);
            Assert.Contains("_HealthSourceBadge", view, StringComparison.Ordinal);
            Assert.Contains("ServerDetails", view, StringComparison.Ordinal);
            Assert.DoesNotContain("SqlConnection", view, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SELECT ", view, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void HealthActions_ReadSharedHealthModulesOnly()
    {
        var controller = Read("src/Monitor.Web/Controllers/OperationsController.cs");
        var portal = Read("src/Monitor.Web/Controllers/PortalController.cs");

        Assert.Contains("GetHealthModulesAsync", controller, StringComparison.Ordinal);
        Assert.Contains("GetHealthModulesAsync", portal, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", portal, StringComparison.Ordinal);
    }

    [Fact]
    public void HealthSourceAndResponsiveContracts_AreShared()
    {
        var sourceBadge = Read("src/Monitor.Web/Views/Shared/_HealthSourceBadge.cshtml");
        var css = Read("src/Monitor.Web/wwwroot/css/portal.css");

        Assert.Contains("LiveFresh", sourceBadge, StringComparison.Ordinal);
        Assert.Contains("LiveStale", sourceBadge, StringComparison.Ordinal);
        Assert.Contains("RegisteredUnavailable", sourceBadge, StringComparison.Ordinal);
        Assert.Contains("No cached snapshot", sourceBadge, StringComparison.Ordinal);
        Assert.Contains(".health-source-badge", css, StringComparison.Ordinal);
        Assert.Contains(".health-detail-grid", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 520px)", css, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
