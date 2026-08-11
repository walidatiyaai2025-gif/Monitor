using System.Security.Cryptography;
using System.Text;

namespace Monitor.Web.Services;

public sealed record FleetSignal(string ServerKey, string Environment, string RuleId, B400Severity Severity, DateTimeOffset AtUtc);
public sealed record SignalCluster(string ClusterKey, DateTimeOffset BucketUtc, string DominantRule, int AffectedServers, IReadOnlyList<string> Environments, B400Severity Severity, double Score);

public static class Batch400FleetCorrelation
{
    public static string NormalizeServerKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "UNKNOWN";
        var normalized = value.Trim().ToUpperInvariant();
        return normalized[..Math.Min(normalized.Length, 96)];
    }

    public static string NormalizeEnvironment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "UNKNOWN";
        var normalized = value.Trim().ToUpperInvariant();
        return normalized[..Math.Min(normalized.Length, 32)];
    }

    public static TimeSpan ClampWindow(TimeSpan requested)
    {
        if (requested <= TimeSpan.Zero) return TimeSpan.FromMinutes(5);
        if (requested > TimeSpan.FromHours(24)) return TimeSpan.FromHours(24);
        return requested;
    }

    public static DateTimeOffset Bucket(DateTimeOffset atUtc, TimeSpan window)
    {
        var bounded = ClampWindow(window);
        var ticks = bounded.Ticks;
        return new DateTimeOffset(atUtc.UtcTicks - atUtc.UtcTicks % ticks, TimeSpan.Zero);
    }

    public static string CorrelationKey(FleetSignal signal, TimeSpan window)
    {
        var canonical = $"{NormalizeEnvironment(signal.Environment)}|{signal.RuleId.Trim().ToUpperInvariant()}|{Bucket(signal.AtUtc, window):O}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes.AsSpan(0, 8));
    }

    public static int SeverityWeight(B400Severity severity) => severity switch { B400Severity.Critical => 100, B400Severity.Warning => 60, B400Severity.Info => 20, _ => 0 };

    public static int BlastRadius(IEnumerable<FleetSignal> signals) => signals.Select(item => NormalizeServerKey(item.ServerKey)).Distinct(StringComparer.Ordinal).Count();

    public static string DominantRule(IEnumerable<FleetSignal> signals) => signals.GroupBy(item => item.RuleId.Trim().ToUpperInvariant(), StringComparer.Ordinal).OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal).Select(group => group.Key).FirstOrDefault() ?? "UNKNOWN";

    public static IReadOnlyList<string> Environments(IEnumerable<FleetSignal> signals) => signals.Select(item => NormalizeEnvironment(item.Environment)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    public static IReadOnlyList<SignalCluster> Correlate(IEnumerable<FleetSignal> signals, TimeSpan window, int limit = 20)
    {
        var boundedWindow = ClampWindow(window);
        var boundedLimit = Math.Clamp(limit, 1, 100);
        return signals.GroupBy(item => CorrelationKey(item, boundedWindow), StringComparer.Ordinal).Select(group =>
        {
            var items = group.ToArray();
            var radius = BlastRadius(items);
            var severityWeight = items.Max(item => SeverityWeight(item.Severity));
            var score = Math.Round(Math.Clamp(severityWeight * 0.7 + Math.Min(100, radius * 10d) * 0.3, 0, 100), 2);
            var severity = items.Any(item => item.Severity == B400Severity.Critical)
                ? B400Severity.Critical
                : score >= 45 ? B400Severity.Warning : score > 0 ? B400Severity.Info : B400Severity.None;
            return new SignalCluster(group.Key, Bucket(items.Min(item => item.AtUtc), boundedWindow), DominantRule(items), radius, Environments(items), severity, score);
        }).OrderByDescending(item => item.Score).ThenBy(item => item.ClusterKey, StringComparer.Ordinal).Take(boundedLimit).ToArray();
    }
}
