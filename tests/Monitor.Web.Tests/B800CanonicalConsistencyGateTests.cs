using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800CanonicalConsistencyGateTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void CanonicalDocuments_AgreeOnFinalRepositoryCloseoutState()
    {
        var batch = Read("docs/BATCH_800.md");
        var status = Read("docs/STATUS.md");
        var catalog = Read("docs/FEATURE_CATALOG.md");
        var plan = Read("docs/IMPLEMENTATION_PLAN.md");

        foreach (var source in new[] { status, catalog, plan })
        {
            Assert.Contains("B800-100", source, StringComparison.Ordinal);
            Assert.Contains("a6832d99f629cdbd3a93887199fe608a3ae474ec", source, StringComparison.Ordinal);
            Assert.Contains("#287", source, StringComparison.Ordinal);
            Assert.DoesNotContain("B800-098..100", source, StringComparison.Ordinal);
            Assert.DoesNotContain("B800-099..100", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Issue #287 remains OPEN", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Issue #287 remains open until the final closeout PR merges", source, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("**State:** COMPLETE", batch, StringComparison.Ordinal);
        Assert.DoesNotContain("**State:** IN PROGRESS", batch, StringComparison.Ordinal);
        Assert.Contains("- [x] B800-098", batch, StringComparison.Ordinal);
        Assert.Contains("- [x] B800-099", batch, StringComparison.Ordinal);
        Assert.Contains("- [x] B800-100", batch, StringComparison.Ordinal);
        Assert.DoesNotContain("- [ ] B800-", batch, StringComparison.Ordinal);
        Assert.DoesNotContain("## Current B800-099 consistency gate", batch, StringComparison.Ordinal);
        Assert.Contains("## Final B800-100 repository closeout", batch, StringComparison.Ordinal);
    }

    [Fact]
    public void B800100_ExactHeadEvidence_IsConsistentAcrossCanonicalSummaries()
    {
        var status = Read("docs/STATUS.md");
        var catalog = Read("docs/FEATURE_CATALOG.md");
        var plan = Read("docs/IMPLEMENTATION_PLAN.md");

        foreach (var source in new[] { status, catalog, plan })
        {
            Assert.Contains("4379dbc0e1b346cb51bebf8e7467823c58f2361c", source, StringComparison.Ordinal);
            Assert.Contains("32093252549", source, StringComparison.Ordinal);
            Assert.Contains("32093252670", source, StringComparison.Ordinal);
            Assert.Contains("32093252563", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DiagnosticTruthBoundary_RemainsConsistent()
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
    public void ProductionAcceptanceBoundary_RemainsIndependent()
    {
        var batch = Read("docs/BATCH_800.md");
        var status = Read("docs/STATUS.md");
        var plan = Read("docs/IMPLEMENTATION_PLAN.md");
        var note = Read("docs/work/B800-099.md");

        foreach (var source in new[] { batch, status, plan, note })
        {
            Assert.Contains("#162", source, StringComparison.Ordinal);
            Assert.Contains("#116", source, StringComparison.Ordinal);
            Assert.Contains("#111", source, StringComparison.Ordinal);
        }

        Assert.Contains("B800-100", note, StringComparison.Ordinal);
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
