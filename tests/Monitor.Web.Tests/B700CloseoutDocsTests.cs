using Xunit;

namespace Monitor.Web.Tests;

public sealed class B700CloseoutDocsTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void CanonicalDocs_RecordB700MergedAndDoNotRegressToPreMergeState()
    {
        var batch = Read("docs/BATCH_700.md");
        var catalog = Read("docs/FEATURE_CATALOG.md");
        var status = Read("docs/STATUS.md");
        var plan = Read("docs/IMPLEMENTATION_PLAN.md");

        foreach (var source in new[] { batch, catalog, status, plan })
        {
            Assert.Contains("fd33e79c6d19d7f9852417b9c35a11f91f21714c", source, StringComparison.Ordinal);
            Assert.Contains("0834db6b5d518fe5c52eec9b47c03e467929aa89", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Implemented / final PR CI gated", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PR #240 remains fail-closed", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PR #240 is the final exact-head CI/merge gate", source, StringComparison.Ordinal);
        }

        Assert.Contains("50/50 COMPLETE", batch, StringComparison.Ordinal);
        Assert.Contains("#162", status, StringComparison.Ordinal);
        Assert.Contains("#116/#111", plan, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
