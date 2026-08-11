using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch400HaReadinessTests
{
    [Fact] public void B400_071_HaStateNormalizationRecognizesSynchronized() => Assert.Equal(ReplicaSyncState.Synchronized, Batch400HaReadiness.NormalizeState("synchronized"));
    [Fact] public void B400_072_HaLagBandDetectsCriticalLag() => Assert.Equal(HaLagBand.Critical, Batch400HaReadiness.LagBand(300));
    [Fact] public void B400_073_HaQueueScoreUsesWorstQueue() => Assert.Equal(10, Batch400HaReadiness.QueueScore(100, 50));
    [Fact] public void B400_074_HaSyncScoreFailsClosedOnDisconnect() => Assert.Equal(100, Batch400HaReadiness.SyncScore("SYNCHRONIZED", false));
    [Fact] public void B400_075_HaFailoverReadinessRequiresSyncAndQuorum() => Assert.True(Batch400HaReadiness.FailoverReady(new("SYNCHRONIZED", 10, 10, 1, true, true), true));
    [Fact] public void B400_076_HaRpoComplianceUsesConfiguredLag() => Assert.True(Batch400HaReadiness.RpoCompliant(10, 30));
    [Fact] public void B400_077_HaRtoReadyAcceptsSynchronizingReplica() => Assert.True(Batch400HaReadiness.RtoReady(new("SYNCHRONIZING", 10, 10, 5, true, false), true));
    [Fact] public void B400_078_HaQuorumRiskUsesMajority() { Assert.True(Batch400HaReadiness.QuorumRisk(1, 3)); Assert.False(Batch400HaReadiness.QuorumRisk(2, 3)); }
    [Fact] public void B400_079_HaSeverityUsesRiskBands() => Assert.Equal(B400Severity.Critical, Batch400HaReadiness.Severity(90));
    [Fact] public void B400_080_HaSummaryExplainsDegradation() { var result = Batch400HaReadiness.Summarize(new("NOT_SYNCHRONIZING", 1000, 1000, 300, true, false), false, 1, 3, 30); Assert.Equal(B400Severity.Critical, result.Severity); Assert.NotEmpty(result.Reason); }
}
