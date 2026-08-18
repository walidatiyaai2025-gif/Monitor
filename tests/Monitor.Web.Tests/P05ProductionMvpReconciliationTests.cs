using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ProductionMvpReconciliationTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void ProductionMvp_TracksCurrentRepositoryAndExplicitOperatorRetentionState()
    {
        var ledger = Read("docs/PRODUCTION_MVP.md");

        Assert.DoesNotContain("complete through #154 / PR #155", ledger, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COMPLETE — repository/CI; RC.61 verified", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("actual RC.61 durable publication + separate read-only verification PENDING MANUAL #162", ledger, StringComparison.Ordinal);

        Assert.Contains("durable-release hardening PR #219", ledger, StringComparison.Ordinal);
        Assert.Contains("75661cfc730f60667d1786a9bcd6ca9427ef2faa", ledger, StringComparison.Ordinal);
        Assert.Contains("3f046143c4dd4e86059d9eb33c55cd2514073fc3", ledger, StringComparison.Ordinal);
        Assert.Contains("f129e63b8ae9e83dda4f89d49e40892f4f36af56", ledger, StringComparison.Ordinal);
        Assert.Contains("dfabec7f8cde7953a3f9c1fb5142b56774949537", ledger, StringComparison.Ordinal);
        Assert.Contains("3cd711b608e4ceaf8872eb22a25541bbbfe2729a", ledger, StringComparison.Ordinal);
        Assert.Contains("actual RC.61 acknowledged promotion + separate read-only verification + explicit run-ID readiness PENDING OPERATOR #162", ledger, StringComparison.Ordinal);
        Assert.Contains("Invoke-Rc61DurablePromotion.ps1", ledger, StringComparison.Ordinal);
        Assert.Contains("READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT", ledger, StringComparison.Ordinal);
        Assert.Contains("-AcknowledgePromotion", ledger, StringComparison.Ordinal);
        Assert.Contains("PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED", ledger, StringComparison.Ordinal);
        Assert.Contains("IndependentVerificationCommand", ledger, StringComparison.Ordinal);
        Assert.Contains("do not redispatch", ledger, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("verify-durable-release.yml", ledger, StringComparison.Ordinal);
        Assert.Contains("contents: read", ledger, StringComparison.Ordinal);
        Assert.Contains("Test-Rc61CutoverReadiness.ps1", ledger, StringComparison.Ordinal);
        Assert.Contains("ExternalGatesPassed = 0", ledger, StringComparison.Ordinal);
        Assert.Contains("sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382", ledger, StringComparison.Ordinal);
        Assert.Contains("tag `v0.1.0-rc.61` absent", ledger, StringComparison.Ordinal);
        Assert.Contains("Close #116 only after the actual external evidence is valid", ledger, StringComparison.Ordinal);
        Assert.Contains("close umbrella #111 only after #116", ledger, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionMvp_PreservesAllP0TaskIdentifiersAndExternalGateTruth()
    {
        var ledger = Read("docs/PRODUCTION_MVP.md");

        for (var task = 1; task <= 50; task++)
        {
            Assert.Contains($"P0-{task:000}", ledger, StringComparison.Ordinal);
        }

        Assert.Contains("P0-043", ledger, StringComparison.Ordinal);
        Assert.Contains("PENDING EXTERNAL", ledger, StringComparison.Ordinal);
        Assert.Contains("P0-050", ledger, StringComparison.Ordinal);
        Assert.Contains("A Green candidate pipeline, successful durable publication", ledger, StringComparison.Ordinal);
        Assert.Contains("cannot close #116/#111", ledger, StringComparison.Ordinal);
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Root, relative)).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
