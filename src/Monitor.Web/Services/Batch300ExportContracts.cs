using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Monitor.Web.Services;

public sealed record Batch300Manifest(string Schema, int MaxRows, int MaxBytes, string Encoding, string LineEndings, string ChecksumAlgorithm);

public static class Batch300ExportContracts
{
    public const string SchemaVersion = "monitor-b300-v1";
    public const int MaxRows = 2000;
    public const int MaxBytes = 2 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static int ClampRowCount(int requested) => Math.Clamp(requested, 1, MaxRows);

    public static string NormalizeLineEndings(string? value) => (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    public static string EscapeCsv(string? value)
    {
        var normalized = Batch300OperatorSafety.FormulaSafeCell(NormalizeLineEndings(value).Replace('\n', ' '));
        return '"' + normalized.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }

    public static byte[] Csv(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);
        if (headers.Count is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(headers));
        var builder = new StringBuilder();
        builder.Append("#schema,").Append(SchemaVersion).Append('\n');
        builder.Append(string.Join(',', headers.Select(EscapeCsv))).Append('\n');
        var count = 0;
        foreach (var row in rows)
        {
            if (count++ >= MaxRows) break;
            if (row.Count != headers.Count) throw new InvalidDataException("CSV row width does not match schema.");
            builder.Append(string.Join(',', row.Select(EscapeCsv))).Append('\n');
            if (Utf8.GetByteCount(builder.ToString()) > MaxBytes) throw new InvalidOperationException("Export exceeds maximum size.");
        }
        var body = Utf8.GetBytes(builder.ToString());
        var preamble = Encoding.UTF8.GetPreamble();
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        if (bytes.Length > MaxBytes) throw new InvalidOperationException("Export exceeds maximum size.");
        return bytes;
    }

    public static string Checksum(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static Batch300Manifest Manifest() => new(SchemaVersion, MaxRows, MaxBytes, "UTF-8 BOM", "LF", "SHA-256");

    public static byte[] ManifestJson() => JsonSerializer.SerializeToUtf8Bytes(Manifest(), new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static string SafeDownloadName(string subject, DateTimeOffset atUtc, string extension = "csv")
    {
        var safeSubject = Batch300OperatorSafety.SafeFileName(subject, "export");
        var safeExtension = Batch300OperatorSafety.SafeFileName(extension, "csv").TrimStart('.');
        return $"monitor-{safeSubject}-{atUtc:yyyyMMdd-HHmmss}.{safeExtension}";
    }

    public static string[] DeterministicSort(IEnumerable<string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Select(value => Batch300OperatorSafety.NormalizeText(value, 200)).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ThenBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public static byte[] BoundedJson<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (bytes.Length > MaxBytes) throw new InvalidOperationException("JSON export exceeds maximum size.");
        return bytes;
    }
}
