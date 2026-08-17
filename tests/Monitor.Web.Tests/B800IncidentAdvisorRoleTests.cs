using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800IncidentAdvisorRoleTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void IncidentMutationAndAdvisorEndpoints_UseNamedPoliciesAndAntiforgery()
    {
        AssertProtectedPost(typeof(OperationsController), nameof(OperationsController.AcknowledgeIncident), MonitorPolicies.Operate);
        AssertProtectedPost(typeof(OperationsController), nameof(OperationsController.ResolveIncident), MonitorPolicies.Operate);
        AssertProtectedPost(typeof(OperationsController), nameof(OperationsController.ReopenIncident), MonitorPolicies.Operate);
        AssertProtectedPost(typeof(OperationsController), nameof(OperationsController.RequestAdvisor), MonitorPolicies.Advisor);
        AssertProtectedPost(typeof(IncidentCollaborationController), nameof(IncidentCollaborationController.ResolveWithNote), MonitorPolicies.Operate);
        AssertProtectedPost(typeof(IncidentCollaborationController), nameof(IncidentCollaborationController.ReopenWithReason), MonitorPolicies.Operate);
    }

    [Fact]
    public void IncidentDetails_HidesMutationAndAdvisorControlsFromViewer()
    {
        var view = Read("src/Monitor.Web/Views/Operations/IncidentDetails.cshtml");
        Assert.Contains("var canOperate = User.IsInRole(MonitorRoles.Operator) || User.IsInRole(MonitorRoles.Administrator);", view, StringComparison.Ordinal);

        var gatedActions = Slice(view, "@if (canOperate)\n    {", "    else\n    {");
        Assert.Contains("asp-action=\"AcknowledgeIncident\"", gatedActions, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"RequestAdvisor\"", gatedActions, StringComparison.Ordinal);

        Assert.Contains("@if (canOperate && Model.Incident.Status != IncidentStatus.Resolved)", view, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"ResolveWithNote\"", view, StringComparison.Ordinal);
        Assert.Contains("@if (canOperate && Model.Incident.Status == IncidentStatus.Resolved)", view, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"ReopenWithReason\"", view, StringComparison.Ordinal);
        Assert.Contains("Viewer access is read-only", view, StringComparison.Ordinal);
    }

    [Fact]
    public void IncidentDetails_PreservesReadOnlyEvidenceForViewer()
    {
        var view = Read("src/Monitor.Web/Views/Operations/IncidentDetails.cshtml");

        Assert.Contains("@Model.Incident.Evidence", view, StringComparison.Ordinal);
        Assert.Contains("@Model.Advisor.Message", view, StringComparison.Ordinal);
        Assert.Contains("Deterministic recommendation", view, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT ", view, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertProtectedPost(Type controller, string methodName, string policy)
    {
        var method = controller.GetMethod(methodName)
            ?? throw new MissingMethodException(controller.Name, methodName);
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Contains(method.GetCustomAttributes<AuthorizeAttribute>(), attribute => attribute.Policy == policy);
    }

    private static string Slice(string value, string startToken, string endToken)
    {
        var start = value.IndexOf(startToken, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start token not found: {startToken}");
        var end = value.IndexOf(endToken, start, StringComparison.Ordinal);
        Assert.True(end > start, $"End token not found after {startToken}: {endToken}");
        return value[start..end];
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
