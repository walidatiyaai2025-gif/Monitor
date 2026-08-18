using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800CloseoutLedgerGuardTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Batch800_RemainsFailClosedUntilFinalCloseoutIsDeliberatelyReconciled()
    {
        var batch = Read("docs/BATCH_800.md");

        Assert.Contains("**State:** IN PROGRESS", batch, StringComparison.Ordinal);
        Assert.DoesNotContain("**State:** COMPLETE", batch, StringComparison.Ordinal);
        Assert.Contains("- [x] B800-094", batch, StringComparison.Ordinal);
        Assert.Contains("- [ ] B800-095..100 continue final canonical exact-head and closeout acceptance", batch, StringComparison.Ordinal);
        Assert.Contains("BATCH-800 repository/product work does not publish or supersede selected RC.61 and cannot satisfy #162/#116/#111.", batch, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalUncheckedTasks_AreExplicitlyInventoriedBeforeReconciliation()
    {
        var batch = Read("docs/BATCH_800.md");
        var note = Read("docs/work/B800-095.md");

        var staleLedgerTasks = new[]
        {
            "B800-020",
            "B800-065",
            "B800-066",
            "B800-067",
            "B800-068",
            "B800-070"
        };

        foreach (var task in staleLedgerTasks)
        {
            Assert.Contains($"- [ ] {task}", batch, StringComparison.Ordinal);
            Assert.Contains($"`{task}`", note, StringComparison.Ordinal);
        }

        Assert.Contains("0d9c05d6c3c2b2980a6c3c8bbfbe241dc305860a", note, StringComparison.Ordinal);
        Assert.Contains("d831f77159e43b446aa7549db5a6d74cd23a3f0e", note, StringComparison.Ordinal);
        Assert.Contains("fad66ef563300f0aaedf8fad472b377ca55db648", note, StringComparison.Ordinal);
        Assert.Contains("1b9518e7ceb813368106f5a04483817414f047b1", note, StringComparison.Ordinal);
        Assert.Contains("8e5aea353b8255849b2c82675ec8f0b5443e88db", note, StringComparison.Ordinal);
        Assert.Contains("3073c3a5b4b802b24a3b59218ee93e1208f534a3", note, StringComparison.Ordinal);
    }

    [Fact]
    public void B800094_FinalMergeEvidence_IsCapturedBeforeNextCanonicalRewrite()
    {
        var note = Read("docs/work/B800-095.md");

        Assert.Contains("PR #329", note, StringComparison.Ordinal);
        Assert.Contains("64488940674a39304010901cb87c2025ba3376a9", note, StringComparison.Ordinal);
        Assert.Contains("67ee71224708153eecc31cf495148ffff00f50dc", note, StringComparison.Ordinal);
        Assert.Contains("32086916585", note, StringComparison.Ordinal);
        Assert.Contains("32086916571", note, StringComparison.Ordinal);
        Assert.Contains("32086916578", note, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationOnlyLegacyItems_AreNotSilentlyDeclaredCompleteByThisSlice()
    {
        var batch = Read("docs/BATCH_800.md");
        var note = Read("docs/work/B800-095.md");

        foreach (var task in new[] { "B800-039", "B800-040", "B800-050", "B800-062" })
        {
            Assert.Contains($"- [ ] {task}", batch, StringComparison.Ordinal);
            Assert.Contains($"`{task}`", note, StringComparison.Ordinal);
        }

        Assert.Contains("B800-096", note, StringComparison.Ordinal);
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
