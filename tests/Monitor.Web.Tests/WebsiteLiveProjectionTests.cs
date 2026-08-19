using Monitor.Web.Services;

namespace Monitor.Web.Tests;

public sealed class WebsiteLiveProjectionTests
{
    [Theory]
    [InlineData("destination.blocked")]
    [InlineData("destination.policy-blocked")]
    public void Policy_blocked_evidence_is_unknown_and_excluded_from_availability(string ruleId)
    {
        var targetId = Guid.NewGuid();
        var at = DateTimeOffset.Parse("2026-08-19T08:00:00Z");
        var blocked = Point(targetId, at, WebsiteProbeState.Down, ruleId, null);

        var normalized = Assert.IsType<WebsiteProbeHistoryPoint>(WebsiteLiveProjection.NormalizeLatest(blocked));
        var availability = WebsiteLiveProjection.SummarizeAvailability([blocked]);

        Assert.Equal(WebsiteProbeState.Unknown, normalized.State);
        Assert.Equal("destination.blocked", normalized.RuleId);
        Assert.Null(availability.Percentage);
        Assert.Equal(0, availability.KnownChecks);
        Assert.Equal(1, availability.UnknownChecks);
    }

    [Fact]
    public void Availability_counts_real_up_degraded_and_down_but_not_unknown()
    {
        var targetId = Guid.NewGuid();
        var at = DateTimeOffset.Parse("2026-08-19T08:00:00Z");
        var summary = WebsiteLiveProjection.SummarizeAvailability([
            Point(targetId, at, WebsiteProbeState.Up, "ok", 200),
            Point(targetId, at.AddMinutes(1), WebsiteProbeState.Degraded, "performance.slow", 200),
            Point(targetId, at.AddMinutes(2), WebsiteProbeState.Down, "http.5xx", 500),
            Point(targetId, at.AddMinutes(3), WebsiteProbeState.Unknown, "unknown", null)
        ]);

        Assert.Equal(66.67, summary.Percentage);
        Assert.Equal(3, summary.KnownChecks);
        Assert.Equal(1, summary.UnknownChecks);
    }

    private static WebsiteProbeHistoryPoint Point(Guid targetId, DateTimeOffset at, WebsiteProbeState state, string ruleId, int? status) => new(
        targetId,
        at,
        state,
        ruleId,
        state == WebsiteProbeState.Unknown ? "Unknown" : "End-to-end HTTP path",
        "high",
        status,
        120,
        at.AddDays(60),
        "example.com",
        0,
        $"Evidence for {ruleId}");
}
