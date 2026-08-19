using System.Net;
using Monitor.Web.Services;

namespace Monitor.Web.Tests;

public sealed class WebsiteOutboundPolicyTests
{
    [Theory]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.10.20")]
    [InlineData("192.168.1.10")]
    [InlineData("100.64.0.1")]
    [InlineData("198.18.1.1")]
    [InlineData("fd00::10")]
    public void Default_policy_blocks_private_or_internal_ranges(string rawAddress)
    {
        Assert.True(WebsiteDestinationPolicy.IsBlockedByDefault(IPAddress.Parse(rawAddress)));
    }

    [Fact]
    public void Exact_allowlisted_host_can_resolve_to_private_address()
    {
        var options = new WebsiteOutboundPolicyOptions { AllowedPrivateHosts = ["portal.internal.example"] };
        options.Validate();
        var authorizer = new ConfiguredWebsiteDestinationAuthorizer(options);

        Assert.True(authorizer.IsAllowed("portal.internal.example", [IPAddress.Parse("10.20.30.40")]));
        Assert.False(authorizer.IsAllowed("other.internal.example", [IPAddress.Parse("10.20.30.40")]));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.100.100.200")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    public void Allowlisted_host_still_cannot_reach_always_blocked_destinations(string rawAddress)
    {
        var options = new WebsiteOutboundPolicyOptions { AllowedPrivateHosts = ["portal.internal.example"] };
        var authorizer = new ConfiguredWebsiteDestinationAuthorizer(options);

        Assert.False(authorizer.IsAllowed("portal.internal.example", [IPAddress.Parse(rawAddress)]));
    }

    [Fact]
    public void Mixed_private_and_metadata_answer_fails_closed_even_for_allowlisted_host()
    {
        var options = new WebsiteOutboundPolicyOptions { AllowedPrivateHosts = ["portal.internal.example"] };
        var authorizer = new ConfiguredWebsiteDestinationAuthorizer(options);

        Assert.False(authorizer.IsAllowed("portal.internal.example", [
            IPAddress.Parse("10.20.30.40"),
            IPAddress.Parse("169.254.169.254")
        ]));
    }

    [Fact]
    public void Public_host_does_not_require_allowlist()
    {
        var authorizer = new ConfiguredWebsiteDestinationAuthorizer(new WebsiteOutboundPolicyOptions());

        Assert.True(authorizer.IsAllowed("example.com", [IPAddress.Parse("1.1.1.1")]));
    }

    [Fact]
    public void Wildcards_are_rejected_to_keep_private_access_explicit()
    {
        var options = new WebsiteOutboundPolicyOptions { AllowedPrivateHosts = ["*.internal.example"] };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
