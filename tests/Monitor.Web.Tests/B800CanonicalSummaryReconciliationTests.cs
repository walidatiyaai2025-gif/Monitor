using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800CanonicalSummaryReconciliationTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void CanonicalSummaries_RetainFinalCloseoutEvidence()
    {
        var status = Read("docs/STATUS.md");
        var catalog = Read("docs/FEATURE_CATALOG.md");
        var plan = Read("docs/IMPLEMENTATION_PLAN.md");

        foreach (var source in new[] { status, catalog, plan })
        {
            Assert.Contains("B800-100", source, StringComparison.Ordinal);
            Assert.Contains("#287", source, StringComparison.Ordinal);
            Assert.Contains("a6832d99f629cdbd3a93887199fe608a3ae474ec", source, StringComparison.Ordinal);
            Assert.Contains("4379dbc0e1b346cb51bebf8e7467823c58f2361c", source, StringComparison.Ordinal);
            Assert.Contains("32093252549", source, StringComparison.Ordinal);
            Assert.Contains("32093252670", source, StringComparison.Ordinal);
            Assert.Contains("32093252563", source, StringComparison.Ordinal);
            Assert.DoesNotContain("B800-098..100", source, StringComparison.Ordinal);
            Assert.DoesNotContain("B800-099..100", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CanonicalSummaries_DoNotRegressToKnownStaleCurrentState()
    {
        var status = Read("docs/STATUS.md");
        var catalog = Read("docs/FEATURE_CATALOG.md");
        var plan = Read("docs/IMPLEMENTATION_PLAN.md");

        Assert.DoesNotContain("Current PR:** #323", status, StringComparison.Ordinal);
        Assert.DoesNotContain("merged through B800-087", status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("current PR #323", status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Issue #287 remains OPEN", status, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("In progress / PR #329", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("TempDB, transaction-log, HA and privacy-safe query-regression evidence remain pending", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("Issue #287 remains open until the final closeout PR merges", catalog, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("Current PR:** #323", plan, StringComparison.Ordinal);
        Assert.DoesNotContain("PR #329 becomes eligible for Ready/merge", plan, StringComparison.Ordinal);
        Assert.DoesNotContain("Issue #287 remains OPEN", plan, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiagnosticSummary_PreservesCollectedVersusUnsupportedBoundary()
    {
        var catalog = Read("docs/FEATURE_CATALOG.md");
        var plan = Read("docs/IMPLEMENTATION_PLAN.md");
        var note = Read("docs/work/B800-098.md");

        foreach (var source in new[] { catalog, plan, note })
        {
            Assert.Contains("TempDB", source, StringComparison.Ordinal);
            Assert.Contains("transaction-log", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("HA", source, StringComparison.Ordinal);
            Assert.Contains("query regression", source, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("NotEvaluated", note, StringComparison.Ordinal);
        Assert.Contains("no live query-regression collection", note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionBoundary_RemainsIndependentFromB800Closeout()
    {
        var status = Read("docs/STATUS.md");
        var plan = Read("docs/IMPLEMENTATION_PLAN.md");
        var note = Read("docs/work/B800-098.md");

        Assert.Contains("#162", status, StringComparison.Ordinal);
        Assert.Contains("#116", status, StringComparison.Ordinal);
        Assert.Contains("#111", status, StringComparison.Ordinal);
        Assert.Contains("#162", plan, StringComparison.Ordinal);
        Assert.Contains("#116", plan, StringComparison.Ordinal);
        Assert.Contains("#111", plan, StringComparison.Ordinal);
        Assert.Contains("#162 -> #116 -> #111", note, StringComparison.Ordinal);
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
