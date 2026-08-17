using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class ServerIntelligenceProjectionTests
{
    [Fact]
    public void Build_ProjectsExistingCachedEvidenceIntoDeterministicIntelligence()
    {
        var model = BuildModel(
            version: "16.0.1000.6",
            edition: "Enterprise Edition",
            memory: new MemoryHealthSnapshot(64_000_000, 8_000_000, 40_000_000, 91, false, false, "Available physical memory is high"),
            blocking: new BlockingHealthSnapshot(3, 15_000),
            performance: new PerformanceHealthSnapshot(12, 8, 4));

        var result = ServerIntelligenceProjection.Build(model);

        Assert.Equal("SQL01 / PROD", result.DisplayLabel);
        Assert.Equal(16, result.MajorVersion);
        Assert.Equal("16", result.VersionFamily);
        Assert.True(result.SupportedMajor);
        Assert.Equal(SqlEditionClass.Enterprise, result.EditionClass);
        Assert.Equal(UptimeBand.Stable, result.UptimeBand);
        Assert.Equal(ServerIntelligenceEvidenceState.Fresh, result.EvidenceState);
        Assert.Equal("Fresh cached evidence", result.EvidenceStateLabel);
        Assert.NotNull(result.RuntimePressure);
        Assert.Equal(56, result.RuntimePressure!.Score);
        Assert.Equal(RuntimePressureClass.High, result.RuntimePressure.Classification);
        Assert.Equal("High", result.RuntimePressureStatusLabel);
        Assert.Equal(new[] { "memory", "blocking", "scheduler", "io" }, result.RuntimePressure.Signals);
    }

    [Fact]
    public void Build_DoesNotInventCompositePressureWhenRequiredEvidenceIsMissing()
    {
        var model = BuildModel(
            version: "not-collected",
            edition: "",
            memory: new MemoryHealthSnapshot(64_000_000, 8_000_000, 40_000_000, 91, false, false, "Unknown"),
            blocking: null,
            performance: new PerformanceHealthSnapshot(12, 8, 4));

        var result = ServerIntelligenceProjection.Build(model);

        Assert.Null(result.MajorVersion);
        Assert.Null(result.SupportedMajor);
        Assert.Equal("unknown", result.VersionFamily);
        Assert.Equal(SqlEditionClass.Unknown, result.EditionClass);
        Assert.Null(result.RuntimePressure);
        Assert.Equal("Not collected", result.RuntimeSignalsLabel);
        Assert.Equal("Unavailable", result.RuntimePressureStatusLabel);
    }

    [Fact]
    public void Build_LabelsDerivedPressureFromStaleCachedEvidence()
    {
        var model = BuildModel(
            version: "16.0.1000.6",
            edition: "Enterprise Edition",
            memory: new MemoryHealthSnapshot(64_000_000, 8_000_000, 40_000_000, 91, false, false, "Available physical memory is high"),
            blocking: new BlockingHealthSnapshot(3, 15_000),
            performance: new PerformanceHealthSnapshot(12, 8, 4),
            source: ServerDataSource.LiveStale);

        var result = ServerIntelligenceProjection.Build(model);

        Assert.Equal(ServerIntelligenceEvidenceState.Stale, result.EvidenceState);
        Assert.Equal("Stale cached evidence", result.EvidenceStateLabel);
        Assert.Equal(UptimeBand.Stable, result.UptimeBand);
        Assert.NotNull(result.RuntimePressure);
        Assert.Equal("High · stale evidence", result.RuntimePressureStatusLabel);
    }

    [Fact]
    public void Build_FailsClosedForUnavailableSourceEvenIfGhostEvidenceIsPresent()
    {
        var model = BuildModel(
            version: "16.0.1000.6",
            edition: "Enterprise Edition",
            memory: new MemoryHealthSnapshot(64_000_000, 8_000_000, 40_000_000, 91, false, false, "Available physical memory is high"),
            blocking: new BlockingHealthSnapshot(3, 15_000),
            performance: new PerformanceHealthSnapshot(12, 8, 4),
            source: ServerDataSource.RegisteredUnavailable);

        var result = ServerIntelligenceProjection.Build(model);

        Assert.Equal(ServerIntelligenceEvidenceState.Unavailable, result.EvidenceState);
        Assert.Equal("Unavailable", result.EvidenceStateLabel);
        Assert.Equal(UptimeBand.Unknown, result.UptimeBand);
        Assert.Null(result.RuntimePressure);
        Assert.Equal("Unavailable", result.RuntimePressureStatusLabel);
    }

    [Fact]
    public void Build_FailsClosedWhenLiveSourceHasNoSnapshotEvidence()
    {
        var model = BuildModel(
            version: "16.0.1000.6",
            edition: "Enterprise Edition",
            memory: null,
            blocking: null,
            performance: null,
            source: ServerDataSource.LiveFresh,
            includeEvidence: false);

        var result = ServerIntelligenceProjection.Build(model);

        Assert.Equal(ServerIntelligenceEvidenceState.Unavailable, result.EvidenceState);
        Assert.Equal(UptimeBand.Unknown, result.UptimeBand);
        Assert.Null(result.RuntimePressure);
        Assert.Equal("Unavailable", result.RuntimePressureStatusLabel);
    }

    [Fact]
    public void ServerDetailsView_WiresProjectionAndPreservesCacheOnlyMissingEvidenceContract()
    {
        var root = FindRoot();
        var view = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Views/Operations/ServerDetails.cshtml"));

        Assert.Contains("ServerIntelligenceProjection.Build(Model)", view, StringComparison.Ordinal);
        Assert.Contains("DBA INTELLIGENCE · BATCH-300 WIRED", view, StringComparison.Ordinal);
        Assert.Contains("No monitored SQL query is started by this GET", view, StringComparison.Ordinal);
        Assert.Contains("Missing evidence is not replaced with zero", view, StringComparison.Ordinal);
    }

    private static ServerDetailsViewModel BuildModel(
        string version,
        string edition,
        MemoryHealthSnapshot? memory,
        BlockingHealthSnapshot? blocking,
        PerformanceHealthSnapshot? performance,
        ServerDataSource source = ServerDataSource.LiveFresh,
        bool includeEvidence = true)
    {
        var server = new ServerCard(
            Id: Guid.Parse("11111111-1111-1111-1111-111111111111").ToString("D"),
            Name: "SQL01",
            Version: version,
            Edition: edition,
            State: HealthState.Healthy,
            CpuPercent: null,
            MemoryPercent: memory?.SqlProcessMemoryUtilizationPercent,
            DatabaseOnline: 4,
            DatabaseTotal: 4,
            JobsHealthy: null,
            JobsTotal: null,
            LastScanSecondsAgo: 5,
            Source: source,
            InstanceName: "PROD",
            UptimeSeconds: 172_800,
            CollectedAtUtc: DateTimeOffset.Parse("2026-08-17T08:00:00Z"));

        return new ServerDetailsViewModel
        {
            Server = server,
            Metrics = [],
            Evidence = includeEvidence
                ? new ServerSnapshotEvidence(
                    "PROD",
                    172_800,
                    DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                    memory,
                    new DatabaseHealthDetailSnapshot(0, 0, 0, 0, 0, 0),
                    new BackupHealthSnapshot(4, 0, DateTimeOffset.Parse("2026-08-17T04:00:00Z")),
                    new SqlAgentHealthSnapshot(10, 9, 0),
                    new StorageHealthSnapshot(1_000_000, 800_000, 200_000),
                    blocking,
                    performance)
                : null
        };
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
