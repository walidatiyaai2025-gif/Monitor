using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05DurableReleaseTagProvenanceTests
{
    private const string VerifierPath = "scripts/Verify-DurableRelease.sh";
    private const string HarnessPath = "scripts/Test-DurableReleaseVerifierSafety.sh";
    private const string ReleaseWorkflow = ".github/workflows/release.yml";
    private const string PromotionWorkflow = ".github/workflows/promote-existing-candidate.yml";
    private const string CiWorkflow = ".github/workflows/ci.yml";

    [Fact]
    public void TG_001_VerifierRequiresCanonicalApprovedCommitSha()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("--expected-commit", verifier, StringComparison.Ordinal);
        Assert.Contains("approved commit SHA must be 40 lowercase hex characters", verifier, StringComparison.Ordinal);
        Assert.Contains("^\[a-f0-9\]{40}$".Replace("\\[", "[", StringComparison.Ordinal).Replace("\\]", "]", StringComparison.Ordinal), verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void TG_002_VerifierSnapshotsExactTagRefObject()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("repos/${repository}/git/ref/tags/${tag}", verifier, StringComparison.Ordinal);
        Assert.Contains("'.ref // empty'", verifier, StringComparison.Ordinal);
        Assert.Contains("'.object.sha // empty'", verifier, StringComparison.Ordinal);
        Assert.Contains("'.object.type // empty'", verifier, StringComparison.Ordinal);
        Assert.Contains("tag ref object type must be commit or tag", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void TG_003_VerifierDereferencesTagToApprovedCommit()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("repos/${repository}/commits/${tag}", verifier, StringComparison.Ordinal);
        Assert.Contains("resolved tag commit SHA must be 40 lowercase hex characters", verifier, StringComparison.Ordinal);
        Assert.Contains("tag does not resolve to the approved commit", verifier, StringComparison.Ordinal);
        Assert.Contains("TAG_RESOLVED_COMMIT=\"$resolved_sha\"", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void TG_004_FirstTagSnapshotPrecedesReleaseSnapshot()
    {
        var verifier = Read(VerifierPath);
        var firstTag = verifier.IndexOf("snapshot_tag_provenance\nfirst_tag_ref_sha", StringComparison.Ordinal);
        var firstRelease = verifier.IndexOf("first_json=\"$(snapshot_release)\"", StringComparison.Ordinal);

        Assert.True(firstTag >= 0 && firstRelease > firstTag, "Tag provenance must be bound before the first release snapshot is accepted.");
        Assert.Contains("first_tag_ref_type=\"$TAG_REF_TYPE\"", verifier, StringComparison.Ordinal);
        Assert.Contains("first_tag_resolved_commit=\"$TAG_RESOLVED_COMMIT\"", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void TG_005_VerifierRechecksTagAfterSecondReleaseSnapshot()
    {
        var verifier = Read(VerifierPath);
        var secondRelease = verifier.IndexOf("second_json=\"$(snapshot_release)\"", StringComparison.Ordinal);
        var secondTag = verifier.LastIndexOf("snapshot_tag_provenance", StringComparison.Ordinal);

        Assert.True(secondRelease >= 0 && secondTag > secondRelease, "Tag provenance must be re-read after the second release snapshot.");
        Assert.Contains("tag ref object changed during verification", verifier, StringComparison.Ordinal);
        Assert.Contains("tag ref object type changed during verification", verifier, StringComparison.Ordinal);
        Assert.Contains("tag resolved commit changed during verification", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void TG_006_PromotionPassesTestedMergeToSharedVerifier()
    {
        var promotion = Read(PromotionWorkflow);

        Assert.Contains("--expected-commit \"${TESTED_SHA}\"", promotion, StringComparison.Ordinal);
        Assert.Contains("--target \"${TESTED_SHA}\"", promotion, StringComparison.Ordinal);
        Assert.Contains("verify_release \"${RUNNER_TEMP}/verified-release\"", promotion, StringComparison.Ordinal);
    }

    [Fact]
    public void TG_007_TaggedReleasePassesTriggerCommitToSharedVerifier()
    {
        var release = Read(ReleaseWorkflow);

        Assert.Contains("--expected-commit \"${GITHUB_SHA}\"", release, StringComparison.Ordinal);
        Assert.Contains("--verify-tag", release, StringComparison.Ordinal);
        Assert.Contains("verify_release_assets \"${RUNNER_TEMP}/created-release\"", release, StringComparison.Ordinal);
    }

    [Fact]
    public void TG_008_OfflineHarnessModelsTagRefAndCommitResolution()
    {
        var harness = Read(HarnessPath);

        Assert.Contains("git/ref/tags/v1.2.3-rc.1", harness, StringComparison.Ordinal);
        Assert.Contains("commits/v1.2.3-rc.1", harness, StringComparison.Ordinal);
        Assert.Contains("FAKE_GH_MUTATE_TAG_ON_SECOND", harness, StringComparison.Ordinal);
        Assert.Contains("tag-ref-count", harness, StringComparison.Ordinal);
    }

    [Fact]
    public void TG_009_OfflineHarnessProvesWrongCommitAndTagMutationFailClosed()
    {
        var harness = Read(HarnessPath);

        Assert.Contains("Wrong approved-commit case unexpectedly passed", harness, StringComparison.Ordinal);
        Assert.Contains("tag does not resolve to the approved commit", harness, StringComparison.Ordinal);
        Assert.Contains("Tag-ref mutation case unexpectedly passed", harness, StringComparison.Ordinal);
        Assert.Contains("tag ref object changed during verification", harness, StringComparison.Ordinal);
        Assert.Contains("assert_no_hidden_staging", harness, StringComparison.Ordinal);
    }

    [Fact]
    public void TG_010_CiExecutesSharedRuntimeHarness()
    {
        var ci = Read(CiWorkflow);

        Assert.Contains("bash -n scripts/Verify-DurableRelease.sh", ci, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/Test-DurableReleaseVerifierSafety.sh", ci, StringComparison.Ordinal);
        Assert.Contains("bash scripts/Test-DurableReleaseVerifierSafety.sh", ci, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for durable release tag-provenance tests.");
    }
}
