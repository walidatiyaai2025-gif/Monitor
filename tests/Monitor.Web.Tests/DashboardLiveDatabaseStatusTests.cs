using Xunit;

namespace Monitor.Web.Tests;

public sealed class DashboardLiveDatabaseStatusTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void DashboardLiveDatabaseStatus_RefreshesOnlyCachedDashboardSurface()
    {
        var site = Read("src/Monitor.Web/wwwroot/js/site.js");
        var marker = "const DASHBOARD_DATABASE_LIVE_STORAGE_KEY";
        var start = site.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Dashboard live database status runtime is missing.");
        var feature = site[start..];

        Assert.Contains("fetch('/dashboard'", feature, StringComparison.Ordinal);
        Assert.Contains("credentials: 'same-origin'", feature, StringComparison.Ordinal);
        Assert.Contains("cache: 'no-store'", feature, StringComparison.Ordinal);
        Assert.Contains("'Accept': 'text/html'", feature, StringComparison.Ordinal);
        Assert.Contains("response.redirected", feature, StringComparison.Ordinal);
        Assert.Contains("contentType.includes('text/html')", feature, StringComparison.Ordinal);
        Assert.Contains("new DOMParser()", feature, StringComparison.Ordinal);
        Assert.Contains("document.hidden", feature, StringComparison.Ordinal);
        Assert.Contains("inFlight", feature, StringComparison.Ordinal);
        Assert.Contains("window.localStorage", feature, StringComparison.Ordinal);
        Assert.Contains("no direct SQL polling from the browser", feature, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-snapshot", feature, StringComparison.Ordinal);
        Assert.DoesNotContain("method: 'POST'", feature, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardLiveDatabaseStatus_ProvidesMotionAndReducedMotionFallback()
    {
        var css = Read("src/Monitor.Web/wwwroot/css/dashboard-live-status.css");

        Assert.Contains("database-live-pulse", css, StringComparison.Ordinal);
        Assert.Contains("database-live-sweep", css, StringComparison.Ordinal);
        Assert.Contains("database-live-confirm", css, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css, StringComparison.Ordinal);
        Assert.Contains("animation: none !important", css, StringComparison.Ordinal);
        Assert.Contains("transition: none !important", css, StringComparison.Ordinal);
        Assert.Contains("var(--text-primary, #e8f2fb)", css, StringComparison.Ordinal);
        Assert.DoesNotContain("color: initial;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardLiveDatabaseStatus_UsesBoundedMinuteChoices()
    {
        var site = Read("src/Monitor.Web/wwwroot/js/site.js");

        Assert.Contains("DASHBOARD_DATABASE_LIVE_INTERVALS = [1, 2, 5, 10, 15, 30]", site, StringComparison.Ordinal);
        Assert.Contains("DASHBOARD_DATABASE_LIVE_DEFAULT_MINUTES = 5", site, StringComparison.Ordinal);
        Assert.Contains("monitor.dashboardDatabaseLive.refreshMinutes", site, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
