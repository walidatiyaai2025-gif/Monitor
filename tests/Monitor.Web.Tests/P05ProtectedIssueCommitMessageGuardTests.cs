using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ProtectedIssueCommitMessageGuardTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void PullRequestCommitGuard_IsLowPrivilegeCompleteAndFailClosed()
    {
        var workflow = Read(".github/workflows/protected-p0-pr-commits.yml");

        Assert.Contains("pull_request:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("pull_request_target:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("workflow_run:", workflow, StringComparison.Ordinal);
        Assert.Contains("types: [ opened, synchronize, reopened ]", workflow, StringComparison.Ordinal);
        Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("GH_TOKEN: ${{ github.token }}", workflow, StringComparison.Ordinal);
        Assert.Contains("PR_NUMBER: ${{ github.event.pull_request.number }}", workflow, StringComparison.Ordinal);
        Assert.Contains("EXPECTED_COMMITS: ${{ github.event.pull_request.commits }}", workflow, StringComparison.Ordinal);
        Assert.Contains("gh api --paginate", workflow, StringComparison.Ordinal);
        Assert.Contains("/pulls/${PR_NUMBER}/commits?per_page=100", workflow, StringComparison.Ordinal);
        Assert.Contains(".[].commit.message | @base64", workflow, StringComparison.Ordinal);
        Assert.Contains("base64 --decode", workflow, StringComparison.Ordinal);
        Assert.Contains("close|closes|closed|fix|fixes|fixed|resolve|resolves|resolved", workflow, StringComparison.Ordinal);
        Assert.Contains("(111|116|162)", workflow, StringComparison.Ordinal);
        Assert.Contains("PR commit enumeration mismatch", workflow, StringComparison.Ordinal);
        Assert.Contains("Expected PR commit count must be positive", workflow, StringComparison.Ordinal);
        Assert.Contains("grep -Eiq", workflow, StringComparison.Ordinal);

        Assert.DoesNotContain("uses: actions/checkout@", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("issues: write", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pull-requests: write", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("permissions: write-all", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh issue", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh pr merge", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secrets.", workflow, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
