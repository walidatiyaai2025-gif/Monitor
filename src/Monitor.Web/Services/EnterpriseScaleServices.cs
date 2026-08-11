using System.Text;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed class EnterpriseScaleOptions
{
    public int DefaultPageSize { get; init; } = 50;
    public int MaxPageSize { get; init; } = 100;
    public int MaxRenderedNotes { get; init; } = 5;
    public int DiagnosticsTimeoutSeconds { get; init; } = 10;

    public void Validate()
    {
        if (DefaultPageSize is < 1 or > 100) throw new InvalidOperationException("Enterprise default page size is invalid.");
        if (MaxPageSize is < 1 or > 200 || DefaultPageSize > MaxPageSize) throw new InvalidOperationException("Enterprise max page size is invalid.");
        if (MaxRenderedNotes is < 1 or > 20) throw new InvalidOperationException("Rendered note limit is invalid.");
        if (DiagnosticsTimeoutSeconds is < 1 or > 30) throw new InvalidOperationException("Diagnostics timeout is invalid.");
    }
}

public sealed record BoundedPage<T>(IReadOnlyList<T> Items, int Offset, int Limit, int Total)
{
    public bool HasPrevious => Offset > 0;
    public bool HasNext => Offset + Items.Count < Total;
}

public sealed class OperatorMetadataIndex
{
    private readonly IReadOnlyDictionary<Guid, ServerOperatorMetadata> _servers;
    private readonly IReadOnlyDictionary<string, IncidentOperatorMetadata> _incidents;

    public OperatorMetadataIndex(EnterpriseOperatorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _servers = snapshot.Servers.ToDictionary(item => item.RegistrationId);
        _incidents = snapshot.Incidents.ToDictionary(item => item.IncidentId, StringComparer.Ordinal);
    }

    public int ServerCount => _servers.Count;
    public int IncidentCount => _incidents.Count;
    public ServerOperatorMetadata? Server(Guid id) => _servers.TryGetValue(id, out var value) ? value : null;
    public IncidentOperatorMetadata? Incident(string id) => _incidents.TryGetValue(id, out var value) ? value : null;
}

public sealed class EnterprisePagingService(
    IServerRegistrationRepository registrations,
    IHealthIncidentRepository incidents,
    IOperatorMetadataStore metadata,
    EnterpriseScaleOptions? options = null)
{
    private readonly EnterpriseScaleOptions _options = Validate(options ?? new EnterpriseScaleOptions());

    public BoundedPage<(ServerRegistration Registration, ServerOperatorMetadata Metadata)> Servers(int offset, int limit)
    {
        var index = new OperatorMetadataIndex(metadata.Snapshot());
        var rows = registrations.GetAll()
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .Select(item => (Registration: item, Metadata: index.Server(item.Id) ?? InMemoryOperatorMetadataStore.EmptyServer(item.Id, item.CreatedAtUtc)))
            .ToArray();
        return Page(rows, offset, limit);
    }

    public BoundedPage<(HealthIncident Incident, IncidentOperatorMetadata Metadata)> Incidents(int offset, int limit)
    {
        var index = new OperatorMetadataIndex(metadata.Snapshot());
        var rows = incidents.GetAll()
            .OrderBy(item => item.Status)
            .ThenByDescending(item => item.Severity)
            .ThenByDescending(item => item.LastSeenUtc)
            .Select(item => (Incident: item, Metadata: index.Incident(item.Id) ?? InMemoryOperatorMetadataStore.EmptyIncident(item.Id, item.LastSeenUtc)))
            .ToArray();
        return Page(rows, offset, limit);
    }

    public IReadOnlyList<IncidentOperatorNote> Notes(string incidentId, int offset, int limit)
    {
        var item = metadata.GetIncident(EnterpriseSecurityPolicy.NormalizeIncidentRouteId(incidentId));
        return item.Notes.OrderByDescending(note => note.OccurredAtUtc)
            .Skip(Math.Max(0, offset))
            .Take(Math.Min(Math.Clamp(limit, 1, _options.MaxPageSize), _options.MaxRenderedNotes))
            .ToArray();
    }

    private BoundedPage<T> Page<T>(T[] rows, int offset, int limit)
    {
        var boundedOffset = Math.Max(0, offset);
        var boundedLimit = limit <= 0 ? _options.DefaultPageSize : Math.Clamp(limit, 1, _options.MaxPageSize);
        return new(rows.Skip(boundedOffset).Take(boundedLimit).ToArray(), boundedOffset, boundedLimit, rows.Length);
    }

    private static EnterpriseScaleOptions Validate(EnterpriseScaleOptions options) { options.Validate(); return options; }
}

public sealed class EnterpriseStreamingCsvWriter
{
    private static readonly UTF8Encoding Utf8 = new(false);

    public async Task<int> WriteAsync(Stream output, IReadOnlyList<string> headers, IAsyncEnumerable<IReadOnlyList<string?>> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite) throw new ArgumentException("Output stream must be writable.", nameof(output));
        var written = 0;
        async Task WriteLineAsync(string line)
        {
            var bytes = Utf8.GetBytes(line + "\n");
            if (written + bytes.Length > EnterpriseReportContract.MaxBytes) throw new InvalidOperationException("Streaming CSV exceeded the bounded size.");
            await output.WriteAsync(bytes, cancellationToken);
            written += bytes.Length;
        }

        await output.WriteAsync(Encoding.UTF8.GetPreamble(), cancellationToken);
        written += Encoding.UTF8.GetPreamble().Length;
        await WriteLineAsync($"#schema,{EnterpriseReportContract.SchemaVersion}");
        await WriteLineAsync(string.Join(',', headers.Select(EnterpriseReportContract.EscapeCell)));
        var count = 0;
        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            if (count++ >= EnterpriseReportContract.MaxRows) break;
            if (row.Count != headers.Count) throw new InvalidDataException("Streaming CSV row width does not match schema.");
            await WriteLineAsync(string.Join(',', row.Select(value => EnterpriseReportContract.EscapeCell(value ?? string.Empty))));
        }
        return written;
    }
}

public sealed class BoundedDiagnosticsRunner(IRedactedDiagnosticsPackageService diagnostics, EnterpriseScaleOptions? options = null)
{
    private readonly EnterpriseScaleOptions _options = Validate(options ?? new EnterpriseScaleOptions());

    public async Task<byte[]> BuildAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.DiagnosticsTimeoutSeconds));
        return await diagnostics.BuildAsync(timeout.Token);
    }

    private static EnterpriseScaleOptions Validate(EnterpriseScaleOptions options) { options.Validate(); return options; }
}

public interface ISharedCasTelemetry
{
    long Attempts { get; }
    long Applied { get; }
    long Conflicts { get; }
}

public sealed class TelemetrySharedStateDocumentStore(ISharedStateDocumentStore inner) : ISharedStateDocumentStore, ISharedCasTelemetry
{
    private long _attempts;
    private long _applied;
    private long _conflicts;
    public long Attempts => Interlocked.Read(ref _attempts);
    public long Applied => Interlocked.Read(ref _applied);
    public long Conflicts => Interlocked.Read(ref _conflicts);

    public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default) => inner.ReadAsync(key, cancellationToken);

    public async Task<SharedStateWriteResult> CompareExchangeAsync(string key, long expectedVersion, string payloadJson, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _attempts);
        var result = await inner.CompareExchangeAsync(key, expectedVersion, payloadJson, cancellationToken);
        if (result.Status == SharedStateWriteStatus.Applied) Interlocked.Increment(ref _applied);
        else if (result.Status == SharedStateWriteStatus.Conflict) Interlocked.Increment(ref _conflicts);
        return result;
    }
}
