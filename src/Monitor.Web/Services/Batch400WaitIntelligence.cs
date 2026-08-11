using System.Security.Cryptography;
using System.Text;

namespace Monitor.Web.Services;

public enum B400Severity { None, Info, Warning, Critical }
public enum WaitCategory { Cpu, Io, Lock, Latch, Network, Parallelism, Memory, TransactionLog, Other, Ignored }
public sealed record WaitSample(string WaitType, long WaitTimeMs, long SignalWaitTimeMs, long WaitingTasks, TimeSpan Interval);
public sealed record WaitIntelligence(string WaitType, WaitCategory Category, double WaitMsPerSecond, double SignalPercent, double SharePercent, double Score, B400Severity Severity, string Fingerprint);

public static class Batch400WaitIntelligence
{
    private static readonly string[] BenignPrefixes = ["SLEEP_", "BROKER_", "XE_", "SQLTRACE_", "LAZYWRITER_", "REQUEST_FOR_DEADLOCK_SEARCH", "LOGMGR_QUEUE"];

    public static string NormalizeWaitType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "UNKNOWN";
        var normalized = new string(value.Trim().ToUpperInvariant().Select(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray());
        return normalized[..Math.Min(normalized.Length, 64)];
    }

    public static WaitCategory Classify(string? waitType)
    {
        var value = NormalizeWaitType(waitType);
        if (IsBenign(value)) return WaitCategory.Ignored;
        if (value.StartsWith("LCK_", StringComparison.Ordinal)) return WaitCategory.Lock;
        if (value.Contains("PAGEIOLATCH", StringComparison.Ordinal) || value.Contains("IO_COMPLETION", StringComparison.Ordinal)) return WaitCategory.Io;
        if (value.Contains("LATCH", StringComparison.Ordinal)) return WaitCategory.Latch;
        if (value is "ASYNC_NETWORK_IO" or "NET_WAITFOR_PACKET") return WaitCategory.Network;
        if (value.StartsWith("CX", StringComparison.Ordinal) || value.Contains("EXCHANGE", StringComparison.Ordinal)) return WaitCategory.Parallelism;
        if (value.Contains("RESOURCE_SEMAPHORE", StringComparison.Ordinal)) return WaitCategory.Memory;
        if (value is "WRITELOG" or "LOGBUFFER") return WaitCategory.TransactionLog;
        if (value is "SOS_SCHEDULER_YIELD" or "THREADPOOL") return WaitCategory.Cpu;
        return WaitCategory.Other;
    }

    public static bool IsBenign(string? waitType)
    {
        var value = NormalizeWaitType(waitType);
        return BenignPrefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));
    }

    public static double RatePerSecond(WaitSample sample)
    {
        var seconds = sample.Interval.TotalSeconds;
        if (seconds <= 0 || sample.WaitTimeMs <= 0) return 0;
        return Math.Round(Math.Clamp(sample.WaitTimeMs / seconds, 0, 1_000_000), 2);
    }

    public static double SignalPercent(WaitSample sample)
    {
        if (sample.WaitTimeMs <= 0 || sample.SignalWaitTimeMs <= 0) return 0;
        return Math.Round(Math.Clamp(sample.SignalWaitTimeMs * 100d / sample.WaitTimeMs, 0, 100), 2);
    }

    public static double SharePercent(WaitSample sample, IEnumerable<WaitSample> all)
    {
        var total = all.Where(item => !IsBenign(item.WaitType)).Sum(item => Math.Max(0L, item.WaitTimeMs));
        return total <= 0 ? 0 : Math.Round(Math.Clamp(Math.Max(0L, sample.WaitTimeMs) * 100d / total, 0, 100), 2);
    }

    public static double Score(WaitSample sample, IEnumerable<WaitSample> all)
    {
        var share = SharePercent(sample, all);
        var rateComponent = Math.Min(100, RatePerSecond(sample) / 10d);
        var signalComponent = SignalPercent(sample);
        return Math.Round(Math.Clamp(share * 0.55 + rateComponent * 0.30 + signalComponent * 0.15, 0, 100), 2);
    }

    public static B400Severity Severity(double score) => score switch
    {
        >= 80 => B400Severity.Critical,
        >= 50 => B400Severity.Warning,
        > 0 => B400Severity.Info,
        _ => B400Severity.None
    };

    public static string Fingerprint(string? waitType)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeWaitType(waitType)));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }

    public static IReadOnlyList<WaitIntelligence> Summarize(IEnumerable<WaitSample> samples, int limit = 20)
    {
        var materialized = samples.Where(item => !IsBenign(item.WaitType)).ToArray();
        var bounded = Math.Clamp(limit, 1, 100);
        return materialized.Select(item =>
        {
            var score = Score(item, materialized);
            return new WaitIntelligence(NormalizeWaitType(item.WaitType), Classify(item.WaitType), RatePerSecond(item), SignalPercent(item), SharePercent(item, materialized), score, Severity(score), Fingerprint(item.WaitType));
        }).OrderByDescending(item => item.Score).ThenBy(item => item.WaitType, StringComparer.Ordinal).Take(bounded).ToArray();
    }
}
