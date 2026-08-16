using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05DurableReleaseAssetMetadataTests
{
    private const string ReleaseWorkflow = ".github/workflows/release.yml";
    private const string PromotionWorkflow = ".github/workflows/promote-existing-candidate.yml";
    private const string Verifier = "scripts/Verify-DurableRelease.sh";

    [Fact]
    public void RA_001_BothPathsUseSharedReleaseByTagRestVerifier()
    {
        AssertBothContain("Verify-DurableRelease.sh");
        Assert.Contains("gh api \"repos/${repository}/releases/tags/${tag}\"", Read(Verifier), StringComparison.Ordinal);
    }

    [Fact]
    public void RA_002_SharedVerifierRequiresExactReleaseMetadataAndTitle()
    {
        var verifier = Read(Verifier);

        Assert.Contains("'.tag_name // empty'", verifier, StringComparison.Ordinal);
        Assert.Contains("'.draft'", verifier, StringComparison.Ordinal);
        Assert.Contains("'.prerelease'", verifier, StringComparison.Ordinal);
        Assert.Contains("'.name // empty'", verifier, StringComparison.Ordinal);
        Assert.Contains("Monitor ${version}", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RA_003_SharedVerifierRequiresExactTwoAssetNames()
    {
        var verifier = Read(Verifier);

        Assert.Contains("${#names[@]}\" -eq 2", verifier, StringComparison.Ordinal);
        Assert.Contains("${names[0]}\" == \"$zip_name", verifier, StringComparison.Ordinal);
        Assert.Contains("${names[1]}\" == \"$checksum_name", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RA_004_SharedVerifierRequiresUploadedAssetState()
    {
        var verifier = Read(Verifier);
        Assert.Contains("'.state // empty'", verifier, StringComparison.Ordinal);
        Assert.Contains("== uploaded", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RA_005_SharedVerifierRequiresDistinctPositiveAssetIds()
    {
        var verifier = Read(Verifier);
        Assert.Contains("asset IDs must be positive integers", verifier, StringComparison.Ordinal);
        Assert.Contains("ZIP and checksum assets must have distinct IDs", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RA_006_SharedVerifierBindsApiSizesToDownloadedFiles()
    {
        var verifier = Read(Verifier);
        Assert.Contains("'.size // empty'", verifier, StringComparison.Ordinal);
        Assert.Contains("stat -c%s", verifier, StringComparison.Ordinal);
        Assert.Contains("first REST snapshot", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RA_007_SharedVerifierRequiresCanonicalSha256ApiDigests()
    {
        var verifier = Read(Verifier);
        Assert.Contains("'.digest // empty'", verifier, StringComparison.Ordinal);
        Assert.Contains("^sha256:[a-f0-9]{64}$", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RA_008_SharedVerifierBindsZipAndChecksumAssetDigestsToBytes()
    {
        var verifier = Read(Verifier);
        Assert.Contains("first_zip_digest", verifier, StringComparison.Ordinal);
        Assert.Contains("first_checksum_digest", verifier, StringComparison.Ordinal);
        Assert.Contains("sha256sum \"$zip_tmp\"", verifier, StringComparison.Ordinal);
        Assert.Contains("sha256sum \"$checksum_tmp\"", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RA_009_SharedVerifierRequiresExactRepositoryTagAssetUrls()
    {
        var verifier = Read(Verifier);
        Assert.Contains("'.browser_download_url // empty'", verifier, StringComparison.Ordinal);
        Assert.Contains("https://github.com/${repository}/releases/download/${tag}/", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RA_010_PreExistingByteLevelVerificationRemainsSharedAcrossBothPaths()
    {
        var verifier = Read(Verifier);
        AssertBothContain("--product-sha256");
        Assert.Contains("sha256sum \"$zip_tmp\"", verifier, StringComparison.Ordinal);
        Assert.Contains("${product_sha256}  ${zip_name}", verifier, StringComparison.Ordinal);
        Assert.Contains("checksum asset is not the canonical approved product checksum line", verifier, StringComparison.Ordinal);
        Assert.Contains("final ZIP bytes changed during atomic publication", verifier, StringComparison.Ordinal);
        Assert.Contains("final checksum bytes changed during atomic publication", verifier, StringComparison.Ordinal);
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
