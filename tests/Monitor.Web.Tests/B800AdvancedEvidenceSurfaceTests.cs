using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800AdvancedEvidenceSurfaceTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void ReadModels_AndService_PassAdvancedEvidenceWithoutLiveCollection()
    {
        var models = Read("src/Monitor.Web/Models/MonitorModels.cs");
        var service = Read("src/Monitor.Web/Services/MonitorReadService.cs");

        Assert.Contains("TempDbHealthSnapshot? TempDb", models, StringComparison.Ordinal);
        Assert.Contains("TransactionLogHealthSnapshot? TransactionLogs", models, StringComparison.Ordinal);
        Assert.Contains("HaHealthSnapshot? Ha", models, StringComparison.Ordinal);
        Assert.Contains("snapshot.TempDb", service, StringComparison.Ordinal);
        Assert.Contains("snapshot.TransactionLogs", service, StringComparison.Ordinal);
        Assert.Contains("snapshot.Ha", service, StringComparison.Ordinal);
        Assert.Contains("new(\"TempDB\"", service, StringComparison.Ordinal);
        Assert.Contains("new(\"Transaction log\"", service, StringComparison.Ordinal);
        Assert.Contains("new(\"HA\"", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SnapshotQuery", service, StringComparison.Ordinal);
    }

    [Fact]
    public void SpecializedSurfaces_UseSharedBoundedDiagnostics()
    {
        var storage = Read("src/Monitor.Web/Views/Operations/Storage.cshtml");
        var database = Read("src/Monitor.Web/Views/Operations/DatabaseHealth.cshtml");
        var tempDb = Read("src/Monitor.Web/Views/Shared/_TempDbDiagnostics.cshtml");
        var logHa = Read("src/Monitor.Web/Views/Shared/_TransactionLogHaDiagnostics.cshtml");

        Assert.Contains("_TempDbDiagnostics", storage, StringComparison.Ordinal);
        Assert.Contains("_TransactionLogHaDiagnostics", database, StringComparison.Ordinal);
        Assert.Contains("AdvancedEvidenceProjection.BuildTempDb", tempDb, StringComparison.Ordinal);
        Assert.Contains("AdvancedEvidenceProjection.BuildTransactionLogs", logHa, StringComparison.Ordinal);
        Assert.Contains("AdvancedEvidenceProjection.BuildHa", logHa, StringComparison.Ordinal);
        Assert.Contains("Take(12)", logHa, StringComparison.Ordinal);

        foreach (var source in new[] { storage, database, tempDb, logHa })
        {
            Assert.DoesNotContain("SqlConnection", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TempDbSnapshotQuery", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TransactionLogSnapshotQuery", source, StringComparison.Ordinal);
            Assert.DoesNotContain("HaSnapshotQuery", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ProjectionAndSurfaces_DoNotInvokeUnsupportedCompositeClassifiers()
    {
        var projection = Read("src/Monitor.Web/Services/AdvancedEvidenceProjection.cs");
        var service = Read("src/Monitor.Web/Services/MonitorReadService.cs");
        var tempDb = Read("src/Monitor.Web/Views/Shared/_TempDbDiagnostics.cshtml");
        var logHa = Read("src/Monitor.Web/Views/Shared/_TransactionLogHaDiagnostics.cshtml");
        var combined = string.Join('\n', projection, service, tempDb, logHa);

        Assert.DoesNotContain("Batch400TempDbPressure.Summarize", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Batch400TransactionLogHealth.Summarize", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Batch400HaReadiness.Summarize", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("QueryRegressionEvidenceContract.Evaluate", combined, StringComparison.Ordinal);
        Assert.Contains("growth/contention not evaluated", service, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quorum/RPO/RTO not evaluated", service, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FleetSurface_RollsUpCoverageAndDirectObservedFactsOnly()
    {
        var fleetService = Read("src/Monitor.Web/Services/FleetIntelligenceService.cs");
        var fleetView = Read("src/Monitor.Web/Views/FleetIntelligence/Index.cshtml");

        Assert.Contains("FleetAdvancedEvidenceSummary", fleetService, StringComparison.Ordinal);
        Assert.Contains("Snapshot?.Snapshot.TempDb", fleetService, StringComparison.Ordinal);
        Assert.Contains("Snapshot?.Snapshot.TransactionLogs", fleetService, StringComparison.Ordinal);
        Assert.Contains("Snapshot?.Snapshot.Ha", fleetService, StringComparison.Ordinal);
        Assert.Contains("SnapshotFreshness.Stale", fleetService, StringComparison.Ordinal);
        Assert.Contains("BOUNDED EVIDENCE", fleetView, StringComparison.Ordinal);
        Assert.Contains("Facts only · no composite readiness claim", fleetView, StringComparison.Ordinal);
        Assert.Contains("Stale advanced evidence", fleetView, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", fleetService, StringComparison.Ordinal);
        Assert.DoesNotContain("SnapshotQuery", fleetService, StringComparison.Ordinal);
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
