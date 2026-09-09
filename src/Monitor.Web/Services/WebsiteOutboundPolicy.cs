using System.Net;

namespace Monitor.Web.Services;

public sealed class WebsiteOutboundPolicyOptions
{
    public const string SectionName = "WebsiteOutboundPolicy";
    public string[] AllowedPrivateHosts { get; set; } = [];

    public void Validate()
    {
        if (AllowedPrivateHosts.Length > 200)
            throw new InvalidOperationException("WebsiteOutboundPolicy:AllowedPrivateHosts may contain at most 200 hosts.");
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in AllowedPrivateHosts)
        {
            var host = raw?.Trim() ?? string.Empty;
            if (host.Length is < 1 or > 253 || host.Any(char.IsWhiteSpace) || host.Contains('/') || host.Contains("*", StringComparison.Ordinal))
                throw new InvalidOperationException("WebsiteOutboundPolicy:AllowedPrivateHosts contains an invalid exact host.");
            if (!normalized.Add(host))
                throw new InvalidOperationException("WebsiteOutboundPolicy:AllowedPrivateHosts must not contain duplicates.");
        }
    }

    public bool IsExplicitlyAllowedPrivateHost(string host) =>
        AllowedPrivateHosts.Any(value => string.Equals(value.Trim(), host, StringComparison.OrdinalIgnoreCase));
}

public sealed class ConfiguredWebsiteDestinationAuthorizer(WebsiteOutboundPolicyOptions options) : IWebsiteDestinationAuthorizer
{
    public bool IsAllowed(string host, IReadOnlyList<IPAddress> addresses)
    {
        if (string.IsNullOrWhiteSpace(host) || addresses is null || addresses.Count == 0) return false;
        if (WebsiteDestinationPolicy.AllAddressesAllowedByDefault(addresses)) return true;
        if (!options.IsExplicitlyAllowedPrivateHost(host)) return false;

        // Explicit allowlisting may admit private/internal address space, but never loopback,
        // link-local/metadata, multicast, unspecified or reserved/broadcast destinations.
        return addresses.All(address => !WebsiteDestinationPolicy.IsAlwaysBlocked(address));
    }
}
