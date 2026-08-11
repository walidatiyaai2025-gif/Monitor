namespace Monitor.Web.Services;

public enum FleetRiskLevel
{
    Healthy,
    Low,
    Medium,
    High,
    Critical
}

public sealed record FleetRiskSignal(string Key, int Severity, DateTimeOffset ObservedAtUtc, bool Suppressed = false, bool InMaintenance = false);
public sealed record FleetRiskSummary(int Score, FleetRiskLevel Level, int ActionableCount, int SuppressedCount, string[] TopKeys);

public static class Batch300FleetRisk
{
    public static int NormalizeSeverity(int severity) => Math.Clamp(severity, 0, 100);

    public static int WeightedSeverity(FleetRiskSignal signal, DateTimeOffset nowUtc)
    {
        var severity = NormalizeSeverity(signal.Severity);
        var age = nowUtc - signal.ObservedAtUtc;
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;
        if (age > TimeSpan.FromDays(7)) severity = (int)Math.Round(severity * 0.5, MidpointRounding.AwayFromZero);
        else if (age > TimeSpan.FromDays(1)) severity = (int)Math.Round(severity * 0.75, MidpointRounding.AwayFromZero);
        if (signal.Suppressed) severity = (int)Math.Round(severity * 0.25, MidpointRounding.AwayFromZero);
        if (signal.InMaintenance) severity = (int)Math.Round(severity * 0.5, MidpointRounding.AwayFromZero);
        return Math.Clamp(severity, 0, 100);
    }

    public static int AggregateScore(IEnumerable<FleetRiskSignal> signals, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(signals);
        var values = signals.Select(signal => WeightedSeverity(signal, nowUtc)).OrderByDescending(value => value).Take(10).ToArray();
        if (values.Length == 0) return 0;
        var weighted = values.Select((value, index) => value * Math.Pow(0.85, index)).Sum();
        var normalizer = Enumerable.Range(0, values.Length).Sum(index => Math.Pow(0.85, index));
        return Math.Clamp((int)Math.Round(weighted / normalizer, MidpointRounding.AwayFromZero), 0, 100);
    }

    public static FleetRiskLevel Classify(int score) => Math.Clamp(score, 0, 100) switch
    {
        >= 85 => FleetRiskLevel.Critical,
        >= 65 => FleetRiskLevel.High,
        >= 40 => FleetRiskLevel.Medium,
        >= 15 => FleetRiskLevel.Low,
        _ => FleetRiskLevel.Healthy
    };

    public static FleetRiskSignal[] DeterministicTop(IEnumerable<FleetRiskSignal> signals, DateTimeOffset nowUtc, int limit = 5)
    {
        ArgumentNullException.ThrowIfNull(signals);
        return signals
            .OrderByDescending(signal => WeightedSeverity(signal, nowUtc))
            .ThenBy(signal => signal.Key, StringComparer.Ordinal)
            .ThenByDescending(signal => signal.ObservedAtUtc)
            .Take(Math.Clamp(limit, 1, 20))
            .ToArray();
    }

    public static int ActionableCount(IEnumerable<FleetRiskSignal> signals) => signals.Count(signal => !signal.Suppressed && !signal.InMaintenance && NormalizeSeverity(signal.Severity) > 0);

    public static int SuppressedCount(IEnumerable<FleetRiskSignal> signals) => signals.Count(signal => signal.Suppressed);

    public static IReadOnlyDictionary<FleetRiskLevel, int> Distribution(IEnumerable<int> scores)
    {
        ArgumentNullException.ThrowIfNull(scores);
        return scores.Select(Classify).GroupBy(level => level).ToDictionary(group => group.Key, group => group.Count());
    }

    public static bool RequiresAttention(int score) => Classify(score) is FleetRiskLevel.High or FleetRiskLevel.Critical;

    public static string SafeKey(string? key)
    {
        var normalized = (key ?? string.Empty).Trim();
        if (normalized.Length == 0) return "unknown";
        var safe = new string(normalized.Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.').ToArray());
        return safe.Length == 0 ? "unknown" : safe[..Math.Min(safe.Length, 64)];
    }

    public static FleetRiskSummary Summarize(IEnumerable<FleetRiskSignal> signals, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(signals);
        var values = signals.Select(signal => signal with { Key = SafeKey(signal.Key) }).ToArray();
        var score = AggregateScore(values, nowUtc);
        return new(score, Classify(score), ActionableCount(values), SuppressedCount(values), DeterministicTop(values, nowUtc).Select(signal => signal.Key).ToArray());
    }
}
