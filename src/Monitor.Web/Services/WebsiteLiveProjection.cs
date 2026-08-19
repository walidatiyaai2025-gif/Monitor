namespace Monitor.Web.Services;

public static class WebsiteLiveProjection
{
    public static bool IsPolicyBlockedRule(string? ruleId) =>
        string.Equals(ruleId, "destination.blocked", StringComparison.Ordinal) ||
        string.Equals(ruleId, "destination.policy-blocked", StringComparison.Ordinal);

    public static WebsiteProbeHistoryPoint? NormalizeLatest(WebsiteProbeHistoryPoint? point)
    {
        if (point is null || !IsPolicyBlockedRule(point.RuleId)) return point;

        return point with
        {
            State = WebsiteProbeState.Unknown,
            RuleId = "destination.blocked",
            ProbableLayer = "Monitoring outbound policy",
            Confidence = "high",
            EvidenceSummary = Bound(point.EvidenceSummary)
        };
    }

    public static WebsiteAvailabilitySummary SummarizeAvailability(IEnumerable<WebsiteProbeHistoryPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var materialized = points.ToArray();
        var known = materialized
            .Where(point => point.State != WebsiteProbeState.Unknown && !IsPolicyBlockedRule(point.RuleId))
            .ToArray();
        var available = known.Count(point => point.State is WebsiteProbeState.Up or WebsiteProbeState.Degraded);
        double? percentage = known.Length == 0 ? null : Math.Round(available * 100d / known.Length, 2);
        return new WebsiteAvailabilitySummary(percentage, known.Length, materialized.Length - known.Length);
    }

    private static string Bound(string value) => value.Length <= 500 ? value : value[..500];
}

public sealed record WebsiteAvailabilitySummary(double? Percentage, int KnownChecks, int UnknownChecks);
