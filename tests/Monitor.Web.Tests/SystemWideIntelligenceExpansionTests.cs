using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SystemWideIntelligenceExpansionTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Projection_CriticalDatabaseEvidence_DrivesCriticalPostureAndDatabaseFirstAction()
    {
        var server = Server(
            databases: new DatabaseHealthDetailSnapshot(0, 0, 1, 1, 0, 0),
            databaseOnline: 3,
            databaseTotal: 5);

        var result = EstateIntelligenceProjection.Build(1, [server], "/database-health");

        Assert.Equal("critical", result.Severity);
        Assert.True(result.DatabaseRiskCount > 0);
        Assert.Contains("Database Health", result.NextAction, StringComparison.Ordinal);
        Assert.Equal("DATABASE INTELLIGENCE", result.ContextLabel);
    }

    [Fact]
    public void Projection_MissingRegisteredEvidence_IsNeverReportedHealthy()
    {
        var result = EstateIntelligenceProjection.Build(3, [], "/dashboard");

        Assert.Equal("unknown", result.Severity);
        Assert.Equal(3, result.UnavailableTargets);
        Assert.Equal(0, result.EvidenceCoveragePercent);
        Assert.Contains("usable cached evidence", result.Headline, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Projection_OperationalSignals_AreCrossDomainAndEvidenceBounded()
    {
        var server = Server(
            backups: new BackupHealthSnapshot(2, 1, DateTimeOffset.UtcNow.AddHours(-3)),
            jobs: new SqlAgentHealthSnapshot(6, 5, 2),
            memory: new MemoryHealthSnapshot(
                16L * 1024 * 1024,
                512L * 1024,
                12L * 1024 * 1024,
                90,
                false,
                false,
                "Available",
                MemoryGrantsPending: 1),
            blocking: new BlockingHealthSnapshot(4, 2200),
            performance: new PerformanceHealthSnapshot(8, 6, 1),
            storage: new StorageHealthSnapshot(30L * 1024 * 1024 * 1024, 24L * 1024 * 1024 * 1024, 6L * 1024 * 1024 * 1024));

        var result = EstateIntelligenceProjection.Build(1, [server], "/storage");

        Assert.Equal("warning", result.Severity);
        Assert.Equal(1, result.BackupGapCount);
        Assert.Equal(2, result.JobFailureCount);
        Assert.Equal(1, result.MemoryPressureCount);
        Assert.Equal(4, result.BlockedRequestCount);
        Assert.Equal(1, result.PerformancePressureCount);
        Assert.Contains(result.Signals, signal => signal.Label == "SQL ALLOCATED");
        Assert.Contains(result.Signals, signal => signal.Detail.Contains("not disk capacity", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Signals, signal => signal.Label.Contains("FREE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Projection_MissingFocusedDomainEvidence_RemainsUnknownInsteadOfHealthyZero()
    {
        var result = EstateIntelligenceProjection.Build(1, [Server()], "/backups");
        var backup = Assert.Single(result.Signals, signal => signal.Label == "BACKUP GAPS");

        Assert.Equal("unknown", result.Severity);
        Assert.Equal("Not collected", backup.Value);
        Assert.Equal("unknown", backup.State);
        Assert.Contains("evidence 0/1", backup.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Backup Health evidence is available for 0/1", result.NextAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Projection_GenericCrossDomainSignals_DoNotTurnMissingModulesIntoHealthyZeroes()
    {
        var result = EstateIntelligenceProjection.Build(1, [Server()], "/dashboard");

        var backup = Assert.Single(result.Signals, signal => signal.Label == "BACKUP GAPS");
        var memoryPerformance = Assert.Single(result.Signals, signal => signal.Label == "MEMORY / PERF");
        var blockingJobs = Assert.Single(result.Signals, signal => signal.Label == "BLOCKING / JOBS");

        Assert.Equal("Not collected", backup.Value);
        Assert.Equal("unknown", backup.State);
        Assert.Contains("Not collected", memoryPerformance.Value, StringComparison.Ordinal);
        Assert.Equal("unknown", memoryPerformance.State);
        Assert.Contains("Not collected", blockingJobs.Value, StringComparison.Ordinal);
        Assert.Equal("unknown", blockingJobs.State);
    }

    [Fact]
    public void Projection_PartialDomainCoverage_IsVisibleAndCannotClaimFocusedHealthyState()
    {
        var withBackupEvidence = Server(backups: new BackupHealthSnapshot(2, 0, DateTimeOffset.UtcNow.AddHours(-1)));
        var withoutBackupEvidence = Server();

        var result = EstateIntelligenceProjection.Build(2, [withBackupEvidence, withoutBackupEvidence], "/backups");
        var backup = Assert.Single(result.Signals, signal => signal.Label == "BACKUP GAPS");

        Assert.Equal("unknown", result.Severity);
        Assert.Equal("0 · 1/2", backup.Value);
        Assert.Equal("unknown", backup.State);
        Assert.Contains("evidence 1/2", backup.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1/2 observed target", result.NextAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Projection_CollectedZeroEvidence_RemainsARealZeroNotMissingEvidence()
    {
        var server = Server(
            backups: new BackupHealthSnapshot(2, 0, DateTimeOffset.UtcNow.AddHours(-1)),
            storage: new StorageHealthSnapshot(0, 0, 0));

        var backupResult = EstateIntelligenceProjection.Build(1, [server], "/backups");
        var backup = Assert.Single(backupResult.Signals, signal => signal.Label == "BACKUP GAPS");
        Assert.Equal("healthy", backupResult.Severity);
        Assert.Equal("0", backup.Value);
        Assert.Equal("healthy", backup.State);

        var storageResult = EstateIntelligenceProjection.Build(1, [server], "/storage");
        var allocated = Assert.Single(storageResult.Signals, signal => signal.Label == "SQL ALLOCATED");
        Assert.Equal("healthy", storageResult.Severity);
        Assert.Equal("0 B", allocated.Value);
        Assert.Equal("healthy", allocated.State);
    }

    [Fact]
    public void SharedLayout_RendersIntelligenceAndAdministratorRefreshFromServerSideRegistrations()
    {
        var layout = File.ReadAllText(Path.Combine(Root, "src", "Monitor.Web", "Views", "Shared", "_Layout.cshtml"));
        var script = File.ReadAllText(Path.Combine(Root, "src", "Monitor.Web", "wwwroot", "js", "site.js"));
        var partial = File.ReadAllText(Path.Combine(Root, "src", "Monitor.Web", "Views", "Shared", "_EstateIntelligenceStrip.cshtml"));

        Assert.Contains("@inject IMonitorReadService SystemMonitorRead", layout, StringComparison.Ordinal);
        Assert.Contains("@inject IServerRegistrationRepository SystemRegistrations", layout, StringComparison.Ordinal);
        Assert.Contains("EstateIntelligenceProjection.Build", layout, StringComparison.Ordinal);
        Assert.Contains("_EstateIntelligenceStrip", layout, StringComparison.Ordinal);
        Assert.Contains("data-refresh-all-connections", layout, StringComparison.Ordinal);
        Assert.Contains("@Html.AntiForgeryToken()", layout, StringComparison.Ordinal);
        Assert.Contains("data-refresh-registration-id", layout, StringComparison.Ordinal);
        Assert.Contains("registration.IsEnabled", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("isConnectionsPage", layout, StringComparison.Ordinal);

        Assert.Contains("[data-refresh-all-runtime]", script, StringComparison.Ordinal);
        Assert.Contains("[data-refresh-registration-id]", script, StringComparison.Ordinal);
        Assert.Contains("/refresh-snapshot`,", script, StringComparison.Ordinal);
        Assert.Contains("credentials: 'same-origin'", script, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken: tokenInput.value", script, StringComparison.Ordinal);
        Assert.DoesNotContain("article[id^=\"target-\"]", script, StringComparison.Ordinal);

        Assert.Contains("PRIORITY NEXT ACTION", partial, StringComparison.Ordinal);
        Assert.Contains("Cached evidence only", partial, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/dashboard", "ESTATE INTELLIGENCE")]
    [InlineData("/servers", "SERVER ESTATE INTELLIGENCE")]
    [InlineData("/alerts", "INCIDENT INTELLIGENCE")]
    [InlineData("/database-health", "DATABASE INTELLIGENCE")]
    [InlineData("/memory-health", "MEMORY INTELLIGENCE")]
    [InlineData("/performance-health", "PERFORMANCE INTELLIGENCE")]
    [InlineData("/backups", "BACKUP INTELLIGENCE")]
    [InlineData("/jobs", "SQL AGENT INTELLIGENCE")]
    [InlineData("/storage", "STORAGE INTELLIGENCE")]
    [InlineData("/blocking", "BLOCKING INTELLIGENCE")]
    [InlineData("/enterprise/fleet", "ENTERPRISE INTELLIGENCE")]
    [InlineData("/recommendations", "RECOMMENDATION INTELLIGENCE")]
    [InlineData("/reports", "REPORTING INTELLIGENCE")]
    [InlineData("/observability", "OBSERVABILITY INTELLIGENCE")]
    [InlineData("/audit", "AUDIT INTELLIGENCE")]
    [InlineData("/settings", "READINESS INTELLIGENCE")]
    public void Projection_ContextCoversPortalRoutes(string path, string expected)
    {
        Assert.Equal(expected, EstateIntelligenceProjection.ContextFor(path));
    }

    private static HealthModuleServerViewModel Server(
        int databaseOnline = 5,
        int databaseTotal = 5,
        DatabaseHealthDetailSnapshot? databases = null,
        BackupHealthSnapshot? backups = null,
        SqlAgentHealthSnapshot? jobs = null,
        StorageHealthSnapshot? storage = null,
        BlockingHealthSnapshot? blocking = null,
        PerformanceHealthSnapshot? performance = null,
        MemoryHealthSnapshot? memory = null) => new(
            Guid.NewGuid().ToString("D"),
            "SQL-UAT-01",
            ServerDataSource.LiveFresh,
            12,
            databaseOnline,
            databaseTotal,
            databases,
            backups,
            jobs,
            storage,
            blocking,
            performance,
            memory,
            UptimeSeconds: 3600);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
