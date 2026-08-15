using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05DurableReleaseVerificationHardeningTests
{
    [Fact]
    public void RH_001_PromotionRequiresCompletedSuccessfulSourceRun()
    {
        var workflow = Read(".github/workflows/promote-existing-candidate.yml");

        Assert.Contains("jq -r '.status'", workflow, StringComparison.Ordinal);
        Assert.Contains("== completed", workflow, StringComparison.Ordinal);
        Assert.Contains("jq -r '.conclusion'", workflow, StringComparison.Ordinal);
        Assert.Contains("== success", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void RH_002_PromotionResolvesArtifactByExactIdEndpoint()
    {
        var workflow = Read(".github/workflows/promote-existing-candidate.yml");

        Assert.Contains("actions/artifacts/${ARTIFACT_ID}", workflow, StringComparison.Ordinal);
        Assert.Contains("jq -r '.id'", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("/artifacts?per_page=100", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void RH_003_ExactArtifactMustBelongToSelectedRun()
    {
        var workflow = Read(".github/workflows/promote-existing-candidate.yml");

        Assert.Contains("jq -r '.workflow_run.id'", workflow, StringComparison.Ordinal);
        Assert.Contains("== \"${RUN_ID}\"", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void RH_004_ArtifactMetadataRequiresExactNameFreshnessAndPositiveSize()
    {
        var workflow = Read(".github/workflows/promote-existing-candidate.yml");

        Assert.Contains("jq -r '.name'", workflow, StringComparison.Ordinal);
        Assert.Contains("== \"${name}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("jq -r '.expired'", workflow, StringComparison.Ordinal);
        Assert.Contains("== false", workflow, StringComparison.Ordinal);
        Assert.Contains("jq -r '.size_in_bytes'", workflow, StringComparison.Ordinal);
        Assert.Contains("[[ \"${size_in_bytes}\" =~ ^[1-9][0-9]*$ ]]", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void RH_005_DownloadUsesExactArtifactId()
    {
        var workflow = Read(".github/workflows/promote-existing-candidate.yml");
        var stepStart = workflow.IndexOf("- name: Download exact artifact from selected run", StringComparison.Ordinal);
        var stepEnd = workflow.IndexOf("- name: Validate exact selected candidate bytes", stepStart, StringComparison.Ordinal);
        Assert.True(stepStart >= 0 && stepEnd > stepStart, "Exact artifact download step must remain directly before byte validation.");
        var step = workflow[stepStart..stepEnd];

        Assert.Contains("artifact-ids: ${{ inputs.source_artifact_id }}", step, StringComparison.Ordinal);
        Assert.DoesNotContain("name: Monitor-", step, StringComparison.Ordinal);
        Assert.Contains("run-id: ${{ inputs.source_run_id }}", step, StringComparison.Ordinal);
    }

    [Fact]
    public void RH_006_PromotionPayloadMustContainExactlyZipAndChecksum()
    {
        var script = Read("scripts/Test-ExistingCandidatePromotion.ps1");

        Assert.Contains("Get-ChildItem -LiteralPath $artifactDirectory -Force", script, StringComparison.Ordinal);
        Assert.Contains("$payloadEntries.Count -ne 2", script, StringComparison.Ordinal);
        Assert.Contains("Downloaded promotion payload must contain exactly the selected ZIP and companion checksum.", script, StringComparison.Ordinal);
        Assert.Contains("Downloaded promotion payload contains an unexpected file name.", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RH_007_ChecksumMustUseCanonicalBytes()
    {
        var script = Read("scripts/Test-ExistingCandidatePromotion.ps1");
        var release = Read(".github/workflows/release.yml");
        var durableVerifier = Read("scripts/Verify-DurableRelease.sh");

        Assert.Contains("$expectedChecksumLine = \"$ExpectedProductSha256  $expectedName\"", script, StringComparison.Ordinal);
        Assert.Contains("$checksumLine -cne $expectedChecksumLine", script, StringComparison.Ordinal);
        Assert.Contains("lowercase SHA-256, two spaces, and the exact ZIP filename", script, StringComparison.Ordinal);
        Assert.Contains("^([a-f0-9]{64})\\ \\ ([^[:space:]]+)$", release, StringComparison.Ordinal);
        Assert.Contains("${product_sha256}  ${zip_name}", durableVerifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RH_008_PromotionRejectsUnsafeOrCollidingZipEntryPaths()
    {
        var script = Read("scripts/Test-ExistingCandidatePromotion.ps1");

        Assert.Contains("[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)", script, StringComparison.Ordinal);
        Assert.Contains("unsafe rooted or Windows-incompatible entry path", script, StringComparison.Ordinal);
        Assert.Contains("unsafe empty path segment", script, StringComparison.Ordinal);
        Assert.Contains("traversal path segment", script, StringComparison.Ordinal);
        Assert.Contains("duplicate or case-colliding entry path", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RH_009_TaggedReleaseRequiresExactSameRunArtifactPayload()
    {
        var release = Read(".github/workflows/release.yml");

        Assert.Contains("find artifacts -mindepth 1 -maxdepth 1", release, StringComparison.Ordinal);
        Assert.Contains("${#payload_entries[@]}\" -ne 2", release, StringComparison.Ordinal);
        Assert.Contains("Verified production artifact must contain exactly the product ZIP and companion checksum.", release, StringComparison.Ordinal);
    }

    [Fact]
    public void RH_010_BothDurableReleasePathsVerifyReleaseMetadataAndClassification()
    {
        var taggedRelease = Read(".github/workflows/release.yml");
        var promotion = Read(".github/workflows/promote-existing-candidate.yml");
        var verifier = Read("scripts/Verify-DurableRelease.sh");

        Assert.Contains("Verify-DurableRelease.sh", taggedRelease, StringComparison.Ordinal);
        Assert.Contains("Verify-DurableRelease.sh", promotion, StringComparison.Ordinal);
        Assert.Contains("gh api \"repos/${repository}/releases/tags/${tag}\"", verifier, StringComparison.Ordinal);
        Assert.Contains("'.tag_name // empty'", verifier, StringComparison.Ordinal);
        Assert.Contains("'.draft'", verifier, StringComparison.Ordinal);
        Assert.Contains("'.prerelease'", verifier, StringComparison.Ordinal);
        Assert.Contains("expected_prerelease=false", verifier, StringComparison.Ordinal);

        Assert.Contains("release_flags=(--latest=false)", promotion, StringComparison.Ordinal);
        Assert.Contains("release_flags+=(--prerelease)", promotion, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for P0.5 durable release verification hardening tests.");
    }
}
