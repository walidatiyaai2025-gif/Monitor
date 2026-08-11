using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch400MaintenanceSafetyTests
{
    [Fact] public void B400_081_MaintenanceOperationNormalizationIsStrict() => Assert.Equal(MaintenanceOperation.IndexRebuild, Batch400MaintenanceSafety.NormalizeOperation("index rebuild"));
    [Fact] public void B400_082_MaintenanceBaseRiskEscalatesProduction() => Assert.Equal(MaintenanceRisk.Moderate, Batch400MaintenanceSafety.BaseRisk(MaintenanceOperation.Backup, true));
    [Fact] public void B400_083_MaintenanceApprovalRequiredForProduction() => Assert.True(Batch400MaintenanceSafety.ApprovalRequired(M(backup: true, approval: false)));
    [Fact] public void B400_084_MaintenanceRollbackRequiredForProduction() => Assert.True(Batch400MaintenanceSafety.RollbackRequired(M(backup: true, rollback: false)));
    [Fact] public void B400_085_MaintenanceWindowNotRequiredForBackup() => Assert.False(Batch400MaintenanceSafety.WindowRequired(M(backup: true)));
    [Fact] public void B400_086_MaintenanceBlockersAreDeterministic() { var blockers = Batch400MaintenanceSafety.Blockers(M(approval: false, rollback: false, critical: 1)); Assert.Contains("active-critical-incidents", blockers); Assert.Contains("approval-required", blockers); }
    [Fact] public void B400_087_MaintenanceAllowedRequiresNoBlockers() => Assert.True(Batch400MaintenanceSafety.Allowed(M(backup: true, approval: true, rollback: true, critical: 0)));
    [Fact] public void B400_088_MaintenanceScoreIsBounded() => Assert.InRange(Batch400MaintenanceSafety.Score(M(approval: false, rollback: false, critical: 2)), 0, 100);
    [Fact] public void B400_089_MaintenanceFingerprintIsStable() { var context = M(); Assert.Equal(Batch400MaintenanceSafety.Fingerprint(context), Batch400MaintenanceSafety.Fingerprint(context)); }
    [Fact] public void B400_090_MaintenanceDecisionCarriesSafeReason() { var decision = Batch400MaintenanceSafety.Decide(M(approval: false)); Assert.False(decision.Allowed); Assert.NotEmpty(decision.Reason); }
    private static MaintenanceContext M(bool backup = false, bool approval = true, bool rollback = true, int critical = 0) => new(backup ? MaintenanceOperation.Backup : MaintenanceOperation.Configuration, true, true, approval, rollback, critical, true, true);
}
