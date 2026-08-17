using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05Rc61AcceptanceControlToolkitTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Toolkit_DoesNotRebuildOrRepackageSelectedRc61()
    {
        var text = Read("deploy/RC61_ACCEPTANCE_CONTROL_TOOLKIT.md");
        Assert.Contains("RC.61 product/deployment bytes remain unchanged", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("must not be rebuilt or repackaged", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not replace RC.61 application/deployment bytes", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Toolkit_DefinesExactlySixAcceptanceControlScriptsPlusManifestAndLock()
    {
        var text = Read("deploy/RC61_ACCEPTANCE_CONTROL_TOOLKIT.md");
        foreach (var file in RequiredFiles)
            Assert.Contains($"`{file}`", text, StringComparison.Ordinal);
        Assert.Contains("`toolkit-manifest.json`", text, StringComparison.Ordinal);
        Assert.Contains("`toolkit-manifest.sha256`", text, StringComparison.Ordinal);
        Assert.Contains("Do not add a seventh acceptance-control script", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Toolkit_RequiresExactImmutableProvenanceHardenedCommitRatherThanMovingRef()
    {
        var text = Read("deploy/RC61_ACCEPTANCE_CONTROL_TOOLKIT.md");
        Assert.Contains("exact final PR #262 head", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PR #259 head remains historical evidence", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not use `main`, `latest`, a moving branch name", text, StringComparison.Ordinal);
        Assert.Contains("operatorToolingCommit", text, StringComparison.Ordinal);
        Assert.Contains("operatorToolkitManifestSha256", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Toolkit_BindsCommitManifestHashAndSixFileHashesIntoLockedSession()
    {
        var doc = Read("deploy/RC61_ACCEPTANCE_CONTROL_TOOLKIT.md");
        var initializer = Read("scripts/New-ProductionAcceptanceSession.ps1");
        var binding = Read("scripts/Test-ProductionAcceptanceSessionBinding.ps1");
        Assert.Contains("operatorToolkitManifestSha256", doc, StringComparison.Ordinal);
        Assert.Contains("operatorToolingFiles", doc, StringComparison.Ordinal);
        Assert.Contains("operatorToolkitManifestSha256 = $expectedToolkitManifestHash", initializer, StringComparison.Ordinal);
        Assert.Contains("operatorToolingFiles = $operatorToolingFiles", initializer, StringComparison.Ordinal);
        Assert.Contains("Assert-ExactProperties -Value $manifest.operatorToolingFiles", binding, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash -LiteralPath $toolkitManifestPath -Algorithm SHA256", binding, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash -LiteralPath $actualToolPath -Algorithm SHA256", binding, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionRunbook_SeparatesVerifiedSidecarAcceptanceControlsFromCandidateBundledDeploymentTools()
    {
        var text = Read("docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md");
        Assert.Contains("Acceptance Control Toolkit sidecar — RC.61 remains immutable", text, StringComparison.Ordinal);
        Assert.Contains("Export-ProductionAcceptanceToolkit.ps1", text, StringComparison.Ordinal);
        Assert.Contains("Test-ProductionAcceptanceToolkit.ps1", text, StringComparison.Ordinal);
        Assert.Contains("-ExpectedOperatorToolkitManifestSha256", text, StringComparison.Ordinal);
        Assert.Contains("candidate-bundled", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$acceptanceTools\\Set-ProductionAcceptanceGate.ps1", text, StringComparison.Ordinal);
        Assert.Contains("$acceptanceTools\\Complete-ProductionAcceptance.ps1", text, StringComparison.Ordinal);
        Assert.Contains("$acceptanceTools\\Test-ProductionAcceptanceEvidence.ps1", text, StringComparison.Ordinal);
        Assert.Contains("_operations\\scripts\\Deploy-ProductionSingleNode.ps1", text, StringComparison.Ordinal);
    }

    private static readonly string[] RequiredFiles =
    [
        "New-ProductionAcceptanceSession.ps1",
        "New-ProductionAcceptanceEvidencePack.ps1",
        "Test-ProductionAcceptanceSessionBinding.ps1",
        "Set-ProductionAcceptanceGate.ps1",
        "Complete-ProductionAcceptance.ps1",
        "Test-ProductionAcceptanceEvidence.ps1"
    ];

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
