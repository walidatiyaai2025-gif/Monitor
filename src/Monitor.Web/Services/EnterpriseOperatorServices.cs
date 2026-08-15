using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum ServerEnvironmentClass
{
    Unspecified,
    Production,
    Staging,
    Test,
    Development
}

public sealed record OperatorWindow(
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string Reason);

public sealed record ServerOperatorMetadata(
    Guid RegistrationId,
    ServerEnvironmentClass Environment,
    string? Group,
    string[] Tags,
    OperatorWindow? MaintenanceWindow,
    OperatorWindow? AlertSuppressionWindow,
    DateTimeOffset UpdatedAtUtc);

public sealed record IncidentOperatorNote(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string Actor,
    string Text);

public sealed record IncidentOperatorMetadata(
    string IncidentId,
    string? Assignee,
    IncidentOperatorNote[] Notes,
    string[] AcknowledgedRecommendationKeys,
    DateTimeOffset UpdatedAtUtc);

public sealed record EnterpriseOperatorSnapshot(
    ServerOperatorMetadata[] Servers,
    IncidentOperatorMetadata[] Incidents);

public interface IOperatorMetadataStore
{
    ServerOperatorMetadata GetServer(Guid registrationId);
    void UpsertServer(ServerOperatorMetadata metadata);
    IncidentOperatorMetadata GetIncident(string incidentId);
    void AssignIncident(string incidentId, string? assignee);
    void AddIncidentNote(string incidentId, string actor, string note);
    void SetRecommendationAcknowledged(string incidentId, string recommendationKey, bool acknowledged);
    EnterpriseOperatorSnapshot Snapshot();
}

public static class EnterpriseOperatorValidation
{
    public const int MaxTags = 10;
    public const int MaxNotesPerIncident = 20;
    public const int MaxNoteLength = 500;
    public const int MaxAssigneeLength = 80;
    public const int MaxGroupLength = 60;
    public const int MaxReasonLength = 200;
    public static readonly TimeSpan MaxWindowDuration = TimeSpan.FromDays(31);

    public static ServerOperatorMetadata NormalizeServer(ServerOperatorMetadata metadata, DateTimeOffset now)
    {
        if (metadata.RegistrationId == Guid.Empty)
            throw new ArgumentException("Registration ID is required.", nameof(metadata));
        if (!Enum.IsDefined(metadata.Environment))
            throw new ArgumentException("Environment classification is invalid.", nameof(metadata));

        var group = NormalizeOptionalDisplay(metadata.Group, MaxGroupLength);
        var tags = (metadata.Tags ?? [])
            .Select(NormalizeTag)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (tags.Length > MaxTags)
            throw new ArgumentException($"At most {MaxTags} server tags are allowed.", nameof(metadata));

        return metadata with
        {
            Group = group,
            Tags = tags,
            MaintenanceWindow = NormalizeWindow(metadata.MaintenanceWindow),
            AlertSuppressionWindow = NormalizeWindow(metadata.AlertSuppressionWindow),
            UpdatedAtUtc = now
        };
    }

    public static string? NormalizeAssignee(string? value) => NormalizeOptionalDisplay(value, MaxAssigneeLength);

    public static string NormalizeNote(string value)
    {
        var normalized = NormalizeDisplay(value, MaxNoteLength);
        if (SecurityInput.LooksSecretBearing(normalized))
            throw new ArgumentException("Operator note contains prohibited credential or connection material.", nameof(value));
        return normalized;
    }

    public static string NormalizeActor(string value) => NormalizeDisplay(value, 100);

    public static string NormalizeRecommendationKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Recommendation key is required.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length is < 8 or > 80 || normalized.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is ':' or '-' or '_')))
            throw new ArgumentException("Recommendation key is invalid.", nameof(value));
        return normalized;
    }

    public static string NormalizeIncidentId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Incident ID is required.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > 180 || normalized.Any(char.IsControl))
            throw new ArgumentException("Incident ID is invalid.", nameof(value));
        return normalized;
    }

    public static string[] ParseTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeTag)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTags + 1)
            .ToArray();
    }

    public static OperatorWindow? BuildWindow(DateTimeOffset? startsAtUtc, DateTimeOffset? endsAtUtc, string? reason)
    {
        if (startsAtUtc is null && endsAtUtc is null && string.IsNullOrWhiteSpace(reason)) return null;
        if (startsAtUtc is null || endsAtUtc is null)
            throw new ArgumentException("Both window start and end are required.");
        return NormalizeWindow(new OperatorWindow(startsAtUtc.Value, endsAtUtc.Value, reason ?? string.Empty));
    }

    private static OperatorWindow? NormalizeWindow(OperatorWindow? window)
    {
        if (window is null) return null;
        if (window.StartsAtUtc == default || window.EndsAtUtc == default || window.EndsAtUtc <= window.StartsAtUtc)
            throw new ArgumentException("Operator window end must be after start.");
        if (window.EndsAtUtc - window.StartsAtUtc > MaxWindowDuration)
            throw new ArgumentException("Operator window exceeds the maximum duration.");
        return window with { Reason = NormalizeDisplay(window.Reason, MaxReasonLength) };
    }

    private static string NormalizeTag(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Server tag cannot be empty.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > 32 || normalized.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '-')))
            throw new ArgumentException("Server tag contains unsupported characters or is too long.", nameof(value));
        return normalized;
    }

    private static string? NormalizeOptionalDisplay(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeDisplay(value, maxLength);

    private static string NormalizeDisplay(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > maxLength || normalized.Any(char.IsControl))
            throw new ArgumentException("Value is invalid or exceeds the supported length.", nameof(value));
        return normalized;
    }
}

public static class RecommendationAcknowledgmentKey
{
    public static string Create(RecommendationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var material = Encoding.UTF8.GetBytes($"{plan.RuleId}\n{plan.Explanation}");
        return $"rec:v1:{Convert.ToHexString(SHA256.HashData(material))[..24]}";
    }
}

public sealed class InMemoryOperatorMetadataStore(TimeProvider timeProvider) : IOperatorMetadataStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, ServerOperatorMetadata> _servers = [];
    private readonly Dictionary<string, IncidentOperatorMetadata> _incidents = new(StringComparer.Ordinal);

    public ServerOperatorMetadata GetServer(Guid registrationId)
    {
        lock (_gate)
        {
            return _servers.TryGetValue(registrationId, out var value) ? value : EmptyServer(registrationId, timeProvider.GetUtcNow());
        }
    }

    public void UpsertServer(ServerOperatorMetadata metadata)
    {
        var normalized = EnterpriseOperatorValidation.NormalizeServer(metadata, timeProvider.GetUtcNow());
        lock (_gate) _servers[normalized.RegistrationId] = normalized;
    }

    public IncidentOperatorMetadata GetIncident(string incidentId)
    {
        incidentId = EnterpriseOperatorValidation.NormalizeIncidentId(incidentId);
        lock (_gate)
        {
            return _incidents.TryGetValue(incidentId, out var value) ? value : EmptyIncident(incidentId, timeProvider.GetUtcNow());
        }
    }

    public void AssignIncident(string incidentId, string? assignee) => MutateIncident(incidentId, current =>
        current with { Assignee = EnterpriseOperatorValidation.NormalizeAssignee(assignee) });

    public void AddIncidentNote(string incidentId, string actor, string note)
    {
        actor = EnterpriseOperatorValidation.NormalizeActor(actor);
        note = EnterpriseOperatorValidation.NormalizeNote(note);
        MutateIncident(incidentId, current =>
        {
            var notes = current.Notes.Concat([new IncidentOperatorNote(Guid.NewGuid(), timeProvider.GetUtcNow(), actor, note)])
                .TakeLast(EnterpriseOperatorValidation.MaxNotesPerIncident).ToArray();
            return current with { Notes = notes };
        });
    }

    public void SetRecommendationAcknowledged(string incidentId, string recommendationKey, bool acknowledged)
    {
        recommendationKey = EnterpriseOperatorValidation.NormalizeRecommendationKey(recommendationKey);
        MutateIncident(incidentId, current =>
        {
            var keys = current.AcknowledgedRecommendationKeys.ToHashSet(StringComparer.Ordinal);
            if (acknowledged) keys.Add(recommendationKey); else keys.Remove(recommendationKey);
            return current with { AcknowledgedRecommendationKeys = keys.OrderBy(value => value, StringComparer.Ordinal).Take(20).ToArray() };
        });
    }

    public EnterpriseOperatorSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new(_servers.Values.OrderBy(item => item.RegistrationId).ToArray(), _incidents.Values.OrderBy(item => item.IncidentId, StringComparer.Ordinal).ToArray());
        }
    }

    private void MutateIncident(string incidentId, Func<IncidentOperatorMetadata, IncidentOperatorMetadata> mutation)
    {
        incidentId = EnterpriseOperatorValidation.NormalizeIncidentId(incidentId);
        lock (_gate)
        {
            var current = _incidents.TryGetValue(incidentId, out var value) ? value : EmptyIncident(incidentId, timeProvider.GetUtcNow());
            _incidents[incidentId] = mutation(current) with { UpdatedAtUtc = timeProvider.GetUtcNow() };
        }
    }

    internal static ServerOperatorMetadata EmptyServer(Guid id, DateTimeOffset now) => new(id, ServerEnvironmentClass.Unspecified, null, [], null, null, now);
    internal static IncidentOperatorMetadata EmptyIncident(string id, DateTimeOffset now) => new(id, null, [], [], now);
}

public sealed class FileOperatorMetadataStore : IOperatorMetadataStore
{
    private const int FormatVersion = 1;
    private readonly object _gate = new();
    private readonly string _path;
    private readonly TimeProvider _timeProvider;
    private EnterpriseOperatorSnapshot _state;

    public FileOperatorMetadataStore(string path, TimeProvider timeProvider)
    {
        _path = Path.GetFullPath(path);
        _timeProvider = timeProvider;
        var envelope = AtomicJsonFile.Load<Envelope>(_path);
        if (envelope is null) _state = new([], []);
        else
        {
            if (envelope.Version != FormatVersion || envelope.State is null) throw new InvalidDataException("Operator metadata store format is invalid.");
            _state = ValidateSnapshot(envelope.State);
        }
    }

    public ServerOperatorMetadata GetServer(Guid registrationId)
    {
        lock (_gate) return _state.Servers.FirstOrDefault(item => item.RegistrationId == registrationId) ?? InMemoryOperatorMetadataStore.EmptyServer(registrationId, _timeProvider.GetUtcNow());
    }

    public void UpsertServer(ServerOperatorMetadata metadata)
    {
        var normalized = EnterpriseOperatorValidation.NormalizeServer(metadata, _timeProvider.GetUtcNow());
        lock (_gate)
        {
            var servers = _state.Servers.Where(item => item.RegistrationId != normalized.RegistrationId).Append(normalized).OrderBy(item => item.RegistrationId).ToArray();
            Commit(new(servers, _state.Incidents));
        }
    }

    public IncidentOperatorMetadata GetIncident(string incidentId)
    {
        incidentId = EnterpriseOperatorValidation.NormalizeIncidentId(incidentId);
        lock (_gate) return _state.Incidents.FirstOrDefault(item => item.IncidentId == incidentId) ?? InMemoryOperatorMetadataStore.EmptyIncident(incidentId, _timeProvider.GetUtcNow());
    }

    public void AssignIncident(string incidentId, string? assignee) => MutateIncident(incidentId, current => current with { Assignee = EnterpriseOperatorValidation.NormalizeAssignee(assignee) });

    public void AddIncidentNote(string incidentId, string actor, string note)
    {
        actor = EnterpriseOperatorValidation.NormalizeActor(actor);
        note = EnterpriseOperatorValidation.NormalizeNote(note);
        MutateIncident(incidentId, current => current with
        {
            Notes = current.Notes.Concat([new IncidentOperatorNote(Guid.NewGuid(), _timeProvider.GetUtcNow(), actor, note)])
                .TakeLast(EnterpriseOperatorValidation.MaxNotesPerIncident).ToArray()
        });
    }

    public void SetRecommendationAcknowledged(string incidentId, string recommendationKey, bool acknowledged)
    {
        recommendationKey = EnterpriseOperatorValidation.NormalizeRecommendationKey(recommendationKey);
        MutateIncident(incidentId, current =>
        {
            var keys = current.AcknowledgedRecommendationKeys.ToHashSet(StringComparer.Ordinal);
            if (acknowledged) keys.Add(recommendationKey); else keys.Remove(recommendationKey);
            return current with { AcknowledgedRecommendationKeys = keys.OrderBy(value => value, StringComparer.Ordinal).Take(20).ToArray() };
        });
    }

    public EnterpriseOperatorSnapshot Snapshot()
    {
        lock (_gate) return new(_state.Servers.ToArray(), _state.Incidents.ToArray());
    }

    private void MutateIncident(string incidentId, Func<IncidentOperatorMetadata, IncidentOperatorMetadata> mutation)
    {
        incidentId = EnterpriseOperatorValidation.NormalizeIncidentId(incidentId);
        lock (_gate)
        {
            var current = _state.Incidents.FirstOrDefault(item => item.IncidentId == incidentId) ?? InMemoryOperatorMetadataStore.EmptyIncident(incidentId, _timeProvider.GetUtcNow());
            var updated = mutation(current) with { UpdatedAtUtc = _timeProvider.GetUtcNow() };
            var incidents = _state.Incidents.Where(item => item.IncidentId != incidentId).Append(updated).OrderBy(item => item.IncidentId, StringComparer.Ordinal).ToArray();
            Commit(new(_state.Servers, incidents));
        }
    }

    private void Commit(EnterpriseOperatorSnapshot candidate)
    {
        candidate = ValidateSnapshot(candidate);
        AtomicJsonFile.Save(_path, new Envelope(FormatVersion, candidate));
        _state = candidate;
    }

    private static EnterpriseOperatorSnapshot ValidateSnapshot(EnterpriseOperatorSnapshot state)
    {
        if (state.Servers.Length > 5000 || state.Incidents.Length > 1000) throw new InvalidDataException("Operator metadata store exceeds bounded capacity.");
        var serverIds = new HashSet<Guid>();
        foreach (var server in state.Servers)
        {
            if (!serverIds.Add(server.RegistrationId)) throw new InvalidDataException("Operator metadata contains duplicate server records.");
            EnterpriseOperatorValidation.NormalizeServer(server, server.UpdatedAtUtc);
        }
        var incidentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var incident in state.Incidents)
        {
            if (!incidentIds.Add(EnterpriseOperatorValidation.NormalizeIncidentId(incident.IncidentId)) || incident.Notes.Length > EnterpriseOperatorValidation.MaxNotesPerIncident || incident.AcknowledgedRecommendationKeys.Length > 20)
                throw new InvalidDataException("Operator metadata contains invalid incident state.");
            _ = EnterpriseOperatorValidation.NormalizeAssignee(incident.Assignee);
            foreach (var note in incident.Notes)
            {
                if (note.Id == Guid.Empty || note.OccurredAtUtc == default) throw new InvalidDataException("Operator note metadata is invalid.");
                EnterpriseOperatorValidation.NormalizeActor(note.Actor);
                EnterpriseOperatorValidation.NormalizeNote(note.Text);
            }
            foreach (var key in incident.AcknowledgedRecommendationKeys) EnterpriseOperatorValidation.NormalizeRecommendationKey(key);
        }
        return state;
    }

    private sealed record Envelope(int Version, EnterpriseOperatorSnapshot? State);
}

public sealed class SharedOperatorMetadataStore(ISharedStateDocumentStore store, TimeProvider timeProvider) : IOperatorMetadataStore
{
    private const string StateKey = "monitor:operator-metadata:v1";
    private const int MaxRetries = 64;

    public ServerOperatorMetadata GetServer(Guid registrationId) => Load().State.Servers.FirstOrDefault(item => item.RegistrationId == registrationId)
        ?? InMemoryOperatorMetadataStore.EmptyServer(registrationId, timeProvider.GetUtcNow());

    public void UpsertServer(ServerOperatorMetadata metadata)
    {
        var normalized = EnterpriseOperatorValidation.NormalizeServer(metadata, timeProvider.GetUtcNow());
        Mutate(state => new(state.Servers.Where(item => item.RegistrationId != normalized.RegistrationId).Append(normalized).OrderBy(item => item.RegistrationId).ToArray(), state.Incidents));
    }

    public IncidentOperatorMetadata GetIncident(string incidentId)
    {
        incidentId = EnterpriseOperatorValidation.NormalizeIncidentId(incidentId);
        return Load().State.Incidents.FirstOrDefault(item => item.IncidentId == incidentId) ?? InMemoryOperatorMetadataStore.EmptyIncident(incidentId, timeProvider.GetUtcNow());
    }

    public void AssignIncident(string incidentId, string? assignee) => MutateIncident(incidentId, current => current with { Assignee = EnterpriseOperatorValidation.NormalizeAssignee(assignee) });

    public void AddIncidentNote(string incidentId, string actor, string note)
    {
        actor = EnterpriseOperatorValidation.NormalizeActor(actor);
        note = EnterpriseOperatorValidation.NormalizeNote(note);
        MutateIncident(incidentId, current => current with
        {
            Notes = current.Notes.Concat([new IncidentOperatorNote(Guid.NewGuid(), timeProvider.GetUtcNow(), actor, note)])
                .TakeLast(EnterpriseOperatorValidation.MaxNotesPerIncident).ToArray()
        });
    }

    public void SetRecommendationAcknowledged(string incidentId, string recommendationKey, bool acknowledged)
    {
        recommendationKey = EnterpriseOperatorValidation.NormalizeRecommendationKey(recommendationKey);
        MutateIncident(incidentId, current =>
        {
            var keys = current.AcknowledgedRecommendationKeys.ToHashSet(StringComparer.Ordinal);
            if (acknowledged) keys.Add(recommendationKey); else keys.Remove(recommendationKey);
            return current with { AcknowledgedRecommendationKeys = keys.OrderBy(value => value, StringComparer.Ordinal).Take(20).ToArray() };
        });
    }

    public EnterpriseOperatorSnapshot Snapshot() => Load().State;

    private void MutateIncident(string incidentId, Func<IncidentOperatorMetadata, IncidentOperatorMetadata> mutation)
    {
        incidentId = EnterpriseOperatorValidation.NormalizeIncidentId(incidentId);
        Mutate(state =>
        {
            var current = state.Incidents.FirstOrDefault(item => item.IncidentId == incidentId) ?? InMemoryOperatorMetadataStore.EmptyIncident(incidentId, timeProvider.GetUtcNow());
            var updated = mutation(current) with { UpdatedAtUtc = timeProvider.GetUtcNow() };
            return new(state.Servers, state.Incidents.Where(item => item.IncidentId != incidentId).Append(updated).OrderBy(item => item.IncidentId, StringComparer.Ordinal).ToArray());
        });
    }

    private void Mutate(Func<EnterpriseOperatorSnapshot, EnterpriseOperatorSnapshot> mutation)
    {
        var contentionWait = new SpinWait();
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var loaded = Load();
            var candidate = mutation(loaded.State);
            var payload = JsonSerializer.Serialize(candidate, AtomicJsonFile.Options);
            var result = store.CompareExchangeAsync(StateKey, loaded.Version, payload).GetAwaiter().GetResult();
            if (result.Applied) return;
            contentionWait.SpinOnce();
        }
        throw new InvalidOperationException("Shared operator metadata update conflicted repeatedly.");
    }

    private (long Version, EnterpriseOperatorSnapshot State) Load()
    {
        var document = store.ReadAsync(StateKey).GetAwaiter().GetResult();
        if (document is null) return (0, new([], []));
        try
        {
            var state = JsonSerializer.Deserialize<EnterpriseOperatorSnapshot>(document.PayloadJson, AtomicJsonFile.Options)
                ?? throw new InvalidDataException("Shared operator metadata is empty.");
            return (document.Version, state);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Shared operator metadata is corrupt.", exception);
        }
    }
}

public interface ISafeCsvReportService
{
    byte[] BuildServerReport();
}

public sealed class SafeCsvReportService(
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache,
    IOperatorMetadataStore operatorMetadata) : ISafeCsvReportService
{
    public byte[] BuildServerReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("RegistrationId,DisplayName,Environment,Group,Tags,Enabled,SnapshotFreshness,CollectedAtUtc");
        foreach (var registration in registrations.GetAll().OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id))
        {
            var metadata = operatorMetadata.GetServer(registration.Id);
            var snapshot = cache.Peek(registration.Id);
            var cells = new[]
            {
                registration.Id.ToString("D"), registration.DisplayName, metadata.Environment.ToString(), metadata.Group ?? string.Empty,
                string.Join('|', metadata.Tags), registration.IsEnabled ? "true" : "false", snapshot?.Freshness.ToString() ?? "Unavailable",
                snapshot?.Snapshot.CollectedAtUtc.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty
            };
            builder.AppendLine(string.Join(',', cells.Select(EscapeCell)));
        }
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
    }

    internal static string EscapeCell(string value)
    {
        value ??= string.Empty;
        var normalized = new string(value.Select(ch => char.IsControl(ch) && ch is not '\t' ? ' ' : ch).ToArray()).Trim();
        if (normalized.Length > 0 && (normalized[0] is '=' or '+' or '-' or '@' or '\t' or '\r')) normalized = "'" + normalized;
        return '"' + normalized.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }
}

public interface IRedactedDiagnosticsPackageService
{
    Task<byte[]> BuildAsync(CancellationToken cancellationToken = default);
}

public sealed class RedactedDiagnosticsPackageService(
    IApplicationReadinessService readiness,
    IServerRegistrationRepository registrations,
    IHealthIncidentRepository incidents,
    IOperatorMetadataStore operatorMetadata,
    DeploymentTopologyOptions deployment,
    TimeProvider timeProvider) : IRedactedDiagnosticsPackageService
{
    public async Task<byte[]> BuildAsync(CancellationToken cancellationToken = default)
    {
        var readinessSnapshot = await readiness.CheckAsync(cancellationToken);
        var operatorSnapshot = operatorMetadata.Snapshot();
        var incidentSnapshot = incidents.GetAll();
        var manifest = new
        {
            formatVersion = 1,
            generatedAtUtc = timeProvider.GetUtcNow(),
            deploymentMode = deployment.Mode.ToString(),
            readiness = readinessSnapshot.Status.ToString(),
            sharedState = readinessSnapshot.SharedStateStatus.ToString(),
            sharedStateSchemaVersion = readinessSnapshot.SharedStateSchemaVersion,
            counts = new
            {
                registrations = registrations.GetAll().Count,
                incidents = incidentSnapshot.Count,
                openIncidents = incidentSnapshot.Count(item => item.Status == IncidentStatus.Open),
                acknowledgedIncidents = incidentSnapshot.Count(item => item.Status == IncidentStatus.Acknowledged),
                resolvedIncidents = incidentSnapshot.Count(item => item.Status == IncidentStatus.Resolved),
                serverOperatorProfiles = operatorSnapshot.Servers.Length,
                incidentOperatorProfiles = operatorSnapshot.Incidents.Length
            }
        };

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Fastest);
            await using (var stream = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(stream, manifest, AtomicJsonFile.Options, cancellationToken);
            }
            var readme = archive.CreateEntry("README.txt", CompressionLevel.Fastest);
            await using var readmeStream = new StreamWriter(readme.Open(), new UTF8Encoding(false));
            await readmeStream.WriteAsync("Redacted Monitor diagnostics. Contains aggregate control-plane status and counts only. No SQL endpoints, credentials, secret references, environment variables, provider errors, SQL text or operator note content are included.");
        }
        var bytes = output.ToArray();
        if (bytes.Length > 256 * 1024) throw new InvalidOperationException("Diagnostics package exceeded the bounded size.");
        return bytes;
    }
}
