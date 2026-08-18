using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800LegacyCloseoutEvidenceMappingTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void EvidenceMap_CoversEveryHistoricalUncheckedCloseoutRow()
    {
        var note = Read("docs/work/B800-096.md");

        foreach (var task in new[]
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
                 })
        {
            Assert.Contains(task, note, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DiagnosticValidationRows_MapToTheExactFrozenPr288GateSet()
    {
        var note = Read("docs/work/B800-096.md");

        Assert.Contains("PR #288", note, StringComparison.Ordinal);
        Assert.Contains("B800-031..063", note, StringComparison.Ordinal);
        Assert.Contains("7d2f0d7caa713b95bf7bc5666c9056c5c22055a8", note, StringComparison.Ordinal);
        Assert.Contains("CI #2245", note, StringComparison.Ordinal);
        Assert.Contains("Real SQL acceptance #265", note, StringComparison.Ordinal);
        Assert.Contains("Windows production-candidate #361", note, StringComparison.Ordinal);
        Assert.Contains("54fac01a1ed2ce7eb06f94b7de7d3681da75ac6d", note, StringComparison.Ordinal);
    }

    [Fact]
    public void FocusedHistoricalSlices_RecordTheirMergedEvidenceOwners()
    {
        var note = Read("docs/work/B800-096.md");

        var expected = new[]
        {
            "0d9c05d6c3c2b2980a6c3c8bbfbe241dc305860a",
            "d831f77159e43b446aa7549db5a6d74cd23a3f0e",
            "fad66ef563300f0aaedf8fad472b377ca55db648",
            "1b9518e7ceb813368106f5a04483817414f047b1",
            "8e5aea353b8255849b2c82675ec8f0b5443e88db",
            "3073c3a5b4b802b24a3b59218ee93e1208f534a3"
        };

        foreach (var sha in expected)
            Assert.Contains(sha, note, StringComparison.Ordinal);
    }

    [Fact]
    public void EvidenceMap_PreservesExplicitScopeLimits()
    {
        var note = Read("docs/work/B800-096.md");

        Assert.Contains("positive multi-replica AG integration", note, StringComparison.Ordinal);
        Assert.Contains("Live query-regression collection remains explicitly outside its scope", note, StringComparison.Ordinal);
        Assert.Contains("No RC.61 publication or selection change", note, StringComparison.Ordinal);
        Assert.Contains("#162 -> #116 -> #111", note, StringComparison.Ordinal);
    }

    [Fact]
    public void B800095_IsRecordedAsMergedBeforeMechanicalChecklistRewrite()
    {
        var note = Read("docs/work/B800-096.md");

        Assert.Contains("PR #330", note, StringComparison.Ordinal);
        Assert.Contains("d026457b2a7bb9f1b43c2f85c47cf01b1c33d7ec", note, StringComparison.Ordinal);
        Assert.Contains("32088182623", note, StringComparison.Ordinal);
        Assert.Contains("cda21b6ef5bbb8e34d32a186f44b3e45dc83bb23", note, StringComparison.Ordinal);
        Assert.Contains("B800-097", note, StringComparison.Ordinal);
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
