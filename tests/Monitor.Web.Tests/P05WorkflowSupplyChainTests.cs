using System.Text.RegularExpressions;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05WorkflowSupplyChainTests
{
    private static readonly IReadOnlyDictionary<string, string> ApprovedPins =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["actions/checkout"] = "11d5960a326750d5838078e36cf38b85af677262",
            ["actions/setup-dotnet"] = "67a3573c9a986a3f9c594539f4ab511d57bb3ce9",
            ["actions/upload-artifact"] = "ea165f8d65b6e75b540449e92b4886f43607fa02",
            ["actions/download-artifact"] = "d3f86a106a0bac45b974a628896c90dbdf5c8093"
        };

    private static readonly string[] ActiveWorkflows =
    {
        "ci.yml",
        "production-candidate.yml",
        "promote-existing-candidate.yml",
        "real-sql-acceptance.yml",
        "release.yml"
    };

    [Fact]
    public void ActiveWorkflows_PinEveryExternalActionToApprovedImmutableCommit()
    {
        var root = FindRepoRoot();
        var workflowsRoot = Path.Combine(root, ".github", "workflows");
        var actionUse = new Regex(
            @"^\s*-?\s*uses:\s*(?<target>\S+?)(?:\s+#.*)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var immutableExternal = new Regex(
            @"^(?<action>[^@]+)@(?<sha>[0-9a-f]{40})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var observed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var workflowName in ActiveWorkflows)
        {
            var path = Path.Combine(workflowsRoot, workflowName);
            Assert.True(File.Exists(path), $"Expected active workflow is missing: {workflowName}");

            foreach (var line in File.ReadLines(path))
            {
                var use = actionUse.Match(line);
                if (!use.Success) continue;

                var target = use.Groups["target"].Value;
                if (target.StartsWith("./", StringComparison.Ordinal)) continue;

                var immutable = immutableExternal.Match(target);
                Assert.True(
                    immutable.Success,
                    $"External workflow dependency must use an exact 40-character commit SHA: {workflowName}: {target}");

                var action = immutable.Groups["action"].Value;
                var sha = immutable.Groups["sha"].Value;
                Assert.True(
                    ApprovedPins.TryGetValue(action, out var approvedSha),
                    $"External workflow dependency is not allowlisted: {workflowName}: {action}");
                Assert.Equal(approvedSha, sha);
                observed.Add(action);
            }
        }

        Assert.Equal(
            ApprovedPins.Keys.OrderBy(value => value, StringComparer.Ordinal),
            observed.OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void CompletedBatch100OneShotPrivilegedMergeWorkflow_IsRemoved()
    {
        var root = FindRepoRoot();
        var obsolete = Path.Combine(root, ".github", "workflows", "batch100-merge-if-verified.yml");

        Assert.False(File.Exists(obsolete));
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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for workflow supply-chain tests.");
    }
}
