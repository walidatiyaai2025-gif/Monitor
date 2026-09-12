using Xunit;

namespace Monitor.Web.Tests;

public sealed class SelectedReleaseCandidateAcceptanceTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void SelectedCandidate_IsFreshRc854AndRc61IsHistoricalOnly()
    {
        var doc = Read("docs/SELECTED_RELEASE_CANDIDATE.md");

        Assert.Contains("0.1.0-rc.854", doc, StringComparison.Ordinal);
        Assert.Contains("10303396821", doc, StringComparison.Ordinal);
        Assert.Contains("34710820438", doc, StringComparison.Ordinal);
        Assert.Contains("sha256:e1f0b7facc756758a13653c3ad2bfa5a4af9107b02e14f4682aaaabb286f01e3", doc, StringComparison.Ordinal);
        Assert.Contains("b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7", doc, StringComparison.Ordinal);
        Assert.Contains("2026-10-12T18:22:15Z", doc, StringComparison.Ordinal);
        Assert.Contains("RC.61 is **historical only**", doc, StringComparison.Ordinal);
        Assert.Contains("9168574442", doc, StringComparison.Ordinal);
        Assert.Contains("expired", doc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PromotionHelper_IsFailClosedAndLocksExactArtifactManifestHashAndWorkflowContract()
    {
        var script = Read("scripts/Invoke-SelectedDurablePromotion.ps1");

        Assert.Contains("$SourceRunId = '34710820438'", script, StringComparison.Ordinal);
        Assert.Contains("$SourceArtifactId = '10303396821'", script, StringComparison.Ordinal);
        Assert.Contains("$OuterArtifactDigest = 'sha256:e1f0b7facc756758a13653c3ad2bfa5a4af9107b02e14f4682aaaabb286f01e3'", script, StringComparison.Ordinal);
        Assert.Contains("$SourceHeadSha = 'ef7209cbf099da65330887508ab4380a8b4196d2'", script, StringComparison.Ordinal);
        Assert.Contains("$TestedMergeSha = 'e1d0daedf8b2209934a1bcd01bff5d46229df20a'", script, StringComparison.Ordinal);
        Assert.Contains("$ProductSha256 = 'b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7'", script, StringComparison.Ordinal);
        Assert.Contains("[bool]$artifact.expired", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains("_operations/release-manifest.json", script, StringComparison.Ordinal);
        Assert.Contains("candidate_version=$Version", script, StringComparison.Ordinal);
        Assert.Contains("expected_outer_artifact_digest=$OuterArtifactDigest", script, StringComparison.Ordinal);
        Assert.Contains("source_commit=$SourceHeadSha", script, StringComparison.Ordinal);
        Assert.Contains("release_tag=$TagName", script, StringComparison.Ordinal);
        Assert.Contains("acknowledge_promotion=true", script, StringComparison.Ordinal);
        Assert.DoesNotContain("tag_name=", script, StringComparison.Ordinal);
        Assert.DoesNotContain("prerelease=", script, StringComparison.Ordinal);
        Assert.Contains("READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT", script, StringComparison.Ordinal);
        Assert.Contains("-AcknowledgePromotion", script, StringComparison.Ordinal);
        Assert.Contains("Do not overwrite or redispatch", script, StringComparison.Ordinal);
        Assert.Contains("PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED", script, StringComparison.Ordinal);
        Assert.Contains("verify-durable-release.yml", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectedCandidate_DoesNotClaimOwnerOrExternalGatesPassed()
    {
        var doc = Read("docs/SELECTED_RELEASE_CANDIDATE.md");
        var script = Read("scripts/Invoke-SelectedDurablePromotion.ps1");

        Assert.Contains("#162", doc, StringComparison.Ordinal);
        Assert.Contains("#116", doc, StringComparison.Ordinal);
        Assert.Contains("#111", doc, StringComparison.Ordinal);
        Assert.Contains("#353", doc, StringComparison.Ordinal);
        Assert.Contains("VERIFIED_FINAL_COMPLETE", doc, StringComparison.Ordinal);
        Assert.Contains("remains forbidden", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ExternalGatesPassed = 0", doc, StringComparison.Ordinal);
        Assert.Contains("ExternalGatesPassed = 0", script, StringComparison.Ordinal);
        Assert.Contains("ProductionMutationPerformed = $false", script, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
