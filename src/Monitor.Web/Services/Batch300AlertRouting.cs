using System.Security.Cryptography;
using System.Text;

namespace Monitor.Web.Services;

public enum AlertRoute
{
    None,
    Queue,
    Notify,
    Page
}

public sealed record AlertRoutingInput(
    string? RuleId,
    string? Environment,
    int Severity,
    bool Suppressed,
    bool InMaintenance,
    string? Assignee,
    DateTimeOffset NowUtc);

public sealed record AlertRoutingDecision(AlertRoute Route, int EscalationTier, TimeSpan Cooldown, string DedupKey, string Owner, string Reason);

public static class Batch300AlertRouting
{
    public static string NormalizeEnvironment(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "prod" or "production" => "production",
        "stage" or "staging" => "staging",
        "test" or "qa" => "test",
        "dev" or "development" => "development",
        _ => "unspecified"
    };

    public static int EscalationTier(int severity, string? environment)
    {
        var normalizedSeverity = Math.Clamp(severity, 0, 100);
        var production = NormalizeEnvironment(environment) == "production";
        if (normalizedSeverity >= 90 && production) return 3;
        if (normalizedSeverity >= 75) return 2;
        if (normalizedSeverity >= 40) return 1;
        return 0;
    }

    public static AlertRoute Route(AlertRoutingInput input)
    {
        if (input.Suppressed || input.InMaintenance) return AlertRoute.None;
        return EscalationTier(input.Severity, input.Environment) switch
        {
            >= 3 => AlertRoute.Page,
            2 => AlertRoute.Notify,
            1 => AlertRoute.Queue,
            _ => AlertRoute.None
        };
    }

    public static bool ShouldPage(AlertRoutingInput input) => Route(input) == AlertRoute.Page;

    public static TimeSpan Cooldown(int escalationTier) => escalationTier switch
    {
        >= 3 => TimeSpan.FromMinutes(5),
        2 => TimeSpan.FromMinutes(15),
        1 => TimeSpan.FromMinutes(30),
        _ => TimeSpan.FromHours(1)
    };

    public static string Owner(string? assignee) => string.IsNullOrWhiteSpace(assignee) ? "unassigned" : Batch300EstateIdentity.NormalizeName(assignee, 80);

    public static string DedupKey(string? ruleId, string? environment)
    {
        var canonical = $"{Batch300FleetRisk.SafeKey(ruleId)}|{NormalizeEnvironment(environment)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes)[..20].ToLowerInvariant();
    }

    public static string Reason(AlertRoutingInput input)
    {
        if (input.Suppressed) return "suppressed";
        if (input.InMaintenance) return "maintenance";
        return Route(input) switch
        {
            AlertRoute.Page => "critical-production",
            AlertRoute.Notify => "high-severity",
            AlertRoute.Queue => "operator-review",
            _ => "below-routing-threshold"
        };
    }

    public static bool InQuietWindow(TimeOnly localTime, TimeOnly start, TimeOnly end)
    {
        if (start == end) return false;
        return start < end ? localTime >= start && localTime < end : localTime >= start || localTime < end;
    }

    public static AlertRoutingDecision Decide(AlertRoutingInput input)
    {
        var tier = EscalationTier(input.Severity, input.Environment);
        return new(Route(input), tier, Cooldown(tier), DedupKey(input.RuleId, input.Environment), Owner(input.Assignee), Reason(input));
    }
}
