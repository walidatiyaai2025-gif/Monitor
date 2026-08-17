using System.Text;
using Microsoft.AspNetCore.Authorization;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800MemoryHealthSummaryExportTests
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

        var csv = Text(MemoryHealthSummaryExport.Build(model));

        Assert.Contains("#schema,monitor-export-v2", csv, StringComparison.Ordinal);
        Assert.Contains("\"Evidence\",\"State\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Memory\",\"State\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Memory\",\"SqlProcessUtilizationPercent\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"OS\",\"AvailablePhysicalMemoryMb\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Counters\",\"MemoryGrantsPending\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("'=unsafe-target", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Memory\",\"SqlProcessUtilizationPercent\",\"0\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"OS\",\"PhysicalMemoryLow\",\"False\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_FreshSnapshotReusesMemoryProjectionAndBoundedCounters()
    {
        var collectedAt = new DateTimeOffset(2026, 8, 18, 2, 0, 0, TimeSpan.Zero);
        var memory = new MemoryHealthSnapshot(
            TotalPhysicalMemoryKb: 16L * 1024 * 1024,
            AvailablePhysicalMemoryKb: 4L * 1024 * 1024,
            SqlProcessPhysicalMemoryKb: 8L * 1024 * 1024,
            SqlProcessMemoryUtilizationPercent: 88,
            IsPhysicalMemoryLow: false,
            IsVirtualMemoryLow: false,
            SystemMemoryState: "Available physical memory is steady",
            MaxServerMemoryMb: 12288,
            TotalServerMemoryKb: 8L * 1024 * 1024,
            TargetServerMemoryKb: 10L * 1024 * 1024,
            PageLifeExpectancySeconds: 120,
            MemoryGrantsPending: 2,
            TopMemoryClerkType: "=CACHESTORE_SQLCP",
            TopMemoryClerkKb: 512L * 1024);
        var expected = MemoryIntelligenceProjection.Build(memory);
        var model = CreateModel("sql-prod-01", ServerDataSource.LiveFresh, collectedAt, memory);

        var csv = Text(MemoryHealthSummaryExport.Build(model));

        Assert.Equal("warning", expected.State);
        Assert.True(expected.NeedsAttention);
        Assert.Equal(80, expected.TargetAttainmentPercent);
        Assert.Contains("\"Evidence\",\"State\",\"Fresh\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Memory\",\"State\",\"Available\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Memory\",\"PressureState\",\"warning\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Memory\",\"NeedsAttention\",\"True\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Memory\",\"SqlProcessUtilizationPercent\",\"88\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Memory\",\"SqlProcessPhysicalMemoryMb\",\"8192.0\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"OS\",\"AvailablePhysicalMemoryMb\",\"4096.0\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Configuration\",\"MaxServerMemoryMb\",\"12288\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Counters\",\"TotalServerMemoryMb\",\"8192.0\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Counters\",\"TargetServerMemoryMb\",\"10240.0\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Counters\",\"TargetAttainmentPercent\",\"80\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Counters\",\"PageLifeExpectancySeconds\",\"120\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Counters\",\"MemoryGrantsPending\",\"2\"", csv, StringComparison.Ordinal);
        Assert.Contains("'=CACHESTORE_SQLCP", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_OptionalMemoryCountersRemainUnavailableInsteadOfSyntheticZero()
    {
        var collectedAt = new DateTimeOffset(2026, 8, 18, 2, 5, 0, TimeSpan.Zero);
        var memory = new MemoryHealthSnapshot(
            TotalPhysicalMemoryKb: 8L * 1024 * 1024,
            AvailablePhysicalMemoryKb: 2L * 1024 * 1024,
            SqlProcessPhysicalMemoryKb: 3L * 1024 * 1024,
            SqlProcessMemoryUtilizationPercent: 60,
            IsPhysicalMemoryLow: false,
            IsVirtualMemoryLow: false,
            SystemMemoryState: "Available",
            MaxServerMemoryMb: null,
            TotalServerMemoryKb: null,
            TargetServerMemoryKb: null,
            PageLifeExpectancySeconds: null,
            MemoryGrantsPending: null,
            TopMemoryClerkType: null,
            TopMemoryClerkKb: null);
        var model = CreateModel("sql-prod-02", ServerDataSource.LiveStale, collectedAt, memory);

        var csv = Text(MemoryHealthSummaryExport.Build(model));

        Assert.Contains("\"Evidence\",\"State\",\"Stale\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Memory\",\"SqlProcessUtilizationPercent\",\"60\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Configuration\",\"MaxServerMemoryMb\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Counters\",\"TargetAttainmentPercent\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Counters\",\"PageLifeExpectancySeconds\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Clerk\",\"Dominant\",\"Not collected\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Configuration\",\"MaxServerMemoryMb\",\"0\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Route_IsViewerSafeContextualCacheOnlyAndDiscoverableFromMemoryHealth()
    {
        var controller = typeof(EnterpriseReportsController);
        var classPolicy = controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy;
        var action = controller.GetMethod(nameof(EnterpriseReportsController.MemoryHealth))!;
        var actionPolicies = action.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().ToArray();

        Assert.Equal(MonitorPolicies.Read, classPolicy);
        Assert.Empty(actionPolicies);
        Assert.Equal(
            "monitor-memoryhealth-20260818-020000.csv",
            EnterpriseSecurityPolicy.SafeDownloadFileName(
                EnterpriseDownloadSubject.MemoryHealth,
                new DateTimeOffset(2026, 8, 18, 2, 0, 0, TimeSpan.Zero),
                "csv"));

        var controllerSource = Read("src/Monitor.Web/Controllers/EnterpriseReportsController.cs");
        var exportSource = Read("src/Monitor.Web/Services/MemoryHealthSummaryExport.cs");
        var readServiceSource = Read("src/Monitor.Web/Services/MonitorReadService.cs");
        var memoryView = Read("src/Monitor.Web/Views/Operations/MemoryHealth.cshtml");
        var reportsView = Read("src/Monitor.Web/Views/Portal/Reports.cshtml");

        Assert.Contains("/reports/memory-health/{registrationId:guid}.csv", controllerSource, StringComparison.Ordinal);
        Assert.Contains("_monitoring.GetServerAsync", controllerSource, StringComparison.Ordinal);
        Assert.Contains("MemoryIntelligenceProjection.Build(memory)", exportSource, StringComparison.Ordinal);
        Assert.Contains("cache.Peek(registration.Id)", readServiceSource, StringComparison.Ordinal);
        Assert.Contains("Guid.TryParse(server.Id, out var memoryExportRegistrationId)", memoryView, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"MemoryHealth\" asp-route-registrationId=\"@memoryExportRegistrationId\"", memoryView, StringComparison.Ordinal);
        Assert.Contains("Cached memory health summary", reportsView, StringComparison.Ordinal);
        Assert.Contains("Choose server for memory export", reportsView, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISnapshotRefreshService", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISnapshotRefreshService", exportSource, StringComparison.Ordinal);
    }

    private static ServerDetailsViewModel CreateModel(
        string name,
        ServerDataSource source,
        DateTimeOffset collectedAt,
        MemoryHealthSnapshot memory) =>
        new()
        {
            Server = new ServerCard(
                Guid.NewGuid().ToString("D"),
                name,
                "16.0.1000.6",
                "Enterprise Edition",
                HealthState.Warning,
                null,
                memory.SqlProcessMemoryUtilizationPercent,
                4,
                4,
                null,
                null,
                10,
                source,
                CollectedAtUtc: collectedAt),
            Metrics = [],
            Evidence = new ServerSnapshotEvidence(
                InstanceName: "MSSQLSERVER",
                UptimeSeconds: 86400,
                CollectedAtUtc: collectedAt,
                Memory: memory,
                Databases: null,
                Backups: null,
                Jobs: null,
                Storage: null,
                Blocking: null,
                Performance: null)
        };

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
