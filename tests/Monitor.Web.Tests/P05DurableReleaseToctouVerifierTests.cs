using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05DurableReleaseToctouVerifierTests
{
    private const string VerifierPath = "scripts/Verify-DurableRelease.sh";
    private const string HarnessPath = "scripts/Test-DurableReleaseVerifierSafety.sh";
    private const string ReleaseWorkflow = ".github/workflows/release.yml";
    private const string PromotionWorkflow = ".github/workflows/promote-existing-candidate.yml";
    private const string CiWorkflow = ".github/workflows/ci.yml";

    [Fact]
    public void RT_001_BothWritePathsUseOneSharedDurableReleaseVerifier()
    {
        var release = Read(ReleaseWorkflow);
        var promotion = Read(PromotionWorkflow);

        Assert.Contains("Verify-DurableRelease.sh", release, StringComparison.Ordinal);
        Assert.Contains("Verify-DurableRelease.sh", promotion, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release download", release, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release download", promotion, StringComparison.Ordinal);
    }

    [Fact]
    public void RT_002_VerifierValidatesRepositoryTagVersionHashAndClassificationAtEntry()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("repository must be an exact owner/name slug", verifier, StringComparison.Ordinal);
        Assert.Contains("version format is invalid", verifier, StringComparison.Ordinal);
        Assert.Contains("tag must equal v<version>", verifier, StringComparison.Ordinal);
        Assert.Contains("product SHA-256 must be 64 lowercase hex characters", verifier, StringComparison.Ordinal);
        Assert.Contains("expected_prerelease=false", verifier, StringComparison.Ordinal);
        Assert.Contains("zip_name=\"Monitor-${version}-win-x64.zip\"", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RT_003_VerifierRequiresPositiveReleaseIdAndExactReleaseMetadata()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("release ID must be a positive integer", verifier, StringComparison.Ordinal);
        Assert.Contains("'.tag_name // empty'", verifier, StringComparison.Ordinal);
        Assert.Contains("'.name // empty'", verifier, StringComparison.Ordinal);
        Assert.Contains("draft releases are not accepted", verifier, StringComparison.Ordinal);
        Assert.Contains("release prerelease classification does not match the version", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RT_004_VerifierRequiresExactTwoUploadedDistinctSizedDigestedAssetsAndUrls()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("release must contain exactly two assets", verifier, StringComparison.Ordinal);
        Assert.Contains("ZIP asset is not fully uploaded", verifier, StringComparison.Ordinal);
        Assert.Contains("checksum asset is not fully uploaded", verifier, StringComparison.Ordinal);
        Assert.Contains("ZIP and checksum assets must have distinct IDs", verifier, StringComparison.Ordinal);
        Assert.Contains("asset sizes must be positive integers", verifier, StringComparison.Ordinal);
        Assert.Contains("asset API digests must be canonical SHA-256 values", verifier, StringComparison.Ordinal);
        Assert.Contains("browser-download URL does not match the exact repository/tag/name contract", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RT_005_VerifierDownloadsBothReleaseAssetsByExactRestAssetId()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("releases/assets/${first_zip_id}", verifier, StringComparison.Ordinal);
        Assert.Contains("releases/assets/${first_checksum_id}", verifier, StringComparison.Ordinal);
        Assert.Contains("Accept: application/octet-stream", verifier, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release download", verifier, StringComparison.Ordinal);
        Assert.DoesNotContain("--pattern", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RT_006_VerifierBindsDownloadedSizesHashesAndCanonicalChecksumToFirstSnapshot()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("downloaded ZIP size differs from the first REST snapshot", verifier, StringComparison.Ordinal);
        Assert.Contains("downloaded checksum size differs from the first REST snapshot", verifier, StringComparison.Ordinal);
        Assert.Contains("downloaded ZIP bytes do not match the approved product SHA-256", verifier, StringComparison.Ordinal);
        Assert.Contains("downloaded checksum bytes do not match the first REST API digest", verifier, StringComparison.Ordinal);
        Assert.Contains("${product_sha256}  ${zip_name}", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RT_007_VerifierRereadsReleaseSnapshotAndRequiresSameReleaseId()
    {
        var verifier = Read(VerifierPath);

        Assert.Equal(2, verifier.Split("snapshot_release)", StringSplitOptions.None).Length - 1);
        Assert.Contains("second_json=\"$(snapshot_release)\"", verifier, StringComparison.Ordinal);
        Assert.Contains("release ID changed during verification", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RT_008_VerifierRejectsSecurityMetadataMutationAcrossSnapshots()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("SNAP_SECURITY", verifier, StringComparison.Ordinal);
        Assert.Contains("release or asset security metadata changed during verification", verifier, StringComparison.Ordinal);
        Assert.Contains("asset IDs changed during verification", verifier, StringComparison.Ordinal);
        Assert.Contains("asset sizes changed during verification", verifier, StringComparison.Ordinal);
        Assert.Contains("asset digests changed during verification", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RT_009_OfflineHarnessProvesPositiveAndToctouMutationCases()
    {
        var harness = Read(HarnessPath);

        Assert.Contains("fake-bin", harness, StringComparison.Ordinal);
        Assert.Contains("FAKE_GH_MUTATE_ON_SECOND", harness, StringComparison.Ordinal);
        Assert.Contains("release-mutated.json", harness, StringComparison.Ordinal);
        Assert.Contains("TOCTOU mutation case unexpectedly passed durable release verification", harness, StringComparison.Ordinal);
        Assert.Contains("Durable release verifier synthetic positive and TOCTOU mutation checks passed", harness, StringComparison.Ordinal);
    }

    [Fact]
    public void RT_010_CiParsesAndExecutesVerifierWhileWriteTokenScopeRemainsStepLocal()
    {
        var ci = Read(CiWorkflow);
        var release = Read(ReleaseWorkflow);
        var promotion = Read(PromotionWorkflow);

        Assert.Contains("bash -n scripts/Verify-DurableRelease.sh", ci, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/Test-DurableReleaseVerifierSafety.sh", ci, StringComparison.Ordinal);
        Assert.Contains("bash scripts/Test-DurableReleaseVerifierSafety.sh", ci, StringComparison.Ordinal);
        Assert.DoesNotContain("\n    env:\n      GH_TOKEN: ${{ github.token }}", release, StringComparison.Ordinal);
        Assert.DoesNotContain("\n    env:\n      GH_TOKEN: ${{ github.token }}", promotion, StringComparison.Ordinal);
        Assert.Contains("GH_TOKEN: ${{ github.token }}", release, StringComparison.Ordinal);
        Assert.Contains("GH_TOKEN: ${{ github.token }}", promotion, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for durable release TOCTOU verifier tests.");
    }
}
