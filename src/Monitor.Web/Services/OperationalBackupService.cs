using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed class BackupStoreOptions
{
    public const string SectionName = "BackupStore";
    public string RootPath { get; set; } = "App_Data/backups";
    public int RetentionCount { get; set; } = 10;
    public int MaxBundleBytes { get; set; } = 8 * 1024 * 1024;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RootPath)) throw new InvalidOperationException("BackupStore:RootPath is required.");
        if (RetentionCount is < 1 or > 100) throw new InvalidOperationException("BackupStore:RetentionCount must be between 1 and 100.");
        if (MaxBundleBytes is < 64 * 1024 or > 64 * 1024 * 1024) throw new InvalidOperationException("BackupStore:MaxBundleBytes is outside the allowed range.");
    }
}

public sealed record BackupRegistration(
    Guid Id,
    string DisplayName,
    string Host,
    int? Port,
    string? InstanceName,
    bool Encrypt,
    bool TrustServerCertificate,
    SqlAuthenticationMode AuthenticationMode,
    string? SecretReference,
    bool IsEnabled,
    DateTimeOffset CreatedAtUtc)
{
    public static BackupRegistration FromDomain(ServerRegistration registration) => new(
        registration.Id,
        registration.DisplayName,
        registration.Endpoint.Host,
        registration.Endpoint.Port,
        registration.Endpoint.InstanceName,
        registration.Endpoint.Encrypt,
        registration.Endpoint.TrustServerCertificate,
        registration.AuthenticationMode,
        registration.SecretReference?.Value,
        registration.IsEnabled,
        registration.CreatedAtUtc);

    public ServerRegistration ToDomain() => new(
        Id,
        DisplayName,
        new SqlServerEndpoint(Host, Port, InstanceName, Encrypt, TrustServerCertificate),
        AuthenticationMode,
        string.IsNullOrWhiteSpace(SecretReference) ? null : new ConnectionSecretReference(SecretReference),
        IsEnabled,
        CreatedAtUtc);
}

public sealed record BackupManifest(
    int Version,
    string RegistrationsSha256,
    string IncidentsSha256,
    string HistorySha256,
    string AuditSha256);

public sealed record MonitorBackupBundle(
    int FormatVersion,
    string BackupId,
    DateTimeOffset CreatedAtUtc,
    BackupManifest Manifest,
    BackupRegistration[] Registrations,
    HealthIncident[] Incidents,
    SnapshotHistoryPoint[] History,
    AuditEvent[] Audit);

public enum BackupValidationStatus
{
    Valid,
    NotFound,
    Invalid
}

public sealed record BackupValidationResult(
    BackupValidationStatus Status,
    string Message,
    MonitorBackupBundle? Bundle = null)
{
    public bool IsValid => Status == BackupValidationStatus.Valid && Bundle is not null;
}

public enum BackupRestoreStatus
{
    Restored,
    ValidationFailed,
    Unsupported,
    Failed
}

public sealed record BackupRestoreResult(
    BackupRestoreStatus Status,
    string Message,
    bool RestartRequired = false)
{
    public bool Succeeded => Status == BackupRestoreStatus.Restored;
}

public sealed record BackupListItem(string BackupId, DateTimeOffset CreatedAtUtc, long SizeBytes);

public sealed record BackupReadinessViewModel(
    bool Ready,
    string Status,
    string Message,
    int BackupCount,
    DateTimeOffset? LatestBackupUtc,
    bool RestoreRequiresRestart,
    IReadOnlyList<BackupListItem> RecentBackups);

public interface IOperationalBackupService
{
    Task<BackupListItem> CreateAsync(CancellationToken cancellationToken = default);
    Task<BackupValidationResult> ValidateAsync(string backupId, CancellationToken cancellationToken = default);
    Task<BackupRestoreResult> RestoreAsync(string backupId, CancellationToken cancellationToken = default);
    BackupReadinessViewModel GetReadiness();
}

internal interface IOperationalRestoreWriter
{
    bool IsSupported { get; }
    bool RestartRequired { get; }
    Task RestoreAsync(MonitorBackupBundle bundle, CancellationToken cancellationToken);
}

internal sealed class OperationalBackupService : IOperationalBackupService
{
    private const int BundleFormatVersion = 1;
    private const int ManifestVersion = 1;
    private const int MaxRegistrations = 5000;
    private const int MaxIncidents = 10000;
    private const int MaxAuditEvents = 1000;
    private const int MaxHistoryPerRegistration = 288;
    private const int RecentBackupLimit = 5;
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions CompactJson = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions FileJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly IServerRegistrationRepository _registrations;
    private readonly IHealthIncidentRepository _incidents;
    private readonly ISnapshotHistoryStore _history;
    private readonly IAuditStore _audit;
    private readonly IOperationalRestoreWriter _restoreWriter;
    private readonly BackupStoreOptions _options;
    private readonly string _root;
    private readonly TimeProvider _timeProvider;
    private readonly object _fileGate = new();

    public OperationalBackupService(
        IServerRegistrationRepository registrations,
        IHealthIncidentRepository incidents,
        ISnapshotHistoryStore history,
        IAuditStore audit,
        IOperationalRestoreWriter restoreWriter,
        BackupStoreOptions options,
        string root,
        TimeProvider timeProvider)
    {
        _registrations = registrations;
        _incidents = incidents;
        _history = history;
        _audit = audit;
        _restoreWriter = restoreWriter;
        _options = options;
        _options.Validate();
        _root = Path.GetFullPath(root);
        _timeProvider = timeProvider;
    }

    public Task<BackupListItem> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var registrationItems = _registrations.GetAll()
            .OrderBy(item => item.Id)
            .Select(BackupRegistration.FromDomain)
            .ToArray();
        var incidentItems = _incidents.GetAll().OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var historyItems = registrationItems
            .SelectMany(item => _history.Read(item.Id, HistoryWindow))
            .OrderBy(item => item.RegistrationId)
            .ThenBy(item => item.CollectedAtUtc)
            .ToArray();
        var auditItems = ReadAllAudit().OrderBy(item => item.OccurredAtUtc).ThenBy(item => item.Id).ToArray();
        var createdAt = _timeProvider.GetUtcNow();
        var backupId = $"{createdAt:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..30];
        var manifest = new BackupManifest(
            ManifestVersion,
            Hash(registrationItems),
            Hash(incidentItems),
            Hash(historyItems),
            Hash(auditItems));
        var bundle = new MonitorBackupBundle(
            BundleFormatVersion,
            backupId,
            createdAt,
            manifest,
            registrationItems,
            incidentItems,
            historyItems,
            auditItems);

        ValidateBundle(bundle);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(bundle, FileJson);
        if (bytes.Length > _options.MaxBundleBytes)
        {
            throw new InvalidOperationException("Operational backup exceeds the configured bundle size limit.");
        }

        lock (_fileGate)
        {
            Directory.CreateDirectory(_root);
            var path = PathFor(backupId);
            WriteAtomic(path, bytes);
            PruneLocked();
            var file = new FileInfo(path);
            return Task.FromResult(new BackupListItem(backupId, createdAt, file.Length));
        }
    }

    public async Task<BackupValidationResult> ValidateAsync(string backupId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryNormalizeBackupId(backupId, out var normalized))
        {
            return new(BackupValidationStatus.Invalid, "Backup identifier is invalid.");
        }

        string path;
        lock (_fileGate) path = PathFor(normalized);

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                32 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length <= 0 || stream.Length > _options.MaxBundleBytes)
            {
                return new(BackupValidationStatus.Invalid, "Backup file size is invalid.");
            }

            var bundle = await JsonSerializer.DeserializeAsync<MonitorBackupBundle>(stream, FileJson, cancellationToken)
                ?? throw new InvalidDataException("Backup is empty or invalid.");
            if (!string.Equals(bundle.BackupId, normalized, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Backup identity does not match its file name.");
            }

            ValidateBundle(bundle);
            return new(BackupValidationStatus.Valid, "Backup validation succeeded.", bundle);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new(BackupValidationStatus.NotFound, "Backup was not found.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or ArgumentException or InvalidOperationException)
        {
            return new(BackupValidationStatus.Invalid, "Backup validation failed.");
        }
    }

    public async Task<BackupRestoreResult> RestoreAsync(string backupId, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(backupId, cancellationToken);
        if (!validation.IsValid)
        {
            return new(BackupRestoreStatus.ValidationFailed, validation.Message);
        }

        if (!_restoreWriter.IsSupported)
        {
            return new(BackupRestoreStatus.Unsupported, "Restore is unavailable for the selected ephemeral persistence mode.");
        }

        try
        {
            await _restoreWriter.RestoreAsync(validation.Bundle!, cancellationToken);
            return new(
                BackupRestoreStatus.Restored,
                _restoreWriter.RestartRequired
                    ? "Restore committed. Restart Monitor before resuming operations."
                    : "Restore committed successfully.",
                _restoreWriter.RestartRequired);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or SharedStateConcurrencyException or SharedStateStoreUnavailableException)
        {
            return new(BackupRestoreStatus.Failed, "Restore failed and the previous persisted state was retained or rolled back.");
        }
    }

    public BackupReadinessViewModel GetReadiness()
    {
        BackupDirectoryScan backups;
        lock (_fileGate)
        {
            backups = BoundedBackupDirectory.ScanReadiness(_root, RecentBackupLimit);
        }

        return new(
            _restoreWriter.IsSupported,
            _restoreWriter.IsSupported ? "Backup ready" : "Backup export only / restore blocked",
            _restoreWriter.IsSupported
                ? "Operational backups include safe registration metadata, incidents, history and audit. Credential ciphertext and Data Protection keys are excluded."
                : "Selected InMemory persistence cannot provide a restart-safe restore target.",
            backups.Count,
            backups.RecentBackups.FirstOrDefault()?.CreatedAtUtc,
            _restoreWriter.RestartRequired,
            backups.RecentBackups);
    }

    private IReadOnlyList<AuditEvent> ReadAllAudit()
    {
        var result = new List<AuditEvent>();
        for (var offset = 0; offset < MaxAuditEvents; offset += 100)
        {
            var page = _audit.Read(offset, 100);
            result.AddRange(page);
            if (page.Count < 100) break;
        }

        return result.Take(MaxAuditEvents).ToArray();
    }

    private void ValidateBundle(MonitorBackupBundle bundle)
    {
        if (bundle.FormatVersion != BundleFormatVersion || bundle.Manifest.Version != ManifestVersion ||
            !TryNormalizeBackupId(bundle.BackupId, out _) || bundle.CreatedAtUtc == default)
        {
            throw new InvalidDataException("Backup format is not supported.");
        }

        if (bundle.Registrations.Length > MaxRegistrations || bundle.Incidents.Length > MaxIncidents ||
            bundle.Audit.Length > MaxAuditEvents)
        {
            throw new InvalidDataException("Backup exceeds a bounded section count.");
        }

        if (!FixedEquals(bundle.Manifest.RegistrationsSha256, Hash(bundle.Registrations)) ||
            !FixedEquals(bundle.Manifest.IncidentsSha256, Hash(bundle.Incidents)) ||
            !FixedEquals(bundle.Manifest.HistorySha256, Hash(bundle.History)) ||
            !FixedEquals(bundle.Manifest.AuditSha256, Hash(bundle.Audit)))
        {
            throw new InvalidDataException("Backup checksum validation failed.");
        }

        var registrationIds = new HashSet<Guid>();
        foreach (var item in bundle.Registrations)
        {
            var domain = item.ToDomain();
            if (!registrationIds.Add(domain.Id) || domain.CreatedAtUtc == default)
            {
                throw new InvalidDataException("Backup registrations are invalid or duplicated.");
            }
        }

        var incidentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in bundle.Incidents)
        {
            if (!registrationIds.Contains(item.RegistrationId) || item.Id.Length > 200 || !incidentIds.Add(item.Id) ||
                item.FirstSeenUtc == default || item.LastSeenUtc == default || item.FirstSeenUtc > item.LastSeenUtc ||
                item.Occurrences < 1 || item.RuleId.Length > 80 || item.Title.Length > 160 || item.Evidence.Length > 500 ||
                !Enum.IsDefined(item.Severity) || !Enum.IsDefined(item.Status))
            {
                throw new InvalidDataException("Backup incident state is invalid.");
            }
        }

        var historyKeys = new HashSet<(Guid, DateTimeOffset)>();
        foreach (var point in bundle.History)
        {
            if (!registrationIds.Contains(point.RegistrationId) || point.CollectedAtUtc == default ||
                point.DatabaseTotal < 0 || point.DatabaseOnline < 0 || point.DatabaseOnline > point.DatabaseTotal ||
                point.MemoryPercent is < 0 or > 100 || point.BlockedRequests is < 0 || point.RunnableTasks is < 0 ||
                !Enum.IsDefined(point.Freshness) || !historyKeys.Add((point.RegistrationId, point.CollectedAtUtc)))
            {
                throw new InvalidDataException("Backup history state is invalid.");
            }
        }

        if (bundle.History.GroupBy(item => item.RegistrationId).Any(group => group.Count() > MaxHistoryPerRegistration))
        {
            throw new InvalidDataException("Backup history exceeds the per-server retention bound.");
        }

        var auditIds = new HashSet<Guid>();
        foreach (var item in bundle.Audit)
        {
            if (item.Id == Guid.Empty || !auditIds.Add(item.Id) || item.OccurredAtUtc == default ||
                item.Actor.Length > 100 || item.Action.Length > 80 || item.Target.Length > 160 || item.Outcome.Length > 40)
            {
                throw new InvalidDataException("Backup audit state is invalid.");
            }
        }

        var serialized = JsonSerializer.Serialize(bundle, CompactJson);
        foreach (var prohibitedProperty in new[] { "\"password\":", "\"username\":", "\"connectionString\":", "\"ciphertext\":", "\"protectedPayload\":", "\"keyRing\":" })
        {
            if (serialized.Contains(prohibitedProperty, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Backup contains a prohibited secret-bearing property.");
            }
        }
    }

    private void PruneLocked() => BoundedBackupDirectory.Prune(_root, _options.RetentionCount);

    private string PathFor(string backupId) => Path.Combine(_root, $"monitor-backup-{backupId}.json");

    private static bool TryNormalizeBackupId(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is >= 20 and <= 64 && normalized.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    }

    private static string Hash<T>(T[] items)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(items, CompactJson);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static bool FixedEquals(string expected, string actual)
    {
        if (expected.Length != actual.Length) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual));
    }

    private static void WriteAtomic(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Backup directory could not be resolved.");
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 32 * 1024, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}

internal sealed class OperationalRestoreWriter : IOperationalRestoreWriter
{
    private const int DefaultRegistrationRollbackSnapshotBytes = 16 * 1024 * 1024;
    private const int DefaultOperationalRollbackSnapshotBytes = 128 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly RegistrationStoreOptions _registrationOptions;
    private readonly OperationalStoreOptions _operationalOptions;
    private readonly HaStateOptions _haState;
    private readonly ISharedStateDocumentStore _sharedState;
    private readonly IServerRegistrationRepository _currentRegistrations;
    private readonly string _contentRoot;
    private readonly string? _webRoot;
    private readonly int _registrationRollbackSnapshotBytes;
    private readonly int _operationalRollbackSnapshotBytes;

    public OperationalRestoreWriter(
        RegistrationStoreOptions registrationOptions,
        OperationalStoreOptions operationalOptions,
        HaStateOptions haState,
        ISharedStateDocumentStore sharedState,
        IServerRegistrationRepository currentRegistrations,
        string contentRoot,
        string? webRoot,
        int registrationRollbackSnapshotBytes = DefaultRegistrationRollbackSnapshotBytes,
        int operationalRollbackSnapshotBytes = DefaultOperationalRollbackSnapshotBytes)
    {
        ValidateRollbackSnapshotBound(registrationRollbackSnapshotBytes, nameof(registrationRollbackSnapshotBytes));
        ValidateRollbackSnapshotBound(operationalRollbackSnapshotBytes, nameof(operationalRollbackSnapshotBytes));
        _registrationOptions = registrationOptions;
        _operationalOptions = operationalOptions;
        _haState = haState;
        _sharedState = sharedState;
        _currentRegistrations = currentRegistrations;
        _contentRoot = contentRoot;
        _webRoot = webRoot;
        _registrationRollbackSnapshotBytes = registrationRollbackSnapshotBytes;
        _operationalRollbackSnapshotBytes = operationalRollbackSnapshotBytes;
    }

    public bool IsSupported =>
        (_haState.UseSharedRegistrations || _registrationOptions.Mode == RegistrationStoreMode.File) &&
        (_haState.UseSharedOperationalState || _operationalOptions.Mode == OperationalStoreMode.File);

    public bool RestartRequired => !_haState.UseSharedRegistrations || !_haState.UseSharedOperationalState;

    public async Task RestoreAsync(MonitorBackupBundle bundle, CancellationToken cancellationToken)
    {
        if (!IsSupported) throw new InvalidOperationException("Ephemeral persistence cannot be restored safely.");
        var operations = BuildOperations(bundle);
        var applied = new Stack<AppliedOperation>();
        try
        {
            foreach (var operation in operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                applied.Push(await operation.Apply(cancellationToken));
            }
        }
        catch
        {
            Exception? rollbackFailure = null;
            while (applied.Count > 0)
            {
                try { await applied.Pop().Rollback(CancellationToken.None); }
                catch (Exception exception) { rollbackFailure ??= exception; }
            }
            if (rollbackFailure is not null) throw new InvalidOperationException("Restore failed and rollback could not complete safely.", rollbackFailure);
            throw;
        }
    }

    private IReadOnlyList<RestoreOperation> BuildOperations(MonitorBackupBundle bundle)
    {
        var operations = new List<RestoreOperation>();
        var registrationsPayload = JsonSerializer.Serialize(new { version = 1, registrations = bundle.Registrations }, JsonOptions);
        if (_haState.UseSharedRegistrations)
            operations.Add(Shared("monitor:registrations:v1", registrationsPayload, JsonSerializer.Serialize(new { version = 1, registrations = Array.Empty<BackupRegistration>() }, JsonOptions)));
        else
            operations.Add(Local(ResolveRegistrationPath(), registrationsPayload, _registrationRollbackSnapshotBytes));

        var auditPayload = JsonSerializer.Serialize(new { version = 1, events = bundle.Audit }, JsonOptions);
        var incidentsPayload = JsonSerializer.Serialize(new { version = 1, incidents = bundle.Incidents }, JsonOptions);
        if (_haState.UseSharedOperationalState)
        {
            operations.Add(Shared("monitor:audit:v1", auditPayload, JsonSerializer.Serialize(new { version = 1, events = Array.Empty<AuditEvent>() }, JsonOptions)));
            operations.Add(Shared("monitor:incidents:v1", incidentsPayload, JsonSerializer.Serialize(new { version = 1, incidents = Array.Empty<HealthIncident>() }, JsonOptions)));
            var currentIds = _currentRegistrations.GetAll().Select(item => item.Id).Concat(bundle.Registrations.Select(item => item.Id)).Distinct().OrderBy(id => id).ToArray();
            foreach (var id in currentIds)
            {
                var points = bundle.History.Where(item => item.RegistrationId == id).OrderBy(item => item.CollectedAtUtc).ToArray();
                var payload = JsonSerializer.Serialize(new { version = 1, registrationId = id, points }, JsonOptions);
                var empty = JsonSerializer.Serialize(new { version = 1, registrationId = id, points = Array.Empty<SnapshotHistoryPoint>() }, JsonOptions);
                operations.Add(Shared($"monitor:history:v1:{id:N}", payload, empty));
            }
        }
        else
        {
            var root = ResolveOperationalRoot();
            operations.Add(Local(Path.Combine(root, "audit.json"), auditPayload, _operationalRollbackSnapshotBytes));
            operations.Add(Local(Path.Combine(root, "incidents.json"), incidentsPayload, _operationalRollbackSnapshotBytes));
            operations.Add(Local(Path.Combine(root, "history.json"), JsonSerializer.Serialize(new { version = 1, points = bundle.History }, JsonOptions), _operationalRollbackSnapshotBytes));
        }
        return operations;
    }

    private RestoreOperation Local(string path, string payload, int maxPreviousBytes) => new(async cancellationToken =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var previous = await ReadRollbackSnapshotAsync(path, maxPreviousBytes, cancellationToken);
        WriteAtomicText(path, payload);
        return new AppliedOperation(async _ =>
        {
            if (previous is null)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else WriteAtomicText(path, previous);
            await Task.CompletedTask;
        });
    });

    private static async Task<string?> ReadRollbackSnapshotAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length > maxBytes)
            {
                throw new InvalidDataException("Existing restore target exceeds its bounded file size.");
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 16 * 1024, leaveOpen: true);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private RestoreOperation Shared(string key, string payload, string emptyPayload) => new(async cancellationToken =>
    {
        var previous = await _sharedState.ReadAsync(key, cancellationToken);
        var write = await _sharedState.CompareExchangeAsync(key, previous?.Version ?? 0, payload, cancellationToken);
        if (!write.Applied || write.Document is null) throw new SharedStateConcurrencyException();
        var appliedVersion = write.Document.Version;
        return new AppliedOperation(async rollbackToken =>
        {
            var rollback = await _sharedState.CompareExchangeAsync(key, appliedVersion, previous?.PayloadJson ?? emptyPayload, rollbackToken);
            if (!rollback.Applied) throw new SharedStateConcurrencyException();
        });
    });

    private string ResolveRegistrationPath()
    {
        var path = Path.IsPathRooted(_registrationOptions.Path)
            ? Path.GetFullPath(_registrationOptions.Path)
            : Path.GetFullPath(Path.Combine(_contentRoot, _registrationOptions.Path));
        EnsureOutsideWebRoot(path);
        return path;
    }

    private string ResolveOperationalRoot() => OperationalStorePath.ResolveOutsideWebRoot(_operationalOptions.RootPath, _contentRoot, _webRoot);

    private void EnsureOutsideWebRoot(string path)
    {
        var webRoot = Path.GetFullPath(_webRoot ?? Path.Combine(_contentRoot, "wwwroot"));
        var relative = Path.GetRelativePath(webRoot, path);
        if (relative == "." || (!relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && relative != ".." && !Path.IsPathRooted(relative)))
            throw new InvalidOperationException("Restore target must be outside wwwroot.");
    }

    private static void ValidateRollbackSnapshotBound(int value, string parameterName)
    {
        if (value < 1024)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Restore rollback snapshot bound must be at least 1024 bytes.");
        }
    }

    private static void WriteAtomicText(string path, string payload)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Restore directory could not be resolved.");
        Directory.CreateDirectory(directory);
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 32 * 1024, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(payload);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private sealed record RestoreOperation(Func<CancellationToken, Task<AppliedOperation>> Apply);
    private sealed record AppliedOperation(Func<CancellationToken, Task> Rollback);
}
