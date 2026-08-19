using System.Text.Json;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum RegistrationStoreMode
{
    File,
    InMemory
}

public sealed class RegistrationStoreOptions
{
    public const string SectionName = "RegistrationStore";

    public RegistrationStoreMode Mode { get; set; } = RegistrationStoreMode.File;
    public string Path { get; set; } = "App_Data/registrations.json";

    public void Validate()
    {
        if (!Enum.IsDefined(Mode))
        {
            throw new InvalidOperationException("RegistrationStore:Mode is not supported.");
        }

        if (Mode == RegistrationStoreMode.File && string.IsNullOrWhiteSpace(Path))
        {
            throw new InvalidOperationException("RegistrationStore:Path is required when file persistence is enabled.");
        }
    }
}

public sealed class FileServerRegistrationRepository : IServerRegistrationRepository
{
    private const int CurrentFormatVersion = 1;
    private const int DefaultMaxStoreFileBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly string _leasePath;
    private readonly int _maxStoreFileBytes;
    private readonly Dictionary<Guid, ServerRegistration> _registrations;

    public FileServerRegistrationRepository(string path)
        : this(path, DefaultMaxStoreFileBytes)
    {
    }

    internal FileServerRegistrationRepository(string path, int maxStoreFileBytes)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Registration store path is required.", nameof(path));
        }

        if (maxStoreFileBytes < 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maxStoreFileBytes), "Registration store file bound must be at least 1024 bytes.");
        }

        _path = System.IO.Path.GetFullPath(path);
        _leasePath = $"{_path}.lock";
        _maxStoreFileBytes = maxStoreFileBytes;
        using var lease = CrossProcessFileLease.Acquire(_leasePath, "Server registration store");
        _registrations = Load(_path, _maxStoreFileBytes);
    }

    public IReadOnlyList<ServerRegistration> GetAll()
    {
        lock (_gate)
        {
            using var lease = CrossProcessFileLease.Acquire(_leasePath, "Server registration store");
            ReloadFromDisk();
            return Ordered(_registrations.Values).ToArray();
        }
    }

    public ServerRegistration? GetById(Guid id)
    {
        lock (_gate)
        {
            using var lease = CrossProcessFileLease.Acquire(_leasePath, "Server registration store");
            ReloadFromDisk();
            return _registrations.TryGetValue(id, out var registration) ? registration : null;
        }
    }

    public void Upsert(ServerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        lock (_gate)
        {
            using var lease = CrossProcessFileLease.Acquire(_leasePath, "Server registration store");
            ReloadFromDisk();
            var hadPrevious = _registrations.TryGetValue(registration.Id, out var previous);
            _registrations[registration.Id] = registration;
            try
            {
                Persist();
            }
            catch
            {
                if (hadPrevious && previous is not null)
                {
                    _registrations[registration.Id] = previous;
                }
                else
                {
                    _registrations.Remove(registration.Id);
                }

                throw;
            }
        }
    }

    public ServerRegistrationFieldMutationResult TryReplaceSecretReference(
        Guid id,
        ConnectionSecretReference? expectedReference,
        ConnectionSecretReference nextReference)
    {
        lock (_gate)
        {
            using var lease = CrossProcessFileLease.Acquire(_leasePath, "Server registration store");
            ReloadFromDisk();
            if (!_registrations.TryGetValue(id, out var current))
            {
                return new(ServerRegistrationFieldMutationStatus.NotFound, null);
            }

            if (!SecretReferencesEqual(current.SecretReference, expectedReference))
            {
                return new(ServerRegistrationFieldMutationStatus.Conflict, current);
            }

            if (SecretReferencesEqual(current.SecretReference, nextReference))
            {
                return new(ServerRegistrationFieldMutationStatus.Unchanged, current);
            }

            var updated = new ServerRegistration(
                current.Id,
                current.DisplayName,
                current.Endpoint,
                current.AuthenticationMode,
                nextReference,
                current.IsEnabled,
                current.CreatedAtUtc);
            _registrations[id] = updated;
            try
            {
                Persist();
                return new(ServerRegistrationFieldMutationStatus.Applied, updated);
            }
            catch
            {
                _registrations[id] = current;
                throw;
            }
        }
    }

    public ServerRegistrationFieldMutationResult SetEnabled(Guid id, bool enabled)
    {
        lock (_gate)
        {
            using var lease = CrossProcessFileLease.Acquire(_leasePath, "Server registration store");
            ReloadFromDisk();
            if (!_registrations.TryGetValue(id, out var current))
            {
                return new(ServerRegistrationFieldMutationStatus.NotFound, null);
            }

            if (current.IsEnabled == enabled)
            {
                return new(ServerRegistrationFieldMutationStatus.Unchanged, current);
            }

            var updated = new ServerRegistration(
                current.Id,
                current.DisplayName,
                current.Endpoint,
                current.AuthenticationMode,
                current.SecretReference,
                enabled,
                current.CreatedAtUtc);
            _registrations[id] = updated;
            try
            {
                Persist();
                return new(ServerRegistrationFieldMutationStatus.Applied, updated);
            }
            catch
            {
                _registrations[id] = current;
                throw;
            }
        }
    }

    public bool Remove(Guid id)
    {
        lock (_gate)
        {
            using var lease = CrossProcessFileLease.Acquire(_leasePath, "Server registration store");
            ReloadFromDisk();
            if (!_registrations.Remove(id, out var previous))
            {
                return false;
            }

            try
            {
                Persist();
                return true;
            }
            catch
            {
                _registrations[id] = previous;
                throw;
            }
        }
    }

    private void ReloadFromDisk()
    {
        var latest = Load(_path, _maxStoreFileBytes);
        _registrations.Clear();
        foreach (var pair in latest)
        {
            _registrations.Add(pair.Key, pair.Value);
        }
    }

    private static bool SecretReferencesEqual(
        ConnectionSecretReference? left,
        ConnectionSecretReference? right) =>
        string.Equals(left?.Value, right?.Value, StringComparison.Ordinal);

    private void Persist()
    {
        var directory = System.IO.Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("Registration store directory could not be resolved.");
        Directory.CreateDirectory(directory);

        var envelope = new PersistedRegistrationStore(
            CurrentFormatVersion,
            Ordered(_registrations.Values).Select(PersistedRegistration.FromDomain).ToArray());
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        if (bytes.Length > _maxStoreFileBytes)
        {
            throw new InvalidOperationException("Registration store exceeds its bounded file size.");
        }

        var tempPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static Dictionary<Guid, ServerRegistration> Load(string path, int maxStoreFileBytes)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                FileOptions.SequentialScan);
            if (stream.Length > maxStoreFileBytes)
            {
                throw new InvalidDataException("Registration store exceeds its bounded file size.");
            }

            var envelope = JsonSerializer.Deserialize<PersistedRegistrationStore>(stream, JsonOptions)
                ?? throw new InvalidDataException("Registration store is empty or invalid.");

            if (envelope.Version != CurrentFormatVersion)
            {
                throw new InvalidDataException("Registration store format version is not supported.");
            }

            var registrations = new Dictionary<Guid, ServerRegistration>();
            foreach (var item in envelope.Registrations ?? [])
            {
                var registration = item.ToDomain();
                if (!registrations.TryAdd(registration.Id, registration))
                {
                    throw new InvalidDataException("Registration store contains a duplicate registration ID.");
                }
            }

            return registrations;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or IOException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException("Registration store is corrupt or contains invalid registration metadata.", exception);
        }
    }

    private static IOrderedEnumerable<ServerRegistration> Ordered(IEnumerable<ServerRegistration> registrations) =>
        registrations
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id);

    private sealed record PersistedRegistrationStore(int Version, PersistedRegistration[]? Registrations);

    private sealed record PersistedRegistration(
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
        public static PersistedRegistration FromDomain(ServerRegistration registration) =>
            new(
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

        public ServerRegistration ToDomain()
        {
            var secretReference = string.IsNullOrWhiteSpace(SecretReference)
                ? (ConnectionSecretReference?)null
                : new ConnectionSecretReference(SecretReference);

            return new ServerRegistration(
                Id,
                DisplayName,
                new SqlServerEndpoint(Host, Port, InstanceName, Encrypt, TrustServerCertificate),
                AuthenticationMode,
                secretReference,
                IsEnabled,
                CreatedAtUtc);
        }
    }
}
