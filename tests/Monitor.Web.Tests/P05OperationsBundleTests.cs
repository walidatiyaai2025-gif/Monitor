using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05OperationsBundleTests
{
    [Fact]
    public void ProductionCandidate_ParsesAndShipsExternalIisAcceptanceTooling()
    {
        var root = FindRepoRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "production-candidate.yml"));

        Assert.Contains("Parse production PowerShell tooling", workflow, StringComparison.Ordinal);
        Assert.Contains("System.Management.Automation.Language.Parser", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/Test-IisProductionPrerequisites.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/Deploy-ProductionSingleNode.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/Accept-ProductionSingleNode.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("Copy-Item scripts/Test-IisProductionPrerequisites.ps1 \"$ops/scripts/\"", workflow, StringComparison.Ordinal);
        Assert.Contains("Copy-Item scripts/Deploy-ProductionSingleNode.ps1 \"$ops/scripts/\"", workflow, StringComparison.Ordinal);
        Assert.Contains("Copy-Item scripts/Accept-ProductionSingleNode.ps1 \"$ops/scripts/\"", workflow, StringComparison.Ordinal);
        Assert.Contains("Copy-Item docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md \"$ops/docs/\"", workflow, StringComparison.Ordinal);
        Assert.Contains("retention-days: 30", workflow, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) return directory.FullName;
                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for P0.5 operations-bundle tests.");
    }
}
