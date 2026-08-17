using System.Text;
using Microsoft.AspNetCore.Authorization;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800MaintenanceDecisionSupportExportTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void SharedEvidenceBuilder_PreservesUnavailablePolicyAndTruncatedIncidentTruth()
    {
        var policy = new ServerOperatorPolicyState(
            Guid.NewGuid(),
            PolicyReadable: false,
            MaintenanceActive: true,
            AlertSuppressed: false,
            ServerEnvironmentClass.Unspecified,
            Group: "SECRET-GROUP",
            Tags: ["SECRET-TAG"]);
        var incidentRead = new BoundedIncidentReadResult([], IsComplete: false, Limit: BoundedIncidentReadModel.DefaultLimit);

        var evidence = MaintenanceDecisionSupport.BuildEvidence(MaintenanceOperation.IndexRebuild, policy, incidentRead);

        Assert.Null(evidence.IsProduction);
        Assert.Null(evidence.ObservedMaintenanceWindowActive);
        Assert.Null(evidence.ActiveCriticalIncidents);
        Assert.Null(evidence.InApprovedWindow);
        Assert.Null(evidence.HasApproval);
        Assert.Null(evidence.HasRollbackPlan);
        Assert.Null(evidence.ReplicaHealthy);
        Assert.Null(evidence.RecentBackupAvailable);
    }

    [Fact]
    public void Export_NotEvaluatedEvidenceIsExplicitAndExcludesTargetPolicyIdentity()
    {
        var registrationId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var policy = new ServerOperatorPolicyState(
            registrationId,
            PolicyReadable: true,
            MaintenanceActive: true,
            AlertSuppressed: false,
            ServerEnvironmentClass.Production,
            Group: "=SECRET-GROUP",
            Tags: ["@SECRET-TAG"]);
        var incidentRead = new BoundedIncidentReadResult([], IsComplete: false, Limit: BoundedIncidentReadModel.DefaultLimit);
        var evidence = MaintenanceDecisionSupport.BuildEvidence(MaintenanceOperation.Patch, policy, incidentRead);
        var result = MaintenanceDecisionSupport.Evaluate(evidence);

        var csv = Text(MaintenanceDecisionSupportExport.Build(policy, incidentRead, result));

        Assert.Contains("#schema,monitor-export-v2", csv, StringComparison.Ordinal);
        Assert.Contains("\"Evidence\",\"IncidentEvidenceComplete\",\"false\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Evidence\",\"ActiveCriticalIncidents\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Decision\",\"Status\",\"NotEvaluated\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Decision\",\"State\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("active-critical-incidents", csv, StringComparison.Ordinal);
        Assert.DoesNotContain(registrationId.ToString("D"), csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SECRET-GROUP", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET-TAG", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Evidence\",\"ActiveCriticalIncidents\",\"0\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_CompleteNonProductionDecisionUsesExistingB400OutcomeOnly()
    {
        var policy = new ServerOperatorPolicyState(
            Guid.NewGuid(),
            PolicyReadable: true,
            MaintenanceActive: false,
            AlertSuppressed: false,
            ServerEnvironmentClass.Development,
            Group: null,
            Tags: []);
        var incidentRead = new BoundedIncidentReadResult([], IsComplete: true, Limit: BoundedIncidentReadModel.DefaultLimit);
        var evidence = MaintenanceDecisionSupport.BuildEvidence(MaintenanceOperation.IndexRebuild, policy, incidentRead);
        var result = MaintenanceDecisionSupport.Evaluate(evidence);

        var csv = Text(MaintenanceDecisionSupportExport.Build(policy, incidentRead, result));

        Assert.Equal(MaintenanceDecisionSupportStatus.Ready, result.Status);
        Assert.NotNull(result.Decision);
        Assert.Contains("\"Evidence\",\"ActiveCriticalIncidents\",\"0\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Decision\",\"Operation\",\"IndexRebuild\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Decision\",\"Status\",\"Ready\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Decision\",\"Risk\",\"Moderate\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Decision\",\"Allowed\",\"true\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Decision\",\"Score\",\"45\"", csv, StringComparison.Ordinal);
        Assert.Contains("Maintenance preconditions are satisfied.", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Route_IsViewerSafeBoundedAndDiscoverableFromMaintenanceSurface()
    {
        var controller = typeof(EnterpriseReportsController);
        var classPolicy = controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy;
        var action = controller.GetMethod(nameof(EnterpriseReportsController.MaintenanceDecisionSupport))!;
        var actionPolicies = action.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().ToArray();

        Assert.Equal(MonitorPolicies.Read, classPolicy);
        Assert.Empty(actionPolicies);
        Assert.Equal(
            "monitor-maintenancedecisionsupport-20260817-203000.csv",
            EnterpriseSecurityPolicy.SafeDownloadFileName(
                EnterpriseDownloadSubject.MaintenanceDecisionSupport,
                new DateTimeOffset(2026, 8, 17, 20, 30, 0, TimeSpan.Zero),
                "csv"));

        var controllerSource = Read("src/Monitor.Web/Controllers/EnterpriseReportsController.cs");
        var reportingSource = Read("src/Monitor.Web/Services/EnterpriseReportingServices.cs");
        var maintenanceSource = Read("src/Monitor.Web/Controllers/MaintenanceDecisionSupportController.cs");
        var view = Read("src/Monitor.Web/Views/MaintenanceDecisionSupport/Index.cshtml");

        Assert.Contains("/reports/maintenance-decision-support/{registrationId:guid}.csv", controllerSource, StringComparison.Ordinal);
        Assert.Contains("ValidateEnterpriseTextBudget(operation)", controllerSource, StringComparison.Ordinal);
        Assert.Contains("EnterpriseDownloadSubject.MaintenanceDecisionSupport", controllerSource, StringComparison.Ordinal);
        Assert.Contains("MaintenanceDecisionSupport.BuildEvidence", maintenanceSource, StringComparison.Ordinal);
        Assert.Contains("MaintenanceDecisionSupport.BuildEvidence", reportingSource, StringComparison.Ordinal);
        Assert.Contains("Export current decision CSV", view, StringComparison.Ordinal);
        Assert.Contains("excludes target identity, incident IDs, assignees", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", reportingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", reportingSource, StringComparison.Ordinal);
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
