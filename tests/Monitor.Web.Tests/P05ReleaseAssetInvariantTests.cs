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

        const string existingCall = "verify_release_assets \"${RUNNER_TEMP}/existing-release\"";
        const string createdCall = "verify_release_assets \"${RUNNER_TEMP}/created-release\"";

        Assert.Contains("verify_release_assets() {", workflow, StringComparison.Ordinal);
        Assert.Contains("gh api \"repos/${GITHUB_REPOSITORY}/releases/tags/${RELEASE_TAG}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("jq -r '.assets[].name' <<<\"${release_json}\" | sort", workflow, StringComparison.Ordinal);
        Assert.Contains("[[ \"${#names[@]}\" -ne 2 || \"${names[0]}\" != \"${ZIP_NAME}\" || \"${names[1]}\" != \"${CHECKSUM_NAME}\" ]]", workflow, StringComparison.Ordinal);
        Assert.Contains(existingCall, workflow, StringComparison.Ordinal);
        Assert.Contains(createdCall, workflow, StringComparison.Ordinal);
        Assert.Equal(1, workflow.Split(existingCall, StringSplitOptions.None).Length - 1);
        Assert.Equal(1, workflow.Split(createdCall, StringSplitOptions.None).Length - 1);

        var createIndex = workflow.IndexOf("gh release create \"${RELEASE_TAG}\"", StringComparison.Ordinal);
        var createdVerificationIndex = workflow.IndexOf(createdCall, StringComparison.Ordinal);
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
