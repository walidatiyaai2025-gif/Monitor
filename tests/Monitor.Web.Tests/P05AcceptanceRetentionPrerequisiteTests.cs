using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05AcceptanceRetentionPrerequisiteTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void AcceptanceRunbook_RequiresExactDurableRetentionBeforeCutover()
    {
        var runbook = Read("docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md");

        Assert.Contains("Mandatory pre-cutover durable-retention prerequisite — #162", runbook, StringComparison.Ordinal);

        // The primary operator path must use the explicit helper introduced by #338/#339.
        Assert.Contains("Invoke-Rc61DurablePromotion.ps1", runbook, StringComparison.Ordinal);
        Assert.Contains("Status = READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT", runbook, StringComparison.Ordinal);
        Assert.Contains("WorkflowDispatchPerformed = False", runbook, StringComparison.Ordinal);
        Assert.Contains("IndependentVerificationDispatched = False", runbook, StringComparison.Ordinal);
        Assert.Contains("ProductionMutationPerformed = False", runbook, StringComparison.Ordinal);
        Assert.Contains("-AcknowledgePromotion", runbook, StringComparison.Ordinal);
        Assert.Contains("Status = PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED", runbook, StringComparison.Ordinal);
        Assert.Contains("IndependentVerificationCommand", runbook, StringComparison.Ordinal);
        Assert.Contains("do not redispatch", runbook, StringComparison.OrdinalIgnoreCase);

        // Keep the lower-level preflight and immutable identity available for diagnosis/audit.
        Assert.Contains("Test-Rc61DurablePromotionPreflight.ps1", runbook, StringComparison.Ordinal);
        Assert.Contains("Status = READY_FOR_EXPLICIT_MANUAL_PROMOTION", runbook, StringComparison.Ordinal);
        Assert.Contains("MutatedGitHubState = False", runbook, StringComparison.Ordinal);
        Assert.Contains("TagExists = False", runbook, StringComparison.Ordinal);
        Assert.Contains("ReleaseExists = False", runbook, StringComparison.Ordinal);
        Assert.Contains("promote-existing-candidate.yml", runbook, StringComparison.Ordinal);
        Assert.Contains("verify-durable-release.yml", runbook, StringComparison.Ordinal);
        Assert.Contains("expected_outer_artifact_digest=sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382", runbook, StringComparison.Ordinal);
        Assert.Contains("expected_product_sha256=d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5", runbook, StringComparison.Ordinal);
        Assert.Contains("expected_commit=158148d8bfd05f724014541bc7a0b1eab5dae1b5", runbook, StringComparison.Ordinal);
        Assert.Contains("audit/troubleshooting reference", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Manually dispatch `.github/workflows/promote-existing-candidate.yml`", runbook, StringComparison.Ordinal);

        // Promotion and independent verification must be rebound by explicit run IDs before #116 preparation.
        Assert.Contains("Test-Rc61CutoverReadiness.ps1", runbook, StringComparison.Ordinal);
        Assert.Contains("-PromotionRunId <PROMOTION_RUN_ID>", runbook, StringComparison.Ordinal);
        Assert.Contains("-VerificationRunId <VERIFICATION_RUN_ID>", runbook, StringComparison.Ordinal);
        Assert.Contains("Status = READY_FOR_P0_5_PRE_CUTOVER_PREPARATION", runbook, StringComparison.Ordinal);
        Assert.Contains("DurableReleasePrerequisiteSatisfied = True", runbook, StringComparison.Ordinal);
        Assert.Contains("ExternalGatesPassed = 0", runbook, StringComparison.Ordinal);

        Assert.Contains("Neither successful preflight, successful promotion nor successful durable verification marks any external production gate PASS", runbook, StringComparison.Ordinal);
        Assert.Contains("does not build, publish, compress or repackage RC.61", runbook, StringComparison.OrdinalIgnoreCase);

        var preview = runbook.IndexOf("### 0. Preview the fail-closed RC.61 promotion helper", StringComparison.Ordinal);
        var promotion = runbook.IndexOf("### 1. Execute the explicit acknowledged promotion", StringComparison.Ordinal);
        var verification = runbook.IndexOf("### 2. Run separate read-only durable verification", StringComparison.Ordinal);
        var readiness = runbook.IndexOf("### 3. Bind the exact Green runs before #116 preparation", StringComparison.Ordinal);
        Assert.True(preview >= 0 && preview < promotion && promotion < verification && verification < readiness,
            "The acceptance runbook must preserve preview -> acknowledged promotion -> separate verification -> explicit run-ID readiness order.");
    }

    [Fact]
    public void AcceptanceRunbook_PreservesExactFifteenExternalGateNames()
    {
        var runbook = Read("docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md");
        var gates = new[]
        {
            "artifactChecksumVerified",
            "iisPreflightPassed",
            "deploymentPlanReviewed",
            "cutoverApplied",
            "trustedHttpsHealthPassed",
            "administratorAuthenticationPassed",
            "leastPrivilegeSqlVerified",
            "iisRecyclePassed",
            "registrationDurabilityVerified",
            "protectedCredentialDurabilityVerified",
            "operationalStateDurabilityVerified",
            "operationalBackupValidated",
            "rollbackRehearsed",
            "postRollbackHealthPassed",
            "finalReadEvidencePassed"
        };

        foreach (var gate in gates)
        {
            Assert.Contains($"`{gate}`", runbook, StringComparison.Ordinal);
        }

        Assert.Contains("exactly 15 required gates", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Durable publication/verification is intentionally **not** an additional evidence-pack gate", runbook, StringComparison.Ordinal);
        Assert.Contains("#116 remains OPEN", runbook, StringComparison.Ordinal);
        Assert.Contains("Umbrella #111 may close only after #116 is accepted", runbook, StringComparison.Ordinal);
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
