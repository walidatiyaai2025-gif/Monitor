using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05WorkflowSupplyChainTests
{
    private const string ApprovedDotnetSdk = "8.0.424";
    private const string ApprovedSqlServerImage = "mcr.microsoft.com/mssql/server@sha256:ba4c8329f48fb8f02e1416be6a930ebfd71268caee78aa985f3af4315e457c89";
    private const string ApprovedUbuntuRunner = "ubuntu-24.04";
    private const string ApprovedNugetSource = "https://api.nuget.org/v3/index.json";

    private static readonly IReadOnlyDictionary<string, string> ApprovedPins =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["actions/checkout"] = "3d3c42e5aac5ba805825da76410c181273ba90b1",
            ["actions/setup-dotnet"] = "a98b56852c35b8e3190ac28c8c2271da59106c68",
            ["actions/upload-artifact"] = "043fb46d1a93c77aae656e7c1c64a875d1fc6a0a",
            ["actions/download-artifact"] = "3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c"
        };

    private static readonly IReadOnlyDictionary<string, string> ApprovedVersionComments =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["actions/checkout"] = "v7.0.1",
            ["actions/setup-dotnet"] = "v6.0.0",
            ["actions/upload-artifact"] = "v7.0.1",
            ["actions/download-artifact"] = "v8.0.1"
        };

    private static readonly IReadOnlyDictionary<string, string> ApprovedPackageReferences =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/Monitor.Web/Monitor.Web.csproj|Microsoft.Data.SqlClient"] = "7.0.2",
            ["src/Monitor.Web/Monitor.Web.csproj|Microsoft.Extensions.Hosting.WindowsServices"] = "8.0.1",
            ["tests/Monitor.Web.Tests/Monitor.Web.Tests.csproj|Microsoft.NET.Test.Sdk"] = "17.12.0",
            ["tests/Monitor.Web.Tests/Monitor.Web.Tests.csproj|xunit"] = "2.9.2",
            ["tests/Monitor.Web.Tests/Monitor.Web.Tests.csproj|xunit.runner.visualstudio"] = "2.8.2"
        };

    private static readonly IReadOnlyDictionary<string, int> LinuxWorkflowRunnerCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["ci.yml"] = 1,
            ["promote-existing-candidate.yml"] = 1,
            ["real-sql-acceptance.yml"] = 1,
            ["release.yml"] = 2
        };

    private static readonly string[] LinuxCheckoutWorkflows =
    {
        "ci.yml",
        "promote-existing-candidate.yml",
        "real-sql-acceptance.yml"
    };

    private static readonly string[] ActiveWorkflows =
    {
        "ci.yml",
        "production-candidate.yml",
        "promote-existing-candidate.yml",
        "real-sql-acceptance.yml",
        "release.yml"
    };

    private static readonly string[] SolutionProjectPaths =
    {
        "src/Monitor.Web/Monitor.Web.csproj",
        "tests/Monitor.Web.Tests/Monitor.Web.Tests.csproj"
    };

    [Fact]
    public void ActiveWorkflows_PinEveryExternalActionToApprovedImmutableCommit()
    {
        var root = FindRepoRoot();
        var workflowsRoot = Path.Combine(root, ".github", "workflows");
        var actionUse = new Regex(
            @"^\s*-?\s*uses:\s*(?<target>\S+?)(?:\s+#\s*(?<comment>\S+))?\s*$",
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

                Assert.True(
                    ApprovedVersionComments.TryGetValue(action, out var approvedVersion),
                    $"External workflow dependency has no approved version metadata: {workflowName}: {action}");
                Assert.Equal(approvedVersion, use.Groups["comment"].Value);
                observed.Add(action);
            }
        }

        Assert.Equal(
            ApprovedPins.Keys.OrderBy(value => value, StringComparer.Ordinal),
            observed.OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void WorkflowPrivilegeBoundary_RejectsTrustedPrEscalationAndWriteAll()
    {
        var root = FindRepoRoot();
        var workflowsRoot = Path.Combine(root, ".github", "workflows");
        var trustedTrigger = new Regex(
            @"(?m)^\s*(pull_request_target|workflow_run)\s*:",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var writeAll = new Regex(
            @"(?m)^\s*permissions:\s*write-all\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var workflowFiles = Directory.EnumerateFiles(workflowsRoot, "*.yml")
            .Concat(Directory.EnumerateFiles(workflowsRoot, "*.yaml"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(workflowFiles);
        foreach (var path in workflowFiles)
        {
            var workflow = File.ReadAllText(path);
            Assert.DoesNotMatch(trustedTrigger, workflow);
            Assert.DoesNotMatch(writeAll, workflow);
        }
    }

    [Fact]
    public void RepositoryDotnetSdk_IsExactAndFailClosed()
    {
        var root = FindRepoRoot();
        var globalJsonPath = Path.Combine(root, "global.json");
        Assert.True(File.Exists(globalJsonPath), "global.json must lock the repository SDK.");

        using var document = JsonDocument.Parse(File.ReadAllText(globalJsonPath));
        var sdk = document.RootElement.GetProperty("sdk");
        Assert.Equal(ApprovedDotnetSdk, sdk.GetProperty("version").GetString());
        Assert.Equal("disable", sdk.GetProperty("rollForward").GetString());
        Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
    }

    [Fact]
    public void RepositoryNugetPolicy_ClearsAmbientSourcesAndMapsAllPackagesToNugetOrg()
    {
        var root = FindRepoRoot();
        var configPath = Path.Combine(root, "nuget.config");
        Assert.True(File.Exists(configPath), "Repository nuget.config must define restore provenance.");

        var document = XDocument.Load(configPath, LoadOptions.None);
        var configuration = Assert.IsType<XElement>(document.Root);
        Assert.Equal("configuration", configuration.Name.LocalName);

        var packageSources = Assert.Single(configuration.Elements("packageSources"));
        Assert.Single(packageSources.Elements("clear"));
        var source = Assert.Single(packageSources.Elements("add"));
        Assert.Equal("nuget.org", source.Attribute("key")?.Value);
        Assert.Equal(ApprovedNugetSource, source.Attribute("value")?.Value);
        Assert.Equal("3", source.Attribute("protocolVersion")?.Value);

        var sourceMapping = Assert.Single(configuration.Elements("packageSourceMapping"));
        Assert.Single(sourceMapping.Elements("clear"));
        var mappedSource = Assert.Single(sourceMapping.Elements("packageSource"));
        Assert.Equal("nuget.org", mappedSource.Attribute("key")?.Value);
        var package = Assert.Single(mappedSource.Elements("package"));
        Assert.Equal("*", package.Attribute("pattern")?.Value);
    }

    [Fact]
    public void SolutionDirectPackageReferences_MatchExactApprovedAllowlist()
    {
        var root = FindRepoRoot();
        var exactVersion = new Regex(
            @"^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var observed = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var projectPath in SolutionProjectPaths)
        {
            var fullPath = Path.Combine(root, projectPath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullPath), $"Expected solution project is missing: {projectPath}");

            var project = XDocument.Load(fullPath, LoadOptions.None);
            foreach (var reference in project.Descendants("PackageReference"))
            {
                var packageId = reference.Attribute("Include")?.Value;
                var version = reference.Attribute("Version")?.Value ?? reference.Element("Version")?.Value;

                Assert.False(string.IsNullOrWhiteSpace(packageId), $"PackageReference without Include in {projectPath}.");
                Assert.False(string.IsNullOrWhiteSpace(version), $"PackageReference {packageId} in {projectPath} must declare an explicit version.");
                Assert.Matches(exactVersion, version!);

                var key = $"{projectPath}|{packageId}";
                Assert.True(
                    ApprovedPackageReferences.TryGetValue(key, out var approvedVersion),
                    $"Direct dependency is not allowlisted: {key} {version}");
                Assert.Equal(approvedVersion, version);
                Assert.True(observed.TryAdd(key, version!), $"Duplicate direct dependency declaration: {key}");
            }
        }

        Assert.Equal(
            ApprovedPackageReferences.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            observed.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("ci.yml")]
    [InlineData("real-sql-acceptance.yml")]
    public void CoreUbuntuGates_InstallRepositoryLockedSdk(string workflowName)
    {
        var root = FindRepoRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", workflowName));

        Assert.Contains("global-json-file: global.json", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void LinuxWorkflows_PinUbuntuOsMajorAndRejectLatestAlias()
    {
        var root = FindRepoRoot();
        var workflowsRoot = Path.Combine(root, ".github", "workflows");
        var approvedRunner = new Regex(
            @"(?m)^\s*runs-on:\s*ubuntu-24\.04\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        foreach (var pair in LinuxWorkflowRunnerCounts)
        {
            var workflow = File.ReadAllText(Path.Combine(workflowsRoot, pair.Key));
            Assert.DoesNotContain("ubuntu-latest", workflow, StringComparison.Ordinal);
            Assert.Equal(pair.Value, approvedRunner.Matches(workflow).Count);
            Assert.Contains(ApprovedUbuntuRunner, workflow, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LinuxCheckoutJobs_DoNotPersistRepositoryCredentials()
    {
        var root = FindRepoRoot();
        var workflowsRoot = Path.Combine(root, ".github", "workflows");
        var disabledPersistence = new Regex(
            @"(?m)^\s*persist-credentials:\s*false\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        foreach (var workflowName in LinuxCheckoutWorkflows)
        {
            var workflow = File.ReadAllText(Path.Combine(workflowsRoot, workflowName));
            Assert.Contains("actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("persist-credentials: true", workflow, StringComparison.Ordinal);
            Assert.Single(disabledPersistence.Matches(workflow));
        }
    }

    [Fact]
    public void RealSqlGate_PinsSqlServerImageByExactDigest()
    {
        var root = FindRepoRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "real-sql-acceptance.yml"));

        Assert.Contains(ApprovedSqlServerImage, workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("mcr.microsoft.com/mssql/server:2022-latest", workflow, StringComparison.Ordinal);
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
