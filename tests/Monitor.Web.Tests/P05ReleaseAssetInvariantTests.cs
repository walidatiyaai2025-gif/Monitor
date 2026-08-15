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
        var verifier = File.ReadAllText(Path.Combine(root, "scripts", "Verify-DurableRelease.sh"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        const string existingCall = "verify_release_assets \"${RUNNER_TEMP}/existing-release\"";
        const string createdCall = "verify_release_assets \"${RUNNER_TEMP}/created-release\"";

        Assert.Contains("verify_release_assets() {", workflow, StringComparison.Ordinal);
        Assert.Contains("bash \"${verifier}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--repository \"${GITHUB_REPOSITORY}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--product-sha256 \"${PRODUCT_SHA256}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("release must contain exactly two assets", verifier, StringComparison.Ordinal);
        Assert.Contains("release asset names do not match the exact ZIP/checksum contract", verifier, StringComparison.Ordinal);
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
