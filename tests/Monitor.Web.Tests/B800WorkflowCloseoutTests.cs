using System.Text.RegularExpressions;

namespace Monitor.Web.Tests;

public sealed class B800WorkflowCloseoutTests
{
    private static readonly string[] RequiredWorkflowRegressionFiles =
    [
        "B800WorkflowSafetyMatrixTests.cs",
        "B800RazorPostWiringTests.cs",
        "B800BoundedGetNavigationTests.cs",
        "B800PrgFeedbackContractTests.cs",
        "B800IncidentAdvisorRoleTests.cs",
        "B800ConnectionLabWorkflowTests.cs",
        "B800SettingsBackupRestoreTests.cs",
        "B800GovernanceRetentionWorkflowTests.cs",
        "B800EnterpriseOperationsRoleTests.cs"
    ];

    [Fact]
    public void B800_030_Workflow_regression_matrix_remains_complete()
    {
        var root = FindRepositoryRoot();
        var testsRoot = Path.Combine(root, "tests", "Monitor.Web.Tests");

        foreach (var fileName in RequiredWorkflowRegressionFiles)
        {
            var path = Path.Combine(testsRoot, fileName);
            Assert.True(File.Exists(path), $"Missing B800 workflow regression file: {fileName}");

            var source = File.ReadAllText(path);
            Assert.False(string.IsNullOrWhiteSpace(source));
            Assert.True(
                source.Contains("[Fact]", StringComparison.Ordinal) ||
                source.Contains("[Theory]", StringComparison.Ordinal),
                $"Workflow regression file has no executable xUnit test: {fileName}");
        }
    }

    [Fact]
    public void B800_030_Destructive_workflows_keep_explicit_typed_confirmation_contracts()
    {
        var root = FindRepositoryRoot();
        var webRoot = Path.Combine(root, "src", "Monitor.Web");
        var runtimeFiles = Directory
            .EnumerateFiles(webRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(runtimeFiles);

        var sources = runtimeFiles
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Contains(sources, item => item.Source.Contains("PRUNE", StringComparison.Ordinal));
        Assert.Contains(sources, item => item.Source.Contains("RESTORE", StringComparison.Ordinal));

        var confirmationViews = sources
            .Where(item => item.Path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Source.Contains("PRUNE", StringComparison.Ordinal) ||
                           item.Source.Contains("RESTORE", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(confirmationViews);
        Assert.All(
            confirmationViews,
            item => Assert.Matches(
                new Regex("<form\\b[^>]*method\\s*=\\s*[\\\"']post[\\\"']", RegexOptions.IgnoreCase),
                item.Source));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var webProject = Path.Combine(directory.FullName, "src", "Monitor.Web", "Monitor.Web.csproj");
            var testProject = Path.Combine(directory.FullName, "tests", "Monitor.Web.Tests", "Monitor.Web.Tests.csproj");

            if (File.Exists(webProject) && File.Exists(testProject))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate the Monitor repository root from the test output directory.");
    }
}
