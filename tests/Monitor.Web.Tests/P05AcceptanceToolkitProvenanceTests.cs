using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05AcceptanceToolkitProvenanceTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private const string FinalRc61OperatorToolingCommit = "b422eaaee53d931a62a43b3c36a53b68cd4f3e27";

    [Fact]
    public void Exporter_RequiresExactCleanGitCommitAndFreshExternalOutput()
    {
        var text = Read("scripts/Export-ProductionAcceptanceToolkit.ps1");
        Assert.Contains("ExpectedToolingCommit", text, StringComparison.Ordinal);
        Assert.Contains("rev-parse', '--verify', 'HEAD", text, StringComparison.Ordinal);
        Assert.Contains("status', '--porcelain=v1', '--untracked-files=no", text, StringComparison.Ordinal);
        Assert.Contains("Tracked Git checkout state must be clean", text, StringComparison.Ordinal);
        Assert.Contains("ls-files', '--error-unmatch", text, StringComparison.Ordinal);
        Assert.Contains("OutputDirectory must be fresh", text, StringComparison.Ordinal);
        Assert.Contains("OutputDirectory must be outside the source Git checkout", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Exporter_ProducesCanonicalSixFileManifestAndShaLock()
    {
        var text = Read("scripts/Export-ProductionAcceptanceToolkit.ps1");
        foreach (var file in RequiredFiles)
            Assert.Contains($"'{file}'", text, StringComparison.Ordinal);
        Assert.Contains("toolkit-manifest.json", text, StringComparison.Ordinal);
        Assert.Contains("toolkit-manifest.sha256", text, StringComparison.Ordinal);
        Assert.Contains("toolkitName = 'Monitor Acceptance Control Toolkit'", text, StringComparison.Ordinal);
        Assert.Contains("toolingCommit = $expectedCommit", text, StringComparison.Ordinal);
        Assert.Contains("fileCount = $requiredFiles.Count", text, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash -LiteralPath $target -Algorithm SHA256", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Verifier_RequiresIndependentCommitAndManifestHashAndExactRootSet()
    {
        var text = Read("scripts/Test-ProductionAcceptanceToolkit.ps1");
        Assert.Contains("ExpectedToolingCommit", text, StringComparison.Ordinal);
        Assert.Contains("ExpectedToolkitManifestSha256", text, StringComparison.Ordinal);
        Assert.Contains("missing or extra entries fail closed", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("toolkit-manifest.sha256 does not match the independently supplied", text, StringComparison.Ordinal);
        Assert.Contains("toolingCommit does not match independently supplied ExpectedToolingCommit", text, StringComparison.Ordinal);
        Assert.Contains("Acceptance Control Toolkit file SHA-256 mismatch", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Session_BindsIndependentToolkitManifestHashAlongsideCommitAndSixFiles()
    {
        var initializer = Read("scripts/New-ProductionAcceptanceSession.ps1");
        var binding = Read("scripts/Test-ProductionAcceptanceSessionBinding.ps1");
        Assert.Contains("ExpectedOperatorToolkitManifestSha256", initializer, StringComparison.Ordinal);
        Assert.Contains("operatorToolkitManifestSha256 = $expectedToolkitManifestHash", initializer, StringComparison.Ordinal);
        Assert.Contains("Acceptance Control Toolkit manifest SHA-256 does not match independently supplied", initializer, StringComparison.Ordinal);
        Assert.Contains("operatorToolkitManifestSha256", binding, StringComparison.Ordinal);
        Assert.Contains("Acceptance Control Toolkit manifest SHA-256 drifted from the locked session manifest", binding, StringComparison.Ordinal);
        Assert.Contains("toolkit-manifest.sha256 drifted from the locked session manifest", binding, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsRuntime_ExercisesExportVerifyDirtyWrongCommitAndTamperNegatives()
    {
        var runtime = Read("scripts/Test-ProductionAcceptanceToolingSidecar.ps1");
        Assert.Contains("Export-ProductionAcceptanceToolkit.ps1", runtime, StringComparison.Ordinal);
        Assert.Contains("Test-ProductionAcceptanceToolkit.ps1", runtime, StringComparison.Ordinal);
        Assert.Contains("wrong expected Git commit unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dirty tracked checkout unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tampered Acceptance Control Toolkit manifest unexpectedly passed", runtime, StringComparison.Ordinal);
        Assert.Contains("Extra Acceptance Control Toolkit file unexpectedly passed", runtime, StringComparison.Ordinal);
        Assert.Contains("Modified acceptance-control sidecar file unexpectedly passed", runtime, StringComparison.Ordinal);
        Assert.Contains("Missing acceptance-control sidecar file unexpectedly passed", runtime, StringComparison.Ordinal);
    }

    [Fact]
    public void FutureWindowsCandidate_PackagesVerifiedToolkitManifestWithSixControlFiles()
    {
        var workflow = Read(".github/workflows/production-candidate.yml");
        Assert.Contains("scripts/Export-ProductionAcceptanceToolkit.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/Test-ProductionAcceptanceToolkit.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("monitor-packaged-acceptance-control-toolkit", workflow, StringComparison.Ordinal);
        Assert.Contains("toolkit-manifest.json", workflow, StringComparison.Ordinal);
        Assert.Contains("toolkit-manifest.sha256", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("contents: write", workflow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rc61OperatorHandoffs_PinFinalReviewedToolkitCommitWithoutPlaceholder()
    {
        foreach (var path in new[]
        {
            "deploy/RC61_ACCEPTANCE_CONTROL_TOOLKIT.md",
            "docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md"
        })
        {
            var text = Read(path);
            Assert.Contains(FinalRc61OperatorToolingCommit, text, StringComparison.Ordinal);
            Assert.DoesNotContain("<exact-final-PR-262-head", text, StringComparison.Ordinal);
            Assert.DoesNotContain("After #261 / PR #262 completes", text, StringComparison.Ordinal);
        }
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
