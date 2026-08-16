using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05CanonicalDeltaCloseoutTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void CanonicalDelta_RecordsCompletedHardeningAndCurrentManualBoundary()
    {
        var delta = Read("docs/P0_5_CANONICAL_TRACKING_DELTA.md");

        Assert.DoesNotContain("Issue #198 is **IN VERIFICATION**", delta, StringComparison.Ordinal);
        Assert.DoesNotContain("Issue #198 step-scoped GitHub CLI token exposure hardening is in verification", delta, StringComparison.Ordinal);
        Assert.DoesNotContain("exceeds the safe complete-file response budget", delta, StringComparison.Ordinal);
        Assert.DoesNotContain("it is therefore not rewritten here", delta, StringComparison.Ordinal);

        Assert.Contains("#198 / #199 | COMPLETE", delta, StringComparison.Ordinal);
        Assert.Contains("#218 / #219 | COMPLETE", delta, StringComparison.Ordinal);
        Assert.Contains("ca1e40acfac635650df32cd0bc60ed63df224380", delta, StringComparison.Ordinal);
        Assert.Contains("verify-durable-release.yml", delta, StringComparison.Ordinal);
        Assert.Contains("contents: read", delta, StringComparison.Ordinal);
        Assert.Contains("sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382", delta, StringComparison.Ordinal);
        Assert.Contains("2026-09-12T04:41:34Z", delta, StringComparison.Ordinal);
        Assert.Contains("75661cfc730f60667d1786a9bcd6ca9427ef2faa", delta, StringComparison.Ordinal);
        Assert.Contains("Issues #116 and #111 remain OPEN", delta, StringComparison.Ordinal);
        Assert.Contains("Keep #162 open until both runs are Green", delta, StringComparison.Ordinal);
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
