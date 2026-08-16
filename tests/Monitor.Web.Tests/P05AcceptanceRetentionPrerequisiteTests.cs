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
        Assert.Contains("promote-existing-candidate.yml", runbook, StringComparison.Ordinal);
        Assert.Contains("verify-durable-release.yml", runbook, StringComparison.Ordinal);
        Assert.Contains("expected_outer_artifact_digest=sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382", runbook, StringComparison.Ordinal);
        Assert.Contains("expected_product_sha256=d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5", runbook, StringComparison.Ordinal);
        Assert.Contains("expected_commit=158148d8bfd05f724014541bc7a0b1eab5dae1b5", runbook, StringComparison.Ordinal);
        Assert.Contains("Neither successful promotion nor successful durable verification marks any external production gate PASS", runbook, StringComparison.Ordinal);
        Assert.Contains("does not build, publish, compress or repackage RC.61", runbook, StringComparison.OrdinalIgnoreCase);
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
