using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800PrgFeedbackContractTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void SnapshotRefresh_UsesPrgFeedback_WhilePreservingNotFound()
    {
        var controller = Read("src/Monitor.Web/Controllers/OperationsController.cs");
        var action = Slice(controller, "public async Task<IActionResult> RefreshServer", "[HttpGet(\"/database-health\")]");

        Assert.Contains("TempData[\"SnapshotRefresh\"]", action, StringComparison.Ordinal);
        Assert.Contains("TempData[\"SnapshotRefreshStatus\"]", action, StringComparison.Ordinal);
        Assert.Contains("RedirectToAction(nameof(ServerDetails)", action, StringComparison.Ordinal);
        Assert.Contains("SnapshotRefreshStatus.RegistrationNotFound", action, StringComparison.Ordinal);
        Assert.Contains("NotFound()", action, StringComparison.Ordinal);
    }

    [Fact]
    public void IncidentTransitions_RedirectOnlyAfterAppliedChange_AndKeepConflictExplicit()
    {
        var operations = Read("src/Monitor.Web/Controllers/OperationsController.cs");
        var transition = Slice(operations, "private IActionResult Transition", "private static string BuildTransitionAuditOutcome");

        Assert.Contains("_audit?.Append", transition, StringComparison.Ordinal);
        Assert.Contains("RedirectToAction(nameof(IncidentDetails)", transition, StringComparison.Ordinal);
        Assert.Contains("Conflict(new { message", transition, StringComparison.Ordinal);

        var collaboration = Read("src/Monitor.Web/Controllers/IncidentCollaborationController.cs");
        Assert.Contains("TempData[\"OperatorStatus\"]", collaboration, StringComparison.Ordinal);
        Assert.Contains("TempData[\"OperatorError\"]", collaboration, StringComparison.Ordinal);
        Assert.Contains("RedirectToAction(\"IncidentDetails\", \"Operations\"", collaboration, StringComparison.Ordinal);
        Assert.Contains("Conflict(new { message", collaboration, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsBackupRestore_UsesFeedbackRedirects_AndRetainsDestructiveConfirmationFailure()
    {
        var controller = Read("src/Monitor.Web/Controllers/OperationalBackupController.cs");

        Assert.Contains("TempData[\"BackupStatus\"]", controller, StringComparison.Ordinal);
        Assert.Contains("RedirectToAction(\"Settings\", \"Operations\")", controller, StringComparison.Ordinal);
        Assert.Contains("string.Equals(confirmation?.Trim(), \"RESTORE\"", controller, StringComparison.Ordinal);
        Assert.Contains("confirmation-rejected", controller, StringComparison.Ordinal);
        Assert.Contains("backup.restore", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void GovernanceAndEnterpriseMutations_UseAuditedFeedbackRedirects()
    {
        var governance = Read("src/Monitor.Web/Controllers/GovernanceController.cs");
        Assert.Contains("TempData[\"GovernanceStatus\"]", governance, StringComparison.Ordinal);
        Assert.Contains("RedirectToAction(nameof(Index))", governance, StringComparison.Ordinal);

        var enterprise = Read("src/Monitor.Web/Controllers/EnterpriseOperationsController.cs");
        Assert.Contains("TempData[\"OperatorStatus\"]", enterprise, StringComparison.Ordinal);
        Assert.Contains("TempData[\"OperatorError\"]", enterprise, StringComparison.Ordinal);
        Assert.Contains("RedirectToAction(nameof(Overview))", enterprise, StringComparison.Ordinal);
        Assert.Contains("_audit.Append", enterprise, StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectionLab_UsesPrgAfterMutations_ButKeepsValidationErrorsOnTheForm()
    {
        var controller = Read("src/Monitor.Web/Controllers/ConnectionLabController.cs");

        Assert.Contains("TempData[\"ConnectionLabMessage\"]", controller, StringComparison.Ordinal);
        Assert.Contains("RedirectToAction(nameof(Index))", controller, StringComparison.Ordinal);
        Assert.Contains("RedirectToAction(\"Servers\", \"Operations\")", controller, StringComparison.Ordinal);
        Assert.Contains("return View(\"Index\", BuildPage(input))", controller, StringComparison.Ordinal);
        Assert.Contains("ModelState.AddModelError", controller, StringComparison.Ordinal);
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
