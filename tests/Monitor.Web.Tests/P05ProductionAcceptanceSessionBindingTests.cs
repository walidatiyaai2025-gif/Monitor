using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ProductionAcceptanceSessionBindingTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void BindingVerifier_RequiresExternalManifestHashAndCanonicalSessionLayout()
    {
        var text = Read("scripts/Test-ProductionAcceptanceSessionBinding.ps1");
        Assert.Contains("ExpectedSessionManifestSha256", text, StringComparison.Ordinal);
        Assert.Contains("ValidatePattern('^[a-fA-F0-9]{64}$')", text, StringComparison.Ordinal);
        Assert.Contains("p0-5-evidence-pack.json", text, StringComparison.Ordinal);
        Assert.Contains("session-manifest.json", text, StringComparison.Ordinal);
        Assert.Contains("session-manifest.sha256", text, StringComparison.Ordinal);
        Assert.Contains("evidence/p0-5-evidence-pack.json", text, StringComparison.Ordinal);
        Assert.Contains("evidence/proof", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BindingVerifier_AuthenticatesManifestAgainstExternallyPreservedShaAndLockFile()
    {
        var text = Read("scripts/Test-ProductionAcceptanceSessionBinding.ps1");
        Assert.Contains("Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256", text, StringComparison.Ordinal);
        Assert.Contains("externally preserved expected session-manifest SHA-256", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$manifestLockLine -cne \"$expectedManifestHash  session-manifest.json\"", text, StringComparison.Ordinal);
        Assert.Contains("PreparedFailClosed", text, StringComparison.Ordinal);
        Assert.Contains("original fail-closed 0/15 anchor", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BindingVerifier_RetainsToolkitManifestSidecarIdentityAndCandidateChain()
    {
        var text = Read("scripts/Test-ProductionAcceptanceSessionBinding.ps1");
        Assert.Contains("operatorToolingCommit", text, StringComparison.Ordinal);
        Assert.Contains("operatorToolkitManifestSha256", text, StringComparison.Ordinal);
        Assert.Contains("full 40-hex repository commit SHA", text, StringComparison.Ordinal);
        Assert.Contains("OperatorToolingCommit = $operatorToolingCommit", text, StringComparison.Ordinal);
        Assert.Contains("OperatorToolkitManifestSha256 = $operatorToolkitManifestHash", text, StringComparison.Ordinal);
        Assert.Contains("Acceptance Control Toolkit manifest SHA-256 drifted from the locked session manifest", text, StringComparison.Ordinal);
        Assert.Contains("toolkit-manifest.sha256 drifted from the locked session manifest", text, StringComparison.Ordinal);
        Assert.Contains("Session candidate artifact bytes no longer match the selected product SHA-256", text, StringComparison.Ordinal);
        Assert.Contains("Session candidate checksum no longer matches the selected product SHA-256", text, StringComparison.Ordinal);
        Assert.Contains("candidate.sourceCommit", text, StringComparison.Ordinal);
        Assert.Contains("candidate.testedMergeCommit", text, StringComparison.Ordinal);
        Assert.Contains("candidate.sha256", text, StringComparison.Ordinal);
        Assert.Contains("environment.hostName", text, StringComparison.Ordinal);
        Assert.Contains("environment.operationalBackupId", text, StringComparison.Ordinal);
        Assert.Contains("does not match the locked acceptance session", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RecorderAndFinalizerRequireBindingBeforeAuthoritativeMutation()
    {
        var recorder = Read("scripts/Set-ProductionAcceptanceGate.ps1");
        var finalizer = Read("scripts/Complete-ProductionAcceptance.ps1");
        Assert.Contains("Test-ProductionAcceptanceSessionBinding.ps1", recorder, StringComparison.Ordinal);
        Assert.Contains("ExpectedSessionManifestSha256", recorder, StringComparison.Ordinal);
        Assert.Contains("Test-ProductionAcceptanceSessionBinding.ps1", finalizer, StringComparison.Ordinal);
        Assert.Contains("ExpectedSessionManifestSha256", finalizer, StringComparison.Ordinal);
        Assert.True(recorder.IndexOf("Test-ProductionAcceptanceSessionBinding.ps1", StringComparison.Ordinal) < recorder.IndexOf("$gate.passed = $true", StringComparison.Ordinal));
        Assert.True(finalizer.LastIndexOf("-ExpectedSessionManifestSha256 $ExpectedSessionManifestSha256 | Out-Null", StringComparison.Ordinal) < finalizer.IndexOf("Move-Item -LiteralPath $prospectivePath -Destination $resolvedPackPath -Force", StringComparison.Ordinal));
    }

    [Fact]
    public void WindowsCandidate_ParsesExercisesAndPackagesBindingVerifierFromVerifiedToolkit()
    {
        var workflow = Read(".github/workflows/production-candidate.yml");
        Assert.Contains("scripts/Test-ProductionAcceptanceSessionBinding.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/Test-ProductionAcceptanceSessionChain.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("Exercise explicit acceptance gate recorder", workflow, StringComparison.Ordinal);
        Assert.Contains("Exercise final operator acceptance finalizer", workflow, StringComparison.Ordinal);
        Assert.Contains("Export verified Acceptance Control Toolkit for candidate operations", workflow, StringComparison.Ordinal);
        Assert.Contains("$env:MONITOR_PACKAGED_TOOLKIT_ROOT/Test-ProductionAcceptanceSessionBinding.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("$ops/acceptance-control-toolkit", workflow, StringComparison.Ordinal);
        Assert.Contains("toolkit-manifest.json", workflow, StringComparison.Ordinal);
        Assert.Contains("toolkit-manifest.sha256", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeChainRejectsPackManifestCandidateAndToolingDrift()
    {
        var runtime = Read("scripts/Test-ProductionAcceptanceSessionChain.ps1");
        Assert.Contains("candidate identity drifted from the locked session", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("operator tooling identity drifted", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("candidate bytes that drifted from the selected product SHA-256", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Closure summary did not retain the locked session-manifest", runtime, StringComparison.Ordinal);
        Assert.Contains("Closure summary did not retain the acceptance-control sidecar tooling commit", runtime, StringComparison.Ordinal);
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
