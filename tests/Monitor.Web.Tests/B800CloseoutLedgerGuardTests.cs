using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800CloseoutLedgerGuardTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Batch800_RemainsFailClosedWhileFinalCloseoutContinues()
    {
        var batch = Read("docs/BATCH_800.md");

        Assert.Contains("**State:** IN PROGRESS", batch, StringComparison.Ordinal);
        Assert.DoesNotContain("**State:** COMPLETE", batch, StringComparison.Ordinal);
        Assert.Contains("- [x] B800-096", batch, StringComparison.Ordinal);
        Assert.Contains("- [ ] B800-097..100 continue final canonical exact-head and closeout acceptance", batch, StringComparison.Ordinal);
        Assert.Contains("BATCH-800 repository/product work does not publish or supersede selected RC.61 and cannot satisfy #162/#116/#111.", batch, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalLedgerRows_AreReconciledOnlyAgainstExplicitEvidenceMap()
    {
        var batch = Read("docs/BATCH_800.md");
        var evidence = Read("docs/work/B800-096.md");

        var reconciledTasks = new[]
        {
            "B800-020",
            "B800-039",
            "B800-040",
            "B800-050",
            "B800-062",
            "B800-065",
            "B800-066",
            "B800-067",
            "B800-068",
            "B800-070"
        };

        foreach (var task in reconciledTasks)
        {
            Assert.Contains($"- [x] {task}", batch, StringComparison.Ordinal);
            Assert.Contains(task, evidence, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("- [ ] B800-020", batch, StringComparison.Ordinal);
        Assert.DoesNotContain("- [ ] B800-039", batch, StringComparison.Ordinal);
        Assert.DoesNotContain("- [ ] B800-062", batch, StringComparison.Ordinal);
        Assert.DoesNotContain("- [ ] B800-065", batch, StringComparison.Ordinal);
        Assert.DoesNotContain("- [ ] B800-070", batch, StringComparison.Ordinal);
    }

    [Fact]
    public void FinalAcceptanceLedger_Records095And096ButNotFutureCompletion()
    {
        var batch = Read("docs/BATCH_800.md");
        var note095 = Read("docs/work/B800-095.md");
        var note096 = Read("docs/work/B800-096.md");
        var note097 = Read("docs/work/B800-097.md");

        Assert.Contains("- [x] B800-095", batch, StringComparison.Ordinal);
        Assert.Contains("- [x] B800-096", batch, StringComparison.Ordinal);
        Assert.Contains("PR #330", note095, StringComparison.Ordinal);
        Assert.Contains("PR #331", note096, StringComparison.Ordinal);
        Assert.Contains("B800-097..100", note097, StringComparison.Ordinal);
        Assert.DoesNotContain("BATCH-800 `COMPLETE`", note097, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticTruthBoundary_IsUpdatedWithoutInventingCompositeReadiness()
    {
        var batch = Read("docs/BATCH_800.md");
        var note = Read("docs/work/B800-097.md");

        Assert.Contains("TempDB, transaction-log and HA", batch, StringComparison.Ordinal);
        Assert.Contains("query regression", batch, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NotEvaluated", note, StringComparison.Ordinal);
        Assert.Contains("No positive multi-replica AG acceptance claim", note, StringComparison.Ordinal);
        Assert.Contains("No live query text/plan collection", note, StringComparison.Ordinal);
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
