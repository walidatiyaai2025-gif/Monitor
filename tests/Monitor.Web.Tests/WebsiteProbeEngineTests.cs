using Monitor.Web.Services;

namespace Monitor.Web.Tests;

public sealed class WebsiteProbeEngineTests
{
    [Fact]
    public async Task Probe_satisfies_status_content_and_final_host_contract()
    {
        var client = new QueueHopClient([
            Hop("https://example.com/health", 200, body: "{\"status\":\"healthy\"}")
        ]);
        var engine = new WebsiteProbeEngine(client, TimeProvider.System);

        var result = await engine.ProbeAsync(Target(expectedContent: "healthy", expectedFinalHost: "example.com"), CancellationToken.None);

        Assert.Equal(WebsiteProbeState.Up, result.Classification.State);
        Assert.Equal("website.available", result.Classification.RuleId);
        Assert.Equal(0, result.RedirectCount);
        Assert.True(result.Evidence.ContentMatched);
        Assert.True(result.Evidence.RedirectExpected);
    }

    [Fact]
    public async Task Probe_rechecks_each_manual_redirect_hop()
    {
        var client = new QueueHopClient([
            Hop("https://example.com/start", 302, redirect: "https://www.example.com/health"),
            Hop("https://www.example.com/health", 200, body: "healthy")
        ]);
        var engine = new WebsiteProbeEngine(client, TimeProvider.System);

        var result = await engine.ProbeAsync(Target(url: "https://example.com/start", expectedContent: "healthy", expectedFinalHost: "www.example.com"), CancellationToken.None);

        Assert.Equal(2, client.RequestedUris.Count);
        Assert.Equal("example.com", client.RequestedUris[0].Host);
        Assert.Equal("www.example.com", client.RequestedUris[1].Host);
        Assert.Equal(1, result.RedirectCount);
        Assert.Equal(WebsiteProbeState.Up, result.Classification.State);
    }

    [Fact]
    public async Task Probe_does_not_follow_redirect_when_disabled()
    {
        var client = new QueueHopClient([
            Hop("https://example.com/start", 302, redirect: "https://www.example.com/health")
        ]);
        var engine = new WebsiteProbeEngine(client, TimeProvider.System);

        var result = await engine.ProbeAsync(Target(url: "https://example.com/start", followRedirects: false, expectedContent: null), CancellationToken.None);

        Assert.Single(client.RequestedUris);
        Assert.Equal(302, result.Evidence.HttpStatusCode);
        Assert.Equal(WebsiteProbeState.Up, result.Classification.State);
    }

    [Fact]
    public async Task Redirect_to_non_http_scheme_is_blocked_before_second_hop()
    {
        var client = new QueueHopClient([
            Hop("https://example.com/start", 302, redirect: "file:///etc/passwd")
        ]);
        var engine = new WebsiteProbeEngine(client, TimeProvider.System);

        var result = await engine.ProbeAsync(Target(url: "https://example.com/start", expectedContent: null), CancellationToken.None);

        Assert.Single(client.RequestedUris);
        Assert.Equal(WebsiteProbeState.Unknown, result.Classification.State);
        Assert.Equal("destination.blocked", result.Classification.RuleId);
    }

    [Fact]
    public async Task Destination_policy_rejection_is_unknown_not_false_site_down()
    {
        var client = new QueueHopClient([
            Hop("https://example.com/", null, destinationAllowed: false, dnsResolved: true,
                failureReason: "Resolved destination is blocked by Website Monitoring outbound policy.")
        ]);
        var engine = new WebsiteProbeEngine(client, TimeProvider.System);

        var result = await engine.ProbeAsync(Target(expectedContent: null), CancellationToken.None);

        Assert.Equal(WebsiteProbeState.Unknown, result.Classification.State);
        Assert.Equal("destination.blocked", result.Classification.RuleId);
        Assert.Equal("high", result.Classification.Confidence);
    }

    [Fact]
    public async Task Http_500_is_classified_as_observed_server_side_failure_without_root_cause_overclaim()
    {
        var client = new QueueHopClient([
            Hop("https://example.com/health", 500, body: "error")
        ]);
        var engine = new WebsiteProbeEngine(client, TimeProvider.System);

        var result = await engine.ProbeAsync(Target(expectedContent: null), CancellationToken.None);

        Assert.Equal(WebsiteProbeState.Down, result.Classification.State);
        Assert.Equal("http.5xx", result.Classification.RuleId);
        Assert.Equal("Web server / proxy / application", result.Classification.ProbableLayer);
        Assert.DoesNotContain("database", result.Classification.EvidenceSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_content_marker_is_application_content_failure()
    {
        var client = new QueueHopClient([
            Hop("https://example.com/health", 200, body: "login page")
        ]);
        var engine = new WebsiteProbeEngine(client, TimeProvider.System);

        var result = await engine.ProbeAsync(Target(expectedContent: "healthy"), CancellationToken.None);

        Assert.Equal("content.mismatch", result.Classification.RuleId);
        Assert.False(result.Evidence.ContentMatched);
    }

    [Fact]
    public async Task Expiring_valid_certificate_is_degraded()
    {
        var client = new QueueHopClient([
            Hop("https://example.com/health", 200, body: "healthy", certificateNotAfterUtc: DateTimeOffset.UtcNow.AddDays(10))
        ]);
        var engine = new WebsiteProbeEngine(client, TimeProvider.System);

        var result = await engine.ProbeAsync(Target(expectedContent: "healthy"), CancellationToken.None);

        Assert.Equal(WebsiteProbeState.Degraded, result.Classification.State);
        Assert.Equal("tls.expiring", result.Classification.RuleId);
    }

    [Fact]
    public async Task Redirect_limit_is_bounded()
    {
        var hops = Enumerable.Range(0, WebsiteProbeEngine.MaxRedirects + 1)
            .Select(index => Hop($"https://example.com/{index}", 302, redirect: $"https://example.com/{index + 1}"))
            .ToArray();
        var client = new QueueHopClient(hops);
        var engine = new WebsiteProbeEngine(client, TimeProvider.System);

        var result = await engine.ProbeAsync(Target(url: "https://example.com/0", expectedContent: null), CancellationToken.None);

        Assert.Equal(WebsiteProbeEngine.MaxRedirects + 1, result.RedirectCount);
        Assert.Equal("redirect.unexpected", result.Classification.RuleId);
        Assert.Equal(WebsiteProbeEngine.MaxRedirects + 1, client.RequestedUris.Count);
    }

    [Fact]
    public void Default_authorizer_fails_closed_for_mixed_public_private_dns_answer()
    {
        var authorizer = new DefaultWebsiteDestinationAuthorizer();

        var allowed = authorizer.IsAllowed("example.com", [
            System.Net.IPAddress.Parse("1.1.1.1"),
            System.Net.IPAddress.Parse("127.0.0.1")
        ]);

        Assert.False(allowed);
    }

    private static WebsiteTargetDefinition Target(
        string url = "https://example.com/health",
        string? expectedContent = "healthy",
        string? expectedFinalHost = null,
        bool followRedirects = true) => new(
            Guid.NewGuid(),
            "Portal",
            url,
            "production",
            IntervalSeconds: 60,
            TimeoutSeconds: 10,
            ExpectedStatusMin: 200,
            ExpectedStatusMax: 399,
            ExpectedContentMarker: expectedContent,
            FollowRedirects: followRedirects,
            ExpectedFinalHost: expectedFinalHost,
            SlowThresholdMilliseconds: 3000);

    private static WebsiteHttpHopResult Hop(
        string uri,
        int? status,
        string? body = null,
        string? redirect = null,
        bool? destinationAllowed = true,
        bool? dnsResolved = true,
        string? failureReason = null,
        DateTimeOffset? certificateNotAfterUtc = null) => new(
            new Uri(uri),
            DnsResolved: dnsResolved,
            DestinationAllowed: destinationAllowed,
            TcpConnected: status is null ? null : true,
            TlsValid: status is null ? null : true,
            TimedOut: false,
            HttpStatusCode: status,
            RedirectLocation: redirect is null ? null : new Uri(redirect),
            CertificateNotAfterUtc: certificateNotAfterUtc,
            CertificateSubject: certificateNotAfterUtc is null ? null : "CN=example.com",
            CertificateIssuer: certificateNotAfterUtc is null ? null : "CN=Test CA",
            ElapsedMilliseconds: 100,
            BoundedBody: body,
            FailureReason: failureReason);

    private sealed class QueueHopClient(IEnumerable<WebsiteHttpHopResult> hops) : IWebsiteHttpHopClient
    {
        private readonly Queue<WebsiteHttpHopResult> _hops = new(hops);
        public List<Uri> RequestedUris { get; } = [];

        public Task<WebsiteHttpHopResult> SendAsync(Uri uri, int maxBodyBytes, CancellationToken cancellationToken)
        {
            RequestedUris.Add(uri);
            if (_hops.Count == 0) throw new InvalidOperationException("No fake hop is available.");
            var hop = _hops.Dequeue();
            Assert.Equal(hop.RequestUri, uri);
            return Task.FromResult(hop);
        }
    }
}
