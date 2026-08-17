using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800MaintenanceDecisionSupportTests
{
    [Theory]
    [InlineData("IndexRebuild", MaintenanceOperation.IndexRebuild)]
    [InlineData("index rebuild", MaintenanceOperation.IndexRebuild)]
    [InlineData("statistics_update", MaintenanceOperation.StatisticsUpdate)]
    public void NormalizeOperation_AcceptsSupportedForms(string input, MaintenanceOperation expected) =>
        Assert.Equal(expected, MaintenanceDecisionSupport.NormalizeOperation(input));

    [Fact]
    public void ProductionConfiguration_IsNotEvaluatedWhenGovernedInputsAreMissing()
    {
        var result = MaintenanceDecisionSupport.Evaluate(new MaintenanceDecisionEvidence(
            MaintenanceOperation.Configuration,
            IsProduction: true,
            ObservedMaintenanceWindowActive: true,
            InApprovedWindow: null,
            HasApproval: null,
            HasRollbackPlan: null,
            ActiveCriticalIncidents: 0,
            ReplicaHealthy: null,
            RecentBackupAvailable: null));

        Assert.Equal(MaintenanceDecisionSupportStatus.NotEvaluated, result.Status);
        Assert.Null(result.Decision);
        Assert.Contains("approval", result.MissingInputs);
        Assert.Contains("approved-window", result.MissingInputs);
        Assert.Contains("rollback-plan", result.MissingInputs);
    }

    [Fact]
    public void ObservedWindow_DoesNotBecomeApprovedWindowEvidence()
    {
        var result = MaintenanceDecisionSupport.Evaluate(new MaintenanceDecisionEvidence(
            MaintenanceOperation.IndexRebuild,
            IsProduction: true,
            ObservedMaintenanceWindowActive: true,
            InApprovedWindow: null,
            HasApproval: true,
            HasRollbackPlan: true,
            ActiveCriticalIncidents: 0,
            ReplicaHealthy: null,
            RecentBackupAvailable: null));

        Assert.Equal(MaintenanceDecisionSupportStatus.NotEvaluated, result.Status);
        Assert.Contains("approved-window", result.MissingInputs);
        Assert.Null(result.Decision);
    }

    [Fact]
    public void NonProductionStatisticsUpdate_CanBeEvaluatedFromRelevantEvidence()
    {
        var result = MaintenanceDecisionSupport.Evaluate(new MaintenanceDecisionEvidence(
            MaintenanceOperation.StatisticsUpdate,
            IsProduction: false,
            ObservedMaintenanceWindowActive: false,
            InApprovedWindow: null,
            HasApproval: null,
            HasRollbackPlan: null,
            ActiveCriticalIncidents: 0,
            ReplicaHealthy: null,
            RecentBackupAvailable: null));

        Assert.Equal(MaintenanceDecisionSupportStatus.Ready, result.Status);
        Assert.NotNull(result.Decision);
        Assert.Empty(result.MissingInputs);
    }

    [Fact]
    public void ObservedCriticalIncident_BlocksOtherwiseEvaluableLowRiskOperation()
    {
        var result = MaintenanceDecisionSupport.Evaluate(new MaintenanceDecisionEvidence(
            MaintenanceOperation.StatisticsUpdate,
            IsProduction: false,
            ObservedMaintenanceWindowActive: false,
            InApprovedWindow: null,
            HasApproval: null,
            HasRollbackPlan: null,
            ActiveCriticalIncidents: 1,
            ReplicaHealthy: null,
            RecentBackupAvailable: null));

        Assert.Equal(MaintenanceDecisionSupportStatus.Blocked, result.Status);
        Assert.NotNull(result.Decision);
        Assert.Contains("active-critical-incidents", result.Decision!.Blockers);
    }
}
