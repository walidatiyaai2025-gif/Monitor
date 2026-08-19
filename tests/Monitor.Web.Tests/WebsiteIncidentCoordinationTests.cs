using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Tests;

public sealed class WebsiteIncidentCoordinationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "monitor-website-incident-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Failure_below_confirmation_threshold_does_not_open_incident()
    {
        var incidents = new InMemoryHealthIncidentRepository();
        var state = new InMemoryWebsiteCheckStateStore();
        var coordinator = new WebsiteIncidentCoordinator(state, incidents);
        var target = Target(failureCount: 3);
        var at = DateTimeOffset.Parse("2026-08-19T07:00:00Z");

        coordinator.Observe(target, Result(target.Id, at, WebsiteProbeState.Down, "http.5xx", 500));
        coordinator.Observe(target, Result(target.Id, at.AddMinutes(1), WebsiteProbeState.Down, "http.5xx", 500));

        Assert.Empty(incidents.GetAll());
        var check = Assert.IsType<WebsiteCheckState>(state.Get(target.Id));
        Assert.Equal(2, check.ConsecutiveFailures);
        Assert.Equal("http.5xx", check.ActiveRuleId);
    }

    [Fact]
    public void Third_same_failure_opens_one_stable_incident_and_continuation_updates_occurrence()
    {
        var incidents = new InMemoryHealthIncidentRepository();
        var state = new InMemoryWebsiteCheckStateStore();
        var coordinator = new WebsiteIncidentCoordinator(state, incidents);
        var target = Target(failureCount: 3);
        var at = DateTimeOffset.Parse("2026-08-19T07:00:00Z");

        coordinator.Observe(target, Result(target.Id, at, WebsiteProbeState.Down, "http.5xx", 500));
        coordinator.Observe(target, Result(target.Id, at.AddMinutes(1), WebsiteProbeState.Down, "http.5xx", 500));
        coordinator.Observe(target, Result(target.Id, at.AddMinutes(2), WebsiteProbeState.Down, "http.5xx", 500));

        var opened = Assert.Single(incidents.GetAll());
        Assert.Equal($"{target.Id:N}:http.5xx", opened.Id);
        Assert.Equal(IncidentStatus.Open, opened.Status);
        Assert.Equal(FindingSeverity.Critical, opened.Severity);
        Assert.Equal(1, opened.Occurrences);

        coordinator.Observe(target, Result(target.Id, at.AddMinutes(3), WebsiteProbeState.Down, "http.5xx", 503));

        var updated = Assert.Single(incidents.GetAll());
        Assert.Equal(opened.Id, updated.Id);
        Assert.Equal(2, updated.Occurrences);
        Assert.Contains("HTTP=503", updated.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void Failure_rule_change_restarts_confirmation_instead_of_reusing_old_count()
    {
        var incidents = new InMemoryHealthIncidentRepository();
        var state = new InMemoryWebsiteCheckStateStore();
        var coordinator = new WebsiteIncidentCoordinator(state, incidents);
        var target = Target(failureCount: 3);
        var at = DateTimeOffset.Parse("2026-08-19T07:00:00Z");

        coordinator.Observe(target, Result(target.Id, at, WebsiteProbeState.Down, "http.5xx", 500));
        coordinator.Observe(target, Result(target.Id, at.AddMinutes(1), WebsiteProbeState.Down, "http.5xx", 500));
        coordinator.Observe(target, Result(target.Id, at.AddMinutes(2), WebsiteProbeState.Down, "dns.failure", null));

        Assert.Empty(incidents.GetAll());
        var check = Assert.IsType<WebsiteCheckState>(state.Get(target.Id));
        Assert.Equal(1, check.ConsecutiveFailures);
        Assert.Equal("dns.failure", check.ActiveRuleId);
    }

    [Fact]
    public void Recovery_requires_configured_success_threshold_before_resolving()
    {
        var incidents = new InMemoryHealthIncidentRepository();
        var state = new InMemoryWebsiteCheckStateStore();
        var coordinator = new WebsiteIncidentCoordinator(state, incidents);
        var target = Target(failureCount: 1, recoveryCount: 2);
        var at = DateTimeOffset.Parse("2026-08-19T07:00:00Z");

        coordinator.Observe(target, Result(target.Id, at, WebsiteProbeState.Down, "network.connect-failure", null));
        Assert.Equal(IncidentStatus.Open, Assert.Single(incidents.GetAll()).Status);

        coordinator.Observe(target, Result(target.Id, at.AddMinutes(1), WebsiteProbeState.Up, "website.available", 200));
        Assert.Equal(IncidentStatus.Open, Assert.Single(incidents.GetAll()).Status);

        coordinator.Observe(target, Result(target.Id, at.AddMinutes(2), WebsiteProbeState.Up, "website.available", 200));
        Assert.Equal(IncidentStatus.Resolved, Assert.Single(incidents.GetAll()).Status);
        Assert.Null(state.Get(target.Id)?.ActiveRuleId);
    }

    [Fact]
    public void Unknown_probe_evidence_never_resolves_existing_incident()
    {
        var incidents = new InMemoryHealthIncidentRepository();
        var state = new InMemoryWebsiteCheckStateStore();
        var coordinator = new WebsiteIncidentCoordinator(state, incidents);
        var target = Target(failureCount: 1, recoveryCount: 1);
        var at = DateTimeOffset.Parse("2026-08-19T07:00:00Z");

        coordinator.Observe(target, Result(target.Id, at, WebsiteProbeState.Down, "http.5xx", 500));
        coordinator.Observe(target, Result(target.Id, at.AddMinutes(1), WebsiteProbeState.Unknown, "destination.blocked", null));

        Assert.Equal(IncidentStatus.Open, Assert.Single(incidents.GetAll()).Status);
    }

    [Fact]
    public void Resolved_incident_reopens_after_new_confirmed_failure()
    {
        var incidents = new InMemoryHealthIncidentRepository();
        var state = new InMemoryWebsiteCheckStateStore();
        var coordinator = new WebsiteIncidentCoordinator(state, incidents);
        var target = Target(failureCount: 1, recoveryCount: 1);
        var at = DateTimeOffset.Parse("2026-08-19T07:00:00Z");

        coordinator.Observe(target, Result(target.Id, at, WebsiteProbeState.Down, "http.5xx", 500));
        coordinator.Observe(target, Result(target.Id, at.AddMinutes(1), WebsiteProbeState.Up, "website.available", 200));
        Assert.Equal(IncidentStatus.Resolved, Assert.Single(incidents.GetAll()).Status);

        coordinator.Observe(target, Result(target.Id, at.AddMinutes(2), WebsiteProbeState.Down, "http.5xx", 503));

        var reopened = Assert.Single(incidents.GetAll());
        Assert.Equal(IncidentStatus.Open, reopened.Status);
        Assert.Equal(2, reopened.Occurrences);
    }

    [Fact]
    public void File_check_state_survives_recreation_and_peer_reads()
    {
        var path = Path.Combine(_root, "website-check-state.json");
        var targetId = Guid.NewGuid();
        var at = DateTimeOffset.Parse("2026-08-19T07:00:00Z");
        var first = new FileWebsiteCheckStateStore(path);
        var second = new FileWebsiteCheckStateStore(path);
        var value = new WebsiteCheckState(targetId, WebsiteProbeState.Down, "dns.failure", 2, 0, at, null, at);

        first.Upsert(value);

        Assert.Equal(value, second.Get(targetId));
        Assert.Equal(value, new FileWebsiteCheckStateStore(path).Get(targetId));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static WebsiteTargetDefinition Target(int failureCount = 3, int recoveryCount = 2) => new(
        Guid.NewGuid(),
        "Citizen Portal",
        "https://example.com/health",
        "production",
        IntervalSeconds: 60,
        TimeoutSeconds: 10,
        FailureConfirmationCount: failureCount,
        RecoveryConfirmationCount: recoveryCount);

    private static WebsiteProbeResult Result(Guid targetId, DateTimeOffset at, WebsiteProbeState state, string ruleId, int? status)
    {
        var classification = new WebsiteProbeClassification(
            state,
            ruleId,
            ruleId switch
            {
                "dns.failure" => "DNS / name resolution",
                "network.connect-failure" => "Network / listener path",
                "http.5xx" => "Web server / proxy / application",
                "destination.blocked" => "Monitoring outbound policy",
                _ => "End-to-end HTTP path"
            },
            state == WebsiteProbeState.Unknown ? "low" : "high",
            $"Synthetic test evidence for {ruleId}.");
        var evidence = new WebsiteProbeEvidence(
            DnsResolved: ruleId == "dns.failure" ? false : true,
            TcpConnected: ruleId is "dns.failure" or "network.connect-failure" ? false : true,
            TlsValid: true,
            TimedOut: false,
            HttpStatusCode: status,
            StatusExpected: state == WebsiteProbeState.Up,
            RedirectExpected: true,
            ContentMatched: true,
            CertificateExpiring: false,
            ElapsedMilliseconds: 120,
            SlowThresholdMilliseconds: 3000,
            FailureReason: null);
        return new WebsiteProbeResult(
            targetId,
            at.AddMilliseconds(-120),
            at,
            new Uri("https://example.com/health"),
            new Uri("https://example.com/health"),
            0,
            evidence,
            classification,
            at.AddDays(90),
            "CN=example.com",
            "CN=Example CA");
    }
}
