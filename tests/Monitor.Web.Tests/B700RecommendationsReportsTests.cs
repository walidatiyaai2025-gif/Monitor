using Microsoft.AspNetCore.Authorization;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B700RecommendationsReportsTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Recommendations_AreBoundedNormalizedAndFilterable()
    {
        var controller = Read("src/Monitor.Web/Controllers/PortalController.cs");

        Assert.Contains("SecurityInput.NormalizeOptionalToken(ruleId, 80)", controller, StringComparison.Ordinal);
        Assert.Contains(".Take(100)", controller, StringComparison.Ordinal);
        Assert.Contains("FindingSeverity? severity", controller, StringComparison.Ordinal);
        Assert.Contains("string.Equals(item.Incident.RuleId, normalizedRuleId", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void RecommendationsSurface_HasSummarySemanticGuidanceAndEvidenceDrilldowns()
    {
        var view = Read("src/Monitor.Web/Views/Portal/Recommendations.cshtml");

        Assert.Contains("BOUNDED ACTIVE GUIDANCE", view, StringComparison.Ordinal);
        Assert.Contains("name=\"severity\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"ruleId\"", view, StringComparison.Ordinal);
        Assert.Contains("maxlength=\"80\"", view, StringComparison.Ordinal);
        Assert.Contains("<ol class=\"recommendation-steps\"", view, StringComparison.Ordinal);
        Assert.Contains("Risk / caution", view, StringComparison.Ordinal);
        Assert.Contains("IncidentDetails", view, StringComparison.Ordinal);
        Assert.Contains("ServerDetails", view, StringComparison.Ordinal);
        Assert.Contains("_PortalState", view, StringComparison.Ordinal);
        Assert.DoesNotContain("execute recommendation", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReportCenter_ListsEveryGlobalExportAndContextualHistoryPath()
    {
        var view = Read("src/Monitor.Web/Views/Portal/Reports.cshtml");
        var history = Read("src/Monitor.Web/Views/Operations/History.cshtml");

        foreach (var route in new[]
        {
            "/reports/servers-v2.csv",
            "/reports/servers.csv",
            "/reports/incidents.csv",
            "/reports/audit.csv",
            "/diagnostics/package",
            "/diagnostics/manifest.json"
        })
            Assert.Contains(route, view, StringComparison.Ordinal);

        Assert.Contains("asp-controller=\"EnterpriseReports\"", history, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"History\"", history, StringComparison.Ordinal);
        Assert.Contains("1h, 6h and 24h", history, StringComparison.Ordinal);
        Assert.Contains("DISCOVERABILITY", view, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportCenter_ExplainsFormatsAccessRedactionAndSafeFailures()
    {
        var view = Read("src/Monitor.Web/Views/Portal/Reports.cshtml");

        Assert.Contains("SAFE DOWNLOAD BEHAVIOR", view, StringComparison.Ordinal);
        Assert.Contains("CSV · v2", view, StringComparison.Ordinal);
        Assert.Contains("CSV · v1", view, StringComparison.Ordinal);
        Assert.Contains("ZIP · bounded", view, StringComparison.Ordinal);
        Assert.Contains("JSON · v1", view, StringComparison.Ordinal);
        Assert.Contains("Administrator access required", view, StringComparison.Ordinal);
        Assert.Contains("raw provider errors", view, StringComparison.Ordinal);
        Assert.Contains("aria-label=", view, StringComparison.Ordinal);
        Assert.Contains("User.IsInRole(MonitorRoles.Administrator)", view, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportEndpoints_PreserveReadAndManagePolicyBoundaries()
    {
        var enterpriseReports = typeof(EnterpriseReportsController);
        var enterpriseOperations = typeof(EnterpriseOperationsController);

        var reportsClassPolicy = enterpriseReports.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy;
        var operationsClassPolicy = enterpriseOperations.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy;
        Assert.Equal(MonitorPolicies.Read, reportsClassPolicy);
        Assert.Equal(MonitorPolicies.Read, operationsClassPolicy);

        var auditPolicy = enterpriseReports.GetMethod(nameof(EnterpriseReportsController.Audit))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy;
        var manifestPolicy = enterpriseReports.GetMethod(nameof(EnterpriseReportsController.Manifest))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy;
        var diagnosticsPolicy = enterpriseOperations.GetMethod(nameof(EnterpriseOperationsController.Diagnostics))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy;

        Assert.Equal(MonitorPolicies.Manage, auditPolicy);
        Assert.Equal(MonitorPolicies.Manage, manifestPolicy);
        Assert.Equal(MonitorPolicies.Manage, diagnosticsPolicy);
    }

    [Fact]
    public void RecommendationAndReportResponsiveContracts_AreDefined()
    {
        var css = Read("src/Monitor.Web/wwwroot/css/portal.css");

        Assert.Contains(".recommendation-filter-grid", css, StringComparison.Ordinal);
        Assert.Contains(".recommendation-grid", css, StringComparison.Ordinal);
        Assert.Contains(".recommendation-steps", css, StringComparison.Ordinal);
        Assert.Contains(".report-grid", css, StringComparison.Ordinal);
        Assert.Contains(".report-metadata", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 620px)", css, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
