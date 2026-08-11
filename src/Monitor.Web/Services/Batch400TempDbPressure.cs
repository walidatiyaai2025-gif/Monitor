namespace Monitor.Web.Services;

public sealed record TempDbFileSample(int FileId, double SizeMb, double UsedMb, double GrowthMbPerHour, double ReadLatencyMs, double WriteLatencyMs);
public sealed record TempDbPressureSummary(double UsedPercent, double SizeImbalancePercent, double UsedImbalancePercent, double GrowthMbPerHour, double LatencyMs, double AllocationContentionScore, int RecommendedFileCount, double Score, B400Severity Severity, bool Hotspot);

public static class Batch400TempDbPressure
{
    public static TempDbFileSample Normalize(TempDbFileSample sample)
    {
        var size = double.IsFinite(sample.SizeMb) && sample.SizeMb > 0 ? sample.SizeMb : 0;
        var used = double.IsFinite(sample.UsedMb) ? Math.Clamp(sample.UsedMb, 0, size) : 0;
        var growth = double.IsFinite(sample.GrowthMbPerHour) ? Math.Clamp(sample.GrowthMbPerHour, -1_000_000, 1_000_000) : 0;
        var read = double.IsFinite(sample.ReadLatencyMs) ? Math.Clamp(sample.ReadLatencyMs, 0, 60_000) : 0;
        var write = double.IsFinite(sample.WriteLatencyMs) ? Math.Clamp(sample.WriteLatencyMs, 0, 60_000) : 0;
        return sample with { SizeMb = size, UsedMb = used, GrowthMbPerHour = growth, ReadLatencyMs = read, WriteLatencyMs = write };
    }

    public static double UsedPercent(IEnumerable<TempDbFileSample> files)
    {
        var normalized = files.Select(Normalize).ToArray();
        var size = normalized.Sum(item => item.SizeMb);
        return size <= 0 ? 0 : Math.Round(Math.Clamp(normalized.Sum(item => item.UsedMb) * 100d / size, 0, 100), 2);
    }

    public static double SizeImbalancePercent(IEnumerable<TempDbFileSample> files)
    {
        var values = files.Select(Normalize).Where(item => item.SizeMb > 0).Select(item => item.SizeMb).ToArray();
        if (values.Length < 2) return 0;
        var average = values.Average();
        return average <= 0 ? 0 : Math.Round(Math.Clamp((values.Max() - values.Min()) * 100d / average, 0, 1000), 2);
    }

    public static double UsedImbalancePercent(IEnumerable<TempDbFileSample> files)
    {
        var values = files.Select(Normalize).Where(item => item.SizeMb > 0).Select(item => item.UsedMb / item.SizeMb * 100d).ToArray();
        return values.Length < 2 ? 0 : Math.Round(Math.Clamp(values.Max() - values.Min(), 0, 100), 2);
    }

    public static double GrowthMbPerHour(IEnumerable<TempDbFileSample> files) => Math.Round(files.Select(Normalize).Sum(item => item.GrowthMbPerHour), 2);

    public static double AverageLatencyMs(IEnumerable<TempDbFileSample> files)
    {
        var normalized = files.Select(Normalize).ToArray();
        return normalized.Length == 0 ? 0 : Math.Round(normalized.Average(item => (item.ReadLatencyMs + item.WriteLatencyMs) / 2d), 2);
    }

    public static double AllocationContentionScore(long pfsWaitMs, long gamWaitMs, long sgamWaitMs, TimeSpan interval)
    {
        if (interval.TotalSeconds <= 0) return 0;
        var waits = Math.Max(0, pfsWaitMs) + Math.Max(0, gamWaitMs) + Math.Max(0, sgamWaitMs);
        return Math.Round(Math.Clamp(waits / interval.TotalSeconds / 10d, 0, 100), 2);
    }

    public static int RecommendedFileCount(int logicalCpuCount, int currentFileCount)
    {
        var cpu = Math.Clamp(logicalCpuCount, 1, 256);
        var target = Math.Min(cpu, 8);
        if (currentFileCount <= 0) return target;
        return Math.Clamp(Math.Max(currentFileCount, target), 1, 32);
    }

    public static B400Severity Severity(double score) => score switch
    {
        >= 75 => B400Severity.Critical,
        >= 45 => B400Severity.Warning,
        > 0 => B400Severity.Info,
        _ => B400Severity.None
    };

    public static TempDbPressureSummary Summarize(IEnumerable<TempDbFileSample> files, long pfsWaitMs, long gamWaitMs, long sgamWaitMs, TimeSpan interval, int logicalCpuCount)
    {
        var materialized = files.Select(Normalize).ToArray();
        var used = UsedPercent(materialized);
        var sizeImbalance = SizeImbalancePercent(materialized);
        var usedImbalance = UsedImbalancePercent(materialized);
        var growth = GrowthMbPerHour(materialized);
        var latency = AverageLatencyMs(materialized);
        var contention = AllocationContentionScore(pfsWaitMs, gamWaitMs, sgamWaitMs, interval);
        var score = Math.Round(Math.Clamp(used * 0.3 + Math.Min(100, sizeImbalance) * 0.15 + usedImbalance * 0.15 + Math.Min(100, Math.Max(0, growth) / 10d) * 0.1 + Math.Min(100, latency * 2) * 0.1 + contention * 0.2, 0, 100), 2);
        var severity = Severity(score);
        return new(used, sizeImbalance, usedImbalance, growth, latency, contention, RecommendedFileCount(logicalCpuCount, materialized.Length), score, severity, severity is B400Severity.Warning or B400Severity.Critical);
    }
}
