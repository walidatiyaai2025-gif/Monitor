using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch400TransactionLogHealthTests
{
    [Fact] public void B400_041_LogUsedPercentIsBounded() => Assert.Equal(75, Batch400TransactionLogHealth.UsedPercent(75, 100));
    [Fact] public void B400_042_LogVlfBandDetectsExtremeCounts() => Assert.Equal(LogVlfBand.Extreme, Batch400TransactionLogHealth.VlfBand(1200));
    [Fact] public void B400_043_LogReuseWaitIsNormalized() => Assert.Equal("ACTIVE_TRANSACTION", Batch400TransactionLogHealth.NormalizeReuseWait("active transaction"));
    [Fact] public void B400_044_ActiveTransactionBandUsesAge() => Assert.Equal(LogActivityBand.Extreme, Batch400TransactionLogHealth.ActiveTransactionBand(TimeSpan.FromHours(3)));
    [Fact] public void B400_045_LogBackupOverdueHonorsRecoveryRequirement() => Assert.True(Batch400TransactionLogHealth.LogBackupOverdue(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(15), true));
    [Fact] public void B400_046_LogGrowthBandDetectsRapidGrowth() => Assert.Equal(LogGrowthBand.Rapid, Batch400TransactionLogHealth.GrowthBand(200));
    [Fact] public void B400_047_LogRiskScoreCombinesSignals() => Assert.True(Batch400TransactionLogHealth.Score(95, 1200, "ACTIVE_TRANSACTION", TimeSpan.FromHours(3), true, 200) > 75);
    [Fact] public void B400_048_LogSeverityUsesExplicitThresholds() => Assert.Equal(B400Severity.Warning, Batch400TransactionLogHealth.Severity(60));
    [Fact] public void B400_049_LogTruncationBlockedExcludesNothingCheckpoint() { Assert.True(Batch400TransactionLogHealth.TruncationBlocked("ACTIVE_TRANSACTION")); Assert.False(Batch400TransactionLogHealth.TruncationBlocked("NOTHING")); }
    [Fact] public void B400_050_LogSummaryReturnsBoundedReason() { var result = Batch400TransactionLogHealth.Summarize(95, 100, 1200, "ACTIVE_TRANSACTION", TimeSpan.FromHours(3), TimeSpan.FromHours(1), TimeSpan.FromMinutes(15), true, 200); Assert.NotEmpty(result.Reason); Assert.True(result.TruncationBlocked); }
}
