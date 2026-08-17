using System.Text;
using Microsoft.AspNetCore.Authorization;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800DatabaseHealthSummaryExportTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Export_UnavailableSnapshotPreservesUnavailableTruthAndFormulaSafety()
    {
        var model = new ServerDetailsViewModel
        {
            Server = new ServerCard(
                Guid.NewGuid().ToString("D"),
                "=unsafe-target",
                "Not collected",
                "Registered target",
                HealthState.Unknown,
                null,
                null,
                0,
                0,
                null,
                null,
                0,
                ServerDataSource.RegisteredUnavailable),
            Metrics = [],
            Evidence = null
        };

        var csv = Text(DatabaseHealthSummaryExport.Build(model));

        Assert.Contains("#schema,monitor-export-v2", csv, StringComparison.Ordinal);
        Assert.Contains("\"Evidence\",\"State\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Aggregate\",\"Online\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Aggregate\",\"Total\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"RetainedState\",\"State\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"RetainedState\",\"Rows\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("'=unsafe-target", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Aggregate\",\"Online\",\"0\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"RetainedState\",\"Actionable\",\"0\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_FreshSnapshotReusesDatabaseStateProjectionWithoutDatabaseNames()
    {
        var collectedAt = new DateTimeOffset(2026, 8, 18, 1, 0, 0, TimeSpan.Zero);
        var detail = new DatabaseHealthDetailSnapshot(
            Restoring: 1,
            Recovering: 2,
            RecoveryPending: 1,
            Suspect: 1,
            Emergency: 0,
            OfflineOrOther: 1,
            Items:
            [
                new DatabaseStateSnapshot("=sensitive-db", "SUSPECT"),
                new DatabaseStateSnapshot("normal-db", "ONLINE"),
                new DatabaseStateSnapshot("unknown-db", "UNSUPPORTED_STATE")
            ]);
        var expected = DatabaseStateProjection.Build(detail);
        var model = new ServerDetailsViewModel
        {
            Server = new ServerCard(
                Guid.NewGuid().ToString("D"),
                "sql-prod-01",
                "16.0.1000.6",
                "Enterprise Edition",
                HealthState.Warning,
                null,
                null,
                7,
                8,
                null,
                null,
                10,
                ServerDataSource.LiveFresh,
                CollectedAtUtc: collectedAt),
            Metrics = [],
            Evidence = new ServerSnapshotEvidence(
                "MSSQLSERVER",
                86400,
                collectedAt,
                null,
                detail,
                null,
                null,
                null,
                null,
                null)
        };

        var csv = Text(DatabaseHealthSummaryExport.Build(model));

        Assert.True(expected.HasEvidence);
        Assert.Contains("\"Evidence\",\"State\",\"Fresh\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Aggregate\",\"Online\",\"7\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Aggregate\",\"Total\",\"8\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Aggregate\",\"Restoring\",\"1\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Aggregate\",\"Recovering\",\"2\"", csv, StringComparison.Ordinal);
        Assert.Contains($"\"RetainedState\",\"Rows\",\"{expected.Items.Count}\"", csv, StringComparison.Ordinal);
        Assert.Contains($"\"RetainedState\",\"WorstObserved\",\"{expected.WorstObserved}\"", csv, StringComparison.Ordinal);
        Assert.Contains($"\"RetainedState\",\"Actionable\",\"{expected.ActionableCount}\"", csv, StringComparison.Ordinal);
        Assert.Contains($"\"RetainedState\",\"Unknown\",\"{expected.UnknownCount}\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-db", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("normal-db", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unknown-db", csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_AggregateDetailCanRemainAvailableWhenRetainedRowsAreUnavailable()
    {
        var collectedAt = new DateTimeOffset(2026, 8, 18, 1, 0, 0, TimeSpan.Zero);
        var detail = new DatabaseHealthDetailSnapshot(1, 0, 0, 0, 0, 0, Items: null);
        var model = new ServerDetailsViewModel
        {
            Server = new ServerCard(
                Guid.NewGuid().ToString("D"),
                "sql-prod-02",
                "16.0",
                "Standard Edition",
                HealthState.Warning,
                null,
                null,
                4,
                5,
                null,
                null,
                5,
                ServerDataSource.LiveStale,
                CollectedAtUtc: collectedAt),
            Metrics = [],
            Evidence = new ServerSnapshotEvidence(
                null,
                3600,
                collectedAt,
                null,
                detail,
                null,
                null,
                null,
                null,
                null)
        };

        var csv = Text(DatabaseHealthSummaryExport.Build(model));

        Assert.Contains("\"Evidence\",\"State\",\"Stale\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Aggregate\",\"Online\",\"4\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Aggregate\",\"Restoring\",\"1\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"RetainedState\",\"State\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"RetainedState\",\"WorstObserved\",\"Unavailable\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Route_IsViewerSafeContextualCacheOnlyAndDatabaseNameRedacted()
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
        var exportSource = Read("src/Monitor.Web/Services/DatabaseHealthSummaryExport.cs");
        var readServiceSource = Read("src/Monitor.Web/Services/MonitorReadService.cs");
        var reportsSource = Read("src/Monitor.Web/Views/Portal/Reports.cshtml");

        Assert.Contains("/reports/database-health/{registrationId:guid}.csv", controllerSource, StringComparison.Ordinal);
        Assert.Contains("_monitoring.GetServerAsync", controllerSource, StringComparison.Ordinal);
        Assert.Contains("DatabaseStateProjection.Build(detail)", exportSource, StringComparison.Ordinal);
        Assert.Contains("cache.Peek(registration.Id)", readServiceSource, StringComparison.Ordinal);
        Assert.Contains("Cached database health summary", reportsSource, StringComparison.Ordinal);
        Assert.Contains("Database names are excluded", reportsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("projection.Items.Select", exportSource, StringComparison.Ordinal);
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
