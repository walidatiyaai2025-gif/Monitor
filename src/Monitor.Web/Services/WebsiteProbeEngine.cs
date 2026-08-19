using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Monitor.Web.Services;

public interface IWebsiteDestinationAuthorizer
{
    bool IsAllowed(string host, IReadOnlyList<IPAddress> addresses);
}

public sealed class DefaultWebsiteDestinationAuthorizer : IWebsiteDestinationAuthorizer
{
    public bool IsAllowed(string host, IReadOnlyList<IPAddress> addresses) =>
        !string.IsNullOrWhiteSpace(host) && WebsiteDestinationPolicy.AllAddressesAllowedByDefault(addresses);
}

public interface IWebsiteDnsResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}

public sealed class SystemWebsiteDnsResolver : IWebsiteDnsResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Dns.GetHostAddressesAsync(host, cancellationToken);
}

public sealed record WebsiteHttpHopResult(
    Uri RequestUri,
    bool? DnsResolved,
    bool? DestinationAllowed,
    bool? TcpConnected,
    bool? TlsValid,
    bool TimedOut,
    int? HttpStatusCode,
    Uri? RedirectLocation,
    DateTimeOffset? CertificateNotAfterUtc,
    string? CertificateSubject,
    string? CertificateIssuer,
    long ElapsedMilliseconds,
    string? BoundedBody,
    string? FailureReason);

public interface IWebsiteHttpHopClient
{
    Task<WebsiteHttpHopResult> SendAsync(Uri uri, int maxBodyBytes, CancellationToken cancellationToken);
}

public sealed class PinnedWebsiteHttpHopClient(
    IWebsiteDnsResolver dnsResolver,
    IWebsiteDestinationAuthorizer destinationAuthorizer) : IWebsiteHttpHopClient
{
    private const int MaxResponseHeadersLengthKb = 32;

    public async Task<WebsiteHttpHopResult> SendAsync(Uri uri, int maxBodyBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (maxBodyBytes is < 0 or > WebsiteProbeEngine.MaxInspectedBodyBytes)
            throw new ArgumentOutOfRangeException(nameof(maxBodyBytes));

        var started = Stopwatch.GetTimestamp();
        var host = uri.DnsSafeHost;
        IPAddress[] addresses;
        try
        {
            addresses = await dnsResolver.ResolveAsync(host, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException)
        {
            return Failure(uri, started, dnsResolved: false, destinationAllowed: null, tcpConnected: null, tlsValid: null,
                timedOut: false, reason: Bound(exception.Message));
        }

        if (addresses.Length == 0)
        {
            return Failure(uri, started, dnsResolved: false, destinationAllowed: null, tcpConnected: null, tlsValid: null,
                timedOut: false, reason: "DNS returned no addresses.");
        }

        if (!destinationAuthorizer.IsAllowed(host, addresses))
        {
            return Failure(uri, started, dnsResolved: true, destinationAllowed: false, tcpConnected: null, tlsValid: null,
                timedOut: false, reason: "Resolved destination is blocked by Website Monitoring outbound policy.");
        }

        var tcpConnected = false;
        bool? tlsValid = uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? null : true;
        DateTimeOffset? certificateNotAfterUtc = null;
        string? certificateSubject = null;
        string? certificateIssuer = null;

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            MaxResponseHeadersLength = MaxResponseHeadersLengthKb,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ConnectCallback = async (_, token) =>
            {
                Exception? lastError = null;
                foreach (var address in addresses)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(address, uri.Port), token);
                        tcpConnected = true;
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (Exception exception) when (exception is SocketException or OperationCanceledException)
                    {
                        socket.Dispose();
                        lastError = exception;
                        if (exception is OperationCanceledException) throw;
                    }
                }

                throw new HttpRequestException("TCP connection failed for all authorized resolved addresses.", lastError);
            }
        };

        if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, certificate, _, policyErrors) =>
                {
                    tlsValid = policyErrors == SslPolicyErrors.None;
                    if (certificate is not null)
                    {
                        using var certificate2 = new X509Certificate2(certificate);
                        certificateNotAfterUtc = new DateTimeOffset(certificate2.NotAfter.ToUniversalTime());
                        certificateSubject = Bound(certificate2.Subject, 200);
                        certificateIssuer = Bound(certificate2.Issuer, 200);
                    }

                    return tlsValid == true;
                }
            };
        }

        using (handler)
        using (var client = new HttpClient(handler, disposeHandler: false))
        using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
        {
            request.Headers.UserAgent.ParseAdd("Monitor-WebsiteProbe/1.0");
            request.Headers.Accept.ParseAdd("text/html,application/json,text/plain;q=0.9,*/*;q=0.1");

            try
            {
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var body = maxBodyBytes == 0
                    ? null
                    : await ReadBoundedBodyAsync(response, maxBodyBytes, cancellationToken);
                var redirect = ResolveRedirect(uri, response.Headers.Location);

                return new WebsiteHttpHopResult(
                    uri,
                    DnsResolved: true,
                    DestinationAllowed: true,
                    TcpConnected: tcpConnected,
                    TlsValid: tlsValid,
                    TimedOut: false,
                    HttpStatusCode: (int)response.StatusCode,
                    RedirectLocation: redirect,
                    CertificateNotAfterUtc: certificateNotAfterUtc,
                    CertificateSubject: certificateSubject,
                    CertificateIssuer: certificateIssuer,
                    ElapsedMilliseconds: ElapsedMilliseconds(started),
                    BoundedBody: body,
                    FailureReason: null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Failure(uri, started, dnsResolved: true, destinationAllowed: true, tcpConnected,
                    tlsValid, timedOut: true, reason: "The bounded website probe timed out or was cancelled.");
            }
            catch (HttpRequestException exception)
            {
                if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) && tcpConnected &&
                    (tlsValid == false || exception.InnerException is AuthenticationException))
                {
                    tlsValid = false;
                }

                return Failure(uri, started, dnsResolved: true, destinationAllowed: true, tcpConnected,
                    tlsValid, timedOut: false, reason: Bound(exception.Message));
            }
        }
    }

    private static async Task<string> ReadBoundedBodyAsync(HttpResponseMessage response, int maxBodyBytes, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[Math.Min(maxBodyBytes, 8192)];
        using var memory = new MemoryStream(Math.Min(maxBodyBytes, 65536));
        var remaining = maxBodyBytes;
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken);
            if (read == 0) break;
            memory.Write(buffer, 0, read);
            remaining -= read;
        }

        var encoding = ResolveEncoding(response.Content.Headers.ContentType?.CharSet);
        return encoding.GetString(memory.ToArray());
    }

    private static Encoding ResolveEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset)) return Encoding.UTF8;
        try { return Encoding.GetEncoding(charset.Trim(' ', '"', '\'')); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException) { return Encoding.UTF8; }
    }

    private static Uri? ResolveRedirect(Uri current, Uri? location)
    {
        if (location is null) return null;
        return location.IsAbsoluteUri ? location : new Uri(current, location);
    }

    private static WebsiteHttpHopResult Failure(
        Uri uri,
        long started,
        bool? dnsResolved,
        bool? destinationAllowed,
        bool? tcpConnected,
        bool? tlsValid,
        bool timedOut,
        string reason) =>
        new(uri, dnsResolved, destinationAllowed, tcpConnected, tlsValid, timedOut, null, null, null, null, null,
            ElapsedMilliseconds(started), null, Bound(reason));

    private static long ElapsedMilliseconds(long started) =>
        (long)Math.Max(0, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

    private static string Bound(string value, int max = 300) =>
        value.Length <= max ? value : value[..max];
}

public sealed record WebsiteProbeResult(
    Guid TargetId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    Uri OriginalUri,
    Uri FinalUri,
    int RedirectCount,
    WebsiteProbeEvidence Evidence,
    WebsiteProbeClassification Classification,
    DateTimeOffset? CertificateNotAfterUtc,
    string? CertificateSubject,
    string? CertificateIssuer);

public interface IWebsiteProbeEngine
{
    Task<WebsiteProbeResult> ProbeAsync(WebsiteTargetDefinition target, CancellationToken cancellationToken);
}

public sealed class WebsiteProbeEngine(IWebsiteHttpHopClient hopClient, TimeProvider timeProvider) : IWebsiteProbeEngine
{
    public const int MaxRedirects = 5;
    public const int MaxInspectedBodyBytes = 64 * 1024;
    public const int CertificateExpiryWarningDays = 30;

    public async Task<WebsiteProbeResult> ProbeAsync(WebsiteTargetDefinition target, CancellationToken cancellationToken)
    {
        var validation = WebsiteTargetValidator.Validate(target);
        if (!validation.IsValid)
            throw new ArgumentException(string.Join(" ", validation.Errors), nameof(target));

        var original = new Uri(target.Url, UriKind.Absolute);
        var current = original;
        var startedAt = timeProvider.GetUtcNow();
        var redirectCount = 0;
        WebsiteHttpHopResult? hop = null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(target.TimeoutSeconds));

        while (true)
        {
            if (!IsSafeHttpUri(current))
            {
                return BuildPolicyResult(target, original, current, redirectCount, startedAt,
                    "Redirect destination is not an allowed absolute HTTP/HTTPS URI.");
            }

            hop = await hopClient.SendAsync(current,
                string.IsNullOrEmpty(target.ExpectedContentMarker) ? 0 : MaxInspectedBodyBytes,
                timeout.Token);

            if (hop.DestinationAllowed == false)
            {
                return BuildPolicyResult(target, original, current, redirectCount, startedAt,
                    hop.FailureReason ?? "Destination was blocked by outbound policy.");
            }

            var status = hop.HttpStatusCode;
            var isRedirect = status is >= 300 and <= 399 && hop.RedirectLocation is not null;
            if (!isRedirect || !target.FollowRedirects)
                break;

            redirectCount++;
            if (redirectCount > MaxRedirects)
            {
                return BuildResult(target, original, current, redirectCount, startedAt, hop,
                    redirectExpected: false, contentMatched: null,
                    failureReason: $"Redirect count exceeded the bounded limit of {MaxRedirects}.");
            }

            current = hop.RedirectLocation!;
        }

        if (hop is null)
            throw new InvalidOperationException("Website probe produced no hop evidence.");

        var redirectExpected = string.IsNullOrWhiteSpace(target.ExpectedFinalHost) ||
            string.Equals(current.DnsSafeHost, target.ExpectedFinalHost.Trim(), StringComparison.OrdinalIgnoreCase);
        bool? contentMatched = string.IsNullOrEmpty(target.ExpectedContentMarker)
            ? null
            : hop.BoundedBody?.Contains(target.ExpectedContentMarker, StringComparison.Ordinal) == true;

        return BuildResult(target, original, current, redirectCount, startedAt, hop,
            redirectExpected, contentMatched, hop.FailureReason);
    }

    private WebsiteProbeResult BuildResult(
        WebsiteTargetDefinition target,
        Uri original,
        Uri final,
        int redirectCount,
        DateTimeOffset startedAt,
        WebsiteHttpHopResult hop,
        bool? redirectExpected,
        bool? contentMatched,
        string? failureReason)
    {
        var completedAt = timeProvider.GetUtcNow();
        var totalElapsed = Math.Max(0L, (long)(completedAt - startedAt).TotalMilliseconds);
        if (totalElapsed == 0) totalElapsed = hop.ElapsedMilliseconds;

        var certificateExpiring = hop.CertificateNotAfterUtc is DateTimeOffset notAfter &&
            notAfter <= completedAt.AddDays(CertificateExpiryWarningDays);
        bool? statusExpected = hop.HttpStatusCode is int status
            ? status >= target.ExpectedStatusMin && status <= target.ExpectedStatusMax
            : null;

        var evidence = new WebsiteProbeEvidence(
            hop.DnsResolved,
            hop.TcpConnected,
            hop.TlsValid,
            hop.TimedOut,
            hop.HttpStatusCode,
            statusExpected,
            redirectExpected,
            contentMatched,
            certificateExpiring,
            totalElapsed,
            target.SlowThresholdMilliseconds,
            failureReason);

        return new WebsiteProbeResult(target.Id, startedAt, completedAt, original, final, redirectCount, evidence,
            WebsiteFailureClassifier.Classify(evidence), hop.CertificateNotAfterUtc, hop.CertificateSubject, hop.CertificateIssuer);
    }

    private WebsiteProbeResult BuildPolicyResult(
        WebsiteTargetDefinition target,
        Uri original,
        Uri final,
        int redirectCount,
        DateTimeOffset startedAt,
        string reason)
    {
        var completedAt = timeProvider.GetUtcNow();
        var evidence = new WebsiteProbeEvidence(
            DnsResolved: null,
            TcpConnected: null,
            TlsValid: null,
            TimedOut: false,
            HttpStatusCode: null,
            StatusExpected: null,
            RedirectExpected: false,
            ContentMatched: null,
            CertificateExpiring: null,
            ElapsedMilliseconds: Math.Max(0L, (long)(completedAt - startedAt).TotalMilliseconds),
            SlowThresholdMilliseconds: target.SlowThresholdMilliseconds,
            FailureReason: reason);
        var classification = new WebsiteProbeClassification(
            WebsiteProbeState.Unknown,
            "destination.blocked",
            "Monitoring outbound policy",
            "high",
            reason.Length <= 500 ? reason : reason[..500]);
        return new WebsiteProbeResult(target.Id, startedAt, completedAt, original, final, redirectCount,
            evidence, classification, null, null, null);
    }

    private static bool IsSafeHttpUri(Uri uri) =>
        uri.IsAbsoluteUri &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        !string.IsNullOrWhiteSpace(uri.Host) &&
        (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
         uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
}
