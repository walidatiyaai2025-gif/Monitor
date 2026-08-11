using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record EnterpriseServerReportFilter(ServerEnvironmentClass? Environment = null, string? Group = null, string? Tag = null);
public sealed record EnterpriseIncidentReportFilter(string? Assignee = null, bool? Suppressed = null);
public sealed record DiagnosticsBuildManifest(string SchemaVersion, string Product, string Version, string Revision, DateTimeOffset GeneratedAtUtc);

public static class EnterpriseReportContract
{
    public const string SchemaVersion = "monitor-export-v2";
    public const int MaxRows = 1000;
    public const int MaxBytes = 1024 * 1024;
    public const int MaxCellLength = 500;
    private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);

    public static byte[] Csv(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);
        if (headers.Count == 0 || headers.Count > 32) throw new ArgumentException("CSV header count is outside the supported range.", nameof(headers));

        var builder = new StringBuilder();
        builder.Append("#schema,").Append(SchemaVersion).Append('\n');
        builder.Append(string.Join(',', headers.Select(EscapeCell))).Append('\n');
        var count = 0;
        foreach (var row in rows)
        {
            if (count++ >= MaxRows) break;
            if (row.Count != headers.Count) throw new InvalidDataException("CSV row width does not match the versioned schema.");
            builder.Append(string.Join(',', row.Select(value => EscapeCell(value ?? string.Empty)))).Append('\n');
            if (Utf8Bom.GetByteCount(builder.ToString()) > MaxBytes) throw new InvalidOperationException("CSV export exceeded the bounded size.");
        }

        var bytes = Utf8Bom.GetBytes(builder.ToString());
        if (bytes.Length > MaxBytes) throw new InvalidOperationException("CSV export exceeded the bounded size.");
        return bytes;
    }

    public static string EscapeCell(string value)
    {
        value ??= string.Empty;
        var normalized = new string(value.Select(character => char.IsControl(character) && character is not '\t' and not '\r' ? ' ' : character).ToArray()).Trim();
        if (normalized.Length > MaxCellLength) normalized = normalized[..MaxCellLength];
        if (normalized.Length > 0 && normalized[0] is '=' or '+' or '-' or '@' or '\t' or '\r') normalized = "'" + normalized;
        return '"' + normalized.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }

    public static DiagnosticsBuildManifest BuildManifest(TimeProvider timeProvider)
    {
        var assembly = typeof(EnterpriseReportContract).Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "unknown";
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        var revision = "unknown";
        var plus = informational.LastIndexOf('+');
        if (plus >= 0 && plus + 1 < informational.Length)
        {
            var candidate = informational[(plus + 1)..];
            if (candidate.Length <= 64 && candidate.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')) revision = candidate;
        }
        else if (!string.IsNullOrWhiteSpace(informational) && informational.Length <= 64 && informational.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'))
        {
            revision = informational;
        }

        return new(SchemaVersion, "Monitor", version, revision, timeProvider.GetUtcNow());
    }

    public static byte[] ManifestJson(TimeProvider timeProvider) => JsonSerializer.SerializeToUtf8Bytes(BuildManifest(timeProvider), new JsonSerializerOptions(JsonSerializerDefaults.Web));
}

public interface IEnterpriseReportingService
{
    byte[] Servers(EnterpriseServerReportFilter filter);
    byte[] Incidents(EnterpriseIncidentReportFilter filter);
    byte[] History(Guid registrationId, TimeSpan window);
    byte[] Audit();
    byte[] Manifest();
}

public sealed class EnterpriseReportingService(
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache,
    IOperatorMetadataStore operatorMetadata,
    IHealthIncidentRepository incidents,
    ISnapshotHistoryStore history,
    IAuditStore audit,
    TimeProvider timeProvider) : IEnterpriseReportingService
{
    public byte[] Servers(EnterpriseServerReportFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var normalizedGroup = NormalizeOptionalFilter(filter.Group, EnterpriseOperatorValidation.MaxGroupLength);
        var normalizedTag = NormalizeOptionalFilter(filter.Tag, 32);
        var rows = registrations.GetAll()
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .Select(registration => (Registration: registration, Metadata: operatorMetadata.GetServer(registration.Id), Snapshot: cache.Peek(registration.Id)))
            .Where(item => filter.Environment is null || item.Metadata.Environment == filter.Environment)
            .Where(item => normalizedGroup is null || string.Equals(item.Metadata.Group, normalizedGroup, StringComparison.OrdinalIgnoreCase))
            .Where(item => normalizedTag is null || item.Metadata.Tags.Contains(normalizedTag, StringComparer.OrdinalIgnoreCase))
            .Select(item => (IReadOnlyList<string?>)
            [
                item.Registration.Id.ToString("D"),
                item.Registration.DisplayName,
                item.Metadata.Environment.ToString(),
                item.Metadata.Group,
                string.Join('|', item.Metadata.Tags),
                item.Registration.IsEnabled ? "true" : "false",
                item.Snapshot?.Freshness.ToString() ?? "Unavailable",
                item.Snapshot?.Snapshot.CollectedAtUtc.ToString("O", CultureInfo.InvariantCulture)
            ]);
        return EnterpriseReportContract.Csv(
            ["RegistrationId", "DisplayName", "Environment", "Group", "Tags", "Enabled", "SnapshotFreshness", "CollectedAtUtc"],
            rows);
    }

    public byte[] Incidents(EnterpriseIncidentReportFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var normalizedAssignee = EnterpriseOperatorValidation.NormalizeAssignee(filter.Assignee);
        var now = timeProvider.GetUtcNow();
        var rows = incidents.GetAll()
            .OrderBy(item => item.Status)
            .ThenByDescending(item => item.Severity)
            .ThenByDescending(item => item.LastSeenUtc)
            .Select(incident =>
            {
                var collaboration = operatorMetadata.GetIncident(incident.Id);
                var server = operatorMetadata.GetServer(incident.RegistrationId);
                return (Incident: incident, collaboration.Assignee, Suppressed: EnterpriseOperatorPolicy.IsAlertSuppressed(server, now));
            })
            .Where(item => normalizedAssignee is null || string.Equals(item.Assignee, normalizedAssignee, StringComparison.OrdinalIgnoreCase))
            .Where(item => filter.Suppressed is null || item.Suppressed == filter.Suppressed)
            .Select(item => (IReadOnlyList<string?>)
            [
                item.Incident.Id,
                item.Incident.RegistrationId.ToString("D"),
                item.Incident.RuleId,
                item.Incident.Severity.ToString(),
                item.Incident.Status.ToString(),
                item.Assignee,
                item.Suppressed ? "true" : "false",
                item.Incident.FirstSeenUtc.ToString("O", CultureInfo.InvariantCulture),
                item.Incident.LastSeenUtc.ToString("O", CultureInfo.InvariantCulture),
                item.Incident.Occurrences.ToString(CultureInfo.InvariantCulture)
            ]);
        return EnterpriseReportContract.Csv(
            ["IncidentId", "RegistrationId", "RuleId", "Severity", "Status", "Assignee", "Suppressed", "FirstSeenUtc", "LastSeenUtc", "Occurrences"],
            rows);
    }

    public byte[] History(Guid registrationId, TimeSpan window)
    {
        if (registrationId == Guid.Empty) throw new ArgumentException("Registration ID is required.", nameof(registrationId));
        if (window is <= TimeSpan.Zero || window > TimeSpan.FromHours(24)) throw new ArgumentOutOfRangeException(nameof(window));
        var rows = history.Read(registrationId, window, 0, EnterpriseReportContract.MaxRows)
            .OrderBy(item => item.CollectedAtUtc)
            .Select(item => (IReadOnlyList<string?>)
            [
                item.RegistrationId.ToString("D"),
                item.CollectedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                item.DatabaseOnline.ToString(CultureInfo.InvariantCulture),
                item.DatabaseTotal.ToString(CultureInfo.InvariantCulture),
                item.MemoryPercent?.ToString(CultureInfo.InvariantCulture),
                item.BlockedRequests?.ToString(CultureInfo.InvariantCulture),
                item.RunnableTasks?.ToString(CultureInfo.InvariantCulture),
                item.Freshness.ToString()
            ]);
        return EnterpriseReportContract.Csv(
            ["RegistrationId", "CollectedAtUtc", "DatabaseOnline", "DatabaseTotal", "MemoryPercent", "BlockedRequests", "RunnableTasks", "Freshness"],
            rows);
    }

    public byte[] Audit()
    {
        var rows = audit.Read(0, Math.Min(100, EnterpriseReportContract.MaxRows))
            .OrderByDescending(item => item.OccurredAtUtc)
            .Select(item => (IReadOnlyList<string?>)
            [
                item.Id.ToString("D"),
                item.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture),
                item.Actor,
                item.Action,
                item.Target,
                item.Outcome
            ]);
        return EnterpriseReportContract.Csv(["EventId", "OccurredAtUtc", "Actor", "Action", "Target", "Outcome"], rows);
    }

    public byte[] Manifest() => EnterpriseReportContract.ManifestJson(timeProvider);

    private static string? NormalizeOptionalFilter(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength || normalized.Any(char.IsControl)) return null;
        return normalized;
    }
}
