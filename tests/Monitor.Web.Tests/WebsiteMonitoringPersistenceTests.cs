using Monitor.Web.Services;

namespace Monitor.Web.Tests;

public sealed class WebsiteMonitoringPersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "monitor-website-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void File_target_store_survives_recreation_and_preserves_updates()
    {
        var path = Path.Combine(_root, "targets.json");
        var first = new FileWebsiteTargetStore(path);
        var target = Target();
        first.Upsert(target);

        var second = new FileWebsiteTargetStore(path);
        Assert.Equal(target, second.Get(target.Id));

        second.Upsert(target with { Name = "Updated portal", IsEnabled = false });
        var third = new FileWebsiteTargetStore(path);
        Assert.Equal("Updated portal", third.Get(target.Id)?.Name);
        Assert.False(third.Get(target.Id)?.IsEnabled);
    }

    [Fact]
    public void File_target_store_rejects_invalid_target()
    {
        var store = new FileWebsiteTargetStore(Path.Combine(_root, "targets-invalid.json"));
        var invalid = Target() with { Url = "file:///etc/passwd" };

        Assert.Throws<ArgumentException>(() => store.Upsert(invalid));
    }

    [Fact]
    public void Independent_target_store_instances_see_peer_write()
    {
        var path = Path.Combine(_root, "targets-peer.json");
        var first = new FileWebsiteTargetStore(path);
        var second = new FileWebsiteTargetStore(path);
        var target = Target();

        first.Upsert(target);

        Assert.Equal(target, second.Get(target.Id));
    }

    [Fact]
    public void Scheduler_claim_prevents_duplicate_worker_ownership_and_survives_recreation()
    {
        var path = Path.Combine(_root, "schedule.json");
        var first = new FileWebsiteScheduleStateStore(path);
        var second = new FileWebsiteScheduleStateStore(path);
        var targetId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-08-19T06:00:00Z");
        var interval = TimeSpan.FromMinutes(1);

        var claim = first.TryClaim(targetId, now, interval, TimeSpan.FromSeconds(30));

        Assert.NotNull(claim);
        Assert.Null(second.TryClaim(targetId, now.AddSeconds(1), interval, TimeSpan.FromSeconds(30)));
        Assert.True(second.Complete(claim!, now.AddSeconds(2), interval));

        var third = new FileWebsiteScheduleStateStore(path);
        Assert.Null(third.TryClaim(targetId, now.AddSeconds(30), interval, TimeSpan.FromSeconds(30)));
        Assert.NotNull(third.TryClaim(targetId, now.AddMinutes(2), interval, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Expired_scheduler_claim_can_be_recovered_by_another_worker()
    {
        var path = Path.Combine(_root, "schedule-expired.json");
        var first = new FileWebsiteScheduleStateStore(path);
        var second = new FileWebsiteScheduleStateStore(path);
        var targetId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-08-19T06:00:00Z");

        var original = first.TryClaim(targetId, now, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10));
        var replacement = second.TryClaim(targetId, now.AddSeconds(11), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10));

        Assert.NotNull(original);
        Assert.NotNull(replacement);
        Assert.NotEqual(original!.Token, replacement!.Token);
        Assert.False(first.Complete(original, now.AddSeconds(12), TimeSpan.FromMinutes(1)));
        Assert.True(second.Complete(replacement, now.AddSeconds(12), TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void History_store_persists_bounded_sanitized_probe_summary()
    {
        var path = Path.Combine(_root, "history.json");
        var time = new FrozenTimeProvider(DateTimeOffset.Parse("2026-08-19T06:10:00Z"));
        var store = new FileWebsiteProbeHistoryStore(path, time);
        var targetId = Guid.NewGuid();
        store.Append(Result(targetId, time.GetUtcNow()));

        var recreated = new FileWebsiteProbeHistoryStore(path, time);
        var points = recreated.Read(targetId, TimeSpan.FromHours(1));

        var point = Assert.Single(points);
        Assert.Equal("website.available", point.RuleId);
        Assert.Equal(200, point.HttpStatusCode);
        Assert.Equal("example.com", point.FinalHost);
        Assert.DoesNotContain("healthy body", point.EvidenceSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Website_monitoring_options_are_default_disabled_and_bounded()
    {
        var options = new WebsiteMonitoringOptions();
        Assert.False(options.Enabled);
        options.Validate();

        options.MaxConcurrency = 17;
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static WebsiteTargetDefinition Target() => new(
        Guid.NewGuid(),
        "Portal",
        "https://example.com/health",
        "production",
        IntervalSeconds: 60,
        TimeoutSeconds: 10,
        ExpectedStatusMin: 200,
        ExpectedStatusMax: 299,
        ExpectedContentMarker: "healthy");

    private static WebsiteProbeResult Result(Guid targetId, DateTimeOffset now)
    {
        var evidence = new WebsiteProbeEvidence(true, true, true, false, 200, true, true, true, false, 120, 3000);
        var classification = WebsiteFailureClassifier.Classify(evidence);
        return new WebsiteProbeResult(
            targetId,
            now.AddMilliseconds(-120),
            now,
            new Uri("https://example.com/health"),
            new Uri("https://example.com/health"),
            0,
            evidence,
            classification,
            now.AddDays(90),
            "CN=example.com",
            "CN=Example CA");
    }

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
