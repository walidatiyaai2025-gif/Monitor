using System.Text;
using Microsoft.AspNetCore.Authorization;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800SqlAgentHealthSummaryExportTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Export_UnavailableEvidenceStaysUnavailableAndFormulaSafe()
    {
        var csv = Text(SqlAgentHealthSummaryExport.Build(
        [
            new SqlAgentHealthSummaryExportRow(
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
                null,
                null,
                null,
                "Unavailable",
                null,
                null,
                "Unavailable")
        ]));

        Assert.Contains("#schema,monitor-export-v2", csv, StringComparison.Ordinal);
        Assert.Contains("'=unsafe-server", csv, StringComparison.Ordinal);
        Assert.Contains("\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"0\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Healthy\"", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"NotEvaluated\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_ObservedAggregateAndAnonymousTopReliabilitySerializeExactly()
    {
        var collectedAt = new DateTimeOffset(2026, 8, 18, 2, 0, 0, TimeSpan.Zero);
        var csv = Text(SqlAgentHealthSummaryExport.Build(
        [
            new SqlAgentHealthSummaryExportRow(
                "sql-prod-01",
                "Fresh",
                collectedAt,
                12,
                10,
                2,
                "Available",
                73.5,
                "Warning",
                80,
                2,
                45.5,
                25,
                true,
                5,
                "Available",
                7,
                1,
                "NotEvaluated")
        ]));

        Assert.Contains("\"sql-prod-01\",\"Fresh\"", csv, StringComparison.Ordinal);
        Assert.Contains($"\"{collectedAt:O}\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"12\",\"10\",\"2\",\"Available\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"73.5\",\"Warning\",\"80\",\"2\",\"45.5\",\"25\",\"true\",\"5\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Available\",\"7\",\"1\",\"NotEvaluated\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Late", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OnTime", csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_ObservedAggregateZeroesRemainZeroWhileMissingHistoryAndActivityStayUnavailable()
    {
        var collectedAt = new DateTimeOffset(2026, 8, 18, 2, 0, 0, TimeSpan.Zero);
        var csv = Text(SqlAgentHealthSummaryExport.Build(
        [
            new SqlAgentHealthSummaryExportRow(
                "sql-empty",
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
                null,
                null,
                null,
                "Unavailable",
                null,
                null,
                "Unavailable")
        ]));

        Assert.Contains("\"0\",\"0\",\"0\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"0\",\"0\",\"Unavailable\",\"0\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"NotEvaluated\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void RouteAndService_AreViewerSafeCacheOnlyRedactedAndDoNotClaimScheduleLateness()
    {
        var controller = typeof(EnterpriseReportsController);
        var classPolicy = controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy;
        var action = controller.GetMethod(nameof(EnterpriseReportsController.SqlAgentHealth))!;
        var actionPolicies = action.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().ToArray();

        Assert.Equal(MonitorPolicies.Read, classPolicy);
        Assert.Empty(actionPolicies);
        Assert.Equal(
            "monitor-sqlagenthealth-20260818-010000.csv",
            EnterpriseSecurityPolicy.SafeDownloadFileName(
                EnterpriseDownloadSubject.SqlAgentHealth,
                new DateTimeOffset(2026, 8, 18, 1, 0, 0, TimeSpan.Zero),
                "csv"));

        var controllerSource = Read("src/Monitor.Web/Controllers/EnterpriseReportsController.cs");
        var serviceSource = Read("src/Monitor.Web/Services/EnterpriseReportingServices.cs");
        var exportSource = Read("src/Monitor.Web/Services/SqlAgentHealthSummaryExport.cs");
        var reportsSource = Read("src/Monitor.Web/Views/Portal/Reports.cshtml");

        Assert.Contains("/reports/sql-agent-health.csv", controllerSource, StringComparison.Ordinal);
        Assert.Contains("_reports.SqlAgentHealth()", controllerSource, StringComparison.Ordinal);
        Assert.Contains("registrations.GetAll()", serviceSource, StringComparison.Ordinal);
        Assert.Contains(".Where(registration => registration.IsEnabled)", serviceSource, StringComparison.Ordinal);
        Assert.Contains("cache.Peek(registration.Id)", serviceSource, StringComparison.Ordinal);
        Assert.Contains("AgentReliabilityProjection.Build(jobs, 1)", serviceSource, StringComparison.Ordinal);
        Assert.Contains("catch (SnapshotCollectionException)", serviceSource, StringComparison.Ordinal);
        Assert.Contains("activityRows is null ? \"Unavailable\" : \"NotEvaluated\"", serviceSource, StringComparison.Ordinal);
        Assert.Contains("SQL Agent health summary", reportsSource, StringComparison.Ordinal);
        Assert.Contains("Job names and owners are excluded", reportsSource, StringComparison.Ordinal);
        Assert.Contains("schedule lateness remains NotEvaluated", reportsSource, StringComparison.Ordinal);
        Assert.Contains("EnterpriseReportContract.Csv", exportSource, StringComparison.Ordinal);

        Assert.DoesNotContain("ISqlSnapshotQuery", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISnapshotRefreshService", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISnapshotRefreshService", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("JobKey", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Owner", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RegistrationId", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NextScheduledRun", exportSource, StringComparison.Ordinal);
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
