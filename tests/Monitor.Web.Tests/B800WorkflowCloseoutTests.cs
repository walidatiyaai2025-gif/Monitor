using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800WorkflowCloseoutTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void HighRiskMutationEndpoints_HaveNamedPolicyPostAndAntiforgeryContracts()
    {
        var matrix = new (Type Controller, string Method, string Policy)[]
        {
            (typeof(OperationsController), nameof(OperationsController.RefreshServer), MonitorPolicies.Operate),
            (typeof(OperationsController), nameof(OperationsController.AcknowledgeIncident), MonitorPolicies.Operate),
            (typeof(OperationsController), nameof(OperationsController.ResolveIncident), MonitorPolicies.Operate),
            (typeof(OperationsController), nameof(OperationsController.ReopenIncident), MonitorPolicies.Operate),
            (typeof(OperationsController), nameof(OperationsController.RequestAdvisor), MonitorPolicies.Advisor),
            (typeof(ConnectionLabController), nameof(ConnectionLabController.Register), MonitorPolicies.Manage),
            (typeof(ConnectionLabController), nameof(ConnectionLabController.Test), MonitorPolicies.Manage),
            (typeof(ConnectionLabController), nameof(ConnectionLabController.ReplaceCredentialReference), MonitorPolicies.Manage),
            (typeof(ConnectionLabController), nameof(ConnectionLabController.ReplaceLocalCredential), MonitorPolicies.Manage),
            (typeof(ConnectionLabController), nameof(ConnectionLabController.CleanupOwnedCredentials), MonitorPolicies.Manage),
            (typeof(ConnectionLabController), nameof(ConnectionLabController.Enable), MonitorPolicies.Manage),
            (typeof(ConnectionLabController), nameof(ConnectionLabController.Disable), MonitorPolicies.Manage),
            (typeof(IncidentCollaborationController), nameof(IncidentCollaborationController.ResolveWithNote), MonitorPolicies.Operate),
            (typeof(IncidentCollaborationController), nameof(IncidentCollaborationController.ReopenWithReason), MonitorPolicies.Operate),
            (typeof(OperationalBackupController), nameof(OperationalBackupController.CreateBackup), MonitorPolicies.Manage),
            (typeof(OperationalBackupController), nameof(OperationalBackupController.ValidateBackup), MonitorPolicies.Manage),
            (typeof(OperationalBackupController), nameof(OperationalBackupController.RestoreBackup), MonitorPolicies.Manage),
            (typeof(GovernanceController), nameof(GovernanceController.Apply), MonitorPolicies.Manage),
            (typeof(EnterpriseOperationsController), nameof(EnterpriseOperationsController.UpdateServerProfile), MonitorPolicies.Manage),
            (typeof(EnterpriseOperationsController), nameof(EnterpriseOperationsController.AssignIncident), MonitorPolicies.Operate),
            (typeof(EnterpriseOperationsController), nameof(EnterpriseOperationsController.AddIncidentNote), MonitorPolicies.Operate),
            (typeof(EnterpriseOperationsController), nameof(EnterpriseOperationsController.AcknowledgeRecommendation), MonitorPolicies.Operate)
        };

        foreach (var item in matrix)
        {
            var method = item.Controller.GetMethod(item.Method)
                ?? throw new MissingMethodException(item.Controller.Name, item.Method);

            Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

            var effectivePolicies = item.Controller.GetCustomAttributes<AuthorizeAttribute>()
                .Concat(method.GetCustomAttributes<AuthorizeAttribute>())
                .Select(attribute => attribute.Policy)
                .Where(policy => !string.IsNullOrWhiteSpace(policy))
                .ToArray();
            Assert.Contains(item.Policy, effectivePolicies);
        }
    }

    [Fact]
    public void WorkflowSlices_021_Through_029_HaveDedicatedRegressionAndWorkEvidence()
    {
        var regressionFiles = new[]
        {
            "tests/Monitor.Web.Tests/B800WorkflowSafetyMatrixTests.cs",
            "tests/Monitor.Web.Tests/B800RazorPostWiringTests.cs",
            "tests/Monitor.Web.Tests/B800BoundedGetNavigationTests.cs",
            "tests/Monitor.Web.Tests/B800PrgFeedbackContractTests.cs",
            "tests/Monitor.Web.Tests/B800IncidentAdvisorRoleTests.cs",
            "tests/Monitor.Web.Tests/B800ConnectionLabWorkflowTests.cs",
            "tests/Monitor.Web.Tests/B800SettingsBackupRestoreTests.cs",
            "tests/Monitor.Web.Tests/B800GovernanceRetentionWorkflowTests.cs",
            "tests/Monitor.Web.Tests/B800EnterpriseOperationsRoleTests.cs"
        };

        foreach (var relative in regressionFiles)
            Assert.True(File.Exists(Path.Combine(Root, relative)), $"Missing workflow regression evidence: {relative}");

        for (var task = 21; task <= 29; task++)
        {
            var relative = $"docs/work/B800-{task:000}.md";
            Assert.True(File.Exists(Path.Combine(Root, relative)), $"Missing workflow work ledger: {relative}");
        }
    }

    [Fact]
    public void WorkflowCloseout_PreservesReadOnlyBrowserSqlBoundary()
    {
        foreach (var relative in new[]
        {
            "src/Monitor.Web/Views/Operations/IncidentDetails.cshtml",
            "src/Monitor.Web/Views/ConnectionLab/Index.cshtml",
            "src/Monitor.Web/Views/Operations/Settings.cshtml",
            "src/Monitor.Web/Views/Governance/Index.cshtml",
            "src/Monitor.Web/Views/EnterpriseOperations/Overview.cshtml"
        })
        {
            var source = File.ReadAllText(Path.Combine(Root, relative));
            Assert.DoesNotContain("SqlConnection", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SELECT ", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
