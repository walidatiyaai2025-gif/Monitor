using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800GovernanceRetentionWorkflowTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Governance_IsManageOnly_WithReadOnlyDryRunAndProtectedApply()
    {
        var policy = typeof(GovernanceController).GetCustomAttributes<AuthorizeAttribute>().Single();
        Assert.Equal(MonitorPolicies.Manage, policy.Policy);

        var index = typeof(GovernanceController).GetMethod(nameof(GovernanceController.Index))
            ?? throw new MissingMethodException(nameof(GovernanceController), nameof(GovernanceController.Index));
        Assert.NotNull(index.GetCustomAttribute<HttpGetAttribute>());

        var apply = typeof(GovernanceController).GetMethod(nameof(GovernanceController.Apply))
            ?? throw new MissingMethodException(nameof(GovernanceController), nameof(GovernanceController.Apply));
        Assert.NotNull(apply.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(apply.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void Apply_FailsClosedWithoutExactPruneConfirmation_AndAuditsRejection()
    {
        var controller = Read("src/Monitor.Web/Controllers/GovernanceController.cs");

        Assert.Contains("string.Equals(confirmation?.Trim(), \"PRUNE\", StringComparison.Ordinal)", controller, StringComparison.Ordinal);
        Assert.Contains("\"governance.cleanup\", \"operator-metadata\", \"confirmation-rejected\"", controller, StringComparison.Ordinal);
        Assert.Contains("Type PRUNE exactly to confirm", controller, StringComparison.Ordinal);
        Assert.Contains("var count = _governance.Apply(actor);", controller, StringComparison.Ordinal);
        Assert.Contains("TempData[\"GovernanceStatus\"]", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void GovernanceView_SeparatesDryRunFromTypedDestructiveApply()
    {
        var view = Read("src/Monitor.Web/Views/Governance/Index.cshtml");

        Assert.Contains("DRY RUN · NO MUTATION", view, StringComparison.Ordinal);
        Assert.Contains("@if (Model.Candidates.Count == 0)", view, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Apply\" method=\"post\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmation\" maxlength=\"5\" placeholder=\"type PRUNE\" required", view, StringComparison.Ordinal);
        Assert.Contains("typed PRUNE confirmation", view, StringComparison.Ordinal);
        Assert.Contains("rejected confirmation is also auditable", view, StringComparison.Ordinal);
        Assert.Contains("does not touch a monitored SQL target", view, StringComparison.Ordinal);
    }

    [Fact]
    public void RetentionService_AppliesOnlyCurrentBoundedDryRunCandidatesAndWritesReceipts()
    {
        var service = Read("src/Monitor.Web/Services/GovernanceRetentionService.cs");

        Assert.Contains(".Take(1000)", service, StringComparison.Ordinal);
        Assert.Contains("var plan = DryRun();", service, StringComparison.Ordinal);
        Assert.Contains("governance.prune.{candidate.Kind}", service, StringComparison.Ordinal);
        Assert.Contains("governance.cleanup", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", service, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
