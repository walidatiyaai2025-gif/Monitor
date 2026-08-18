using Xunit;

namespace Monitor.Web.Tests;

public sealed class CanonicalCloseoutStateTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void CanonicalTracking_KeepsBatch800ClosedAndEvidenceBound()
    {
        var status = Read("docs/STATUS.md");
        var catalog = Read("docs/FEATURE_CATALOG.md");
        var plan = Read("docs/IMPLEMENTATION_PLAN.md");

        Assert.Contains("**Umbrella:** #287 — CLOSED / COMPLETED", status, StringComparison.Ordinal);
        Assert.Contains("Issue #287 CLOSED / COMPLETED", catalog, StringComparison.Ordinal);
        Assert.Contains("**Umbrella:** Issue #287 — CLOSED / COMPLETED", plan, StringComparison.Ordinal);

        foreach (var value in new[] { status, catalog, plan })
        {
            Assert.Contains("a6832d99f629cdbd3a93887199fe608a3ae474ec", value, StringComparison.Ordinal);
            Assert.Contains("4379dbc0e1b346cb51bebf8e7467823c58f2361c", value, StringComparison.Ordinal);
            Assert.Contains("32093252549", value, StringComparison.Ordinal);
            Assert.Contains("32093252670", value, StringComparison.Ordinal);
            Assert.Contains("32093252563", value, StringComparison.Ordinal);

            Assert.DoesNotContain("Issue #287 remains OPEN", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Issue #287 — OPEN", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("BATCH-800 — Full functional operator wiring — IN PROGRESS", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Issue #287 remains open until the final closeout PR merges", value, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
