using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch400WaitIntelligenceTests
{
    [Fact] public void B400_011_NormalizeWaitTypeIsBoundedAndSafe() => Assert.Equal("PAGEIOLATCH_SH", Batch400WaitIntelligence.NormalizeWaitType(" pageiolatch sh "));
    [Fact] public void B400_012_ClassifyWaitTypeDetectsLocks() => Assert.Equal(WaitCategory.Lock, Batch400WaitIntelligence.Classify("LCK_M_X"));
    [Fact] public void B400_013_BenignWaitsAreIgnored() => Assert.True(Batch400WaitIntelligence.IsBenign("SLEEP_TASK"));
    [Fact] public void B400_014_WaitRateUsesIntervalSeconds() => Assert.Equal(100, Batch400WaitIntelligence.RatePerSecond(new("WRITELOG", 1000, 0, 1, TimeSpan.FromSeconds(10))));
    [Fact] public void B400_015_SignalPercentIsBounded() => Assert.Equal(25, Batch400WaitIntelligence.SignalPercent(new("SOS_SCHEDULER_YIELD", 1000, 250, 1, TimeSpan.FromSeconds(10))));
    [Fact] public void B400_016_WaitShareUsesActionableTotal() { var a = new WaitSample("WRITELOG", 750, 0, 1, TimeSpan.FromSeconds(10)); var all = new[] { a, new WaitSample("LCK_M_X", 250, 0, 1, TimeSpan.FromSeconds(10)), new WaitSample("SLEEP_TASK", 10000, 0, 1, TimeSpan.FromSeconds(10)) }; Assert.Equal(75, Batch400WaitIntelligence.SharePercent(a, all)); }
    [Fact] public void B400_017_WaitScoreIsDeterministic() { var a = new WaitSample("WRITELOG", 1000, 200, 1, TimeSpan.FromSeconds(10)); Assert.InRange(Batch400WaitIntelligence.Score(a, new[] { a }), 1, 100); }
    [Fact] public void B400_018_WaitSeverityUsesExplicitThresholds() { Assert.Equal(B400Severity.Critical, Batch400WaitIntelligence.Severity(80)); Assert.Equal(B400Severity.Warning, Batch400WaitIntelligence.Severity(50)); }
    [Fact] public void B400_019_WaitFingerprintIsStableAndOpaque() { var value = Batch400WaitIntelligence.Fingerprint("WRITELOG"); Assert.Equal(16, value.Length); Assert.Equal(value, Batch400WaitIntelligence.Fingerprint("writelog")); }
    [Fact] public void B400_020_WaitSummaryExcludesBenignAndSorts() { var list = Batch400WaitIntelligence.Summarize([new("SLEEP_TASK", 99999, 0, 1, TimeSpan.FromSeconds(10)), new("WRITELOG", 1000, 0, 1, TimeSpan.FromSeconds(10)), new("LCK_M_X", 100, 0, 1, TimeSpan.FromSeconds(10))]); Assert.Equal(2, list.Count); Assert.Equal("WRITELOG", list[0].WaitType); }
}
