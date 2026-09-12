using Xunit;

namespace Monitor.Web.Tests;

public sealed class RemainingOwnerExternalHandoffTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void CanonicalHandoff_LocksSelectedRc854IdentityAndGateSeparation()
    {
        var handoff = Read("deploy/REMAINING_OWNER_EXTERNAL_GATES.md");

        foreach (var value in new[]
        {
            "0.1.0-rc.854",
            "v0.1.0-rc.854",
            "Monitor-0.1.0-rc.854-win-x64.zip",
            "Monitor-0.1.0-rc.854-win-x64.zip.sha256",
            "b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7",
            "10303396821",
            "34710820438",
            "sha256:e1f0b7facc756758a13653c3ad2bfa5a4af9107b02e14f4682aaaabb286f01e3",
            "ef7209cbf099da65330887508ab4380a8b4196d2",
            "e1d0daedf8b2209934a1bcd01bff5d46229df20a"
        })
        {
            Assert.Contains(value, handoff, StringComparison.Ordinal);
        }

        Assert.Contains("RC.61 is historical only", handoff, StringComparison.Ordinal);
        Assert.Contains("#162 -> #116 -> #111", handoff, StringComparison.Ordinal);
        Assert.Contains("# #162 — OWNER_ONLY durable publication", handoff, StringComparison.Ordinal);
        Assert.Contains("# #116 — EXTERNAL_ENVIRONMENT production acceptance", handoff, StringComparison.Ordinal);
        Assert.Contains("ExternalGatesPassed = 0", handoff, StringComparison.Ordinal);
        Assert.Contains("0/15", handoff, StringComparison.Ordinal);
        Assert.Contains("15/15", handoff, StringComparison.Ordinal);
        Assert.Contains("Do not mutate production until #162", handoff, StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalHandoff_ContainsExactOwnerCommandsAndFailClosedBoundaries()
    {
        var handoff = Read("deploy/REMAINING_OWNER_EXTERNAL_GATES.md");

        foreach (var value in new[]
        {
            "pwsh ./scripts/Invoke-SelectedDurablePromotion.ps1",
            "pwsh ./scripts/Invoke-SelectedDurablePromotion.ps1 -AcknowledgePromotion",
            "READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT",
            "IndependentVerificationCommand",
            "verify-durable-release.yml",
            "C:\\ProgramData\\Monitor\\OwnerEvidence\\162-rc854",
            "pwsh ./scripts/Set-MainBranchProtection.ps1",
            "pwsh ./scripts/Set-MainBranchProtection.ps1 -AcknowledgeProtection",
            "BRANCH_PROTECTION_APPLIED_AND_VERIFIED",
            "build",
            "protected-p0-pr-metadata",
            "protected-p0-pr-commits"
        })
        {
            Assert.Contains(value, handoff, StringComparison.Ordinal);
        }

        Assert.Contains("DO NOT REDISPATCH", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("force pushes/deletion disabled", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("independent admin read-back", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VERIFIED_FINAL_COMPLETE", handoff, StringComparison.Ordinal);
    }

    [Fact]
    public void UmbrellaClosure_IsClosureOnlyAndFailClosedForSelectedCandidate()
    {
        var closure = Read("deploy/P0_UMBRELLA_CLOSURE.md");

        foreach (var value in new[]
        {
            "#111 is **not** an independent production or OWNER_ONLY acceptance gate",
            "#162 durable selected-candidate publication + separate independent verification",
            "#116 real trusted-IIS/HTTPS SingleNode 15/15 acceptance",
            "Monitor-0.1.0-rc.854-win-x64.zip",
            "b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7",
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
        Assert.Contains("RC.61 is historical only", closure, StringComparison.Ordinal);
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
