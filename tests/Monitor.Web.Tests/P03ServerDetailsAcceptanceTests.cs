using Xunit;

namespace Monitor.Web.Tests;

public sealed class P03ServerDetailsAcceptanceTests
{
    [Fact]
    public void ServerDetails_IsEvidenceFirstAndContainsEveryP03ProductionModule()
    {
        var source = File.ReadAllText(FindRepoFile("src", "Monitor.Web", "Views", "Operations", "ServerDetails.cshtml"));

        Assert.Contains("SERVER DETAILS · EVIDENCE FIRST", source, StringComparison.Ordinal);
        Assert.Contains("Freshness", source, StringComparison.Ordinal);
        Assert.Contains("Collected at", source, StringComparison.Ordinal);
        Assert.Contains("SNAPSHOT IDENTITY", source, StringComparison.Ordinal);
        Assert.Contains("DATABASE AVAILABILITY", source, StringComparison.Ordinal);
        Assert.Contains("MEMORY EVIDENCE", source, StringComparison.Ordinal);
        Assert.Contains("BACKUP EVIDENCE", source, StringComparison.Ordinal);
        Assert.Contains("SQL AGENT", source, StringComparison.Ordinal);
        Assert.Contains("STORAGE ALLOCATION", source, StringComparison.Ordinal);
        Assert.Contains("BLOCKING", source, StringComparison.Ordinal);
        Assert.Contains("RUNTIME PRESSURE", source, StringComparison.Ordinal);
        Assert.Contains("CPU remains outside the v0.1 bounded snapshot contract", source, StringComparison.Ordinal);
        Assert.Contains("Not collected", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ServerDetails_DoesNotPublishSyntheticHealthScoreOrClaimGetTriggersCollection()
    {
        var source = File.ReadAllText(FindRepoFile("src", "Monitor.Web", "Views", "Operations", "ServerDetails.cshtml"));

        Assert.DoesNotContain("Health score", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("healthScore", source, StringComparison.Ordinal);
        Assert.Contains("Opening this page never initiates monitored-SQL collection", source, StringComparison.Ordinal);
        Assert.Contains("Normal GET navigation reads cache only", source, StringComparison.Ordinal);
    }

    private static string FindRepoFile(params string[] segments)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
                {
                    return Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
                }
                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for P0.3 source acceptance.");
    }
}
