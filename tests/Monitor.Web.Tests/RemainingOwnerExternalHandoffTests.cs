using Xunit;

namespace Monitor.Web.Tests;

public sealed class RemainingOwnerExternalHandoffTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void CanonicalHandoff_LocksImmutableRc61IdentityAndGateSeparation()
    {
        var handoff = Read("deploy/REMAINING_OWNER_EXTERNAL_GATES.md");

        foreach (var value in new[]
        {
            "0.1.0-rc.61",
            "v0.1.0-rc.61",
            "Monitor-0.1.0-rc.61-win-x64.zip",
            "Monitor-0.1.0-rc.61-win-x64.zip.sha256",
            "d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5",
            "9168574442",
            "sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382",
            "e28158da67b36dfc5dbf8f4c38b5c43d99c7c728",
            "158148d8bfd05f724014541bc7a0b1eab5dae1b5",
            "b422eaaee53d931a62a43b3c36a53b68cd4f3e27"
        })
        {
            Assert.Contains(value, handoff, StringComparison.Ordinal);
        }

        Assert.Contains("#162 -> #116 -> #111", handoff, StringComparison.Ordinal);
        Assert.Contains("# #162 — OWNER_ONLY durable RC.61 release", handoff, StringComparison.Ordinal);
        Assert.Contains("# #116 — EXTERNAL trusted-IIS real 15/15 acceptance", handoff, StringComparison.Ordinal);
        Assert.Contains("This does **not** pass any #116 gate.", handoff, StringComparison.Ordinal);
        Assert.Contains("#162 retention is separate and never counts as one of these 15 gates", handoff, StringComparison.Ordinal);
        Assert.Contains("ExternalGatesPassed                 0", handoff, StringComparison.Ordinal);
        Assert.Contains("PreparedFailClosed", handoff, StringComparison.Ordinal);
        Assert.Contains("Production acceptance evidence PASS: 15/15 external gates verified with matching evidence hashes.", handoff, StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalHandoff_ContainsExactOwnerCommandsEvidenceRootsRollbackAndStops()
    {
        var handoff = Read("deploy/REMAINING_OWNER_EXTERNAL_GATES.md");

        foreach (var value in new[]
        {
            ".\\scripts\\Invoke-Rc61DurablePromotion.ps1",
            ".\\scripts\\Invoke-Rc61DurablePromotion.ps1 -AcknowledgePromotion",
            "PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED",
            "IndependentVerificationCommand",
            ".\\scripts\\Test-Rc61CutoverReadiness.ps1",
            "C:\\ProgramData\\Monitor\\OwnerEvidence\\162",
            "gh run cancel $promotionRunId --repo 'walidatiyaai2025-gif/Monitor'",
            ".\\scripts\\Set-MainBranchProtection.ps1",
            ".\\scripts\\Set-MainBranchProtection.ps1 -AcknowledgeProtection",
            "BRANCH_PROTECTION_APPLIED_AND_VERIFIED",
            "C:\\ProgramData\\Monitor\\OwnerEvidence\\353",
            "gh api --method DELETE \"repos/$repo/branches/main/protection\"",
            "C:\\ProgramData\\Monitor\\Cutover\\rc61",
            "C:\\ProgramData\\Monitor\\Acceptance\\p0-5-rc-61",
            "Deploy-ProductionSingleNode.ps1",
            "Test-IisProductionPrerequisites.ps1",
            "Set-ProductionAcceptanceGate.ps1",
            "Complete-ProductionAcceptance.ps1",
            "Test-ProductionAcceptanceEvidence.ps1",
            "C:\\ProgramData\\Monitor\\App_Data\\deployment-current.json",
            "### #162 STOP conditions",
            "### #353 STOP conditions",
            "### #116 STOP conditions"
        })
        {
            Assert.Contains(value, handoff, StringComparison.Ordinal);
        }

        Assert.Contains("allow_force_pushes.enabled                 false", handoff, StringComparison.Ordinal);
        Assert.Contains("allow_deletions.enabled                    false", handoff, StringComparison.Ordinal);
        Assert.Contains("provider app_id for each                   15368", handoff, StringComparison.Ordinal);
    }

    [Fact]
    public void UmbrellaClosure_IsClosureOnlyAndFailClosed()
    {
        var closure = Read("deploy/P0_UMBRELLA_CLOSURE.md");

        foreach (var value in new[]
        {
            "#111 is **not** an independent production or OWNER_ONLY acceptance gate",
            "#162 durable RC.61 publication + separate independent verification",
            "#116 real trusted-IIS/HTTPS SingleNode 15/15 acceptance",
            "Monitor-0.1.0-rc.61-win-x64.zip",
            "d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5",
            "C:\\ProgramData\\Monitor\\OwnerEvidence\\111",
            "gh issue close 111 --repo $repo --reason completed",
            "P0_UMBRELLA_CLOSURE_VERIFIED",
            "gh issue reopen 111 --repo 'walidatiyaai2025-gif/Monitor'",
            "## Explicit STOP conditions"
        })
        {
            Assert.Contains(value, closure, StringComparison.Ordinal);
        }

        Assert.Contains("15/15 PASS", closure, StringComparison.Ordinal);
        Assert.Contains("session-manifest.sha256", closure, StringComparison.Ordinal);
        Assert.DoesNotContain("16/16", closure, StringComparison.Ordinal);
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
