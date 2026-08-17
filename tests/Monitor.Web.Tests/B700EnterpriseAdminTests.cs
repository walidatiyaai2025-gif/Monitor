using Xunit;

namespace Monitor.Web.Tests;

public sealed class B700EnterpriseAdminTests
{
    [Fact]
    public void EnterpriseAdminCss_IsLoadedFromLayout()
    {
        var layout = Read("src/Monitor.Web/Views/Shared/_Layout.cshtml");
        var css = Read("src/Monitor.Web/wwwroot/css/b700-admin.css");

        Assert.Contains("~/css/b700-admin.css", layout, StringComparison.Ordinal);
        Assert.Contains(".admin-hero", css, StringComparison.Ordinal);
        Assert.Contains(".admin-action-grid", css, StringComparison.Ordinal);
        Assert.Contains(".admin-quick-link", css, StringComparison.Ordinal);
        Assert.Contains(".admin-state-card", css, StringComparison.Ordinal);
        Assert.Contains(".admin-status-banner", css, StringComparison.Ordinal);
    }

    [Fact]
    public void AuditAndHistory_HaveEnterpriseEmptyAndBoundaryStates()
    {
        var audit = Read("src/Monitor.Web/Views/Operations/Audit.cshtml");
        var history = Read("src/Monitor.Web/Views/Operations/History.cshtml");

        Assert.Contains("No audit entries", audit, StringComparison.Ordinal);
        Assert.Contains("Audit entries appear after authenticated operators use protected workflows", audit, StringComparison.Ordinal);
        Assert.Contains("Open Settings", audit, StringComparison.Ordinal);
        Assert.Contains("Open connections", audit, StringComparison.Ordinal);
        Assert.Contains("AUDIT RETENTION", audit, StringComparison.Ordinal);

        Assert.Contains("No snapshot history", history, StringComparison.Ordinal);
        Assert.Contains("Open servers", history, StringComparison.Ordinal);
        Assert.Contains("Server source", history, StringComparison.Ordinal);
        Assert.Contains("Cached snapshot chronology", history, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_HaveEnterpriseExportBoundaries()
    {
        var reports = Read("src/Monitor.Web/Views/Operations/Reports.cshtml");

        Assert.Contains("REPORT CENTER", reports, StringComparison.Ordinal);
        Assert.Contains("EXPORT BOUNDARY", reports, StringComparison.Ordinal);
        Assert.Contains("Safe CSV", reports, StringComparison.Ordinal);
        Assert.Contains("Redacted diagnostics", reports, StringComparison.Ordinal);
        Assert.Contains("No SQL text", reports, StringComparison.Ordinal);
        Assert.Contains("No connection strings", reports, StringComparison.Ordinal);
    }

    [Fact]
    public void ObservabilityAndSettings_HaveReadinessAndAdminStateSurfaces()
    {
        var observability = Read("src/Monitor.Web/Views/Portal/Observability.cshtml");
        var settings = Read("src/Monitor.Web/Views/Operations/Settings.cshtml");
        var operationsController = Read("src/Monitor.Web/Controllers/OperationsController.cs");

        Assert.Contains("[Authorize(Policy = MonitorPolicies.Manage)]", operationsController, StringComparison.Ordinal);
        Assert.Contains("Task<IActionResult> Settings", operationsController, StringComparison.Ordinal);

        Assert.Contains("CHECKED UTC", observability, StringComparison.Ordinal);
        Assert.Contains("MONITORED SQL", observability, StringComparison.Ordinal);
        Assert.Contains("NOT QUERIED", observability, StringComparison.Ordinal);
        Assert.Contains("_PortalState", observability, StringComparison.Ordinal);
        Assert.Contains("Not collected", observability, StringComparison.Ordinal);

        Assert.Contains("settings-jump-nav", settings, StringComparison.Ordinal);
        Assert.Contains("settings-deployment", settings, StringComparison.Ordinal);
        Assert.Contains("settings-state", settings, StringComparison.Ordinal);
        Assert.Contains("settings-credentials", settings, StringComparison.Ordinal);
        Assert.Contains("settings-backup", settings, StringComparison.Ordinal);
        Assert.Contains("settings-runtime", settings, StringComparison.Ordinal);
        Assert.Contains("settings-security", settings, StringComparison.Ordinal);
        Assert.Contains("Dry-run validate", settings, StringComparison.Ordinal);
        Assert.Contains("type RESTORE", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectionLab_AlreadyHasCompleteStateSurfacesAndAdminBoundary()
    {
        var controller = Read("src/Monitor.Web/Controllers/ConnectionLabController.cs");
        var view = Read("src/Monitor.Web/Views/ConnectionLab/Index.cshtml");
        var css = Read("src/Monitor.Web/wwwroot/css/b700-admin.css");

        Assert.Contains("[Authorize(Policy = MonitorPolicies.Manage)]", controller, StringComparison.Ordinal);
        Assert.Contains("[ValidateAntiForgeryToken]", controller, StringComparison.Ordinal);
        Assert.Contains("lab-success", view, StringComparison.Ordinal);
        Assert.Contains("asp-validation-summary=\"ModelOnly\"", view, StringComparison.Ordinal);
        Assert.Contains("Local SQL credential entry disabled", view, StringComparison.Ordinal);
        Assert.Contains("No real SQL target registered yet", view, StringComparison.Ordinal);
        Assert.Contains("disabled=\"@(!registration.IsEnabled)\"", view, StringComparison.Ordinal);
        Assert.Contains("lab-result state-@resultState", view, StringComparison.Ordinal);
        Assert.Contains(".lab-validation:not(:empty)", css, StringComparison.Ordinal);
        Assert.Contains("button:disabled", css, StringComparison.Ordinal);
    }

    [Fact]
    public void FleetAndEnterprise_HaveDrilldownsAndRoleAwareActions()
    {
        var fleet = Read("src/Monitor.Web/Views/FleetIntelligence/Index.cshtml");
        var enterprise = Read("src/Monitor.Web/Views/EnterpriseOperations/Overview.cshtml");
        var layout = Read("src/Monitor.Web/Views/Shared/_Layout.cshtml");

        Assert.Contains("asp-controller=\"Operations\" asp-action=\"ServerDetails\"", fleet, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"Operations\" asp-action=\"ServerDetails\"", enterprise, StringComparison.Ordinal);
        Assert.Contains("MonitorRoles.Administrator", enterprise, StringComparison.Ordinal);
        Assert.Contains("MonitorRoles.Operator", enterprise, StringComparison.Ordinal);
        Assert.Contains("Fleet Intelligence", layout, StringComparison.Ordinal);
        Assert.Contains("Enterprise Operations", layout, StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
    {
        var root = FindRoot();
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
