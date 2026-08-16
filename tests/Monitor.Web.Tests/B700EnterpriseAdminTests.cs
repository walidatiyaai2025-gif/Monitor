using Xunit;

namespace Monitor.Web.Tests;

public sealed class B700EnterpriseAdminTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void ReadinessAndHelp_AreTaskOrientedAndRoleAware()
    {
        var readiness = Read("src/Monitor.Web/Views/EnterpriseHelp/Readiness.cshtml");
        var help = Read("src/Monitor.Web/Views/EnterpriseHelp/Help.cshtml");

        Assert.Contains("OPERATOR CHECKLIST", readiness, StringComparison.Ordinal);
        Assert.Contains("readiness-steps", readiness, StringComparison.Ordinal);
        Assert.Contains("User.IsInRole(MonitorRoles.Administrator)", readiness, StringComparison.Ordinal);
        Assert.Contains("ConnectionLab", readiness, StringComparison.Ordinal);
        Assert.Contains("Observability", readiness, StringComparison.Ordinal);
        Assert.Contains("_PortalState", readiness, StringComparison.Ordinal);

        Assert.Contains("OPERATOR RUNBOOKS", help, StringComparison.Ordinal);
        Assert.Contains("runbook-grid", help, StringComparison.Ordinal);
        Assert.Contains("Register a SQL target", help, StringComparison.Ordinal);
        Assert.Contains("Triage operational evidence", help, StringComparison.Ordinal);
        Assert.Contains("Protect operational state", help, StringComparison.Ordinal);
        Assert.Contains("Dry-run governance cleanup", help, StringComparison.Ordinal);
        Assert.Contains("SQL changes stay outside Monitor", help, StringComparison.Ordinal);
    }

    [Fact]
    public void Governance_PreservesManagePostAntiforgeryAndExplainsDestructiveImpact()
    {
        var controller = Read("src/Monitor.Web/Controllers/GovernanceController.cs");
        var view = Read("src/Monitor.Web/Views/Governance/Index.cshtml");

        Assert.Contains("[Authorize(Policy = MonitorPolicies.Manage)]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"/governance/retention/apply\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[ValidateAntiForgeryToken]", controller, StringComparison.Ordinal);
        Assert.Contains("bounded governance prune receipt", controller, StringComparison.Ordinal);

        Assert.Contains("DRY RUN", view, StringComparison.Ordinal);
        Assert.Contains("MUTATING ACTION", view, StringComparison.Ordinal);
        Assert.Contains("Audit receipt", view, StringComparison.Ordinal);
        Assert.Contains("destructive metadata-retention operation", view, StringComparison.Ordinal);
        Assert.Contains("Model.Candidates.Take(100)", view, StringComparison.Ordinal);
        Assert.Contains("method=\"post\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void ObservabilityAndSettings_ExposeSourceStateAndGroupedInformationArchitecture()
    {
        var healthController = Read("src/Monitor.Web/Controllers/HealthController.cs");
        var operationsController = Read("src/Monitor.Web/Controllers/OperationsController.cs");
        var observability = Read("src/Monitor.Web/Views/Observability/Index.cshtml");
        var settings = Read("src/Monitor.Web/Views/Operations/Settings.cshtml");

        Assert.Contains("[Authorize(Policy = MonitorPolicies.Manage)]", healthController, StringComparison.Ordinal);
        Assert.Contains("public sealed class ObservabilityController", healthController, StringComparison.Ordinal);
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

        Assert.Contains("[Authorize(Roles = \"Administrator\")]", controller, StringComparison.Ordinal);
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

        Assert.Contains("asp-route-environment=\"@bucket.Key\"", fleet, StringComparison.Ordinal);
        Assert.Contains("asp-route-group=\"@bucket.Key\"", fleet, StringComparison.Ordinal);
        Assert.Contains("asp-route-tag=\"@bucket.Key\"", fleet, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Backups\"", fleet, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Blocking\"", fleet, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Performance\"", fleet, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Alerts\"", fleet, StringComparison.Ordinal);
        Assert.Contains("asp-route-ruleId=\"@hotspot.RuleId\"", fleet, StringComparison.Ordinal);

        Assert.Contains("var canOperate = User.IsInRole(MonitorRoles.Operator) || User.IsInRole(MonitorRoles.Administrator)", enterprise, StringComparison.Ordinal);
        Assert.Contains("var canManage = User.IsInRole(MonitorRoles.Administrator)", enterprise, StringComparison.Ordinal);
        Assert.Contains("@if (canManage)", enterprise, StringComparison.Ordinal);
        Assert.Contains("@if (canOperate)", enterprise, StringComparison.Ordinal);
        Assert.Contains("User.IsInRole(Monitor.Web.Services.MonitorRoles.Administrator)", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminCompletion_DefinesKeyboardResponsiveAnd390Contracts()
    {
        var layout = Read("src/Monitor.Web/Views/Shared/_Layout.cshtml");
        var css = Read("src/Monitor.Web/wwwroot/css/b700-admin.css");

        Assert.Contains("~/css/b700-admin.css", layout, StringComparison.Ordinal);
        Assert.Contains(":focus-visible", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 1000px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 760px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 520px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 390px)", css, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", css, StringComparison.Ordinal);
        Assert.Contains(".fleet-hotspot-grid", css, StringComparison.Ordinal);
        Assert.Contains(".governance-detail-grid", css, StringComparison.Ordinal);
    }

    [Fact]
    public void EnterpriseAdminViews_DoNotIntroduceMonitoredSqlExecution()
    {
        foreach (var relative in new[]
        {
            "src/Monitor.Web/Views/EnterpriseHelp/Readiness.cshtml",
            "src/Monitor.Web/Views/EnterpriseHelp/Help.cshtml",
            "src/Monitor.Web/Views/Governance/Index.cshtml",
            "src/Monitor.Web/Views/Observability/Index.cshtml",
            "src/Monitor.Web/Views/Operations/Settings.cshtml",
            "src/Monitor.Web/Views/FleetIntelligence/Index.cshtml"
        })
        {
            var source = Read(relative);
            Assert.DoesNotContain("SqlConnection", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SELECT ", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ISqlSnapshotQuery", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ISqlServerSnapshotCollector", source, StringComparison.Ordinal);
        }
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
