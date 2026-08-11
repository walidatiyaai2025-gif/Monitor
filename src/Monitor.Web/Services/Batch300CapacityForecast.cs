namespace Monitor.Web.Services;

public enum CapacityTrend
{
    Shrinking,
    Flat,
    Growing
}

public enum CapacityGrowthBand
{
    None,
    Low,
    Moderate,
    High
}

public sealed record CapacitySample(DateTimeOffset AtUtc, double UsedGb, double CapacityGb);
public sealed record CapacityProjection(double DailyGrowthGb, double HeadroomPercent, double? DaysToThreshold, DateTimeOffset? ThresholdAtUtc, CapacityGrowthBand GrowthBand);

public static class Batch300CapacityForecast
{
    public static CapacitySample NormalizeSample(CapacitySample sample)
    {
        var capacity = double.IsFinite(sample.CapacityGb) && sample.CapacityGb > 0 ? sample.CapacityGb : 0;
        var used = double.IsFinite(sample.UsedGb) ? Math.Clamp(sample.UsedGb, 0, capacity) : 0;
        return sample with { UsedGb = used, CapacityGb = capacity };
    }

    public static double DailyGrowthGb(CapacitySample older, CapacitySample newer)
    {
        older = NormalizeSample(older);
        newer = NormalizeSample(newer);
        var days = (newer.AtUtc - older.AtUtc).TotalDays;
        if (days <= 0) return 0;
        var result = (newer.UsedGb - older.UsedGb) / days;
        return double.IsFinite(result) ? result : 0;
    }

    public static CapacityTrend Trend(double dailyGrowthGb, double flatTolerance = 0.01)
    {
        if (!double.IsFinite(dailyGrowthGb)) return CapacityTrend.Flat;
        if (dailyGrowthGb > flatTolerance) return CapacityTrend.Growing;
        if (dailyGrowthGb < -flatTolerance) return CapacityTrend.Shrinking;
        return CapacityTrend.Flat;
    }

    public static double HeadroomPercent(double usedGb, double capacityGb)
    {
        if (!double.IsFinite(usedGb) || !double.IsFinite(capacityGb) || capacityGb <= 0) return 0;
        return Math.Round(Math.Clamp((capacityGb - Math.Clamp(usedGb, 0, capacityGb)) / capacityGb * 100, 0, 100), 2);
    }

    public static double? DaysToThreshold(double usedGb, double capacityGb, double dailyGrowthGb, double thresholdPercent = 90)
    {
        if (capacityGb <= 0 || dailyGrowthGb <= 0 || thresholdPercent is <= 0 or > 100) return null;
        var threshold = capacityGb * thresholdPercent / 100d;
        if (usedGb >= threshold) return 0;
        var days = (threshold - Math.Max(0, usedGb)) / dailyGrowthGb;
        return double.IsFinite(days) && days >= 0 ? days : null;
    }

    public static DateTimeOffset? ThresholdDate(DateTimeOffset fromUtc, double? daysToThreshold)
    {
        if (daysToThreshold is null || !double.IsFinite(daysToThreshold.Value) || daysToThreshold.Value < 0) return null;
        return fromUtc.AddDays(Math.Min(daysToThreshold.Value, 36500));
    }

    public static CapacityGrowthBand GrowthBand(double dailyGrowthGb, double capacityGb)
    {
        if (dailyGrowthGb <= 0 || capacityGb <= 0) return CapacityGrowthBand.None;
        var percentPerDay = dailyGrowthGb / capacityGb * 100;
        if (percentPerDay < 0.1) return CapacityGrowthBand.Low;
        if (percentPerDay < 0.5) return CapacityGrowthBand.Moderate;
        return CapacityGrowthBand.High;
    }

    public static int ClampForecastHorizonDays(int requested) => Math.Clamp(requested, 1, 365);

    public static double RequiredCapacityGb(double currentUsedGb, double dailyGrowthGb, int horizonDays, double targetUtilizationPercent = 80)
    {
        if (targetUtilizationPercent is <= 0 or > 100) throw new ArgumentOutOfRangeException(nameof(targetUtilizationPercent));
        var horizon = ClampForecastHorizonDays(horizonDays);
        var projectedUsed = Math.Max(0, currentUsedGb) + Math.Max(0, dailyGrowthGb) * horizon;
        return Math.Round(projectedUsed / (targetUtilizationPercent / 100d), 2);
    }

    public static CapacityProjection Project(CapacitySample older, CapacitySample newer, double thresholdPercent = 90)
    {
        newer = NormalizeSample(newer);
        var growth = DailyGrowthGb(older, newer);
        var days = DaysToThreshold(newer.UsedGb, newer.CapacityGb, growth, thresholdPercent);
        return new(growth, HeadroomPercent(newer.UsedGb, newer.CapacityGb), days, ThresholdDate(newer.AtUtc, days), GrowthBand(growth, newer.CapacityGb));
    }
}
