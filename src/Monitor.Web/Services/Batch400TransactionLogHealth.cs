namespace Monitor.Web.Services;

public enum LogVlfBand { Healthy, Elevated, High, Extreme }
public enum LogActivityBand { None, Short, Long, Extreme }
public enum LogGrowthBand { Shrinking, Flat, Growing, Rapid }
public sealed record TransactionLogHealth(double UsedPercent, LogVlfBand VlfBand, string ReuseWait, LogActivityBand ActiveTransactionBand, bool LogBackupOverdue, LogGrowthBand GrowthBand, double Score, B400Severity Severity, bool TruncationBlocked, string Reason);

public static class Batch400TransactionLogHealth
{
    public static double UsedPercent(double usedMb, double totalMb)
    {
        if (!double.IsFinite(usedMb) || !double.IsFinite(totalMb) || totalMb <= 0) return 0;
        return Math.Round(Math.Clamp(Math.Max(0, usedMb) * 100d / totalMb, 0, 100), 2);
    }

    public static LogVlfBand VlfBand(int vlfCount) => Math.Max(0, vlfCount) switch
    {
        >= 1000 => LogVlfBand.Extreme,
        >= 500 => LogVlfBand.High,
        >= 200 => LogVlfBand.Elevated,
        _ => LogVlfBand.Healthy
    };

    public static string NormalizeReuseWait(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "UNKNOWN";
        var normalized = new string(value.Trim().ToUpperInvariant().Select(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray());
        return normalized[..Math.Min(48, normalized.Length)];
    }

    public static LogActivityBand ActiveTransactionBand(TimeSpan age) => age.TotalMinutes switch
    {
        >= 120 => LogActivityBand.Extreme,
        >= 30 => LogActivityBand.Long,
        > 0 => LogActivityBand.Short,
        _ => LogActivityBand.None
    };

    public static bool LogBackupOverdue(TimeSpan? age, TimeSpan threshold, bool logBackupsRequired)
    {
        if (!logBackupsRequired) return false;
        if (threshold <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(threshold));
        return age is null || age.Value > threshold;
    }

    public static LogGrowthBand GrowthBand(double growthMbPerHour)
    {
        if (!double.IsFinite(growthMbPerHour)) return LogGrowthBand.Flat;
        if (growthMbPerHour < -0.1) return LogGrowthBand.Shrinking;
        if (growthMbPerHour <= 1) return LogGrowthBand.Flat;
        if (growthMbPerHour <= 100) return LogGrowthBand.Growing;
        return LogGrowthBand.Rapid;
    }

    public static double Score(double usedPercent, int vlfCount, string? reuseWait, TimeSpan activeTransactionAge, bool logBackupOverdue, double growthMbPerHour)
    {
        var used = Math.Clamp(usedPercent, 0, 100);
        var vlf = VlfBand(vlfCount) switch { LogVlfBand.Extreme => 100, LogVlfBand.High => 70, LogVlfBand.Elevated => 40, _ => 0 };
        var tx = ActiveTransactionBand(activeTransactionAge) switch { LogActivityBand.Extreme => 100, LogActivityBand.Long => 60, LogActivityBand.Short => 20, _ => 0 };
        var reuse = NormalizeReuseWait(reuseWait) is "NOTHING" or "CHECKPOINT" ? 0 : 60;
        var backup = logBackupOverdue ? 100 : 0;
        var growth = GrowthBand(growthMbPerHour) switch { LogGrowthBand.Rapid => 100, LogGrowthBand.Growing => 50, _ => 0 };
        return Math.Round(Math.Clamp(used * 0.3 + vlf * 0.15 + tx * 0.15 + reuse * 0.1 + backup * 0.2 + growth * 0.1, 0, 100), 2);
    }

    public static B400Severity Severity(double score) => score switch
    {
        >= 75 => B400Severity.Critical,
        >= 45 => B400Severity.Warning,
        > 0 => B400Severity.Info,
        _ => B400Severity.None
    };

    public static bool TruncationBlocked(string? reuseWait)
    {
        var normalized = NormalizeReuseWait(reuseWait);
        return normalized is not ("NOTHING" or "CHECKPOINT" or "UNKNOWN");
    }

    public static TransactionLogHealth Summarize(double usedMb, double totalMb, int vlfCount, string? reuseWait, TimeSpan activeTransactionAge, TimeSpan? logBackupAge, TimeSpan logBackupThreshold, bool logBackupsRequired, double growthMbPerHour)
    {
        var used = UsedPercent(usedMb, totalMb);
        var overdue = LogBackupOverdue(logBackupAge, logBackupThreshold, logBackupsRequired);
        var score = Score(used, vlfCount, reuseWait, activeTransactionAge, overdue, growthMbPerHour);
        var severity = Severity(score);
        var blocked = TruncationBlocked(reuseWait);
        var reason = severity == B400Severity.None ? "No material transaction-log risk detected." : blocked ? $"Log reuse is constrained by {NormalizeReuseWait(reuseWait)}." : overdue ? "Required log backup is overdue." : "Transaction-log pressure exceeds the configured risk threshold.";
        return new(used, VlfBand(vlfCount), NormalizeReuseWait(reuseWait), ActiveTransactionBand(activeTransactionAge), overdue, GrowthBand(growthMbPerHour), score, severity, blocked, reason);
    }
}
