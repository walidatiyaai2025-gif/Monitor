using Xunit;

namespace Monitor.Web.Tests;

public sealed class ProductionSingleNodeAcceptanceTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void AcceptanceHarness_RequiresHttps_ArtifactChecksum_AndThreeHealthEndpoints()
    {
        var text = Read("scripts/Accept-ProductionSingleNode.ps1");

        Assert.Contains("requires an absolute HTTPS BaseUri", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Get-FileHash", text, StringComparison.Ordinal);
        Assert.Contains("SHA256", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'/health/live'", text, StringComparison.Ordinal);
        Assert.Contains("'/health/ready'", text, StringComparison.Ordinal);
        Assert.Contains("'/health'", text, StringComparison.Ordinal);
        Assert.DoesNotContain("-AllowHttpLoopback", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptanceHarness_DoesNotFalseCloseOperatorOnlyProductionGates()
    {
        var text = Read("scripts/Accept-ProductionSingleNode.ps1");

        Assert.Contains("processRecycleRestartVerified = $false", text, StringComparison.Ordinal);
        Assert.Contains("durableRegistrationVerifiedAfterRestart = $false", text, StringComparison.Ordinal);
        Assert.Contains("protectedCredentialVerifiedAfterRestart = $false", text, StringComparison.Ordinal);
        Assert.Contains("monitoredSqlLeastPrivilegeVerified = $false", text, StringComparison.Ordinal);
        Assert.Contains("operationalBackupCreated = $false", text, StringComparison.Ordinal);
        Assert.Contains("rollbackDryRunVerified = $false", text, StringComparison.Ordinal);
        Assert.Contains("P0.5 is NOT complete", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptanceRunbook_FreezesSingleNode_AndRequiresActualIisEvidence()
    {
        var text = Read("docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md");

        Assert.Contains("Deployment:Mode=SingleNode", text, StringComparison.Ordinal);
        Assert.Contains("MultiNode is explicitly out of scope", text, StringComparison.Ordinal);
        Assert.Contains("IIS HTTPS binding", text, StringComparison.Ordinal);
        Assert.Contains("Recycle the IIS application pool", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Protected credential durability", text, StringComparison.Ordinal);
        Assert.Contains("Operational backup", text, StringComparison.Ordinal);
        Assert.Contains("ROLLBACK_RUNBOOK.md", text, StringComparison.Ordinal);
        Assert.Contains("all rows are PASS", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleaseWorkflow_ProducesVersionedWindowsArtifactAndChecksum()
    {
        var text = Read(".github/workflows/release.yml");

        Assert.Contains("--runtime win-x64", text, StringComparison.Ordinal);
        Assert.Contains("Monitor-${version}-win-x64.zip", text, StringComparison.Ordinal);
        Assert.Contains("sha256sum", text, StringComparison.Ordinal);
        Assert.Contains("actions/upload-artifact@v4", text, StringComparison.Ordinal);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root containing Monitor.sln was not found.");
    }
}
