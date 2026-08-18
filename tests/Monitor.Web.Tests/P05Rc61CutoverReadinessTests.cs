using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05Rc61CutoverReadinessTests
{
    private static readonly string Root = FindRoot();
    private const string ScriptPath = "scripts/Test-Rc61CutoverReadiness.ps1";
    private const string HandoffPath = "deploy/RC61_CUTOVER_READINESS.md";

    [Fact]
    public void ReadinessGate_PinsExactRc61AndToolkitIdentity()
    {
        var script = Read(ScriptPath);

        foreach (var value in new[]
        {
            "walidatiyaai2025-gif/Monitor",
            "1329517438",
            "0.1.0-rc.61",
            "v0.1.0-rc.61",
            "158148d8bfd05f724014541bc7a0b1eab5dae1b5",
            "d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5",
            "b422eaaee53d931a62a43b3c36a53b68cd4f3e27",
            "Monitor-$version-win-x64.zip"
        })
        {
            Assert.Contains(value, script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ReadinessGate_RequiresTwoExplicitSuccessfulMainDispatchRunsInOrder()
    {
        var script = Read(ScriptPath);

        Assert.Contains("[long]$PromotionRunId", script, StringComparison.Ordinal);
        Assert.Contains("[long]$VerificationRunId", script, StringComparison.Ordinal);
        Assert.Contains("PromotionRunId and VerificationRunId must identify two separate workflow runs", script, StringComparison.Ordinal);
        Assert.Contains(".github/workflows/promote-existing-candidate.yml", script, StringComparison.Ordinal);
        Assert.Contains(".github/workflows/verify-durable-release.yml", script, StringComparison.Ordinal);
        Assert.Contains("$Run.status -cne 'completed'", script, StringComparison.Ordinal);
        Assert.Contains("$Run.conclusion -cne 'success'", script, StringComparison.Ordinal);
        Assert.Contains("$Run.event -cne 'workflow_dispatch'", script, StringComparison.Ordinal);
        Assert.Contains("$Run.head_branch -cne 'main'", script, StringComparison.Ordinal);
        Assert.Contains("verificationCreatedAt -lt $promotionCompletedAt", script, StringComparison.Ordinal);
        Assert.Contains("Independent verification run was created before the promotion run completed", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadinessGate_VerifiesDurableTagReleaseExactTwoAssetsAndProductDigest()
    {
        var script = Read(ScriptPath);

        Assert.Contains("git/ref/tags/$releaseTag", script, StringComparison.Ordinal);
        Assert.Contains("commits/$releaseTag", script, StringComparison.Ordinal);
        Assert.Contains("releases/tags/$releaseTag", script, StringComparison.Ordinal);
        Assert.Contains("$assets.Count -ne 2", script, StringComparison.Ordinal);
        Assert.Contains("Durable release asset names do not match the exact RC.61 ZIP/checksum contract", script, StringComparison.Ordinal);
        Assert.Contains("$asset.state -cne 'uploaded'", script, StringComparison.Ordinal);
        Assert.Contains("^sha256:[a-f0-9]{64}$", script, StringComparison.Ordinal);
        Assert.Contains("sha256:$productSha256", script, StringComparison.Ordinal);
        Assert.Contains("browser_download_url", script, StringComparison.Ordinal);
        Assert.Contains("releases/download/$releaseTag", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadinessGate_ProvesLockedToolkitSourceIsRetrievable()
    {
        var script = Read(ScriptPath);

        foreach (var file in new[]
        {
            "scripts/Export-ProductionAcceptanceToolkit.ps1",
            "scripts/Test-ProductionAcceptanceToolkit.ps1",
            "scripts/New-ProductionAcceptanceSession.ps1",
            "scripts/New-ProductionAcceptanceEvidencePack.ps1",
            "scripts/Test-ProductionAcceptanceSessionBinding.ps1",
            "scripts/Set-ProductionAcceptanceGate.ps1",
            "scripts/Complete-ProductionAcceptance.ps1",
            "scripts/Test-ProductionAcceptanceEvidence.ps1"
        })
        {
            Assert.Contains(file, script, StringComparison.Ordinal);
        }

        Assert.Contains("repos/$Repository/commits/$operatorToolingCommit", script, StringComparison.Ordinal);
        Assert.Contains("contents/$encodedPath`?ref=$operatorToolingCommit", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadinessGate_IsReadOnlyAndCannotManufactureProductionAcceptance()
    {
        var script = Read(ScriptPath);

        Assert.Contains("Status = 'READY_FOR_P0_5_PRE_CUTOVER_PREPARATION'", script, StringComparison.Ordinal);
        Assert.Contains("DurableReleasePrerequisiteSatisfied = $true", script, StringComparison.Ordinal);
        Assert.Contains("ExternalGatesPassed = 0", script, StringComparison.Ordinal);
        Assert.Contains("ProductionMutationPerformed = $false", script, StringComparison.Ordinal);
        Assert.Contains("MutatedGitHubState = $false", script, StringComparison.Ordinal);

        Assert.DoesNotContain("gh workflow run", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh release create", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--method POST", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--method PATCH", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--method PUT", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--method DELETE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git tag", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git push", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Deploy-ProductionSingleNode.ps1", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Set-ProductionAcceptanceGate.ps1' -", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Handoff_UsesReadinessGateOnlyAfterPromotionAndIndependentVerification()
    {
        var handoff = Read(HandoffPath);

        Assert.Contains("Test-Rc61CutoverReadiness.ps1", handoff, StringComparison.Ordinal);
        Assert.Contains("-PromotionRunId", handoff, StringComparison.Ordinal);
        Assert.Contains("-VerificationRunId", handoff, StringComparison.Ordinal);
        Assert.Contains("READY_FOR_P0_5_PRE_CUTOVER_PREPARATION", handoff, StringComparison.Ordinal);
        Assert.Contains("does not dispatch", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not close #162", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0/15", handoff, StringComparison.Ordinal);
        Assert.Contains("b422eaaee53d931a62a43b3c36a53b68cd4f3e27", handoff, StringComparison.Ordinal);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRoot()
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
        throw new DirectoryNotFoundException("Could not locate Monitor.sln for RC.61 cutover readiness tests.");
    }
}
