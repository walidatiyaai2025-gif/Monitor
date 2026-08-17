using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800EnterpriseOperationsRoleTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void EnterpriseMutationEndpoints_UseExpectedNamedPoliciesAndAntiforgery()
    {
        var readPolicy = typeof(EnterpriseOperationsController).GetCustomAttributes<AuthorizeAttribute>().Single();
        Assert.Equal(MonitorPolicies.Read, readPolicy.Policy);

        AssertProtectedPost(nameof(EnterpriseOperationsController.UpdateServerProfile), MonitorPolicies.Manage);
        AssertProtectedPost(nameof(EnterpriseOperationsController.AssignIncident), MonitorPolicies.Operate);
        AssertProtectedPost(nameof(EnterpriseOperationsController.AddIncidentNote), MonitorPolicies.Operate);
        AssertProtectedPost(nameof(EnterpriseOperationsController.AcknowledgeRecommendation), MonitorPolicies.Operate);
    }

    [Fact]
    public void EnterpriseView_GatesMetadataMutationToAdministrator_AndIncidentMutationToOperatorPlus()
    {
        var view = Read("src/Monitor.Web/Views/EnterpriseOperations/Overview.cshtml");

        Assert.Contains("var canOperate = User.IsInRole(MonitorRoles.Operator) || User.IsInRole(MonitorRoles.Administrator);", view, StringComparison.Ordinal);
        Assert.Contains("var canManage = User.IsInRole(MonitorRoles.Administrator);", view, StringComparison.Ordinal);

        var metadataSection = Slice(view, "<section class=\"panel\" aria-labelledby=\"server-ops-heading\">", "<section class=\"panel\" aria-labelledby=\"incident-ops-heading\">");
        Assert.Contains("@if (canManage)", metadataSection, StringComparison.Ordinal);
        Assert.Contains("action=\"/servers/@row.Registration.Id/operator-profile\"", metadataSection, StringComparison.Ordinal);
        Assert.Contains("@Html.AntiForgeryToken()", metadataSection, StringComparison.Ordinal);

        var incidentSection = view[view.IndexOf("<section class=\"panel\" aria-labelledby=\"incident-ops-heading\">", StringComparison.Ordinal)..];
        Assert.Contains("@if (canOperate)", incidentSection, StringComparison.Ordinal);
        Assert.Contains("/owner", incidentSection, StringComparison.Ordinal);
        Assert.Contains("/notes", incidentSection, StringComparison.Ordinal);
        Assert.Contains("/recommendation/", incidentSection, StringComparison.Ordinal);
        Assert.Contains("@Html.AntiForgeryToken()", incidentSection, StringComparison.Ordinal);
    }

    [Fact]
    public void EnterpriseFiltersAndFeedback_RemainReadOnlyAndSafe()
    {
        var view = Read("src/Monitor.Web/Views/EnterpriseOperations/Overview.cshtml");
        var controller = Read("src/Monitor.Web/Controllers/EnterpriseOperationsController.cs");

        Assert.Contains("method=\"get\" action=\"/enterprise\"", view, StringComparison.Ordinal);
        Assert.Contains("TempData[\"OperatorStatus\"]", view, StringComparison.Ordinal);
        Assert.Contains("TempData[\"OperatorError\"]", view, StringComparison.Ordinal);
        Assert.Contains("Filters and navigation read Monitor-owned metadata and cached evidence only", view, StringComparison.Ordinal);
        Assert.Contains("_audit.Append", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", view, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertProtectedPost(string methodName, string policy)
    {
        var method = typeof(EnterpriseOperationsController).GetMethod(methodName)
            ?? throw new MissingMethodException(nameof(EnterpriseOperationsController), methodName);
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
