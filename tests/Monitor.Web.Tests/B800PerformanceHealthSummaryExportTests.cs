using System.Text;
using Microsoft.AspNetCore.Authorization;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800PerformanceHealthSummaryExportTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Export_UnavailableEvidenceStaysUnavailableAndFormulaSafe()
    {
        var csv = Text(PerformanceHealthSummaryExport.Build(
        [
            new PerformanceHealthSummaryExportRow(
                "=unsafe-server",
                "Unavailable",
                null,
                null,
                null,
                null,
                "Unavailable",
                null,
                null,
                null,
                null,
                null,
                null)
        ]));

        Assert.Contains("#schema,monitor-export-v2", csv, StringComparison.Ordinal);
        Assert.Contains("'=unsafe-server", csv, StringComparison.Ordinal);
        Assert.Contains("\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"0\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Healthy\"", csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_ObservedWorkloadAndAnonymousTopWaitSerializeExactly()
    {
        var collectedAt = new DateTimeOffset(2026, 8, 18, 3, 0, 0, TimeSpan.Zero);
        var csv = Text(PerformanceHealthSummaryExport.Build(
        [
            new PerformanceHealthSummaryExportRow(
                "sql-prod-01",
                "Fresh",
                collectedAt,
                9,
                5,
                2,
                "Available",
                "Io",
                72.25,
                "Warning",
                123.45,
                44.5,
                12.75)
        ]));

        Assert.Contains("\"sql-prod-01\",\"Fresh\"", csv, StringComparison.Ordinal);
        Assert.Contains($"\"{collectedAt:O}\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"9\",\"5\",\"2\",\"Available\",\"Io\",\"72.25\",\"Warning\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"123.45\",\"44.5\",\"12.75\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("PAGEIOLATCH", csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_ObservedWorkloadZeroesRemainZeroWhileMissingWaitsStayUnavailable()
    {
        var collectedAt = new DateTimeOffset(2026, 8, 18, 3, 0, 0, TimeSpan.Zero);
        var csv = Text(PerformanceHealthSummaryExport.Build(
        [
            new PerformanceHealthSummaryExportRow(
                "sql-idle",
                "Stale",
                collectedAt,
                0,
                0,
                0,
                "Unavailable",
                null,
                null,
                null,
                null,
                null,
                null)
        ]));

        Assert.Contains("\"0\",\"0\",\"0\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Healthy\"", csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RouteAndService_AreViewerSafeCacheOnlyAndRedactWaitIdentity()
    {
        var controller = typeof(EnterpriseReportsController);
        var classPolicy = controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy;
        var action = controller.GetMethod(nameof(EnterpriseReportsController.PerformanceHealth))!;
        var actionPolicies = action.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().ToArray();

        Assert.Equal(MonitorPolicies.Read, classPolicy);
        Assert.Empty(actionPolicies);
        Assert.Equal(
            "monitor-performancehealth-20260818-010000.csv",
            EnterpriseSecurityPolicy.SafeDownloadFileName(
                EnterpriseDownloadSubject.PerformanceHealth,
                new DateTimeOffset(2026, 8, 18, 1, 0, 0, TimeSpan.Zero),
                "csv"));

        var controllerSource = Read("src/Monitor.Web/Controllers/EnterpriseReportsController.cs");
        var serviceSource = Read("src/Monitor.Web/Services/EnterpriseReportingServices.cs");
        var exportSource = Read("src/Monitor.Web/Services/PerformanceHealthSummaryExport.cs");
        var reportsSource = Read("src/Monitor.Web/Views/Portal/Reports.cshtml");

        Assert.Contains("/reports/performance-health.csv", controllerSource, StringComparison.Ordinal);
        Assert.Contains("_reports.PerformanceHealth()", controllerSource, StringComparison.Ordinal);
        Assert.Contains("registrations.GetAll()", serviceSource, StringComparison.Ordinal);
        Assert.Contains(".Where(registration => registration.IsEnabled)", serviceSource, StringComparison.Ordinal);
        Assert.Contains("cache.Peek(registration.Id)", serviceSource, StringComparison.Ordinal);
        Assert.Contains("WaitIntelligenceProjection.Build(performance, snapshot?.UptimeSeconds, 1)", serviceSource, StringComparison.Ordinal);
        Assert.Contains("catch (SnapshotCollectionException)", serviceSource, StringComparison.Ordinal);
        Assert.Contains("Performance health summary", reportsSource, StringComparison.Ordinal);
        Assert.Contains("Wait type and fingerprint identifiers are excluded", reportsSource, StringComparison.Ordinal);
        Assert.Contains("EnterpriseReportContract.Csv", exportSource, StringComparison.Ordinal);

        Assert.DoesNotContain("ISqlSnapshotQuery", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISnapshotRefreshService", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISnapshotRefreshService", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WaitType", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Fingerprint", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RegistrationId", exportSource, StringComparison.Ordinal);
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
