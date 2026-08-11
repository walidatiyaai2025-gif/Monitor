using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Monitor.Web.Services;

public enum SqlEditionClass
{
    Unknown,
    Express,
    Developer,
    Standard,
    Enterprise,
    Azure
}

public enum UptimeBand
{
    Unknown,
    New,
    Stable,
    LongRunning
}

public sealed record SqlVersionInfo(int Major, int Minor, int Build, int Revision)
{
    public override string ToString() => $"{Major}.{Minor}.{Build}.{Revision}";
}

public static class Batch300EstateIdentity
{
    public static string NormalizeName(string? value, int maxLength = 80)
    {
        if (maxLength is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(maxLength));
        var normalized = string.Join(' ', (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length == 0) return "Unknown";
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    public static string NormalizeTag(string? value)
    {
        var source = (value ?? string.Empty).Trim().ToLowerInvariant();
        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.') builder.Append(character);
        }
        return builder.Length == 0 ? "untagged" : builder.ToString()[..Math.Min(builder.Length, 32)];
    }

    public static SqlVersionInfo? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 2 or > 4) return null;
        var values = new int[4];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out values[index]) || values[index] < 0) return null;
        }
        return new(values[0], values[1], values[2], values[3]);
    }

    public static int? MajorVersion(string? value) => ParseVersion(value)?.Major;

    public static string VersionFamily(int major) => major switch
    {
        >= 17 => "17+",
        16 => "16",
        15 => "15",
        14 => "14",
        13 => "13",
        > 0 => "legacy",
        _ => "unknown"
    };

    public static SqlEditionClass ClassifyEdition(string? edition)
    {
        var value = (edition ?? string.Empty).Trim();
        if (value.Contains("azure", StringComparison.OrdinalIgnoreCase)) return SqlEditionClass.Azure;
        if (value.Contains("enterprise", StringComparison.OrdinalIgnoreCase)) return SqlEditionClass.Enterprise;
        if (value.Contains("standard", StringComparison.OrdinalIgnoreCase)) return SqlEditionClass.Standard;
        if (value.Contains("developer", StringComparison.OrdinalIgnoreCase)) return SqlEditionClass.Developer;
        if (value.Contains("express", StringComparison.OrdinalIgnoreCase)) return SqlEditionClass.Express;
        return SqlEditionClass.Unknown;
    }

    public static UptimeBand ClassifyUptime(long uptimeSeconds)
    {
        if (uptimeSeconds < 0) return UptimeBand.Unknown;
        if (uptimeSeconds < 3600) return UptimeBand.New;
        if (uptimeSeconds < 30L * 24 * 3600) return UptimeBand.Stable;
        return UptimeBand.LongRunning;
    }

    public static string StableId(params string?[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var canonical = string.Join('|', parts.Select(part => (part ?? string.Empty).Trim().ToLowerInvariant()));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash)[..24].ToLowerInvariant();
    }

    public static string SafeDisplayLabel(string? server, string? instance)
    {
        var serverName = NormalizeName(server, 60);
        var instanceName = string.IsNullOrWhiteSpace(instance) ? null : NormalizeName(instance, 40);
        return instanceName is null ? serverName : $"{serverName} / {instanceName}";
    }

    public static bool IsSupportedMajor(int major) => major >= 13;
}
