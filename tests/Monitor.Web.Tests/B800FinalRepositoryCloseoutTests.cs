using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800FinalRepositoryCloseoutTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void CanonicalDocuments_DeclareRepositoryBatchCompleteOnlyAtB800100()
    {
        var batch = Read("docs/BATCH_800.md");
        var status = Read("docs/STATUS.md");
        var catalog = Read("docs/FEATURE_CATALOG.md");
        var plan = Read("docs/IMPLEMENTATION_PLAN.md");

        Assert.Contains("**State:** COMPLETE", batch, StringComparison.Ordinal);
        Assert.Contains("100/100 COMPLETE", batch, StringComparison.Ordinal);
        Assert.DoesNotContain("**State:** IN PROGRESS", batch, StringComparison.Ordinal);
        Assert.DoesNotContain("- [ ] B800-", batch, StringComparison.Ordinal);
        Assert.Contains("B800-099", batch, StringComparison.Ordinal);
        Assert.Contains("B800-100", batch, StringComparison.Ordinal);

        foreach (var source in new[] { status, catalog, plan })
        {
            Assert.Contains("BATCH-800", source, StringComparison.Ordinal);
            Assert.Contains("COMPLETE", source, StringComparison.Ordinal);
            Assert.Contains("B800-100", source, StringComparison.Ordinal);
            Assert.DoesNotContain("B800-099..100", source, StringComparison.Ordinal);
            Assert.DoesNotContain("B800-098..100", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FinalCloseout_PreservesExternalProductionBoundary()
    {
        var batch = Read("docs/BATCH_800.md");
        var status = Read("docs/STATUS.md");
        var plan = Read("docs/IMPLEMENTATION_PLAN.md");
        var note = Read("docs/work/B800-100.md");

        foreach (var source in new[] { batch, status, plan, note })
        {
            Assert.Contains("#162", source, StringComparison.Ordinal);
            Assert.Contains("#116", source, StringComparison.Ordinal);
            Assert.Contains("#111", source, StringComparison.Ordinal);
        }

        Assert.Contains("#162 -> #116 -> #111", note, StringComparison.Ordinal);
        Assert.Contains("not production acceptance", note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FinalCloseout_PreservesDiagnosticTruthBoundary()
    {
        var batch = Read("docs/BATCH_800.md");
        var status = Read("docs/STATUS.md");
        var catalog = Read("docs/FEATURE_CATALOG.md");
        var plan = Read("docs/IMPLEMENTATION_PLAN.md");

        foreach (var source in new[] { batch, status, catalog, plan })
        {
            Assert.Contains("TempDB", source, StringComparison.Ordinal);
            Assert.Contains("transaction-log", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("HA", source, StringComparison.Ordinal);
            Assert.Contains("query regression", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CompletedTaskAccounting_IncludesB800WithoutCountingCloseoutPrsTwice()
    {
        var status = Read("docs/STATUS.md");
        var plan = Read("docs/IMPLEMENTATION_PLAN.md");

        Assert.Contains("760", status, StringComparison.Ordinal);
        Assert.Contains("BATCH-800", status, StringComparison.Ordinal);
        Assert.Contains("760", plan, StringComparison.Ordinal);
        Assert.Contains("BATCH-800", plan, StringComparison.Ordinal);
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
