using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300FleetRiskTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 0, 0, 0, TimeSpan.Zero);

    [Fact] public void B300_051_NormalizeSeverity_BoundsInput() => Assert.Equal(100, Batch300FleetRisk.NormalizeSeverity(150));

    [Fact] public void B300_052_WeightedSeverity_AgesOldSignals()
    {
        var recent = Batch300FleetRisk.WeightedSeverity(new("x", 80, Now), Now);
        var old = Batch300FleetRisk.WeightedSeverity(new("x", 80, Now.AddDays(-8)), Now);
        Assert.True(old < recent);
    }

    [Fact] public void B300_053_AggregateScore_UsesTopSignals()
    {
        var score = Batch300FleetRisk.AggregateScore([new("a", 90, Now), new("b", 50, Now)], Now);
        Assert.InRange(score, 50, 90);
    }

    [Fact] public void B300_054_Classify_MapsHighThreshold() => Assert.Equal(FleetRiskLevel.High, Batch300FleetRisk.Classify(70));

    [Fact] public void B300_055_DeterministicTop_UsesKeyTieBreak()
    {
        var top = Batch300FleetRisk.DeterministicTop([new("b", 80, Now), new("a", 80, Now)], Now, 2);
        Assert.Equal("a", top[0].Key);
        Assert.Equal("b", top[1].Key);
    }

    [Fact] public void B300_056_ActionableCount_ExcludesSuppressedAndMaintenance()
    {
        var count = Batch300FleetRisk.ActionableCount([new("a", 50, Now), new("b", 50, Now, true), new("c", 50, Now, false, true)]);
        Assert.Equal(1, count);
    }

    [Fact] public void B300_057_SuppressedCount_CountsSuppressed() => Assert.Equal(2, Batch300FleetRisk.SuppressedCount([new("a", 1, Now, true), new("b", 1, Now, true)]));

    [Fact] public void B300_058_Distribution_GroupsLevels()
    {
        var distribution = Batch300FleetRisk.Distribution([0, 20, 70, 90]);
        Assert.Equal(1, distribution[FleetRiskLevel.Healthy]);
        Assert.Equal(1, distribution[FleetRiskLevel.Critical]);
    }

    [Fact] public void B300_059_SafeKey_RemovesUnsafeCharacters() => Assert.Equal("rule-prod_1", Batch300FleetRisk.SafeKey(" rule-prod_1! "));

    [Fact] public void B300_060_Summarize_ProducesTopKeysAndCounts()
    {
        var summary = Batch300FleetRisk.Summarize([new("b", 90, Now), new("a", 80, Now, true)], Now);
        Assert.Equal(1, summary.ActionableCount);
        Assert.Equal(1, summary.SuppressedCount);
        Assert.NotEmpty(summary.TopKeys);
    }
}
