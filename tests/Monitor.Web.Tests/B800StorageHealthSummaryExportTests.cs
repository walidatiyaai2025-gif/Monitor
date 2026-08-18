using System.Text;
using Microsoft.AspNetCore.Authorization;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800StorageHealthSummaryExportTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Export_UnavailableEvidenceStaysUnavailableAndFormulaSafe()
    {
        var csv = Text(StorageHealthSummaryExport.Build(
        [
            new StorageHealthSummaryExportRow(
                "=unsafe-server",
                "Unavailable",
                null,
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
                null)
        ]));

        Assert.Contains("#schema,monitor-export-v2", csv, StringComparison.Ordinal);
        Assert.Contains("'=unsafe-server", csv, StringComparison.Ordinal);
        Assert.Contains("\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Healthy\"", csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_ObservedAllocationAndAnonymousTopIoSerializeExactly()
    {
        var collectedAt = new DateTimeOffset(2026, 8, 18, 4, 0, 0, TimeSpan.Zero);
        var csv = Text(StorageHealthSummaryExport.Build(
        [
            new StorageHealthSummaryExportRow(
                "sql-prod-01",
                "Fresh",
                collectedAt,
                "Available",
                9000,
                7000,
                2000,
                7200,
                "Available",
                4,
                2,
                82.5,
                "Critical",
                41.25,
                35.5,
                60.25,
                "High")
        ]));

        Assert.Contains("\"sql-prod-01\",\"Fresh\"", csv, StringComparison.Ordinal);
        Assert.Contains($"\"{collectedAt:O}\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Available\",\"9000\",\"7000\",\"2000\",\"7200\",\"Available\",\"4\",\"2\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"82.5\",\"Critical\",\"41.25\",\"35.5\",\"60.25\",\"High\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("FileKey", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Fingerprint", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_ObservedAllocationZeroesRemainZeroWhileMissingIoStaysUnavailable()
    {
        var collectedAt = new DateTimeOffset(2026, 8, 18, 4, 0, 0, TimeSpan.Zero);
        var csv = Text(StorageHealthSummaryExport.Build(
        [
            new StorageHealthSummaryExportRow(
                "sql-empty",
                "Stale",
                collectedAt,
                "Available",
                0,
                0,
                0,
                null,
                "Unavailable",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null)
        ]));

        Assert.Contains("\"Available\",\"0\",\"0\",\"0\",\"Unavailable\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Healthy\"", csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RouteAndService_AreViewerSafeCacheOnlyAndRedactFileIdentity()
    {
        var controller = typeof(StorageHealthReportsController);
        var classPolicy = controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy;
        var action = controller.GetMethod(nameof(StorageHealthReportsController.StorageHealth))!;
        var actionPolicies = action.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().ToArray();

        Assert.Equal(MonitorPolicies.Read, classPolicy);
        Assert.Empty(actionPolicies);
        Assert.Equal(
            "monitor-storagehealth-20260818-010000.csv",
            EnterpriseSecurityPolicy.SafeDownloadFileName(
                EnterpriseDownloadSubject.StorageHealth,
                new DateTimeOffset(2026, 8, 18, 1, 0, 0, TimeSpan.Zero),
                "csv"));

        var controllerSource = Read("src/Monitor.Web/Controllers/StorageHealthReportsController.cs");
        var exportSource = Read("src/Monitor.Web/Services/StorageHealthSummaryExport.cs");
        var reportsSource = Read("src/Monitor.Web/Views/Portal/Reports.cshtml");

        Assert.Contains("/reports/storage-health.csv", controllerSource, StringComparison.Ordinal);
        Assert.Contains("registrations.GetAll()", exportSource, StringComparison.Ordinal);
        Assert.Contains(".Where(registration => registration.IsEnabled)", exportSource, StringComparison.Ordinal);
        Assert.Contains("cache.Peek(registration.Id)", exportSource, StringComparison.Ordinal);
        Assert.Contains("IoLatencyProjection.Build(storage, snapshot?.UptimeSeconds, 20)", exportSource, StringComparison.Ordinal);
        Assert.Contains("catch (SnapshotCollectionException)", exportSource, StringComparison.Ordinal);
        Assert.Contains("Storage health summary", reportsSource, StringComparison.Ordinal);
        Assert.Contains("file keys, fingerprints and physical paths are excluded", reportsSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EnterpriseReportContract.Csv", exportSource, StringComparison.Ordinal);

        Assert.DoesNotContain("ISqlSnapshotQuery", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISnapshotRefreshService", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISnapshotRefreshService", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FileKey", exportSource, StringComparison.Ordinal);
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
