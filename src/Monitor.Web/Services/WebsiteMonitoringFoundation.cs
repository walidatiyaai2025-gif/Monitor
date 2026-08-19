using System.Net;

namespace Monitor.Web.Services;

public enum WebsiteProbeState
{
    Unknown,
    Up,
    Degraded,
    Down
}

public sealed record WebsiteTargetDefinition(
    Guid Id,
    string Name,
    string Url,
    string Environment,
    bool IsEnabled = true,
    int IntervalSeconds = 60,
    int TimeoutSeconds = 15,
    int ExpectedStatusMin = 200,
    int ExpectedStatusMax = 399,
    string? ExpectedContentMarker = null,
    bool FollowRedirects = true,
    string? ExpectedFinalHost = null,
    int SlowThresholdMilliseconds = 3000,
    int FailureConfirmationCount = 3,
    int RecoveryConfirmationCount = 2,
    IReadOnlyList<string>? NotificationGroupIds = null);

public sealed record WebsiteTargetValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static WebsiteTargetValidationResult Success { get; } = new(true, Array.Empty<string>());
}

public static class WebsiteTargetValidator
{
    public const int MaxNameLength = 120;
    public const int MaxUrlLength = 2048;
    public const int MaxContentMarkerLength = 256;
    public const int MaxNotificationGroups = 16;

    public static WebsiteTargetValidationResult Validate(WebsiteTargetDefinition target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var errors = new List<string>();

        if (target.Id == Guid.Empty) errors.Add("Target id is required.");

        var name = target.Name?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > MaxNameLength) errors.Add($"Name must contain 1-{MaxNameLength} characters.");

        var rawUrl = target.Url?.Trim() ?? string.Empty;
        if (rawUrl.Length is < 1 or > MaxUrlLength)
        {
            errors.Add($"URL must contain 1-{MaxUrlLength} characters.");
        }
        else if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
        {
            errors.Add("URL must be an absolute URI.");
        }
        else
        {
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Only HTTP and HTTPS URLs are allowed.");
            }

            if (!string.IsNullOrEmpty(uri.UserInfo)) errors.Add("Embedded URL credentials are not allowed.");
            if (string.IsNullOrWhiteSpace(uri.Host)) errors.Add("URL host is required.");
            if (uri.Port is < 1 or > 65535) errors.Add("URL port is outside the valid range.");
        }

        if (target.IntervalSeconds is < 15 or > 86400) errors.Add("Check interval must be between 15 seconds and 24 hours.");
        if (target.TimeoutSeconds is < 1 or > 60) errors.Add("Timeout must be between 1 and 60 seconds.");
        if (target.TimeoutSeconds >= target.IntervalSeconds) errors.Add("Timeout must be shorter than the check interval.");
        if (target.ExpectedStatusMin is < 100 or > 599) errors.Add("Expected minimum HTTP status must be between 100 and 599.");
        if (target.ExpectedStatusMax is < 100 or > 599) errors.Add("Expected maximum HTTP status must be between 100 and 599.");
        if (target.ExpectedStatusMin > target.ExpectedStatusMax) errors.Add("Expected HTTP status range is invalid.");
        if (target.ExpectedContentMarker is { Length: > MaxContentMarkerLength }) errors.Add($"Expected content marker must not exceed {MaxContentMarkerLength} characters.");
        if (target.SlowThresholdMilliseconds is < 100 or > 120000) errors.Add("Slow-response threshold must be between 100 ms and 120 seconds.");
        if (target.FailureConfirmationCount is < 1 or > 10) errors.Add("Failure confirmation count must be between 1 and 10.");
        if (target.RecoveryConfirmationCount is < 1 or > 10) errors.Add("Recovery confirmation count must be between 1 and 10.");

        var expectedFinalHost = target.ExpectedFinalHost?.Trim();
        if (!string.IsNullOrEmpty(expectedFinalHost) && (expectedFinalHost.Length > 253 || expectedFinalHost.Any(char.IsWhiteSpace)))
            errors.Add("Expected final host is invalid.");

        var groups = target.NotificationGroupIds ?? Array.Empty<string>();
        if (groups.Count > MaxNotificationGroups) errors.Add($"A target may reference at most {MaxNotificationGroups} notification groups.");
        if (groups.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 80)) errors.Add("Notification group ids must contain 1-80 characters.");
        if (groups.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != groups.Count)
            errors.Add("Notification group ids must be unique.");

        return errors.Count == 0 ? WebsiteTargetValidationResult.Success : new(false, errors);
    }
}

public static class WebsiteDestinationPolicy
{
    public static bool IsBlockedByDefault(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (IPAddress.IsLoopback(address)) return true;

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            if (bytes.SequenceEqual(new byte[] { 0, 0, 0, 0 })) return true;
            if (bytes[0] == 10) return true;
            if (bytes[0] == 127) return true;
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            if (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) return true;
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            if (bytes[0] is >= 224 and <= 239) return true;
            if (bytes[0] >= 240) return true;
            return false;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6None) || address.Equals(IPAddress.IPv6Loopback)) return true;
            var bytes = address.GetAddressBytes();
            if (bytes[0] == 0xff) return true; // multicast
            if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80) return true; // link local fe80::/10
            if ((bytes[0] & 0xfe) == 0xfc) return true; // unique local fc00::/7
            return false;
        }

        return true;
    }

    public static bool AllAddressesAllowedByDefault(IEnumerable<IPAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var materialized = addresses.ToArray();
        return materialized.Length > 0 && materialized.All(address => !IsBlockedByDefault(address));
    }
}

public sealed record WebsiteProbeEvidence(
    bool? DnsResolved,
    bool? TcpConnected,
    bool? TlsValid,
    bool? TimedOut,
    int? HttpStatusCode,
    bool? StatusExpected,
    bool? RedirectExpected,
    bool? ContentMatched,
    bool? CertificateExpiring,
    long? ElapsedMilliseconds,
    long SlowThresholdMilliseconds,
    string? FailureReason = null);

public sealed record WebsiteProbeClassification(
    WebsiteProbeState State,
    string RuleId,
    string ProbableLayer,
    string Confidence,
    string EvidenceSummary);

public static class WebsiteFailureClassifier
{
    public static WebsiteProbeClassification Classify(WebsiteProbeEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (evidence.DnsResolved == false)
            return Down("dns.failure", "DNS / name resolution", "high", evidence.FailureReason ?? "The target hostname did not resolve.");

        if (evidence.DnsResolved == true && evidence.TcpConnected == false)
            return Down("network.connect-failure", "Network / listener path", "high", evidence.FailureReason ?? "DNS resolved, but the TCP connection did not succeed.");

        if (evidence.TlsValid == false)
            return Down("tls.invalid", "TLS / certificate", "high", evidence.FailureReason ?? "The HTTPS TLS or certificate validation failed.");

        if (evidence.HttpStatusCode is >= 500 and <= 599)
            return Down("http.5xx", "Web server / proxy / application", "high", $"HTTP {evidence.HttpStatusCode} was returned by the target path.");

        if (evidence.HttpStatusCode is >= 400 and <= 499)
            return Down("http.4xx", "HTTP / application / authentication / routing", "high", $"HTTP {evidence.HttpStatusCode} was returned by the target path.");

        if (evidence.TimedOut == true)
            return Down("network.timeout", "Network / proxy / application unknown", "medium", evidence.FailureReason ?? "The bounded connection/request operation timed out without stronger failure evidence.");

        if (evidence.RedirectExpected == false)
            return Down("redirect.unexpected", "HTTP / proxy / routing", "high", evidence.FailureReason ?? "The redirect/final-host contract was not satisfied.");

        if (evidence.StatusExpected == false && evidence.HttpStatusCode is not null)
            return Down("http.unexpected-status", "HTTP / application contract", "high", $"HTTP {evidence.HttpStatusCode} was outside the configured expected range.");

        if (evidence.ContentMatched == false)
            return Down("content.mismatch", "Application / content", "high", evidence.FailureReason ?? "The configured bounded content marker was not present.");

        if (evidence.CertificateExpiring == true)
            return Degraded("tls.expiring", "Certificate lifecycle", "high", evidence.FailureReason ?? "The certificate is valid but inside the configured expiry warning window.");

        if (evidence.ElapsedMilliseconds is long elapsed && elapsed > evidence.SlowThresholdMilliseconds)
            return Degraded("performance.slow", "Performance path", "medium", $"Successful response took {elapsed} ms, above the {evidence.SlowThresholdMilliseconds} ms threshold.");

        if (evidence.DnsResolved == true && evidence.TcpConnected == true &&
            evidence.TlsValid is not false && evidence.HttpStatusCode is >= 100 and <= 599 &&
            evidence.StatusExpected is not false && evidence.RedirectExpected is not false && evidence.ContentMatched is not false)
        {
            return new(WebsiteProbeState.Up, "website.available", "End-to-end HTTP path", "high", "The configured website contract was satisfied.");
        }

        return new(WebsiteProbeState.Unknown, "unknown", "Unknown", "low", evidence.FailureReason ?? "Collected evidence is insufficient for a narrower classification.");
    }

    private static WebsiteProbeClassification Down(string ruleId, string layer, string confidence, string summary) =>
        new(WebsiteProbeState.Down, ruleId, layer, confidence, Bound(summary));

    private static WebsiteProbeClassification Degraded(string ruleId, string layer, string confidence, string summary) =>
        new(WebsiteProbeState.Degraded, ruleId, layer, confidence, Bound(summary));

    private static string Bound(string value) => value.Length <= 500 ? value : value[..500];
}
