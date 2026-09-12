using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05CanonicalOperatorHelperConsistencyTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void CanonicalActiveDocuments_AgreeOnSelectedRc854ReleaseBoundary()
    {
        var documents = new Dictionary<string, string>
        {
            ["STATUS"] = Read("docs/STATUS.md"),
            ["CURRENT_EXECUTION_PLAN"] = Read("docs/CURRENT_EXECUTION_PLAN.md"),
            ["SELECTED_RELEASE_CANDIDATE"] = Read("docs/SELECTED_RELEASE_CANDIDATE.md"),
            ["OWNER_EXTERNAL_HANDOFF"] = Read("deploy/REMAINING_OWNER_EXTERNAL_GATES.md")
        };

        foreach (var (name, text) in documents)
        {
            Assert.Contains("0.1.0-rc.854", text, StringComparison.Ordinal);
            Assert.Contains("b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7", text, StringComparison.Ordinal);
            Assert.Contains("#162", text, StringComparison.Ordinal);
            Assert.Contains("#116", text, StringComparison.Ordinal);
            Assert.Contains("#111", text, StringComparison.Ordinal);
            Assert.Contains("#353", text, StringComparison.Ordinal);
            Assert.Contains("#162 -> #116 -> #111", text, StringComparison.Ordinal);
            Assert.Contains("NOT PASS", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Current operator handoff: PR #271", text, StringComparison.Ordinal);
            Assert.True(text.Contains("no production mutation", StringComparison.OrdinalIgnoreCase) ||
                        text.Contains("ProductionMutationPerformed", StringComparison.Ordinal),
                $"{name} must preserve the no-production-mutation boundary.");
        }

        Assert.Contains("Invoke-SelectedDurablePromotion.ps1", documents["STATUS"], StringComparison.Ordinal);
        Assert.Contains("Invoke-SelectedDurablePromotion.ps1", documents["SELECTED_RELEASE_CANDIDATE"], StringComparison.Ordinal);
        Assert.Contains("Invoke-SelectedDurablePromotion.ps1", documents["OWNER_EXTERNAL_HANDOFF"], StringComparison.Ordinal);
        Assert.Contains("RC.61 is", documents["SELECTED_RELEASE_CANDIDATE"], StringComparison.Ordinal);
        Assert.Contains("historical only", documents["SELECTED_RELEASE_CANDIDATE"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanonicalHandoff_KeepsIndependentVerifierSeparateFromPromotion()
    {
        var handoff = Read("deploy/REMAINING_OWNER_EXTERNAL_GATES.md");
        var preview = handoff.IndexOf("Invoke-SelectedDurablePromotion.ps1", StringComparison.Ordinal);
        var acknowledgement = handoff.IndexOf("-AcknowledgePromotion", StringComparison.Ordinal);
        var verifier = handoff.IndexOf("IndependentVerificationCommand", StringComparison.Ordinal);
        var verifyWorkflow = handoff.IndexOf("verify-durable-release.yml", verifier, StringComparison.Ordinal);

        Assert.True(preview >= 0 && preview < acknowledgement && acknowledgement < verifier && verifier < verifyWorkflow,
            "Active handoff must preserve preview -> explicit acknowledgement -> separate verifier order.");
        Assert.Contains("READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT", handoff, StringComparison.Ordinal);
        Assert.Contains("ExternalGatesPassed = 0", handoff, StringComparison.Ordinal);
        Assert.Contains("DO NOT REDISPATCH", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0/15", handoff, StringComparison.Ordinal);
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
