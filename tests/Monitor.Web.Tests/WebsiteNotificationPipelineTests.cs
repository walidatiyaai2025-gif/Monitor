using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Tests;

public sealed class WebsiteNotificationPipelineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "monitor-website-notification-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Planner_queues_open_event_to_enabled_groups_with_recipient_deduplication()
    {
        var groups = new InMemoryWebsiteNotificationGroupStore();
        groups.Upsert(new WebsiteNotificationGroup("web", "Web Team", ["web1@example.com", "shared@example.com"]));
        groups.Upsert(new WebsiteNotificationGroup("ops", "Ops Team", ["shared@example.com", "ops@example.com"]));
        var outbox = new FileWebsiteNotificationOutbox(Path.Combine(_root, "outbox.json"));
        var options = EnabledOptions();
        var planner = new WebsiteNotificationPlanner(options, groups, outbox);
        var target = Target(["web", "ops"]);
        var result = Result(target.Id, DateTimeOffset.Parse("2026-08-19T07:30:00Z"), WebsiteProbeState.Down, "http.5xx", 503);
        var incident = Incident(target.Id, result.CompletedAtUtc, "http.5xx", IncidentStatus.Open);

        var queued = planner.Queue(target, result, new WebsiteIncidentObservation(WebsiteIncidentTransition.Opened, incident));

        Assert.True(queued);
        var item = Assert.Single(outbox.Snapshot());
        Assert.Equal(WebsiteNotificationKind.IncidentOpened, item.Kind);
        Assert.Equal(3, item.Recipients.Length);
        Assert.Equal(item.Recipients.Distinct(StringComparer.OrdinalIgnoreCase).Count(), item.Recipients.Length);
        Assert.Contains("ALERT", item.Subject, StringComparison.Ordinal);
        Assert.Contains("probable layer", item.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", item.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Planner_deduplicates_same_transition_and_ignores_incident_updates()
    {
        var groups = new InMemoryWebsiteNotificationGroupStore();
        groups.Upsert(new WebsiteNotificationGroup("web", "Web Team", ["web@example.com"]));
        var outbox = new FileWebsiteNotificationOutbox(Path.Combine(_root, "outbox-dedup.json"));
        var planner = new WebsiteNotificationPlanner(EnabledOptions(), groups, outbox);
        var target = Target(["web"]);
        var at = DateTimeOffset.Parse("2026-08-19T07:30:00Z");
        var result = Result(target.Id, at, WebsiteProbeState.Down, "dns.failure", null);
        var incident = Incident(target.Id, at, "dns.failure", IncidentStatus.Open);
        var opened = new WebsiteIncidentObservation(WebsiteIncidentTransition.Opened, incident);

        Assert.True(planner.Queue(target, result, opened));
        Assert.False(planner.Queue(target, result, opened));
        Assert.False(planner.Queue(target, result, new WebsiteIncidentObservation(WebsiteIncidentTransition.Updated, incident)));
        Assert.Single(outbox.Snapshot());
    }

    [Fact]
    public void Recovery_event_generates_recovered_notification()
    {
        var groups = new InMemoryWebsiteNotificationGroupStore();
        groups.Upsert(new WebsiteNotificationGroup("web", "Web Team", ["web@example.com"]));
        var outbox = new FileWebsiteNotificationOutbox(Path.Combine(_root, "outbox-recovery.json"));
        var planner = new WebsiteNotificationPlanner(EnabledOptions(), groups, outbox);
        var target = Target(["web"]);
        var at = DateTimeOffset.Parse("2026-08-19T07:30:00Z");
        var result = Result(target.Id, at, WebsiteProbeState.Up, "website.available", 200);
        var incident = Incident(target.Id, at, "http.5xx", IncidentStatus.Resolved);

        Assert.True(planner.Queue(target, result, new WebsiteIncidentObservation(WebsiteIncidentTransition.Recovered, incident)));

        var item = Assert.Single(outbox.Snapshot());
        Assert.Equal(WebsiteNotificationKind.IncidentRecovered, item.Kind);
        Assert.Contains("RECOVERED", item.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void Disabled_notifications_do_not_queue()
    {
        var groups = new InMemoryWebsiteNotificationGroupStore();
        groups.Upsert(new WebsiteNotificationGroup("web", "Web Team", ["web@example.com"]));
        var outbox = new FileWebsiteNotificationOutbox(Path.Combine(_root, "outbox-disabled.json"));
        var options = EnabledOptions();
        options.Enabled = false;
        var planner = new WebsiteNotificationPlanner(options, groups, outbox);
        var target = Target(["web"]);
        var at = DateTimeOffset.Parse("2026-08-19T07:30:00Z");
        var result = Result(target.Id, at, WebsiteProbeState.Down, "http.5xx", 500);

        Assert.False(planner.Queue(target, result, new WebsiteIncidentObservation(WebsiteIncidentTransition.Opened, Incident(target.Id, at, "http.5xx", IncidentStatus.Open))));
        Assert.Empty(outbox.Snapshot());
    }

    [Fact]
    public void Independent_outbox_instances_do_not_claim_same_item()
    {
        var path = Path.Combine(_root, "outbox-claim.json");
        var first = new FileWebsiteNotificationOutbox(path);
        var second = new FileWebsiteNotificationOutbox(path);
        var at = DateTimeOffset.Parse("2026-08-19T07:30:00Z");
        Assert.True(first.Enqueue(Item(at)));

        var claim = first.TryClaimDue(at, TimeSpan.FromMinutes(1));
        var duplicate = second.TryClaimDue(at.AddSeconds(1), TimeSpan.FromMinutes(1));

        Assert.NotNull(claim);
        Assert.Null(duplicate);
        Assert.True(second.MarkSent(claim!, at.AddSeconds(2)));
        Assert.Equal(WebsiteNotificationDeliveryStatus.Sent, Assert.Single(first.Snapshot()).Status);
    }

    [Fact]
    public void Failed_delivery_uses_bounded_retry_then_dead_letters()
    {
        var outbox = new FileWebsiteNotificationOutbox(Path.Combine(_root, "outbox-retry.json"));
        var at = DateTimeOffset.Parse("2026-08-19T07:30:00Z");
        outbox.Enqueue(Item(at));

        var first = Assert.IsType<WebsiteNotificationClaim>(outbox.TryClaimDue(at, TimeSpan.FromMinutes(1)));
        Assert.True(outbox.MarkFailed(first, at, 2, "SmtpException"));
        var pending = Assert.Single(outbox.Snapshot());
        Assert.Equal(WebsiteNotificationDeliveryStatus.Pending, pending.Status);
        Assert.Equal(1, pending.Attempts);

        var second = Assert.IsType<WebsiteNotificationClaim>(outbox.TryClaimDue(pending.NextAttemptUtc, TimeSpan.FromMinutes(1)));
        Assert.True(outbox.MarkFailed(second, pending.NextAttemptUtc, 2, "SmtpException"));
        var dead = Assert.Single(outbox.Snapshot());
        Assert.Equal(WebsiteNotificationDeliveryStatus.DeadLetter, dead.Status);
        Assert.Equal(2, dead.Attempts);
    }

    [Fact]
    public async Task Authenticated_smtp_fails_closed_when_environment_secret_is_missing()
    {
        var options = EnabledOptions();
        options.Username = "monitor-service";
        options.PasswordEnvironmentVariable = $"MONITOR_TEST_MISSING_{Guid.NewGuid():N}";
        var sender = new SmtpWebsiteEmailSender(options, new EnvironmentWebsiteSmtpCredentialProvider());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(Item(DateTimeOffset.UtcNow), CancellationToken.None));

        Assert.Contains("secret is unavailable", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Notification_options_expose_secret_reference_not_plaintext_password()
    {
        var propertyNames = typeof(WebsiteNotificationOptions).GetProperties().Select(property => property.Name).ToArray();

        Assert.Contains(nameof(WebsiteNotificationOptions.PasswordEnvironmentVariable), propertyNames);
        Assert.DoesNotContain("Password", propertyNames);
        Assert.False(new WebsiteNotificationOptions().Enabled);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static WebsiteNotificationOptions EnabledOptions() => new()
    {
        Enabled = true,
        SmtpHost = "smtp.example.com",
        SmtpPort = 587,
        EnableSsl = true,
        FromAddress = "monitor@example.com"
    };

    private static WebsiteTargetDefinition Target(IReadOnlyList<string> groups) => new(
        Guid.NewGuid(),
        "Citizen Portal",
        "https://example.com/health",
        "production",
        IntervalSeconds: 60,
        TimeoutSeconds: 10,
        NotificationGroupIds: groups);

    private static HealthIncident Incident(Guid targetId, DateTimeOffset at, string ruleId, IncidentStatus status) => new(
        $"{targetId:N}:{ruleId}", targetId, ruleId, FindingSeverity.Critical, "Website incident", "Bounded evidence", at, at, 1, status);

    private static WebsiteProbeResult Result(Guid targetId, DateTimeOffset at, WebsiteProbeState state, string ruleId, int? httpStatus)
    {
        var evidence = new WebsiteProbeEvidence(true, true, true, false, httpStatus, state == WebsiteProbeState.Up, true, true, false, 120, 3000);
        var classification = new WebsiteProbeClassification(state, ruleId,
            ruleId == "dns.failure" ? "DNS / name resolution" : state == WebsiteProbeState.Up ? "End-to-end HTTP path" : "Web server / proxy / application",
            "high", $"Observed evidence for {ruleId}.");
        return new WebsiteProbeResult(targetId, at.AddMilliseconds(-120), at, new Uri("https://example.com/health"),
            new Uri("https://example.com/health"), 0, evidence, classification, at.AddDays(90), "CN=example.com", "CN=Example CA");
    }

    private static WebsiteNotificationOutboxItem Item(DateTimeOffset at) => new(
        Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), Guid.NewGuid(), $"{Guid.NewGuid():N}:http.5xx",
        WebsiteNotificationKind.IncidentOpened, ["ops@example.com"], "[ALERT] Test", "Bounded body", at, at, 0,
        WebsiteNotificationDeliveryStatus.Pending, null, null, null);
}
