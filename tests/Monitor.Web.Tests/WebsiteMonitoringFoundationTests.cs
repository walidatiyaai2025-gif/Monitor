using System.Net;
using Monitor.Web.Services;

namespace Monitor.Web.Tests;

public sealed class WebsiteMonitoringFoundationTests
{
    [Fact]
    public void Target_validation_accepts_bounded_https_contract()
    {
        var target = ValidTarget();

        var result = WebsiteTargetValidator.Validate(target);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("ftp://example.com/")]
    [InlineData("file:///c:/windows/win.ini")]
    [InlineData("https://user:password@example.com/")]
    public void Target_validation_rejects_unsafe_url_shapes(string url)
    {
        var result = WebsiteTargetValidator.Validate(ValidTarget() with { Url = url });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Target_validation_rejects_unbounded_or_inconsistent_limits()
    {
        var target = ValidTarget() with
        {
            IntervalSeconds = 10,
            TimeoutSeconds = 60,
            ExpectedStatusMin = 500,
            ExpectedStatusMax = 200,
            ExpectedContentMarker = new string('x', WebsiteTargetValidator.MaxContentMarkerLength + 1),
            FailureConfirmationCount = 0,
            RecoveryConfirmationCount = 11
        };

        var result = WebsiteTargetValidator.Validate(target);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 6);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("ff02::1")]
    public void Default_destination_policy_blocks_non_public_addresses(string value)
    {
        Assert.True(WebsiteDestinationPolicy.IsBlockedByDefault(IPAddress.Parse(value)));
    }

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    public void Default_destination_policy_allows_public_addresses(string value)
    {
        Assert.False(WebsiteDestinationPolicy.IsBlockedByDefault(IPAddress.Parse(value)));
    }

    [Fact]
    public void Destination_set_fails_closed_when_any_resolved_address_is_blocked()
    {
        var result = WebsiteDestinationPolicy.AllAddressesAllowedByDefault([
            IPAddress.Parse("1.1.1.1"),
            IPAddress.Parse("127.0.0.1")
        ]);

        Assert.False(result);
    }

    [Fact]
    public void Classifier_identifies_dns_failure()
    {
        var result = WebsiteFailureClassifier.Classify(Evidence() with { DnsResolved = false, FailureReason = "Host not found." });

        Assert.Equal(WebsiteProbeState.Down, result.State);
        Assert.Equal("dns.failure", result.RuleId);
        Assert.Equal("high", result.Confidence);
    }

    [Fact]
    public void Classifier_identifies_connect_failure_after_dns_success()
    {
        var result = WebsiteFailureClassifier.Classify(Evidence() with { DnsResolved = true, TcpConnected = false });

        Assert.Equal("network.connect-failure", result.RuleId);
    }

    [Fact]
    public void Classifier_prefers_tls_evidence_over_generic_timeout()
    {
        var result = WebsiteFailureClassifier.Classify(Evidence() with
        {
            DnsResolved = true,
            TcpConnected = true,
            TlsValid = false,
            TimedOut = true
        });

        Assert.Equal("tls.invalid", result.RuleId);
    }

    [Theory]
    [InlineData(404, "http.4xx")]
    [InlineData(500, "http.5xx")]
    [InlineData(503, "http.5xx")]
    public void Classifier_identifies_observed_http_error_family(int status, string rule)
    {
        var result = WebsiteFailureClassifier.Classify(Evidence() with
        {
            DnsResolved = true,
            TcpConnected = true,
            TlsValid = true,
            HttpStatusCode = status,
            StatusExpected = false
        });

        Assert.Equal(WebsiteProbeState.Down, result.State);
        Assert.Equal(rule, result.RuleId);
    }

    [Fact]
    public void Classifier_identifies_content_mismatch_after_successful_http()
    {
        var result = WebsiteFailureClassifier.Classify(Evidence() with
        {
            DnsResolved = true,
            TcpConnected = true,
            TlsValid = true,
            HttpStatusCode = 200,
            StatusExpected = true,
            RedirectExpected = true,
            ContentMatched = false
        });

        Assert.Equal("content.mismatch", result.RuleId);
    }

    [Fact]
    public void Classifier_marks_expiring_certificate_degraded_not_down()
    {
        var result = WebsiteFailureClassifier.Classify(HealthyEvidence() with { CertificateExpiring = true });

        Assert.Equal(WebsiteProbeState.Degraded, result.State);
        Assert.Equal("tls.expiring", result.RuleId);
    }

    [Fact]
    public void Classifier_marks_slow_success_degraded()
    {
        var result = WebsiteFailureClassifier.Classify(HealthyEvidence() with { ElapsedMilliseconds = 4000, SlowThresholdMilliseconds = 3000 });

        Assert.Equal(WebsiteProbeState.Degraded, result.State);
        Assert.Equal("performance.slow", result.RuleId);
    }

    [Fact]
    public void Classifier_marks_satisfied_contract_up()
    {
        var result = WebsiteFailureClassifier.Classify(HealthyEvidence());

        Assert.Equal(WebsiteProbeState.Up, result.State);
        Assert.Equal("website.available", result.RuleId);
    }

    [Fact]
    public void Classifier_does_not_overclaim_when_evidence_is_insufficient()
    {
        var result = WebsiteFailureClassifier.Classify(Evidence());

        Assert.Equal(WebsiteProbeState.Unknown, result.State);
        Assert.Equal("unknown", result.RuleId);
        Assert.Equal("low", result.Confidence);
    }

    private static WebsiteTargetDefinition ValidTarget() => new(
        Guid.NewGuid(),
        "Public portal",
        "https://example.com/health",
        "production",
        IntervalSeconds: 60,
        TimeoutSeconds: 10,
        ExpectedStatusMin: 200,
        ExpectedStatusMax: 299,
        ExpectedContentMarker: "healthy",
        SlowThresholdMilliseconds: 3000,
        FailureConfirmationCount: 3,
        RecoveryConfirmationCount: 2,
        NotificationGroupIds: ["web-team", "network-oncall"]);

    private static WebsiteProbeEvidence Evidence() => new(
        DnsResolved: null,
        TcpConnected: null,
        TlsValid: null,
        TimedOut: null,
        HttpStatusCode: null,
        StatusExpected: null,
        RedirectExpected: null,
        ContentMatched: null,
        CertificateExpiring: null,
        ElapsedMilliseconds: null,
        SlowThresholdMilliseconds: 3000);

    private static WebsiteProbeEvidence HealthyEvidence() => new(
        DnsResolved: true,
        TcpConnected: true,
        TlsValid: true,
        TimedOut: false,
        HttpStatusCode: 200,
        StatusExpected: true,
        RedirectExpected: true,
        ContentMatched: true,
        CertificateExpiring: false,
        ElapsedMilliseconds: 250,
        SlowThresholdMilliseconds: 3000);
}
