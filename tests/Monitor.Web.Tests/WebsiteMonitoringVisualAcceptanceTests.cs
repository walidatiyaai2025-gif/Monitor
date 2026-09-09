using Xunit;

namespace Monitor.Web.Tests;

public sealed class WebsiteMonitoringVisualAcceptanceTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void WebsiteManagementSurface_HasTruthfulResponsiveAccessibleOperatorContracts()
    {
        var view = Read("src/Monitor.Web/Views/WebsiteMonitoring/Index.cshtml");
        var layout = Read("src/Monitor.Web/Views/Shared/_Layout.cshtml");

        Assert.Contains("Website Monitoring", view, StringComparison.Ordinal);
        Assert.Contains("RUNTIME SAFETY", view, StringComparison.Ordinal);
        Assert.Contains("CURRENT EVIDENCE", view, StringComparison.Ordinal);
        Assert.Contains("24-hour known availability excludes Unknown checks", view, StringComparison.Ordinal);
        Assert.Contains("class=\"table-scroll\"", view, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", view, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", view, StringComparison.Ordinal);
        Assert.Contains("@Html.AntiForgeryToken()", view, StringComparison.Ordinal);
        Assert.Contains("disabled=\"@(!Model.MonitoringEnabled)\"", view, StringComparison.Ordinal);
        Assert.Contains("No SMTP password is stored here", view, StringComparison.Ordinal);
        Assert.Contains("correlation never proves root cause by itself", view, StringComparison.Ordinal);
        Assert.Contains("User.IsInRole(MonitorRoles.Administrator)", view, StringComparison.Ordinal);
        Assert.Contains("User.IsInRole(MonitorRoles.Operator)", view, StringComparison.Ordinal);
        Assert.Contains("WebsiteMonitoring", layout, StringComparison.Ordinal);

        Assert.DoesNotContain("SqlConnection", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IWebsiteProbeEngine", view, StringComparison.Ordinal);
    }

    [Fact]
    public void WebsiteLiveSurface_IsCachedReadOnlyResponsiveAndMotionSafe()
    {
        var view = Read("src/Monitor.Web/Views/WebsiteMonitoringLive/Index.cshtml");
        var css = Read("src/Monitor.Web/wwwroot/css/website-monitoring.css");
        var js = Read("src/Monitor.Web/wwwroot/js/website-monitoring-live.js");

        Assert.Contains("Live cached evidence", view, StringComparison.Ordinal);
        Assert.Contains("dashboard never starts a probe", view, StringComparison.Ordinal);
        Assert.Contains("data-website-live", view, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", view, StringComparison.Ordinal);
        Assert.Contains("class=\"table-scroll\"", view, StringComparison.Ordinal);
        Assert.Contains("Unknown", view, StringComparison.Ordinal);
        Assert.Contains("No incident is invented before its configured threshold", view, StringComparison.Ordinal);

        Assert.Contains("prefers-reduced-motion:reduce", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width:1180px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width:760px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width:520px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width:390px)", css, StringComparison.Ordinal);

        Assert.Contains("document.visibilityState === 'visible'", js, StringComparison.Ordinal);
        Assert.Contains("window.location.reload()", js, StringComparison.Ordinal);
        Assert.DoesNotContain("fetch(", js, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("XMLHttpRequest", js, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CheckNow", js, StringComparison.Ordinal);
        Assert.DoesNotContain("probe", js, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Wm7BrowserEvidence_UsesRealLoginScreenshotsAndFailClosedDefaults()
    {
        var workflow = Read(".github/workflows/website-monitoring-visual.yml");
        var browser = Read("scripts/Test-WebsiteMonitoringVisual.cjs");

        Assert.Contains("authenticated-browser-evidence", workflow, StringComparison.Ordinal);
        Assert.Contains("WebsiteMonitoring__Enabled=false", workflow, StringComparison.Ordinal);
        Assert.Contains("WebsiteNotifications__Enabled=false", workflow, StringComparison.Ordinal);
        Assert.Contains("playwright@1.63.0", workflow, StringComparison.Ordinal);
        Assert.Contains("npm audit --audit-level=high", workflow, StringComparison.Ordinal);
        Assert.Contains("npx playwright install chromium", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("playwright install --with-deps", workflow, StringComparison.Ordinal);
        Assert.Contains("--no-launch-profile", workflow, StringComparison.Ordinal);
        Assert.Contains("npm-audit.txt", workflow, StringComparison.Ordinal);
        Assert.Contains("playwright-install.txt", workflow, StringComparison.Ordinal);
        Assert.Contains("Upload WM7 browser evidence", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("WebsiteMonitoring__Enabled=true", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("WebsiteNotifications__Enabled=true", workflow, StringComparison.Ordinal);

        Assert.Contains("/login", browser, StringComparison.Ordinal);
        Assert.Contains("/websites", browser, StringComparison.Ordinal);
        Assert.Contains("width: 1440", browser, StringComparison.Ordinal);
        Assert.Contains("width: 390", browser, StringComparison.Ordinal);
        Assert.Contains("reducedMotion: 'reduce'", browser, StringComparison.Ordinal);
        Assert.Contains("screenshot", browser, StringComparison.Ordinal);
        Assert.Contains("Website Monitoring must remain disabled", browser, StringComparison.Ordinal);
        Assert.Contains("Check now must be disabled", browser, StringComparison.Ordinal);
        Assert.DoesNotContain("/websites/{id:guid}/check", browser, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}