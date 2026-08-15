using System.Text.RegularExpressions;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ReleaseAssetInvariantTests
{
    [Fact]
    public void TaggedReleasePublisher_RequiresExactlyTwoVerifiedAssetsBeforeAcceptance()
    {
        var root = FindRepoRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("verify_release_assets() {", workflow, StringComparison.Ordinal);
        Assert.Contains("gh release view \"${RELEASE_TAG}\" --json assets --jq '.assets[].name' | sort", workflow, StringComparison.Ordinal);
        Assert.Contains("[[ \"${#names[@]}\" -ne 2 || \"${names[0]}\" != \"${ZIP_NAME}\" || \"${names[1]}\" != \"${CHECKSUM_NAME}\" ]]", workflow, StringComparison.Ordinal);
        Assert.Contains("verify_release_assets \"${RUNNER_TEMP}/existing-release\"", workflow, StringComparison.Ordinal);
        Assert.Contains("verify_release_assets \"${RUNNER_TEMP}/created-release\"", workflow, StringComparison.Ordinal);

        var calls = Regex.Matches(workflow, @"(?m)^\s*verify_release_assets \"\$\{RUNNER_TEMP\}/(?:existing|created)-release\"\s*$");
        Assert.Equal(2, calls.Count);

        var createIndex = workflow.IndexOf("gh release create \"${RELEASE_TAG}\"", StringComparison.Ordinal);
        var createdVerificationIndex = workflow.IndexOf("verify_release_assets \"${RUNNER_TEMP}/created-release\"", StringComparison.Ordinal);
        Assert.True(createIndex >= 0 && createdVerificationIndex > createIndex, "New release must be re-read and verified after creation.");
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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for release asset invariant tests.");
    }
}
