using System.Text;
using Microsoft.AspNetCore.Authorization;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800BackupHealthSummaryExportTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Export_UnavailableEvidenceStaysUnavailableAndFormulaSafe()
    {
        var csv = Text(BackupHealthSummaryExport.Build(
        [
            new BackupHealthSummaryExportRow("=unsafe-server", "Unavailable", null, null, null, null)
        ]));

        Assert.Contains("#schema,monitor-export-v2", csv, StringComparison.Ordinal);
        Assert.Contains("'=unsafe-server", csv, StringComparison.Ordinal);
        Assert.Contains("\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Unavailable\",\"Unavailable\",\"Unavailable\",\"Unavailable\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"0\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Healthy\"", csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_ObservedBackupAggregateIsSerializedButComplianceRemainsNotEvaluated()
    {
        var collectedAt = new DateTimeOffset(2026, 8, 18, 2, 0, 0, TimeSpan.Zero);
        var lastFull = new DateTimeOffset(2026, 8, 18, 0, 30, 0, TimeSpan.Zero);

        var csv = Text(BackupHealthSummaryExport.Build(
        [
            new BackupHealthSummaryExportRow("sql-prod-01", "Fresh", collectedAt, 7, 2, lastFull)
        ]));

        Assert.Contains("\"sql-prod-01\",\"Fresh\"", csv, StringComparison.Ordinal);
        Assert.Contains($"\"{collectedAt:O}\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"7\",\"2\"", csv, StringComparison.Ordinal);
        Assert.Contains($"\"{lastFull:O}\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"NotEvaluated\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Compliant", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NonCompliant", csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_ObservedZeroesRemainTruthfulZeroes()
    {
        var collectedAt = new DateTimeOffset(2026, 8, 18, 2, 0, 0, TimeSpan.Zero);
        var csv = Text(BackupHealthSummaryExport.Build(
        [
            new BackupHealthSummaryExportRow("sql-empty", "Stale", collectedAt, 0, 0, null)
        ]));

        Assert.Contains("\"0\",\"0\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"NotEvaluated\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"LastFullBackupAtUtc\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Unavailable\",\"NotEvaluated\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void RouteAndService_AreViewerSafeCacheOnlyAndDoNotExportDatabaseNames()
    {
        var controller = typeof(EnterpriseReportsController);
        var classPolicy = controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy;
        var action = controller.GetMethod(nameof(EnterpriseReportsController.BackupHealth))!;
        var actionPolicies = action.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().ToArray();

        Assert.Equal(MonitorPolicies.Read, classPolicy);
        Assert.Empty(actionPolicies);
        Assert.Equal(
            "monitor-backuphealth-20260818-010000.csv",
            EnterpriseSecurityPolicy.SafeDownloadFileName(
                EnterpriseDownloadSubject.BackupHealth,
                new DateTimeOffset(2026, 8, 18, 1, 0, 0, TimeSpan.Zero),
                "csv"));

        var controllerSource = Read("src/Monitor.Web/Controllers/EnterpriseReportsController.cs");
        var serviceSource = Read("src/Monitor.Web/Services/EnterpriseReportingServices.cs");
        var exportSource = Read("src/Monitor.Web/Services/BackupHealthSummaryExport.cs");
        var reportsSource = Read("src/Monitor.Web/Views/Portal/Reports.cshtml");

        Assert.Contains("/reports/backup-health.csv", controllerSource, StringComparison.Ordinal);
        Assert.Contains("_reports.BackupHealth()", controllerSource, StringComparison.Ordinal);
        Assert.Contains("registrations.GetAll()", serviceSource, StringComparison.Ordinal);
        Assert.Contains("cache.Peek(registration.Id)", serviceSource, StringComparison.Ordinal);
        Assert.Contains("catch (SnapshotCollectionException)", serviceSource, StringComparison.Ordinal);
        Assert.Contains("Backup health summary", reportsSource, StringComparison.Ordinal);
        Assert.Contains("RPO compliance remains NotEvaluated", reportsSource, StringComparison.Ordinal);
        Assert.Contains("database names are excluded", reportsSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EnterpriseReportContract.Csv", exportSource, StringComparison.Ordinal);

        Assert.DoesNotContain("ISqlSnapshotQuery", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DatabaseName", exportSource, StringComparison.Ordinal);
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
