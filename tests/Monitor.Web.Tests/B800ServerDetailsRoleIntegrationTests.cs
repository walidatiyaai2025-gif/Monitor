using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800ServerDetailsRoleIntegrationTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void ServerDetails_Get_InheritsReadPolicy_AndRemainsCacheOnly()
    {
        var controllerPolicy = typeof(OperationsController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .Single();
        Assert.Equal(MonitorPolicies.Read, controllerPolicy.Policy);

        var action = typeof(OperationsController).GetMethod(nameof(OperationsController.ServerDetails))
            ?? throw new MissingMethodException(nameof(OperationsController), nameof(OperationsController.ServerDetails));
        Assert.Equal("/servers/{id}", action.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Empty(action.GetCustomAttributes<AuthorizeAttribute>());

        var controller = Read("src/Monitor.Web/Controllers/OperationsController.cs");
        var serverDetails = Slice(
            controller,
            "public async Task<IActionResult> ServerDetails",
            "[HttpPost(\"/servers/{id:guid}/refresh\")]");

        Assert.Contains("_readService.GetServerAsync", serverDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("_snapshotRefresh.RefreshAsync", serverDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", serverDetails, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", serverDetails, StringComparison.Ordinal);
    }

    [Fact]
    public void RefreshServer_IsOperateOnly_Post_WithAntiforgery()
    {
        var action = typeof(OperationsController).GetMethod(nameof(OperationsController.RefreshServer))
            ?? throw new MissingMethodException(nameof(OperationsController), nameof(OperationsController.RefreshServer));

        Assert.Equal("/servers/{id:guid}/refresh", action.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        var policy = action.GetCustomAttributes<AuthorizeAttribute>().Single();
        Assert.Equal(MonitorPolicies.Operate, policy.Policy);
    }

    [Fact]
    public void ServerDetails_View_GatesMutatingControls_ToOperatorOrAdministrator()
    {
        var view = Read("src/Monitor.Web/Views/Operations/ServerDetails.cshtml");
        const string operateGate = "User.IsInRole(MonitorRoles.Administrator) || User.IsInRole(MonitorRoles.Operator)";

        Assert.Equal(2, Count(view, operateGate));
        Assert.Equal(2, Count(view, "<form asp-action=\"RefreshServer\""));
        Assert.Contains("User.IsInRole(MonitorRoles.Administrator)", view, StringComparison.Ordinal);
        Assert.Contains("Open Connection Lab", view, StringComparison.Ordinal);
        Assert.DoesNotContain("User.IsInRole(MonitorRoles.Reader)", view, StringComparison.Ordinal);
    }

    [Fact]
    public void ServerDetails_View_StatesTheReadAndMutationBoundary()
    {
        var view = Read("src/Monitor.Web/Views/Operations/ServerDetails.cshtml");

        Assert.Contains("Opening this page never initiates monitored-SQL collection", view, StringComparison.Ordinal);
        Assert.Contains("Normal GET navigation reads cache only", view, StringComparison.Ordinal);
        Assert.Contains("Manual refresh is an explicit protected POST", view, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT ", view, StringComparison.OrdinalIgnoreCase);
    }

    private static int Count(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static string Slice(string value, string startToken, string endToken)
    {
        var start = value.IndexOf(startToken, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start token not found: {startToken}");
        var end = value.IndexOf(endToken, start, StringComparison.Ordinal);
        Assert.True(end > start, $"End token not found after start token: {endToken}");
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
