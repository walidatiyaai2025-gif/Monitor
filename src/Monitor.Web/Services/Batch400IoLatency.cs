using System.Security.Cryptography;
using System.Text;

namespace Monitor.Web.Services;

public enum IoLatencyBand { Excellent, Healthy, Elevated, High, Severe }
public sealed record IoFileSample(string FileKey, double ReadLatencyMs, double WriteLatencyMs, double ReadMbPerSecond, double WriteMbPerSecond, long Reads, long Writes);
public sealed record IoFileIntelligence(string FileKey, double WeightedLatencyMs, double ThroughputMbPerSecond, double WriteSharePercent, IoLatencyBand LatencyBand, double Score, B400Severity Severity, bool Hotspot, string Fingerprint);

public static class Batch400IoLatency
{
    public static string NormalizeFileKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "UNKNOWN";
        var normalized = value.Trim().Replace('\\', '/');
        return normalized[..Math.Min(normalized.Length, 128)];
    }

    public static double ClampLatency(double value) => !double.IsFinite(value) ? 0 : Math.Round(Math.Clamp(value, 0, 60_000), 2);

    public static double Throughput(IoFileSample sample)
    {
        var read = double.IsFinite(sample.ReadMbPerSecond) ? Math.Max(0, sample.ReadMbPerSecond) : 0;
        var write = double.IsFinite(sample.WriteMbPerSecond) ? Math.Max(0, sample.WriteMbPerSecond) : 0;
        return Math.Round(Math.Clamp(read + write, 0, 10_000_000), 2);
    }

    public static double WeightedLatency(IoFileSample sample)
    {
        var reads = Math.Max(0L, sample.Reads);
        var writes = Math.Max(0L, sample.Writes);
        var total = reads + writes;
        if (total <= 0) return Math.Round((ClampLatency(sample.ReadLatencyMs) + ClampLatency(sample.WriteLatencyMs)) / 2d, 2);
        return Math.Round((ClampLatency(sample.ReadLatencyMs) * reads + ClampLatency(sample.WriteLatencyMs) * writes) / total, 2);
    }

    public static double WriteSharePercent(IoFileSample sample)
    {
        var reads = Math.Max(0L, sample.Reads);
        var writes = Math.Max(0L, sample.Writes);
        var total = reads + writes;
        return total <= 0 ? 0 : Math.Round(writes * 100d / total, 2);
    }

    public static IoLatencyBand LatencyBand(double latencyMs) => ClampLatency(latencyMs) switch
    {
        < 2 => IoLatencyBand.Excellent,
        < 10 => IoLatencyBand.Healthy,
        < 20 => IoLatencyBand.Elevated,
        < 50 => IoLatencyBand.High,
        _ => IoLatencyBand.Severe
    };

    public static double Score(IoFileSample sample)
    {
        var latency = WeightedLatency(sample);
        var latencyScore = Math.Min(100, latency * 2);
        var throughputPenalty = Throughput(sample) <= 0 && Math.Max(0L, sample.Reads) + Math.Max(0L, sample.Writes) > 0 ? 20 : 0;
        return Math.Round(Math.Clamp(latencyScore + throughputPenalty, 0, 100), 2);
    }

    public static B400Severity Severity(double score) => score switch
    {
        >= 80 => B400Severity.Critical,
        >= 45 => B400Severity.Warning,
        > 0 => B400Severity.Info,
        _ => B400Severity.None
    };

    public static string Fingerprint(string? fileKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeFileKey(fileKey)));
        return Convert.ToHexString(bytes.AsSpan(0, 8));
    }

    public static IReadOnlyList<IoFileIntelligence> TopFiles(IEnumerable<IoFileSample> files, int limit = 20)
    {
        var bounded = Math.Clamp(limit, 1, 100);
        return files.Select(sample =>
        {
            var score = Score(sample);
            var severity = Severity(score);
            return new IoFileIntelligence(NormalizeFileKey(sample.FileKey), WeightedLatency(sample), Throughput(sample), WriteSharePercent(sample), LatencyBand(WeightedLatency(sample)), score, severity, severity is B400Severity.Warning or B400Severity.Critical, Fingerprint(sample.FileKey));
        }).OrderByDescending(item => item.Score).ThenBy(item => item.FileKey, StringComparer.Ordinal).Take(bounded).ToArray();
    }
}
