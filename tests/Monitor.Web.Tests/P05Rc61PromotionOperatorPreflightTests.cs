using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05Rc61PromotionOperatorPreflightTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Preflight_IsReadOnlyAndPinsExactSelectedCandidate()
    {
        var script = Read("scripts/Test-Rc61DurablePromotionPreflight.ps1");

        foreach (var value in new[]
        {
            "walidatiyaai2025-gif/Monitor",
            "1329517438",
            "0.1.0-rc.61",
            "31667721306",
            "9168574442",
            "sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382",
            "d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5",
            "e28158da67b36dfc5dbf8f4c38b5c43d99c7c728",
            "158148d8bfd05f724014541bc7a0b1eab5dae1b5",
            "v0.1.0-rc.61"
        })
        {
            Assert.Contains(value, script, StringComparison.Ordinal);
        }

        Assert.Contains("MutatedGitHubState = $false", script, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release create", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invoke-Gh -Arguments @('workflow'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("& gh workflow run", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Preflight_EmitsExactManualPromotionAndIndependentVerificationCommands()
    {
        var script = Read("scripts/Test-Rc61DurablePromotionPreflight.ps1");

        foreach (var promotionInput in new[]
        {
            "candidate_version=$version",
            "source_run_id=$sourceRunId",
            "source_artifact_id=$sourceArtifactId",
            "expected_outer_artifact_digest=$outerDigest",
            "expected_product_sha256=$productSha256",
            "source_commit=$sourceCommit",
            "tested_merge_commit=$testedMergeCommit",
            "release_tag=$releaseTag",
            "acknowledge_promotion=true"
        })
        {
            Assert.Contains(promotionInput, script, StringComparison.Ordinal);
        }

        Assert.Contains("gh workflow run promote-existing-candidate.yml", script, StringComparison.Ordinal);
        Assert.Contains("gh workflow run verify-durable-release.yml", script, StringComparison.Ordinal);
        Assert.Contains("release_version=$version", script, StringComparison.Ordinal);
        Assert.Contains("expected_commit=$testedMergeCommit", script, StringComparison.Ordinal);
        Assert.Contains("IndependentVerificationCommand", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Preflight_FailsClosedOnRepositorySourceRunArtifactAndProbeDrift()
    {
        var script = Read("scripts/Test-Rc61DurablePromotionPreflight.ps1");

        Assert.Contains("Repository identity/default branch does not match", script, StringComparison.Ordinal);
        Assert.Contains("Selected source run head SHA drifted", script, StringComparison.Ordinal);
        Assert.Contains("Selected source run repository identity drifted", script, StringComparison.Ordinal);
        Assert.Contains("Selected artifact outer digest drifted", script, StringComparison.Ordinal);
        Assert.Contains("Selected artifact source provenance drifted", script, StringComparison.Ordinal);
        Assert.Contains("Selected artifact repository provenance drifted", script, StringComparison.Ordinal);
        Assert.Contains("Selected RC.61 Actions artifact is expired", script, StringComparison.Ordinal);
        Assert.Contains("HTTP\\s+404|Not Found", script, StringComparison.Ordinal);
        Assert.Contains("refusing to treat the error as absence", script, StringComparison.Ordinal);
        Assert.Contains("git/ref/tags/$releaseTag", script, StringComparison.Ordinal);
        Assert.Contains("TagExists = $tagExists", script, StringComparison.Ordinal);
        Assert.Contains("ReleaseExists = $releaseExists", script, StringComparison.Ordinal);
        Assert.Contains("DURABLE_STATE_EXISTS_VERIFY_OR_INVESTIGATE", script, StringComparison.Ordinal);
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Root, relative)).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
