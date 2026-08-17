using System.Text;
using Microsoft.AspNetCore.Authorization;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800DatabaseHealthExportTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Export_UsesB300DatabaseProjectionAndFormulaSafeServerIdentity()
    {
        var detail = new DatabaseHealthDetailSnapshot(
            Restoring: 0,
            Recovering: 0,
            RecoveryPending: 1,
            Suspect: 1,
            Emergency: 0,
            OfflineOrOther: 0,
            Items:
            [
                new DatabaseStateSnapshot("AppDb", "ONLINE"),
                new DatabaseStateSnapshot("Warehouse", "RECOVERY_PENDING"),
                new DatabaseStateSnapshot("Legacy", "SUSPECT")
            ]);
        var server = new HealthModuleServerViewModel(
            Guid.NewGuid().ToString("D"),
            "=unsafe-server",
            ServerDataSource.LiveFresh,
            12,
            1,
            3,
            detail,
            null,
            null,
            null,
            null,
            null);

        var csv = Text(DatabaseHealthExport.Build([server]));

        Assert.Contains("#schema,monitor-export-v2", csv, StringComparison.Ordinal);
        Assert.Contains("ObservedServers=1;ExportedServers=1;Truncated=No", csv, StringComparison.Ordinal);
        Assert.Contains("'=unsafe-server", csv, StringComparison.Ordinal);
        Assert.Contains("\"Available\",\"Suspect\",\"2\",\"0\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"0\",\"0\",\"1\",\"1\",\"0\",\"0\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("AppDb", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Warehouse", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Legacy", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_UnavailableRowDoesNotSerializePlaceholderZeroAsObservedDatabaseTruth()
    {
        var server = new HealthModuleServerViewModel(
            Guid.NewGuid().ToString("D"),
            "registered-target",
            ServerDataSource.RegisteredUnavailable,
            0,
            0,
            0,
            null,
            null,
            null,
            null,
            null,
            null);

        var csv = Text(DatabaseHealthExport.Build([server]));

        Assert.Contains("\"registered-target\",\"RegisteredUnavailable\",\"Unavailable\",\"Unavailable\",\"Unavailable\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"registered-target\",\"RegisteredUnavailable\",\"0\",\"0\",\"0\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_ReportsCoverageWhenServerRowsAreBounded()
    {
        var servers = Enumerable.Range(0, DatabaseHealthExport.MaxServerRows + 3)
            .Select(index => new HealthModuleServerViewModel(
                index.ToString("D4"),
                $"sql-{index:D4}",
                ServerDataSource.LiveFresh,
                index,
                1,
                1,
                new DatabaseHealthDetailSnapshot(0, 0, 0, 0, 0, 0, [new DatabaseStateSnapshot("Db", "ONLINE")]),
                null,
                null,
                null,
                null,
                null))
            .ToArray();

        var csv = Text(DatabaseHealthExport.Build(servers));

        Assert.Contains($"ObservedServers={DatabaseHealthExport.MaxServerRows + 3};ExportedServers={DatabaseHealthExport.MaxServerRows};Truncated=Yes", csv, StringComparison.Ordinal);
        Assert.Contains("sql-0249", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("sql-0250", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Route_IsViewerSafeCacheOnlyAndDoesNotCollectMonitoredSql()
    {
        var controller = typeof(EnterpriseReportsController);
        var classPolicy = controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy;
        var action = controller.GetMethod(nameof(EnterpriseReportsController.DatabaseHealth))!;
        var actionPolicies = action.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().ToArray();

        Assert.Equal(MonitorPolicies.Read, classPolicy);
        Assert.Empty(actionPolicies);
        Assert.Equal(
            "monitor-databasehealth-20260818-010000.csv",
            EnterpriseSecurityPolicy.SafeDownloadFileName(
                EnterpriseDownloadSubject.DatabaseHealth,
                new DateTimeOffset(2026, 8, 18, 1, 0, 0, TimeSpan.Zero),
                "csv"));

        var controllerSource = Read("src/Monitor.Web/Controllers/EnterpriseReportsController.cs");
        var exportSource = Read("src/Monitor.Web/Services/DatabaseHealthExport.cs");
        var readServiceSource = Read("src/Monitor.Web/Services/MonitorReadService.cs");

        Assert.Contains("/reports/database-health.csv", controllerSource, StringComparison.Ordinal);
        Assert.Contains("_monitoring.GetHealthModulesAsync", controllerSource, StringComparison.Ordinal);
        Assert.Contains("DatabaseStateProjection.Build", exportSource, StringComparison.Ordinal);
        Assert.Contains("cache.Peek(registration.Id)", readServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", exportSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("physical_name", exportSource, StringComparison.OrdinalIgnoreCase);
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
