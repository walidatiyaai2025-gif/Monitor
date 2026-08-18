using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.WebUtilities;

namespace Monitor.Web.Services;

public sealed class WebSecurityOptions
{
    public const string SectionName = "WebSecurity";

    public int SessionIdleMinutes { get; set; } = 30;
    public int SessionAbsoluteHours { get; set; } = 8;
    public int HstsDays { get; set; } = 365;
    public bool HstsIncludeSubDomains { get; set; } = true;
    public bool HstsPreload { get; set; }
    public string[] TrustedProxies { get; set; } = [];
    public string[] TrustedNetworks { get; set; } = [];

    public bool HasTrustedForwarders => TrustedProxies.Length > 0 || TrustedNetworks.Length > 0;

    public void Validate()
    {
        if (SessionIdleMinutes is < 5 or > 240)
            throw new InvalidOperationException("WebSecurity:SessionIdleMinutes must be between 5 and 240.");
        if (SessionAbsoluteHours is < 1 or > 24)
            throw new InvalidOperationException("WebSecurity:SessionAbsoluteHours must be between 1 and 24.");
        if (TimeSpan.FromMinutes(SessionIdleMinutes) >= TimeSpan.FromHours(SessionAbsoluteHours))
            throw new InvalidOperationException("WebSecurity session idle lifetime must be shorter than the absolute lifetime.");
        if (HstsDays is < 180 or > 730)
            throw new InvalidOperationException("WebSecurity:HstsDays must be between 180 and 730.");

        TrustedForwarderPolicy.Validate(this);
    }
}

public static class MonitorClaimTypes
{
    public const string SessionStartedUtc = "monitor:session-started-utc";
}

public sealed class AbsoluteSessionCookieEvents(WebSecurityOptions options, TimeProvider timeProvider) : CookieAuthenticationEvents
{
    public override Task SigningIn(CookieSigningInContext context)
    {
        if (context.Principal?.Identity is ClaimsIdentity identity &&
            !context.Principal.HasClaim(claim => claim.Type == MonitorClaimTypes.SessionStartedUtc))
        {
            identity.AddClaim(new Claim(
                MonitorClaimTypes.SessionStartedUtc,
                timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
        }

        return Task.CompletedTask;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!IsExpired(context.Principal, timeProvider.GetUtcNow(), options))
        {
            return;
        }

        context.RejectPrincipal();
        context.ShouldRenew = false;
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsAjaxRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        return base.RedirectToLogin(context);
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsAjaxRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return base.RedirectToAccessDenied(context);
    }

    internal static bool IsAjaxRequest(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return string.Equals(
            request.Headers["X-Requested-With"].ToString(),
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsExpired(ClaimsPrincipal? principal, DateTimeOffset now, WebSecurityOptions options)
    {
        var value = principal?.FindFirstValue(MonitorClaimTypes.SessionStartedUtc);
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            return true;

        DateTimeOffset started;
        try
        {
            started = DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return true;
        }

        if (started > now.AddMinutes(1))
            return true;

        return now - started >= TimeSpan.FromHours(options.SessionAbsoluteHours);
    }
}

public static class LoginAttemptKey
{
    internal static string Create(IPAddress? address, string? username)
    {
        var normalizedAddress = address?.ToString() ?? "unknown";
        var normalizedUsername = username?.Trim().ToUpperInvariant() ?? string.Empty;
        var material = Encoding.UTF8.GetBytes($"{normalizedAddress}\n{normalizedUsername}");
        var hash = SHA256.HashData(material);
        return $"login:v1:{Convert.ToHexString(hash)}";
    }
}

public static class SecurityInput
{
    public static string NormalizeAuditField(string? value, int maxLength)
    {
        if (maxLength < 1) throw new ArgumentOutOfRangeException(nameof(maxLength));
        var normalized = (value ?? string.Empty).Trim();
        if (LooksSecretBearing(normalized)) return "[redacted]";

        var buffer = normalized.Select(character => char.IsControl(character) ? ' ' : character).ToArray();
        normalized = new string(buffer).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    public static string? NormalizeOptionalToken(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength) return null;
        return normalized.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')
            ? normalized
            : null;
    }

    internal static bool LooksSecretBearing(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        string[] markers = [
            "password=", "pwd=", "user id=", "uid=", "connection string", "data source=", "server=", "initial catalog="
        ];
        return markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}

public static class TrustedForwarderPolicy
{
    internal static void Validate(WebSecurityOptions policy)
    {
        foreach (var proxy in policy.TrustedProxies)
        {
            if (!IPAddress.TryParse(proxy?.Trim(), out _))
                throw new InvalidOperationException("WebSecurity:TrustedProxies contains an invalid IP address.");
        }

        foreach (var network in policy.TrustedNetworks)
        {
            ParseNetwork(network);
        }
    }

    public static void Configure(ForwardedHeadersOptions options, WebSecurityOptions policy)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(policy);
        if (!policy.HasTrustedForwarders)
            throw new InvalidOperationException("Forwarded headers cannot be enabled without an explicit trusted proxy or network.");

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.RequireHeaderSymmetry = true;
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();

        foreach (var proxy in policy.TrustedProxies)
            options.KnownProxies.Add(IPAddress.Parse(proxy.Trim()));

        foreach (var network in policy.TrustedNetworks)
        {
            var parsed = ParseNetwork(network);
#pragma warning disable CS0618
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(parsed.Address, parsed.PrefixLength));
#pragma warning restore CS0618
        }
    }

    private static (IPAddress Address, int PrefixLength) ParseNetwork(string? value)
    {
        var parts = (value ?? string.Empty).Trim().Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var address) || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var prefix))
            throw new InvalidOperationException("WebSecurity:TrustedNetworks must use CIDR notation.");

        var maxPrefix = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (prefix < 0 || prefix > maxPrefix)
            throw new InvalidOperationException("WebSecurity:TrustedNetworks contains an invalid CIDR prefix length.");

        return (address, prefix);
    }
}

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string NonceItemKey = "Monitor.CspNonce";

    public async Task InvokeAsync(HttpContext context)
    {
        var nonce = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
        context.Items[NonceItemKey] = nonce;

        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        context.Response.Headers["Content-Security-Policy"] =
            $"default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data:; font-src 'self'; style-src 'self'; script-src 'self' 'nonce-{nonce}'; connect-src 'self'";

        await next(context);
    }

    public static string? GetNonce(HttpContext context) => context.Items.TryGetValue(NonceItemKey, out var value) ? value as string : null;
}
