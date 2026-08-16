using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05Rc61PromotionHandoffTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Rc61DeployHandoff_MatchesCurrentPromotionInputContract()
    {
        var workflow = Read(".github/workflows/promote-existing-candidate.yml");
        var handoff = Read("deploy/RC61_DURABLE_PROMOTION.md");

        foreach (var input in new[]
        {
            "candidate_version",
            "source_run_id",
            "source_artifact_id",
            "expected_outer_artifact_digest",
            "expected_product_sha256",
            "source_commit",
            "tested_merge_commit",
            "release_tag",
            "acknowledge_promotion"
        })
        {
            Assert.Contains($"{input}:", workflow, StringComparison.Ordinal);
            Assert.Contains($"{input}=", handoff, StringComparison.Ordinal);
        }

        Assert.Contains("refs/heads/main", workflow, StringComparison.Ordinal);
        Assert.Contains("from `main`", handoff, StringComparison.Ordinal);
        Assert.Contains("sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382", handoff, StringComparison.Ordinal);
        Assert.Contains("d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5", handoff, StringComparison.Ordinal);
        Assert.Contains("158148d8bfd05f724014541bc7a0b1eab5dae1b5", handoff, StringComparison.Ordinal);
    }

    [Fact]
    public void Rc61DeployHandoff_RequiresIndependentReadOnlyVerification()
    {
        var verification = Read(".github/workflows/verify-durable-release.yml");
        var handoff = Read("deploy/RC61_DURABLE_PROMOTION.md");

        foreach (var input in new[]
        {
            "release_version",
            "release_tag",
            "expected_commit",
            "expected_product_sha256"
        })
        {
            Assert.Contains($"{input}:", verification, StringComparison.Ordinal);
            Assert.Contains($"{input}=", handoff, StringComparison.Ordinal);
        }

        Assert.Contains("permissions:\n  contents: read", verification, StringComparison.Ordinal);
        Assert.Contains("separate `verify-durable-release` run", handoff, StringComparison.Ordinal);
        Assert.Contains("#116 remains the production acceptance authority", handoff, StringComparison.Ordinal);
        Assert.Contains("#111 remains open until #116 is accepted", handoff, StringComparison.Ordinal);
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
