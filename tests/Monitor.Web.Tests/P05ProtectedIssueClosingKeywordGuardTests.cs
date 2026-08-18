using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ProtectedIssueClosingKeywordGuardTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Ci_RejectsClosingKeywordsForProtectedP0IssueMetadata()
    {
        var ci = Read(".github/workflows/ci.yml");
        var metadataWorkflow = Read(".github/workflows/protected-p0-pr-metadata.yml");
        var guard = Read("scripts/Test-P0ProtectedIssueClosingKeywords.sh");
        var safety = Read("scripts/Test-P0ProtectedIssueClosingKeywordSafety.sh");

        Assert.Contains("Reject protected P0 issue closing keywords in PR metadata", ci, StringComparison.Ordinal);
        Assert.Contains("if: github.event_name == 'pull_request'", ci, StringComparison.Ordinal);
        Assert.Contains("PR_TITLE: ${{ github.event.pull_request.title }}", ci, StringComparison.Ordinal);
        Assert.Contains("PR_BODY: ${{ github.event.pull_request.body }}", ci, StringComparison.Ordinal);
        Assert.Contains("bash scripts/Test-P0ProtectedIssueClosingKeywords.sh", ci, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/Test-P0ProtectedIssueClosingKeywords.sh", ci, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/Test-P0ProtectedIssueClosingKeywordSafety.sh", ci, StringComparison.Ordinal);
        Assert.Contains("bash scripts/Test-P0ProtectedIssueClosingKeywordSafety.sh", ci, StringComparison.Ordinal);

        Assert.Contains("pull_request:", metadataWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("pull_request_target:", metadataWorkflow, StringComparison.Ordinal);
        Assert.Contains("types: [ opened, synchronize, reopened, edited ]", metadataWorkflow, StringComparison.Ordinal);
        Assert.Contains("permissions:\n  contents: read", metadataWorkflow, StringComparison.Ordinal);
        Assert.Contains("PR_TITLE: ${{ github.event.pull_request.title }}", metadataWorkflow, StringComparison.Ordinal);
        Assert.Contains("PR_BODY: ${{ github.event.pull_request.body }}", metadataWorkflow, StringComparison.Ordinal);
        Assert.Contains("close|closes|closed|fix|fixes|fixed|resolve|resolves|resolved", metadataWorkflow, StringComparison.Ordinal);
        Assert.Contains("(111|116|162)", metadataWorkflow, StringComparison.Ordinal);
        Assert.Contains("grep -Eiq", metadataWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("uses: actions/checkout@", metadataWorkflow, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("close|closes|closed|fix|fixes|fixed|resolve|resolves|resolved", guard, StringComparison.Ordinal);
        Assert.Contains("(111|116|162)", guard, StringComparison.Ordinal);
        Assert.Contains("walidatiyaai2025-gif/Monitor", guard, StringComparison.Ordinal);
        Assert.Contains("Protected P0 gates must be closed explicitly after their evidence contract is satisfied", guard, StringComparison.Ordinal);

        Assert.Contains("run_allowed 'P0.5: add guard' 'Closes #347'", safety, StringComparison.Ordinal);
        Assert.Contains("run_blocked 'close #162'", safety, StringComparison.Ordinal);
        Assert.Contains("run_blocked 'docs' 'Closes: #116 after evidence.'", safety, StringComparison.Ordinal);
        Assert.Contains("run_blocked 'docs' 'fixed walidatiyaai2025-gif/Monitor#111'", safety, StringComparison.Ordinal);
        Assert.Contains("run_blocked 'docs' 'do not close #162 from CI'", safety, StringComparison.Ordinal);

        Assert.DoesNotContain("gh issue close", guard, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("issues: write", ci, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("issues: write", metadataWorkflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github.token", metadataWorkflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secrets.", metadataWorkflow, StringComparison.OrdinalIgnoreCase);
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
