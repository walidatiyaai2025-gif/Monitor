using Microsoft.AspNetCore.Authorization;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800ServerIntelligenceSelectionTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void ReportCenterSelection_IsViewerBoundedAndReusesExistingExportOwner()
    {
        var reportsController = typeof(EnterpriseReportsController);
        var classPolicy = reportsController
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Policy;

        var portalSource = Read("src/Monitor.Web/Controllers/PortalController.cs");
        var reportControllerSource = Read("src/Monitor.Web/Controllers/EnterpriseReportsController.cs");
        var viewSource = Read("src/Monitor.Web/Views/Portal/Reports.cshtml");

        Assert.Equal(MonitorPolicies.Read, classPolicy);
        Assert.Contains("GetServersPageAsync(0, 50, cancellationToken)", portalSource, StringComparison.Ordinal);
        Assert.Contains("Guid.TryParse(item.Id, out _)", portalSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetServersAsync(", portalSource, StringComparison.Ordinal);

        Assert.Contains("/reports/server-intelligence.csv", reportControllerSource, StringComparison.Ordinal);
        Assert.Contains("ServerIntelligence(registrationId, cancellationToken)", reportControllerSource, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"ServerIntelligenceSelection\"", viewSource, StringComparison.Ordinal);
        Assert.Contains("name=\"registrationId\"", viewSource, StringComparison.Ordinal);
        Assert.Contains("Model.TotalServers > Model.Servers.Count", viewSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectionWorkflow_RemainsCacheOnlyAndAddsNoRefreshOrSqlCollectorPath()
    {
        var portalSource = Read("src/Monitor.Web/Controllers/PortalController.cs");
        var reportControllerSource = Read("src/Monitor.Web/Controllers/EnterpriseReportsController.cs");
        var readServiceSource = Read("src/Monitor.Web/Services/MonitorReadService.cs");

        Assert.Contains("_monitoring.GetServerAsync", reportControllerSource, StringComparison.Ordinal);
        Assert.Contains("cache.Peek(registration.Id)", readServiceSource, StringComparison.Ordinal);

        Assert.DoesNotContain("ISqlSnapshotQuery", portalSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", portalSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISnapshotRefreshService", portalSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", reportControllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", reportControllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISnapshotRefreshService", reportControllerSource, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
