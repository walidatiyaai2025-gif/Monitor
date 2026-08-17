using System.Text;
using Microsoft.AspNetCore.Authorization;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800ServerIntelligenceExportTests
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

        var csv = Text(ServerIntelligenceExport.Build(model));

        Assert.Contains("#schema,monitor-export-v2", csv, StringComparison.Ordinal);
        Assert.Contains("\"Evidence\",\"State\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Evidence\",\"CollectedAtUtc\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Database\",\"Online\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"RuntimePressure\",\"State\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("'=unsafe-target", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Database\",\"Online\",\"0\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_FreshSnapshotReusesVisibleServerIntelligenceProjection()
    {
        var collectedAt = new DateTimeOffset(2026, 8, 18, 0, 30, 0, TimeSpan.Zero);
        var memory = new MemoryHealthSnapshot(
            32L * 1024 * 1024,
            8L * 1024 * 1024,
            12L * 1024 * 1024,
            90,
            false,
            false,
            "Available");
        var blocking = new BlockingHealthSnapshot(3, 2500);
        var performance = new PerformanceHealthSnapshot(8, 2, 1);
        var model = new ServerDetailsViewModel
        {
            Server = new ServerCard(
                Guid.NewGuid().ToString("D"),
                "sql-prod-01",
                "16.0.1000.6",
                "Enterprise Edition",
                HealthState.Warning,
                null,
                90,
                12,
                12,
                null,
                null,
                10,
                ServerDataSource.LiveFresh,
                InstanceName: "MSSQLSERVER",
                UptimeSeconds: 172800,
                CollectedAtUtc: collectedAt),
            Metrics = [],
            Evidence = new ServerSnapshotEvidence(
                "MSSQLSERVER",
                172800,
                collectedAt,
                memory,
                null,
                null,
                null,
                null,
                blocking,
                performance)
        };

        var expected = ServerIntelligenceProjection.Build(model);
        var csv = Text(ServerIntelligenceExport.Build(model));

        Assert.NotNull(expected.RuntimePressure);
        Assert.Contains("\"Evidence\",\"State\",\"Fresh\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Identity\",\"SqlMajor\",\"16\"", csv, StringComparison.Ordinal);
        Assert.Contains($"\"Identity\",\"EditionClass\",\"{expected.EditionClass}\"", csv, StringComparison.Ordinal);
        Assert.Contains($"\"RuntimePressure\",\"Score\",\"{expected.RuntimePressure!.Score}\"", csv, StringComparison.Ordinal);
        Assert.Contains($"\"RuntimePressure\",\"Classification\",\"{expected.RuntimePressure.Classification}\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Memory\",\"SqlProcessUtilizationPercent\",\"90\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Blocking\",\"BlockedRequests\",\"3\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Performance\",\"PendingIoRequests\",\"1\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Route_IsViewerSafeContextualAndCacheOnly()
    {
        var controller = typeof(EnterpriseReportsController);
        var classPolicy = controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy;
        var action = controller.GetMethod(nameof(EnterpriseReportsController.ServerIntelligence))!;
        var actionPolicies = action.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().ToArray();

        Assert.Equal(MonitorPolicies.Read, classPolicy);
        Assert.Empty(actionPolicies);
        Assert.Equal(
            "monitor-serverintelligence-20260818-003000.csv",
            EnterpriseSecurityPolicy.SafeDownloadFileName(
                EnterpriseDownloadSubject.ServerIntelligence,
                new DateTimeOffset(2026, 8, 18, 0, 30, 0, TimeSpan.Zero),
                "csv"));

        var controllerSource = Read("src/Monitor.Web/Controllers/EnterpriseReportsController.cs");
        var exportSource = Read("src/Monitor.Web/Services/ServerIntelligenceExport.cs");
        var readServiceSource = Read("src/Monitor.Web/Services/MonitorReadService.cs");

        Assert.Contains("/reports/server-intelligence/{registrationId:guid}.csv", controllerSource, StringComparison.Ordinal);
        Assert.Contains("_monitoring.GetServerAsync", controllerSource, StringComparison.Ordinal);
        Assert.Contains("ServerIntelligenceProjection.Build(model)", exportSource, StringComparison.Ordinal);
        Assert.Contains("cache.Peek(registration.Id)", readServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", exportSource, StringComparison.Ordinal);
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
