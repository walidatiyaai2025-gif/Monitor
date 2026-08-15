using System.Text.RegularExpressions;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ReleaseMutationConcurrencyTests
{
    [Fact]
    public void WriteCapableReleasePaths_ShareTagScopedNonCancellingMutationLock()
    {
        var root = FindRepoRoot();
        var workflowsRoot = Path.Combine(root, ".github", "workflows");
        var release = File.ReadAllText(Path.Combine(workflowsRoot, "release.yml"));
        var promotion = File.ReadAllText(Path.Combine(workflowsRoot, "promote-existing-candidate.yml"));

        Assert.Contains("publish-tagged-release:", release, StringComparison.Ordinal);
        Assert.Contains("group: monitor-release-tag-${{ github.ref_name }}", release, StringComparison.Ordinal);
        Assert.DoesNotContain("group: promote-existing-", release, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(release, @"(?m)^\s*concurrency:\s*$"));
        Assert.Single(Regex.Matches(release, @"(?m)^\s*cancel-in-progress:\s*false\s*$"));

        Assert.Contains("group: monitor-release-tag-${{ inputs.release_tag }}", promotion, StringComparison.Ordinal);
        Assert.DoesNotContain("group: promote-existing-", promotion, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(promotion, @"(?m)^\s*concurrency:\s*$"));
        Assert.Single(Regex.Matches(promotion, @"(?m)^\s*cancel-in-progress:\s*false\s*$"));
    }

    private static string FindRepoRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) return directory.FullName;
                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for release mutation concurrency tests.");
    }
}
