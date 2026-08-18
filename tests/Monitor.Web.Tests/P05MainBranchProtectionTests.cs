using System.Text.RegularExpressions;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05MainBranchProtectionTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void MainProtectionHelper_IsExplicitFailClosedAndPinsStableCheckNames()
    {
        var helper = Read("scripts/Set-MainBranchProtection.ps1");
        var ci = Read(".github/workflows/ci.yml");
        var metadata = Read(".github/workflows/protected-p0-pr-metadata.yml");
        var commits = Read(".github/workflows/protected-p0-pr-commits.yml");

        Assert.Contains("name: protected-p0-pr-metadata", metadata, StringComparison.Ordinal);
        Assert.Contains("name: protected-p0-pr-commits", commits, StringComparison.Ordinal);
        Assert.Contains("'scripts/Set-MainBranchProtection.ps1'", ci, StringComparison.Ordinal);

        Assert.Contains("[switch]$AcknowledgeProtection", helper, StringComparison.Ordinal);
        Assert.Contains("walidatiyaai2025-gif/Monitor", helper, StringComparison.Ordinal);
        Assert.Contains("1329517438", helper, StringComparison.Ordinal);
        Assert.Contains("'main'", helper, StringComparison.Ordinal);
        Assert.Contains("'build'", helper, StringComparison.Ordinal);
        Assert.Contains("'protected-p0-pr-metadata'", helper, StringComparison.Ordinal);
        Assert.Contains("'protected-p0-pr-commits'", helper, StringComparison.Ordinal);
        Assert.Contains("READY_FOR_EXPLICIT_BRANCH_PROTECTION_ACKNOWLEDGEMENT", helper, StringComparison.Ordinal);
        Assert.Contains("BRANCH_PROTECTION_APPLIED_AND_VERIFIED", helper, StringComparison.Ordinal);
        Assert.Contains("ALREADY_PROTECTED_AS_REQUIRED", helper, StringComparison.Ordinal);
        Assert.Contains("ExternalProductionGatesPassed = 0", helper, StringComparison.Ordinal);
        Assert.Contains("MutationPerformed = $false", helper, StringComparison.Ordinal);
        Assert.Contains("MutationPerformed = $true", helper, StringComparison.Ordinal);

        Assert.Contains("strict = $true", helper, StringComparison.Ordinal);
        Assert.Contains("enforce_admins = $true", helper, StringComparison.Ordinal);
        Assert.Contains("required_pull_request_reviews = $null", helper, StringComparison.Ordinal);
        Assert.Contains("required_conversation_resolution = $true", helper, StringComparison.Ordinal);
        Assert.Contains("allow_force_pushes = $false", helper, StringComparison.Ordinal);
        Assert.Contains("allow_deletions = $false", helper, StringComparison.Ordinal);
        Assert.Contains("Test-ProtectionExact -Snapshot $after", helper, StringComparison.Ordinal);
        Assert.Contains("read-back verification did not match the exact required policy", helper, StringComparison.Ordinal);

        var acknowledgementIndex = helper.IndexOf("if (-not $AcknowledgeProtection)", StringComparison.Ordinal);
        var putIndex = helper.IndexOf("'--method', 'PUT'", StringComparison.Ordinal);
        Assert.True(acknowledgementIndex >= 0 && putIndex > acknowledgementIndex,
            "The branch-protection mutation must remain after the explicit acknowledgement gate.");
        Assert.Equal(1, Regex.Matches(helper, Regex.Escape("'--method', 'PUT'"), RegexOptions.CultureInvariant).Count);
        Assert.DoesNotContain("'DELETE'", helper, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh issue", helper, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Deploy-ProductionSingleNode", helper, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
