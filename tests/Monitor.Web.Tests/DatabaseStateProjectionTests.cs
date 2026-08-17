using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class DatabaseStateProjectionTests
{
    [Fact]
    public void Build_ClassifiesRetainedDatabaseStatesWithB300Rules()
    {
        var detail = new DatabaseHealthDetailSnapshot(
            Restoring: 1,
            Recovering: 0,
            RecoveryPending: 1,
            Suspect: 1,
            Emergency: 0,
            OfflineOrOther: 1,
            Items:
            [
                new DatabaseStateSnapshot("AppDb", "ONLINE"),
                new DatabaseStateSnapshot("Warehouse", "RECOVERY_PENDING"),
                new DatabaseStateSnapshot("Legacy", "SUSPECT"),
                new DatabaseStateSnapshot("Archive", "RESTORING")
            ]);

        var result = DatabaseStateProjection.Build(detail);

        Assert.True(result.HasEvidence);
        Assert.Equal(4, result.Items.Count);
        Assert.Equal(DatabaseStateClass.Suspect, result.WorstObserved);
        Assert.Equal(2, result.ActionableCount);
        Assert.Equal(0, result.UnknownCount);
        Assert.True(result.Items.Single(item => item.Name == "Warehouse").Actionable);
        Assert.False(result.Items.Single(item => item.Name == "Archive").Actionable);
    }

    [Fact]
    public void Build_MissingDetailDoesNotInventOnlineState()
    {
        var result = DatabaseStateProjection.Build(null);

        Assert.False(result.HasEvidence);
        Assert.Empty(result.Items);
        Assert.Equal(DatabaseStateClass.Unknown, result.WorstObserved);
        Assert.Equal(0, result.ActionableCount);
    }

    [Fact]
    public void DatabaseHealthView_UsesBoundedCacheOnlyStateProjection()
    {
        var root = FindRoot();
        var view = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Views/Operations/DatabaseHealth.cshtml"));
        var collector = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Services/SqlServerSnapshotCollector.cs"));

        Assert.Contains("DatabaseStateProjection.Build", view, StringComparison.Ordinal);
        Assert.Contains("B300 DATABASE STATE", view, StringComparison.Ordinal);
        Assert.Contains("Up to 50 user databases", view, StringComparison.Ordinal);
        Assert.Contains("DatabaseStatesJson", collector, StringComparison.Ordinal);
        Assert.Contains("x.state_desc", collector, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT ", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DatabaseHealthView_DoesNotPresentMissingPerDatabaseEvidenceAsHealthy()
    {
        var root = FindRoot();
        var view = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Views/Operations/DatabaseHealth.cshtml"));

        Assert.Contains("var perDatabaseUnavailable = projections.Count", view, StringComparison.Ordinal);
        Assert.Contains("actionableObserved > 0 ? \"critical\" : perDatabaseUnavailable > 0 ? \"warning\" : \"healthy\"", view, StringComparison.Ordinal);
        Assert.Contains("server(s) lack retained per-database evidence", view, StringComparison.Ordinal);
        Assert.Contains("restoring + recovering > 0 || unavailable > 0 ? \"warning\" : \"healthy\"", view, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
