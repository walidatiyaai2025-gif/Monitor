using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ProductionMvpReconciliationTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void ProductionMvp_TracksCurrentRepositoryAndManualRetentionState()
    {
        var ledger = Read("docs/PRODUCTION_MVP.md");

        Assert.DoesNotContain("complete through #154 / PR #155", ledger, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COMPLETE — repository/CI; RC.61 verified", ledger, StringComparison.Ordinal);

        Assert.Contains("COMPLETE through PR #219", ledger, StringComparison.Ordinal);
        Assert.Contains("75661cfc730f60667d1786a9bcd6ca9427ef2faa", ledger, StringComparison.Ordinal);
        Assert.Contains("3f046143c4dd4e86059d9eb33c55cd2514073fc3", ledger, StringComparison.Ordinal);
        Assert.Contains("actual RC.61 durable publication + separate read-only verification PENDING MANUAL #162", ledger, StringComparison.Ordinal);
        Assert.Contains("verify-durable-release.yml", ledger, StringComparison.Ordinal);
        Assert.Contains("contents: read", ledger, StringComparison.Ordinal);
        Assert.Contains("sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382", ledger, StringComparison.Ordinal);
        Assert.Contains("2026-09-12T04:41:34Z", ledger, StringComparison.Ordinal);
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
