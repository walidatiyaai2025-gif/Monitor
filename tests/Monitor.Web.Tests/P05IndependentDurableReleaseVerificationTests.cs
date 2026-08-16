using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05IndependentDurableReleaseVerificationTests
{
    private const string WorkflowPath = ".github/workflows/verify-durable-release.yml";
    private const string RunbookPath = "docs/P05_EXISTING_CANDIDATE_PROMOTION.md";

    [Fact]
    public void IV_001_WorkflowIsManualAndReadOnly()
    {
        var workflow = Read(WorkflowPath);

        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("permissions:\n      contents: read", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("contents: write", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("pull_request:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("push:", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void IV_002_WorkflowRequiresCanonicalIdentityInputs()
    {
        var workflow = Read(WorkflowPath);

        Assert.Contains("release_version:", workflow, StringComparison.Ordinal);
        Assert.Contains("release_tag:", workflow, StringComparison.Ordinal);
        Assert.Contains("expected_commit:", workflow, StringComparison.Ordinal);
        Assert.Contains("expected_product_sha256:", workflow, StringComparison.Ordinal);
        Assert.Contains("[[ \"${RELEASE_TAG}\" == \"v${RELEASE_VERSION}\" ]]", workflow, StringComparison.Ordinal);
        Assert.Contains("[[ \"${EXPECTED_COMMIT}\" =~ ^[a-f0-9]{40}$ ]]", workflow, StringComparison.Ordinal);
        Assert.Contains("[[ \"${EXPECTED_PRODUCT_SHA256}\" =~ ^[a-f0-9]{64}$ ]]", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void IV_003_WorkflowRejectsNonMainDispatchesBeforeCheckout()
    {
        var workflow = Read(WorkflowPath);
        var gate = workflow.IndexOf("${GITHUB_REF}\" != \"refs/heads/main", StringComparison.Ordinal);
        var checkout = workflow.IndexOf("actions/checkout@", StringComparison.Ordinal);

        Assert.True(gate >= 0 && checkout > gate, "The main-ref dispatch gate must execute before checkout.");
        Assert.Contains("Independent durable-release verification must be dispatched from refs/heads/main", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void IV_004_WorkflowUsesNonPersistingCheckoutAndSharedVerifier()
    {
        var workflow = Read(WorkflowPath);

        Assert.Contains("actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1", workflow, StringComparison.Ordinal);
        Assert.Contains("persist-credentials: false", workflow, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/Verify-DurableRelease.sh", workflow, StringComparison.Ordinal);
        Assert.Contains("bash scripts/Verify-DurableRelease.sh", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void IV_005_SharedVerifierReceivesAllApprovedIdentityAndByteInputs()
    {
        var workflow = Read(WorkflowPath);

        Assert.Contains("--repository \"${GITHUB_REPOSITORY}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--tag \"${RELEASE_TAG}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--version \"${RELEASE_VERSION}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--product-sha256 \"${EXPECTED_PRODUCT_SHA256}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--expected-commit \"${EXPECTED_COMMIT}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--trusted-root \"${trusted_root}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--destination \"${destination}\"", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void IV_006_VerifiedBytesRemainRunnerTemporaryAndAreNeverRepublished()
    {
        var workflow = Read(WorkflowPath);

        Assert.Contains("trusted_root=\"$(realpath -e -- \"${RUNNER_TEMP}\")\"", workflow, StringComparison.Ordinal);
        Assert.Contains("destination=\"${trusted_root}/independent-durable-release-${GITHUB_RUN_ID}-${GITHUB_RUN_ATTEMPT}\"", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("actions/upload-artifact", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release create", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release upload", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("git tag", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void IV_007_WorkflowEmitsNonSecretIndependentPassSummary()
    {
        var workflow = Read(WorkflowPath);

        Assert.Contains("${GITHUB_STEP_SUMMARY}", workflow, StringComparison.Ordinal);
        Assert.Contains("Status: **PASS**", workflow, StringComparison.Ordinal);
        Assert.Contains("Approved commit:", workflow, StringComparison.Ordinal);
        Assert.Contains("Approved product SHA-256:", workflow, StringComparison.Ordinal);
        Assert.Contains("Mutation: none", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("password", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", workflow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IV_008_RunbookRequiresSeparateReadOnlyVerificationBeforeClosure()
    {
        var runbook = Read(RunbookPath);

        Assert.Contains("## Independent read-only verification after promotion", runbook, StringComparison.Ordinal);
        Assert.Contains(".github/workflows/verify-durable-release.yml", runbook, StringComparison.Ordinal);
        Assert.Contains("separately from `main`", runbook, StringComparison.Ordinal);
        Assert.Contains("Do not use the promotion run's own post-publication verification as a substitute", runbook, StringComparison.Ordinal);
        Assert.Contains("a separate `verify-durable-release` run from `main` completed successfully", runbook, StringComparison.Ordinal);
    }

    [Fact]
    public void IV_009_RunbookPinsExactSelectedRc61IndependentVerificationInputs()
    {
        var runbook = Read(RunbookPath);

        Assert.Contains("`release_version`: `0.1.0-rc.61`", runbook, StringComparison.Ordinal);
        Assert.Contains("`release_tag`: `v0.1.0-rc.61`", runbook, StringComparison.Ordinal);
        Assert.Contains("`expected_commit`: `158148d8bfd05f724014541bc7a0b1eab5dae1b5`", runbook, StringComparison.Ordinal);
        Assert.Contains("`expected_product_sha256`: `d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5`", runbook, StringComparison.Ordinal);
        Assert.Contains("Retain the Green verification run URL and its Step Summary", runbook, StringComparison.Ordinal);
    }

    [Fact]
    public void IV_010_IndependentWorkflowContainsNoBuildSqlOrMutationSurface()
    {
        var workflow = Read(WorkflowPath);

        Assert.DoesNotContain("dotnet build", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet publish", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("sqlcmd", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("docker", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--method POST", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--method PATCH", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--method DELETE", workflow, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for independent durable-release verification tests.");
    }
}
