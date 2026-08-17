using Microsoft.AspNetCore.Http;

namespace Monitor.Web.Services;

public enum EnterpriseDownloadSubject
{
    Servers,
    Incidents,
    History,
    FleetDecisionSupport,
    MaintenanceDecisionSupport,
    ServerIntelligence,
    DatabaseHealth,
    Audit,
    Manifest,
    Diagnostics
}

public static class EnterpriseSecurityPolicy
{
    public const int MaxEnterpriseTextBudget = 4096;
    public const int MaxDownloadFileNameLength = 96;

    public static string SafeDownloadFileName(EnterpriseDownloadSubject subject, DateTimeOffset now, string extension)
    {
        var stem = subject.ToString().ToLowerInvariant();
        var safeExtension = extension.ToLowerInvariant() switch
        {
            "csv" => "csv",
            "json" => "json",
            "zip" => "zip",
            _ => throw new ArgumentException("Download extension is not allowed.", nameof(extension))
        };
        var name = $"monitor-{stem}-{now:yyyyMMdd-HHmmss}.{safeExtension}";
        if (name.Length > MaxDownloadFileNameLength) throw new InvalidOperationException("Download filename exceeds the supported bound.");
        return name;
    }

    public static void ApplySecureDownloadHeaders(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.CacheControl = "no-store, max-age=0";
        response.Headers.Pragma = "no-cache";
        response.Headers["X-Download-Options"] = "noopen";
        response.Headers["X-Content-Type-Options"] = "nosniff";
    }

    public static void ValidateEnterpriseTextBudget(params string?[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var total = values.Where(value => value is not null).Sum(value => value!.Length);
        if (total > MaxEnterpriseTextBudget) throw new ArgumentException("Enterprise text input exceeds the bounded request budget.", nameof(values));
    }

    public static string NormalizeIncidentRouteId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Incident route ID is required.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > 180 || normalized.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is ':' or '.' or '_' or '-')))
            throw new ArgumentException("Incident route ID is invalid.", nameof(value));
        return normalized;
    }

    public static bool IsSafeZipEntryName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 80) return false;
        if (value.Contains("..", StringComparison.Ordinal) || value.Contains('/') || value.Contains('\\')) return false;
        return value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
    }
}
