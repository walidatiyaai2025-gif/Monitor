using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800ReportTrancheAcceptanceTests
{
    private static readonly string Root = FindRoot();

    public static TheoryData<Type, string, string> ViewerReportRoutes => new()
    {
        { typeof(EnterpriseReportsController), nameof(EnterpriseReportsController.FleetDecisionSupport), "/reports/fleet-decision-support.csv" },
        { typeof(EnterpriseReportsController), nameof(EnterpriseReportsController.MaintenanceDecisionSupport), "/reports/maintenance-decision-support/{registrationId:guid}.csv" },
        { typeof(EnterpriseReportsController), nameof(EnterpriseReportsController.ServerIntelligence), "/reports/server-intelligence/{registrationId:guid}.csv" },
        { typeof(EnterpriseReportsController), nameof(EnterpriseReportsController.DatabaseHealth), "/reports/database-health/{registrationId:guid}.csv" },
        { typeof(EnterpriseReportsController), nameof(EnterpriseReportsController.MemoryHealth), "/reports/memory-health/{registrationId:guid}.csv" },
        { typeof(EnterpriseReportsController), nameof(EnterpriseReportsController.BackupHealth), "/reports/backup-health.csv" },
        { typeof(EnterpriseReportsController), nameof(EnterpriseReportsController.SqlAgentHealth), "/reports/sql-agent-health.csv" },
        { typeof(EnterpriseReportsController), nameof(EnterpriseReportsController.PerformanceHealth), "/reports/performance-health.csv" },
        { typeof(StorageHealthReportsController), nameof(StorageHealthReportsController.StorageHealth), "/reports/storage-health.csv" }
    };

    [Theory]
    [MemberData(nameof(ViewerReportRoutes))]
    public void CompletedReportTranche_ViewerRoutesKeepExactTemplatesAndReadPolicy(
        Type controllerType,
        string actionName,
        string expectedTemplate)
    {
        var classPolicies = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToArray();
        var action = controllerType.GetMethod(actionName);

        Assert.NotNull(action);
        Assert.Equal([MonitorPolicies.Read], classPolicies);
        Assert.Equal(
            expectedTemplate,
            action!.GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>().Single().Template);
        Assert.Empty(action.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
    }

    [Fact]
    public void AdministratorReportOverrides_RemainManageOnly()
    {
        var controllerType = typeof(EnterpriseReportsController);
        var cases = new[]
        {
            (nameof(EnterpriseReportsController.Audit), "/reports/audit.csv"),
            (nameof(EnterpriseReportsController.Manifest), "/diagnostics/manifest.json")
        };

        foreach (var (actionName, template) in cases)
        {
            var action = controllerType.GetMethod(actionName);
            Assert.NotNull(action);
            Assert.Equal(
                template,
                action!.GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>().Single().Template);
            Assert.Equal(
                [MonitorPolicies.Manage],
                action.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Select(attribute => attribute.Policy).ToArray());
        }
    }

    [Fact]
    public void ReportsPage_GlobalTrancheExportsAreDiscoverableExactlyOnceAndAdminEntriesStaySeparated()
    {
        var view = Read("src/Monitor.Web/Views/Portal/Reports.cshtml");
        var standard = Section(view, "<section class=\"report-section\" aria-labelledby=\"standard-reports-title\">", "</section>");
        var admin = Section(view, "<section class=\"report-section\" aria-labelledby=\"admin-reports-title\">", "</section>");

        foreach (var route in new[]
        {
            "/reports/fleet-decision-support.csv",
            "/reports/backup-health.csv",
            "/reports/sql-agent-health.csv",
            "/reports/performance-health.csv",
            "/reports/storage-health.csv"
        })
        {
            Assert.Equal(1, Count(standard, $"href=\"{route}\""));
            Assert.Equal(1, Count(view, $"href=\"{route}\""));
        }

        foreach (var token in new[]
        {
            "Cached server intelligence",
            "Cached database health summary",
            "Cached memory health summary",
            "Choose server for intelligence export",
            "Choose server for database export",
            "Choose server for memory export"
        })
        {
            Assert.Contains(token, standard, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("href=\"/reports/audit.csv\"", standard, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/diagnostics/manifest.json\"", standard, StringComparison.Ordinal);
        Assert.Equal(1, Count(admin, "href=\"/reports/audit.csv\""));
        Assert.Equal(1, Count(admin, "href=\"/diagnostics/manifest.json\""));
        Assert.Contains("Administrator access required", admin, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportTranche_WebAndServiceLayersDoNotAcquireDirectCollectionOrRefreshDependencies()
    {
        var enterpriseController = Read("src/Monitor.Web/Controllers/EnterpriseReportsController.cs");
        var storageController = Read("src/Monitor.Web/Controllers/StorageHealthReportsController.cs");
        var enterpriseService = Read("src/Monitor.Web/Services/EnterpriseReportingServices.cs");
        var storageService = Read("src/Monitor.Web/Services/StorageHealthSummaryExport.cs");
        var combined = string.Join('\n', enterpriseController, storageController, enterpriseService, storageService);

        foreach (var forbidden in new[]
        {
            "ISqlSnapshotQuery",
            "ISqlServerSnapshotCollector",
            "ISnapshotRefreshService",
            "SqlConnection"
        })
        {
            Assert.DoesNotContain(forbidden, combined, StringComparison.Ordinal);
        }

        Assert.Contains("cache.Peek(registration.Id)", enterpriseService, StringComparison.Ordinal);
        Assert.Contains("cache.Peek(registration.Id)", storageService, StringComparison.Ordinal);
        Assert.Contains("_monitoring.GetServerAsync", enterpriseController, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpPost", enterpriseController, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpPost", storageController, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsPage_StatesCompletedTrancheTruthBoundariesWithoutMutationClaims()
    {
        var view = Read("src/Monitor.Web/Views/Portal/Reports.cshtml");

        Assert.Contains("Opening this page never contacts a monitored SQL Server", view, StringComparison.Ordinal);
        Assert.Contains("RPO compliance remains NotEvaluated", view, StringComparison.Ordinal);
        Assert.Contains("schedule lateness remains NotEvaluated", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing wait evidence remains explicitly Unavailable", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Allocation is not disk capacity", view, StringComparison.Ordinal);
        Assert.Contains("missing I/O evidence remains explicitly Unavailable", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("method=\"post\"", view, StringComparison.OrdinalIgnoreCase);
    }

    private static int Count(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }

    private static string Section(string value, string startToken, string endToken)
    {
        var start = value.IndexOf(startToken, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Section start token not found: {startToken}");
        var end = value.IndexOf(endToken, start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Section end token not found after: {startToken}");
        return value[start..(end + endToken.Length)];
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
