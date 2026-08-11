using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300DatabaseStateTests
{
    [Fact] public void B300_031_NormalizeState_CollapsesAndUppercases() => Assert.Equal("RECOVERY PENDING", Batch300DatabaseState.NormalizeState(" recovery   pending "));

    [Fact] public void B300_032_Classify_RecognizesRecoveryPending() => Assert.Equal(DatabaseStateClass.RecoveryPending, Batch300DatabaseState.Classify("RECOVERY_PENDING"));

    [Fact] public void B300_033_IsOnline_IsStrict() => Assert.True(Batch300DatabaseState.IsOnline("ONLINE"));

    [Fact] public void B300_034_IsActionable_FlagsSuspect() => Assert.True(Batch300DatabaseState.IsActionable(DatabaseStateClass.Suspect));

    [Fact] public void B300_035_AvailabilityScore_IsPercentage() => Assert.Equal(50, Batch300DatabaseState.AvailabilityScore(["ONLINE", "OFFLINE"]));

    [Fact] public void B300_036_CountUnavailable_CountsNonOnline() => Assert.Equal(2, Batch300DatabaseState.CountUnavailable(["ONLINE", "RESTORING", "SUSPECT"]));

    [Fact] public void B300_037_CountRestoring_IsExact() => Assert.Equal(2, Batch300DatabaseState.CountRestoring(["RESTORING", "ONLINE", "RESTORING"]));

    [Fact] public void B300_038_Worst_PrioritizesSuspect() => Assert.Equal(DatabaseStateClass.Suspect, Batch300DatabaseState.Worst(["ONLINE", "OFFLINE", "SUSPECT"]));

    [Fact] public void B300_039_FailoverReady_RequiresAllOnline()
    {
        Assert.True(Batch300DatabaseState.FailoverReady(["ONLINE", "ONLINE"]));
        Assert.False(Batch300DatabaseState.FailoverReady(["ONLINE", "RESTORING"]));
    }

    [Fact] public void B300_040_Summarize_ProducesDeterministicCounts()
    {
        var summary = Batch300DatabaseState.Summarize(["ONLINE", "RESTORING", "OFFLINE", "unknown-state"]);
        Assert.Equal(4, summary.Total);
        Assert.Equal(1, summary.Online);
        Assert.Equal(1, summary.Unknown);
    }
}
