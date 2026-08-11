using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300CapacityForecastTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact] public void B300_011_NormalizeSample_ClampsUsedToCapacity()
    {
        var sample = Batch300CapacityForecast.NormalizeSample(new(Start, 150, 100));
        Assert.Equal(100, sample.UsedGb);
    }

    [Fact] public void B300_012_DailyGrowthGb_ComputesRate()
    {
        var growth = Batch300CapacityForecast.DailyGrowthGb(new(Start, 50, 100), new(Start.AddDays(2), 60, 100));
        Assert.Equal(5, growth);
    }

    [Fact] public void B300_013_Trend_UsesTolerance()
    {
        Assert.Equal(CapacityTrend.Flat, Batch300CapacityForecast.Trend(0.005));
        Assert.Equal(CapacityTrend.Growing, Batch300CapacityForecast.Trend(0.2));
    }

    [Fact] public void B300_014_HeadroomPercent_IsBounded() => Assert.Equal(20, Batch300CapacityForecast.HeadroomPercent(80, 100));

    [Fact] public void B300_015_DaysToThreshold_ReturnsZeroWhenAlreadyOver() => Assert.Equal(0, Batch300CapacityForecast.DaysToThreshold(95, 100, 1, 90));

    [Fact] public void B300_016_ThresholdDate_IsBoundedToFiniteHorizon()
    {
        var date = Batch300CapacityForecast.ThresholdDate(Start, 10);
        Assert.Equal(Start.AddDays(10), date);
    }

    [Fact] public void B300_017_GrowthBand_UsesPercentOfCapacity() => Assert.Equal(CapacityGrowthBand.High, Batch300CapacityForecast.GrowthBand(1, 100));

    [Fact] public void B300_018_ClampForecastHorizonDays_BoundsInput()
    {
        Assert.Equal(1, Batch300CapacityForecast.ClampForecastHorizonDays(0));
        Assert.Equal(365, Batch300CapacityForecast.ClampForecastHorizonDays(999));
    }

    [Fact] public void B300_019_RequiredCapacityGb_PreservesTargetHeadroom() => Assert.Equal(137.5, Batch300CapacityForecast.RequiredCapacityGb(100, 1, 10, 80));

    [Fact] public void B300_020_Project_ReturnsDeterministicProjection()
    {
        var projection = Batch300CapacityForecast.Project(new(Start, 70, 100), new(Start.AddDays(10), 80, 100));
        Assert.Equal(1, projection.DailyGrowthGb);
        Assert.Equal(10, projection.DaysToThreshold);
        Assert.Equal(20, projection.HeadroomPercent);
    }
}
