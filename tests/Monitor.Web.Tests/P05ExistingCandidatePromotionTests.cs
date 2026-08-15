using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ExistingCandidatePromotionTests
{
    [Fact]
    public void PromotionWorkflow_IsManualExactRunAndNeverRebuildsOrRepackages()
    {
        var workflow = Read(".github/workflows/promote-existing-candidate.yml");

        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.Contains("acknowledge_promotion", workflow, StringComparison.Ordinal);
        Assert.Contains("source_run_id", workflow, StringComparison.Ordinal);
        Assert.Contains("source_artifact_id", workflow, StringComparison.Ordinal);
        Assert.Contains("expected_product_sha256", workflow, StringComparison.Ordinal);
        Assert.Contains("validate-dispatch-ref:", workflow, StringComparison.Ordinal);
        Assert.Contains("Require default-branch dispatch", workflow, StringComparison.Ordinal);
        Assert.Contains("refs/heads/main", workflow, StringComparison.Ordinal);
        Assert.Contains("needs: validate-dispatch-ref", workflow, StringComparison.Ordinal);
        Assert.Contains("inputs.acknowledge_promotion && github.ref == 'refs/heads/main'", workflow, StringComparison.Ordinal);
        Assert.True(
            workflow.IndexOf("validate-dispatch-ref:", StringComparison.Ordinal) < workflow.IndexOf("promote:", StringComparison.Ordinal),
            "Read-only dispatch-ref validation must be declared before the write-capable promotion job.");
        Assert.Contains(".github/workflows/production-candidate.yml", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/download-artifact@", workflow, StringComparison.Ordinal);
        Assert.Contains("github-token: ${{ github.token }}", workflow, StringComparison.Ordinal);
        Assert.Contains("run-id: ${{ inputs.source_run_id }}", workflow, StringComparison.Ordinal);
        Assert.Contains("Test-ExistingCandidatePromotion.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("contents: write", workflow, StringComparison.Ordinal);
        Assert.Contains("actions: read", workflow, StringComparison.Ordinal);

        Assert.DoesNotContain("dotnet build", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dotnet test", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dotnet publish", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Compress-Archive", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("upload-artifact", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh release upload", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--clobber", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("push:\n", workflow.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void PromotionWorkflow_IsImmutableAndBindsReleaseTagToTestedMerge()
    {
        var workflow = Read(".github/workflows/promote-existing-candidate.yml");
        var verifier = Read("scripts/Verify-DurableRelease.sh");

        Assert.Contains("Verify-DurableRelease.sh", workflow, StringComparison.Ordinal);
        Assert.Contains("gh release create", workflow, StringComparison.Ordinal);
        Assert.Contains("--verify-tag", workflow, StringComparison.Ordinal);
        Assert.Contains("--target \"${TESTED_SHA}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("Exact durable release already exists; no mutation performed.", workflow, StringComparison.Ordinal);
        Assert.Contains("External IIS acceptance remains governed by #116", workflow, StringComparison.Ordinal);
        Assert.Contains("releases/assets/${first_zip_id}", verifier, StringComparison.Ordinal);
        Assert.Contains("releases/assets/${first_checksum_id}", verifier, StringComparison.Ordinal);
        Assert.Contains("release or asset security metadata changed during verification", verifier, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release download", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release download", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingCandidateValidator_RequiresHashAndEmbeddedManifestIdentity()
    {
        var script = Read("scripts/Test-ExistingCandidatePromotion.ps1");

        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains("_operations/release-manifest.json", script, StringComparison.Ordinal);
        Assert.Contains("schemaVersion", script, StringComparison.Ordinal);
        Assert.Contains("sourceHeadSha", script, StringComparison.Ordinal);
        Assert.Contains("testedMergeSha", script, StringComparison.Ordinal);
        Assert.Contains("deploymentMode", script, StringComparison.Ordinal);
        Assert.Contains("SingleNode", script, StringComparison.Ordinal);
        Assert.Contains("candidateVerification.sourceOfTruth", script, StringComparison.Ordinal);
        Assert.Contains("realSqlAcceptance", script, StringComparison.Ordinal);
        Assert.Contains("ExpectedProductSha256", script, StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
    {
        var root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for P0.5 promotion tests.");
    }
}
