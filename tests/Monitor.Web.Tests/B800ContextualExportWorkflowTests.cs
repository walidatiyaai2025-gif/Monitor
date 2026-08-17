using Microsoft.AspNetCore.Authorization;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800ContextualExportWorkflowTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void ServerDetails_OffersExistingContextualExportsOnlyForRegisteredGuidContext()
    {
        var view = Read("src/Monitor.Web/Views/Operations/ServerDetails.cshtml");

        Assert.Contains("Guid.TryParse(server.Id, out var exportRegistrationId) && exportRegistrationId != Guid.Empty", view, StringComparison.Ordinal);
        Assert.Contains("@if (hasRegisteredExportContext)", view, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"EnterpriseReports\" asp-action=\"ServerIntelligence\" asp-route-registrationId=\"@exportRegistrationId\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"EnterpriseReports\" asp-action=\"DatabaseHealth\" asp-route-registrationId=\"@exportRegistrationId\"", view, StringComparison.Ordinal);
        Assert.Contains("Viewer+ · cache only", view, StringComparison.Ordinal);
        Assert.Contains("They do not refresh the snapshot or contact the monitored SQL Server.", view, StringComparison.Ordinal);
    }

    [Fact]
    public void ContextualExportRoutes_RemainViewerReadAndCacheOnly()
    {
        var controllerType = typeof(EnterpriseReportsController);
        var policy = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Policy;
        var controller = Read("src/Monitor.Web/Controllers/EnterpriseReportsController.cs");

        Assert.Equal(MonitorPolicies.Read, policy);
        Assert.Contains("/reports/server-intelligence/{registrationId:guid}.csv", controller, StringComparison.Ordinal);
        Assert.Contains("/reports/database-health/{registrationId:guid}.csv", controller, StringComparison.Ordinal);
        Assert.Contains("_monitoring.GetServerAsync(registrationId.ToString(\"D\"), cancellationToken)", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("ISnapshotRefreshService", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportCenter_ContextualCardsStillLeadToRegisteredServerSelection()
    {
        var reports = Read("src/Monitor.Web/Views/Portal/Reports.cshtml");

        Assert.Contains("Choose server for intelligence export", reports, StringComparison.Ordinal);
        Assert.Contains("Choose server for database export", reports, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"Operations\" asp-action=\"Servers\"", reports, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
