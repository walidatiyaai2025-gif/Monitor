using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800MaintenanceDecisionSupportSurfaceTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Controller_IsReadOnlyAndUsesReadPolicy()
    {
        var type = typeof(MaintenanceDecisionSupportController);
        var authorize = Assert.Single(type.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("Monitor.Read", authorize.Policy);

        var action = Assert.Single(type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));
        Assert.Equal(nameof(MaintenanceDecisionSupportController.Index), action.Name);
        Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
        Assert.Null(action.GetCustomAttribute<HttpPostAttribute>());
    }

    [Fact]
    public void Controller_UsesExplicitPolicyAvailabilityAndBoundedIncidentEvidenceWithoutSnapshotOrSqlCollection()
    {
        var source = Read("src/Monitor.Web/Controllers/MaintenanceDecisionSupportController.cs");

        Assert.Contains("IOperatorPolicyReadService operatorPolicy", source, StringComparison.Ordinal);
        Assert.Contains("operatorPolicy.GetServer(id)", source, StringComparison.Ordinal);
        Assert.Contains("policy.PolicyReadable", source, StringComparison.Ordinal);
        Assert.Contains("IsProduction: policy.PolicyReadable", source, StringComparison.Ordinal);
        Assert.Contains("ObservedMaintenanceWindowActive: policy.PolicyReadable", source, StringComparison.Ordinal);
        Assert.Contains("BoundedIncidentReadModel.ActiveForServer(incidents, id)", source, StringComparison.Ordinal);
        Assert.Contains("incidentRead.IsComplete", source, StringComparison.Ordinal);
        Assert.Contains("FindingSeverity.Critical", source, StringComparison.Ordinal);
        Assert.Contains("InApprovedWindow: null", source, StringComparison.Ordinal);
        Assert.Contains("HasApproval: null", source, StringComparison.Ordinal);
        Assert.Contains("HasRollbackPlan: null", source, StringComparison.Ordinal);
        Assert.Contains("ReplicaHealthy: null", source, StringComparison.Ordinal);
        Assert.Contains("RecentBackupAvailable: null", source, StringComparison.Ordinal);
        Assert.DoesNotContain("operatorMetadata.GetServer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IOperatorMetadataStore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("incidents.GetAll()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SnapshotQuery", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void View_ProvidesGetOnlyEvaluationAndExplicitUnavailablePolicyState()
    {
        var view = Read("src/Monitor.Web/Views/MaintenanceDecisionSupport/Index.cshtml");
        var enterprise = Read("src/Monitor.Web/Views/EnterpriseOperations/Overview.cshtml");

        Assert.Contains("DECISION SUPPORT ONLY", view, StringComparison.Ordinal);
        Assert.Contains("method=\"get\"", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No action executed", view, StringComparison.Ordinal);
        Assert.Contains("not treated as approved-window evidence", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OPERATOR POLICY EVIDENCE UNAVAILABLE", view, StringComparison.Ordinal);
        Assert.Contains("POLICY UNAVAILABLE", view, StringComparison.Ordinal);
        Assert.Contains("rather than treating unavailable metadata as non-production or an inactive window", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INCIDENT EVIDENCE INCOMPLETE", view, StringComparison.Ordinal);
        Assert.Contains("Not evaluated", view, StringComparison.Ordinal);
        Assert.Contains("MissingInputs", view, StringComparison.Ordinal);
        Assert.DoesNotContain("method=\"post\"", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("type=\"hidden\"", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asp-action=\"Execute", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Maintenance decision support", enterprise, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"MaintenanceDecisionSupport\"", enterprise, StringComparison.Ordinal);
    }

    [Fact]
    public void Wrapper_DelegatesOnlyAfterRequiredEvidenceIsPresent()
    {
        var source = Read("src/Monitor.Web/Services/MaintenanceDecisionSupport.cs");

        var missingCheck = source.IndexOf("if (missing.Count > 0)", StringComparison.Ordinal);
        var decisionCall = source.IndexOf("Batch400MaintenanceSafety.Decide(context)", StringComparison.Ordinal);
        Assert.True(missingCheck >= 0);
        Assert.True(decisionCall > missingCheck);
        Assert.Contains("!evidence.IsProduction.HasValue", source, StringComparison.Ordinal);
        Assert.Contains("environment-class", source, StringComparison.Ordinal);
        Assert.Contains("evidence.IsProduction!.Value", source, StringComparison.Ordinal);
        Assert.Contains("MaintenanceDecisionSupportStatus.NotEvaluated", source, StringComparison.Ordinal);
        Assert.Contains("Batch400MaintenanceSafety.ApprovalRequired", source, StringComparison.Ordinal);
        Assert.Contains("Batch400MaintenanceSafety.RollbackRequired", source, StringComparison.Ordinal);
        Assert.Contains("Batch400MaintenanceSafety.WindowRequired", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OperatorPolicyState_RetainsMetadataOnlyOnSuccessfulRead()
    {
        var source = Read("src/Monitor.Web/Services/OperatorPolicyServices.cs");

        Assert.Contains("ServerOperatorMetadata? Metadata = null", source, StringComparison.Ordinal);
        Assert.Contains("item.Tags.ToArray(), item", source, StringComparison.Ordinal);
        Assert.Contains("return Unavailable(registrationId)", source, StringComparison.Ordinal);
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
