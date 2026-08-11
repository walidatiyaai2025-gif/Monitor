using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Monitor.Web.Services;

public static partial class Batch300OperatorSafety
{
    [GeneratedRegex("(?i)(password\\s*=|pwd\\s*=|user id\\s*=|uid\\s*=|server\\s*=|data source\\s*=|accountkey\\s*=|secret\\s*=|token\\s*=)")]
    private static partial Regex SecretShapeRegex();

    public static string NormalizeText(string? value, int maxLength = 500)
    {
        if (maxLength is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(maxLength));
        var source = (value ?? string.Empty).Trim();
        var normalized = new string(source.Select(character => char.IsControl(character) ? ' ' : character).ToArray());
        normalized = string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    public static bool LooksSecretBearing(string? value) => !string.IsNullOrWhiteSpace(value) && SecretShapeRegex().IsMatch(value);

    public static string SafeNote(string? value, int maxLength = 500)
    {
        var normalized = NormalizeText(value, maxLength);
        if (normalized.Length == 0) throw new ArgumentException("Operator note is required.", nameof(value));
        if (LooksSecretBearing(normalized)) throw new ArgumentException("Operator note contains credential or connection-shaped material.", nameof(value));
        return normalized;
    }

    public static bool IsSafeRouteId(string? value, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength) return false;
        return value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    }

    public static string SafeFileName(string? value, string fallback = "monitor")
    {
        var source = (value ?? string.Empty).Trim();
        var safe = new string(source.Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.').ToArray());
        if (safe.Length == 0) safe = fallback;
        safe = safe.Trim('.');
        return safe.Length == 0 ? fallback : safe[..Math.Min(safe.Length, 96)];
    }

    public static string FormulaSafeCell(string? value)
    {
        var normalized = NormalizeText(value, 500);
        if (normalized.Length > 0 && normalized[0] is '=' or '+' or '-' or '@') return "'" + normalized;
        return normalized;
    }

    public static string CorrelationId(string? proposed)
    {
        if (IsSafeRouteId(proposed, 64)) return proposed!;
        return Guid.NewGuid().ToString("N");
    }

    public static string Fingerprint(string? value)
    {
        var normalized = NormalizeText(value, 1024).ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash)[..24].ToLowerInvariant();
    }

    public static bool IsAllowedDiagnosticsEntry(string? value) => value is "manifest.json" or "README.txt" or "status.json";

    public static string RedactValue(string? key, string? value)
    {
        var normalizedKey = (key ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedKey.Contains("password", StringComparison.Ordinal) || normalizedKey.Contains("secret", StringComparison.Ordinal) || normalizedKey.Contains("token", StringComparison.Ordinal) || normalizedKey.Contains("connection", StringComparison.Ordinal)) return "[redacted]";
        var normalizedValue = NormalizeText(value, 200);
        return LooksSecretBearing(normalizedValue) ? "[redacted]" : normalizedValue;
    }
}
