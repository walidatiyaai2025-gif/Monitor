using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300RuntimePressureTests
{
    [Fact] public void B300_041_NormalizePercent_BoundsInput() => Assert.Equal(100, Batch300RuntimePressure.NormalizePercent(140));

    [Fact] public void B300_042_MemoryPoints_ScoresCriticalMemory() => Assert.Equal(40, Batch300RuntimePressure.MemoryPoints(97));

    [Fact] public void B300_043_BlockingPoints_CombinesCountAndWait() => Assert.Equal(23, Batch300RuntimePressure.BlockingPoints(10, 20_000));

    [Fact] public void B300_044_SchedulerPoints_BoundsRunnablePressure() => Assert.Equal(20, Batch300RuntimePressure.SchedulerPoints(100));

    [Fact] public void B300_045_IoPoints_ScoresPendingIo() => Assert.Equal(10, Batch300RuntimePressure.IoPoints(20));

    [Fact] public void B300_046_Score_IsBoundedToHundred()
    {
        var score = Batch300RuntimePressure.Score(new(100, 100, 120_000, 100, 100));
        Assert.Equal(100, score);
    }

    [Fact] public void B300_047_Classify_MapsCriticalThreshold() => Assert.Equal(RuntimePressureClass.Critical, Batch300RuntimePressure.Classify(80));

    [Fact] public void B300_048_IsHotspot_UsesHighThreshold() => Assert.True(Batch300RuntimePressure.IsHotspot(new(95, 10, 20_000, 16, 8)));

    [Fact] public void B300_049_Signals_ReturnsOnlyActiveDomains()
    {
        var signals = Batch300RuntimePressure.Signals(new(90, 0, 0, 0, 0));
        Assert.Single(signals);
        Assert.Equal("memory", signals[0]);
    }

    [Fact] public void B300_050_Evaluate_CombinesScoreClassAndSignals()
    {
        var result = Batch300RuntimePressure.Evaluate(new(95, 10, 20_000, 16, 8));
        Assert.True(result.Score >= 55);
        Assert.NotEmpty(result.Signals);
        Assert.True(result.Classification is RuntimePressureClass.High or RuntimePressureClass.Critical);
    }
}
