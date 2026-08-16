using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B700RouteSmokeTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void VisibleOperatorRoutes_AreDeclaredAndBackedByViews()
    {
        var expected = new (Type Controller, string Route, string View)[]
        {
            (typeof(OperationsController), "/dashboard", "src/Monitor.Web/Views/Operations/Dashboard.cshtml"),
            (typeof(OperationsController), "/servers", "src/Monitor.Web/Views/Operations/Servers.cshtml"),
            (typeof(OperationsController), "/servers/{id}", "src/Monitor.Web/Views/Operations/ServerDetails.cshtml"),
            (typeof(OperationsController), "/alerts", "src/Monitor.Web/Views/Operations/Alerts.cshtml"),
            (typeof(OperationsController), "/database-health", "src/Monitor.Web/Views/Operations/DatabaseHealth.cshtml"),
            (typeof(OperationsController), "/memory-health", "src/Monitor.Web/Views/Operations/MemoryHealth.cshtml"),
            (typeof(PortalController), "/performance-health", "src/Monitor.Web/Views/Portal/Performance.cshtml"),
            (typeof(OperationsController), "/backups", "src/Monitor.Web/Views/Operations/Backups.cshtml"),
            (typeof(OperationsController), "/jobs", "src/Monitor.Web/Views/Operations/Jobs.cshtml"),
            (typeof(OperationsController), "/storage", "src/Monitor.Web/Views/Operations/Storage.cshtml"),
            (typeof(OperationsController), "/blocking", "src/Monitor.Web/Views/Operations/Blocking.cshtml"),
            (typeof(FleetIntelligenceController), "/enterprise/fleet", "src/Monitor.Web/Views/FleetIntelligence/Index.cshtml"),
            (typeof(EnterpriseOperationsController), "/enterprise", "src/Monitor.Web/Views/EnterpriseOperations/Overview.cshtml"),
            (typeof(PortalController), "/recommendations", "src/Monitor.Web/Views/Portal/Recommendations.cshtml"),
            (typeof(PortalController), "/reports", "src/Monitor.Web/Views/Portal/Reports.cshtml"),
            (typeof(ConnectionLabController), "/servers/connections", "src/Monitor.Web/Views/ConnectionLab/Index.cshtml"),
            (typeof(ObservabilityController), "/observability", "src/Monitor.Web/Views/Observability/Index.cshtml"),
            (typeof(OperationsController), "/audit", "src/Monitor.Web/Views/Operations/Audit.cshtml"),
            (typeof(EnterpriseHelpController), "/enterprise/readiness", "src/Monitor.Web/Views/EnterpriseHelp/Readiness.cshtml"),
            (typeof(OperationsController), "/settings", "src/Monitor.Web/Views/Operations/Settings.cshtml"),
            (typeof(EnterpriseHelpController), "/enterprise/help", "src/Monitor.Web/Views/EnterpriseHelp/Help.cshtml"),
            (typeof(GovernanceController), "/governance/retention", "src/Monitor.Web/Views/Governance/Index.cshtml")
        };

        foreach (var item in expected)
        {
            var routes = item.Controller.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SelectMany(method => method.GetCustomAttributes<HttpGetAttribute>())
                .Select(attribute => attribute.Template)
                .Where(template => template is not null)
                .ToArray();

            Assert.Contains(item.Route, routes);
            Assert.True(File.Exists(Path.Combine(Root, item.View)), $"Visible route {item.Route} is missing view {item.View}.");
        }
    }

    [Fact]
    public void SpecializedHealthRoutes_DoNotFallBackToGenericHealthModulesView()
    {
        var controller = Read("src/Monitor.Web/Controllers/OperationsController.cs");
        foreach (var action in new[] { "DatabaseHealth", "Backups", "Jobs", "Storage", "Blocking" })
            Assert.Contains($"IActionResult> {action}", controller, StringComparison.Ordinal);

        Assert.DoesNotContain("View(\"HealthModules\"", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void VisibleNavigation_HasNoPlaceholderLinksAndIncludesCoreDestinations()
    {
        var layout = Read("src/Monitor.Web/Views/Shared/_Layout.cshtml");

        Assert.DoesNotContain("href=\"#\"", layout, StringComparison.Ordinal);
        foreach (var label in new[]
        {
            "Command Center", "Servers", "Alerts & Incidents", "Database Health", "Memory Health",
            "Performance", "Backups", "SQL Agent", "Storage", "Blocking", "Fleet Intelligence",
            "Enterprise Operations", "Recommendations", "Reports", "Connections", "Observability",
            "Audit Trail", "Readiness", "Settings", "Operator Help"
        })
            Assert.Contains(label, layout, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
