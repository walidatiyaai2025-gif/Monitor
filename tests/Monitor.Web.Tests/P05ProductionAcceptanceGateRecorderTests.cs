using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ProductionAcceptanceGateRecorderTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Recorder_RequiresExplicitOperatorAcknowledgementAndAllowlistedGate()
    {
        var text = Read("scripts/Set-ProductionAcceptanceGate.ps1");
        Assert.Contains("[ValidateSet(", text, StringComparison.Ordinal);
        Assert.Contains("[switch]$AcknowledgePass", text, StringComparison.Ordinal);
        Assert.Contains("requires explicit -AcknowledgePass", text, StringComparison.Ordinal);
        Assert.Contains("The recorder never infers PASS from file presence", text, StringComparison.Ordinal);
        Assert.Contains("artifactChecksumVerified", text, StringComparison.Ordinal);
        Assert.Contains("finalReadEvidencePassed", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Recorder_RequiresExternallyPreservedSessionManifestAnchorBeforePassMutation()
    {
        var text = Read("scripts/Set-ProductionAcceptanceGate.ps1");
        Assert.Contains("ExpectedSessionManifestSha256", text, StringComparison.Ordinal);
        Assert.Contains("ValidatePattern('^[a-fA-F0-9]{64}$')", text, StringComparison.Ordinal);
        Assert.Contains("Test-ProductionAcceptanceSessionBinding.ps1", text, StringComparison.Ordinal);
        var binding = text.IndexOf("-ExpectedSessionManifestSha256 $ExpectedSessionManifestSha256", StringComparison.Ordinal);
        var mutation = text.IndexOf("$gate.passed = $true", StringComparison.Ordinal);
        Assert.True(binding >= 0 && mutation > binding);
    }

    [Fact]
    public void Recorder_BindsOnlyRelativeInRootEvidenceWithComputedSha256()
    {
        var text = Read("scripts/Set-ProductionAcceptanceGate.ps1");
        Assert.Contains("[IO.Path]::IsPathRooted($EvidenceFile)", text, StringComparison.Ordinal);
        Assert.Contains("EvidenceFile escapes the evidence-pack root", text, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash -LiteralPath $targetFull -Algorithm SHA256", text, StringComparison.Ordinal);
        Assert.Contains("$gate.evidenceSha256 = $evidenceHash", text, StringComparison.Ordinal);
        Assert.Contains("$gate.verifiedAtUtc = $verifiedAtUtc", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Recorder_RejectsUnsafeEvidenceAndClosedOrContradictoryPacks()
    {
        var text = Read("scripts/Set-ProductionAcceptanceGate.ps1");
        Assert.Contains("SqlException", text, StringComparison.Ordinal);
        Assert.Contains("Login failed for user", text, StringComparison.Ordinal);
        Assert.Contains("select|insert|update|delete|drop|alter|create", text, StringComparison.Ordinal);
        Assert.Contains("secret-like", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("already contains final operator acceptance metadata and is immutable", text, StringComparison.Ordinal);
        Assert.Contains("contains contradictory evidence metadata", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Recorder_DoesNotCreateClosureOrMutateAcceptanceMetadata()
    {
        var text = Read("scripts/Set-ProductionAcceptanceGate.ps1");
        Assert.DoesNotContain("ClosureSummaryPath", text, StringComparison.Ordinal);
        Assert.DoesNotContain("acceptedBy =", text, StringComparison.Ordinal);
        Assert.DoesNotContain("acceptedAtUtc =", text, StringComparison.Ordinal);
        Assert.Contains("Final P0.5 closure still requires all 15 gates plus Test-ProductionAcceptanceEvidence.ps1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Recorder_ProtectsExistingPassUnlessReplacementIsExplicit()
    {
        var text = Read("scripts/Set-ProductionAcceptanceGate.ps1");
        Assert.Contains("[switch]$ReplaceExistingPass", text, StringComparison.Ordinal);
        Assert.Contains("already PASS", text, StringComparison.Ordinal);
        Assert.Contains("Use -ReplaceExistingPass", text, StringComparison.Ordinal);
        Assert.Contains("ReplacedExistingPass = $wasPassed", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsCandidate_ExecutesSessionBoundRecorderWithDriftNegativeCases()
    {
        var workflow = Read(".github/workflows/production-candidate.yml");
        var runtime = Read("scripts/Test-ProductionAcceptanceSessionChain.ps1");
        Assert.Contains("scripts/Test-ProductionAcceptanceSessionChain.ps1 -Mode Recorder", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/Test-ProductionAcceptanceSessionBinding.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("recorder without acknowledgement unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("traversal evidence unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secret evidence unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("duplicate PASS unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wrong expected session-manifest hash unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("candidate identity drifted from the locked session", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("candidate bytes that drifted", runtime, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Runbook_UsesRecorderButKeepsFinalValidatorAsClosureAuthority()
    {
        var text = Read("docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md");
        Assert.Contains("Set-ProductionAcceptanceGate.ps1", text, StringComparison.Ordinal);
        Assert.Contains("AcknowledgePass", text, StringComparison.Ordinal);
        Assert.Contains("one gate at a time", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Test-ProductionAcceptanceEvidence.ps1", text, StringComparison.Ordinal);
        Assert.Contains("must remain OPEN", text, StringComparison.OrdinalIgnoreCase);
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
