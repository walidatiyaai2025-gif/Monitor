using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05DurableReleaseAssetMetadataTests
{
    private const string ReleaseWorkflow = ".github/workflows/release.yml";
    private const string PromotionWorkflow = ".github/workflows/promote-existing-candidate.yml";

    [Fact]
    public void RA_001_BothPathsReadReleaseByTagThroughRestApi()
    {
        AssertBothContain("gh api \"repos/${GITHUB_REPOSITORY}/releases/tags/${RELEASE_TAG}\"");
    }

    [Fact]
    public void RA_002_BothPathsRequireExactReleaseMetadataAndTitle()
    {
        var release = Read(ReleaseWorkflow);
        var promotion = Read(PromotionWorkflow);

        Assert.Contains("'.tag_name'", release, StringComparison.Ordinal);
        Assert.Contains("'.draft'", release, StringComparison.Ordinal);
        Assert.Contains("'.prerelease'", release, StringComparison.Ordinal);
        Assert.Contains("'.name'", release, StringComparison.Ordinal);
        Assert.Contains("Monitor ${RELEASE_VERSION}", release, StringComparison.Ordinal);

        Assert.Contains("'.tag_name'", promotion, StringComparison.Ordinal);
        Assert.Contains("'.draft'", promotion, StringComparison.Ordinal);
        Assert.Contains("'.prerelease'", promotion, StringComparison.Ordinal);
        Assert.Contains("'.name'", promotion, StringComparison.Ordinal);
        Assert.Contains("Monitor ${VERSION}", promotion, StringComparison.Ordinal);
    }

    [Fact]
    public void RA_003_BothPathsRequireExactTwoAssetNames()
    {
        var release = Read(ReleaseWorkflow);
        var promotion = Read(PromotionWorkflow);

        Assert.Contains("${#names[@]}\" -ne 2", release, StringComparison.Ordinal);
        Assert.Contains("${names[0]}\" != \"${ZIP_NAME}", release, StringComparison.Ordinal);
        Assert.Contains("${names[1]}\" != \"${CHECKSUM_NAME}", release, StringComparison.Ordinal);

        Assert.Contains("${#names[@]}\" -eq 2", promotion, StringComparison.Ordinal);
        Assert.Contains("${names[0]}\" == \"${zip}", promotion, StringComparison.Ordinal);
        Assert.Contains("${names[1]}\" == \"${sum}", promotion, StringComparison.Ordinal);
    }

    [Fact]
    public void RA_004_BothPathsRequireUploadedAssetState()
    {
        AssertBothContain("'.state'");
        AssertBothContain("== uploaded");
    }

    [Fact]
    public void RA_005_BothPathsRequireDistinctPositiveAssetIds()
    {
        var release = Read(ReleaseWorkflow);
        var promotion = Read(PromotionWorkflow);

        Assert.Contains("zip_id", release, StringComparison.Ordinal);
        Assert.Contains("checksum_id", release, StringComparison.Ordinal);
        Assert.Contains("${zip_id}\" != \"${checksum_id}", release, StringComparison.Ordinal);

        Assert.Contains("zip_id", promotion, StringComparison.Ordinal);
        Assert.Contains("sum_id", promotion, StringComparison.Ordinal);
        Assert.Contains("${zip_id}\" != \"${sum_id}", promotion, StringComparison.Ordinal);
    }

    [Fact]
    public void RA_006_BothPathsBindApiSizesToDownloadedFiles()
    {
        AssertBothContain("'.size'");
        AssertBothContain("stat -c%s");
    }

    [Fact]
    public void RA_007_BothPathsRequireCanonicalSha256ApiDigests()
    {
        AssertBothContain("'.digest'");
        AssertBothContain("^sha256:[a-f0-9]{64}$");
    }

    [Fact]
    public void RA_008_BothPathsBindZipAndChecksumAssetDigestsToBytes()
    {
        var release = Read(ReleaseWorkflow);
        var promotion = Read(PromotionWorkflow);

        Assert.Contains("${zip_digest}\" == \"sha256:${PRODUCT_SHA256}", release, StringComparison.Ordinal);
        Assert.Contains("existing_checksum_asset_hash", release, StringComparison.Ordinal);
        Assert.Contains("${checksum_digest}\" != \"sha256:${existing_checksum_asset_hash}", release, StringComparison.Ordinal);

        Assert.Contains("${zip_digest}\" == \"sha256:${PRODUCT_SHA}", promotion, StringComparison.Ordinal);
        Assert.Contains("sum_hash", promotion, StringComparison.Ordinal);
        Assert.Contains("${sum_digest}\" == \"sha256:${sum_hash}", promotion, StringComparison.Ordinal);
    }

    [Fact]
    public void RA_009_BothPathsRequireExactRepositoryTagAssetUrls()
    {
        AssertBothContain("'.browser_download_url'");
        AssertBothContain("https://github.com/${GITHUB_REPOSITORY}/releases/download/${RELEASE_TAG}/");
    }

    [Fact]
    public void RA_010_PreExistingByteLevelVerificationRemainsInBothPaths()
    {
        var release = Read(ReleaseWorkflow);
        var promotion = Read(PromotionWorkflow);

        Assert.Contains("sha256sum \"${dir}/${ZIP_NAME}\"", release, StringComparison.Ordinal);
        Assert.Contains("Release checksum asset is non-canonical", release, StringComparison.Ordinal);
        Assert.Contains("sha256sum \"${dir}/${zip}\"", promotion, StringComparison.Ordinal);
        Assert.Contains("${PRODUCT_SHA}  ${zip}", promotion, StringComparison.Ordinal);
    }

    private static void AssertBothContain(string value)
    {
        Assert.Contains(value, Read(ReleaseWorkflow), StringComparison.Ordinal);
        Assert.Contains(value, Read(PromotionWorkflow), StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
    {
        var root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for P0.5 durable release asset metadata tests.");
    }
}
