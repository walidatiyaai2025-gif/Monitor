using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300BackupComplianceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 0, 0, 0, TimeSpan.Zero);
    private static BackupComplianceInput GoodFull() => new("FULL", Now.AddHours(-12), Now.AddMinutes(-5), Now, TimeSpan.FromHours(24), TimeSpan.FromMinutes(15));

    [Fact] public void B300_021_ClassifyRecoveryModel_RecognizesBulkLogged() => Assert.Equal(RecoveryModelClass.BulkLogged, Batch300BackupCompliance.ClassifyRecoveryModel("BULK_LOGGED"));

    [Fact] public void B300_022_Age_ClampsFutureBackupToZero() => Assert.Equal(TimeSpan.Zero, Batch300BackupCompliance.Age(Now, Now.AddMinutes(5)));

    [Fact] public void B300_023_IsFullOverdue_DetectsMissingFull()
    {
        var input = GoodFull() with { LastFullUtc = null };
        Assert.True(Batch300BackupCompliance.IsFullOverdue(input));
    }

    [Fact] public void B300_024_RequiresLogBackup_OnlyForAppropriateModels()
    {
        Assert.True(Batch300BackupCompliance.RequiresLogBackup(GoodFull()));
        Assert.False(Batch300BackupCompliance.RequiresLogBackup(GoodFull() with { RecoveryModel = "SIMPLE" }));
    }

    [Fact] public void B300_025_IsLogOverdue_DetectsStaleLog()
    {
        var input = GoodFull() with { LastLogUtc = Now.AddHours(-1) };
        Assert.True(Batch300BackupCompliance.IsLogOverdue(input));
    }

    [Fact] public void B300_026_Score_ReturnsPerfectForCompliantBackup() => Assert.Equal(100, Batch300BackupCompliance.Score(GoodFull()));

    [Fact] public void B300_027_ClassifyRisk_MapsThresholds()
    {
        Assert.Equal(BackupRisk.Compliant, Batch300BackupCompliance.ClassifyRisk(95));
        Assert.Equal(BackupRisk.Critical, Batch300BackupCompliance.ClassifyRisk(20));
    }

    [Fact] public void B300_028_Reasons_AreSafePolicyMessages()
    {
        var reasons = Batch300BackupCompliance.Reasons(GoodFull() with { LastFullUtc = null });
        Assert.Contains(reasons, reason => reason.Contains("Full backup", StringComparison.Ordinal));
    }

    [Fact] public void B300_029_ComplianceLabel_UsesRiskName() => Assert.Equal("Compliant", Batch300BackupCompliance.ComplianceLabel(GoodFull()));

    [Fact] public void B300_030_Evaluate_CombinesBackupSignals()
    {
        var result = Batch300BackupCompliance.Evaluate(GoodFull() with { LastFullUtc = null, LastLogUtc = null });
        Assert.Equal(BackupRisk.Critical, result.Risk);
        Assert.True(result.FullOverdue);
        Assert.True(result.LogOverdue);
    }
}
