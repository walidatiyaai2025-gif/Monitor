using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed class SecretStoreOptions
{
    public const string SectionName = "SecretStore";
    public string Path { get; set; } = "App_Data/secrets.json";
    public string KeyRingPath { get; set; } = "App_Data/keyring";
}

internal sealed class ProtectedFileConnectionSecretStore : IConnectionSecretStore, IRuntimeCredentialWriter, IOwnedConnectionSecretStore
{
    private const string Prefix = "local:v1:";
    private const int MaxEntries = 1024;
    private const int MaxReferenceLength = 128;
    private const int MaxProtectedPayloadLength = 16 * 1024;
    private const int MaxUsernameLength = 128;
    private const int MaxPasswordLength = 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _path;
    private readonly IDataProtector _protector;
    private readonly IConfiguration _configuration;
    private readonly IExternalConnectionSecretProvider[] _externalProviders;
    private readonly CredentialPolicyOptions _credentialPolicy;
    private readonly object _gate = new();
    private Dictionary<string, string> _entries;

    public ProtectedFileConnectionSecretStore(
        string path,
        IDataProtectionProvider protectionProvider,
        IConfiguration configuration,
        IEnumerable<IExternalConnectionSecretProvider> externalProviders,
        CredentialPolicyOptions? credentialPolicy = null)
    {
        _path = Path.GetFullPath(path);
        _protector = protectionProvider.CreateProtector("Monitor.SqlSecrets.v1");
        _configuration = configuration;
        _externalProviders = externalProviders.ToArray();
        _credentialPolicy = credentialPolicy ?? new CredentialPolicyOptions();
        _entries = Load();
    }

    public async ValueTask<SqlLoginSecret?> ResolveAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (reference.Value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            string? protectedPayload;
            lock (_gate) _entries.TryGetValue(reference.Value, out protectedPayload);
            if (protectedPayload is null) return null;
            try
            {
                var payload = JsonSerializer.Deserialize<SecretPayload>(
                    _protector.CreateProtector(reference.Value).Unprotect(protectedPayload), JsonOptions);
                return payload is null || string.IsNullOrWhiteSpace(payload.Username) || string.IsNullOrEmpty(payload.Password)
                    ? null
                    : new SqlLoginSecret(payload.Username, payload.Password);
            }
            catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException or JsonException)
            {
                return null;
            }
        }

        var provider = _externalProviders.FirstOrDefault(candidate => candidate.Handles(reference));
        if (provider is not null) return await provider.ResolveAsync(reference, cancellationToken);
        var section = _configuration.GetSection($"ConnectionSecrets:{reference.Value}");
        var username = section["Username"];
        var password = section["Password"];
        return string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)
            ? null
            : new SqlLoginSecret(username.Trim(), password);
    }

    public ValueTask<ConnectionSecretReference> StoreAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureLocalCredentialCreationAllowed();
        ValidateCredentialBounds(username, password);

        var reference = new ConnectionSecretReference($"{Prefix}{Guid.NewGuid():N}");
        var plaintext = JsonSerializer.Serialize(new SecretPayload(username.Trim(), password), JsonOptions);
        var protectedPayload = _protector.CreateProtector(reference.Value).Protect(plaintext);
        lock (_gate)
        {
            EnsureCapacityFor(reference.Value);
            var candidate = new Dictionary<string, string>(_entries, StringComparer.Ordinal) { [reference.Value] = protectedPayload };
            Persist(candidate);
            _entries = candidate;
        }
        return ValueTask.FromResult(reference);
    }

    public ValueTask StoreAsync(ConnectionSecretReference reference, SqlLoginSecret secret, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOwnedReference(reference.Value);
        EnsureLocalCredentialCreationAllowed();
        ValidateCredentialBounds(secret.Username, secret.Password);

        var plaintext = JsonSerializer.Serialize(new SecretPayload(secret.Username.Trim(), secret.Password), JsonOptions);
        var protectedPayload = _protector.CreateProtector(reference.Value).Protect(plaintext);
        lock (_gate)
        {
            EnsureCapacityFor(reference.Value);
            var candidate = new Dictionary<string, string>(_entries, StringComparer.Ordinal)
            {
                [reference.Value] = protectedPayload
            };
            Persist(candidate);
            _entries = candidate;
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default) =>
        DeleteOwnedAsync(reference, cancellationToken);

    public bool Owns(ConnectionSecretReference reference) =>
        reference.Value.StartsWith(Prefix, StringComparison.Ordinal);

    public IReadOnlyList<ConnectionSecretReference> GetOwnedReferences()
    {
        lock (_gate)
        {
            return _entries.Keys
                .Where(key => key.StartsWith(Prefix, StringComparison.Ordinal))
                .OrderBy(key => key, StringComparer.Ordinal)
                .Select(key => new ConnectionSecretReference(key))
                .ToArray();
        }
    }

    public ValueTask DeleteOwnedAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Owns(reference)) return ValueTask.CompletedTask;
        lock (_gate)
        {
            var candidate = new Dictionary<string, string>(_entries, StringComparer.Ordinal);
            if (candidate.Remove(reference.Value))
            {
                Persist(candidate);
                _entries = candidate;
            }
        }
        return ValueTask.CompletedTask;
    }

    private Dictionary<string, string> Load()
    {
        if (!File.Exists(_path)) return new(StringComparer.Ordinal);
        try
        {
            var envelope = JsonSerializer.Deserialize<SecretEnvelope>(File.ReadAllText(_path), JsonOptions);
            if (envelope?.Version != 1 || envelope.Entries is null) throw new InvalidOperationException();
            ValidateEntries(envelope.Entries);
            return new(envelope.Entries, StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidOperationException)
        {
            throw new InvalidOperationException("The protected SQL secret store is invalid.", exception);
        }
    }

    private void Persist(Dictionary<string, string> entries)
    {
        ValidateEntries(entries);
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new SecretEnvelope(1, entries), JsonOptions);
            using (var stream = new FileStream(
                temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                16 * 1024, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, _path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private void EnsureCapacityFor(string reference)
    {
        if (!_entries.ContainsKey(reference) && _entries.Count >= MaxEntries)
        {
            throw new InvalidOperationException("The protected SQL secret store reached its bounded entry limit.");
        }
    }

    private void EnsureLocalCredentialCreationAllowed()
    {
        if (!_credentialPolicy.AllowLocalOwnedCredentials)
        {
            throw new InvalidOperationException("Local SQL credential creation is disabled by deployment policy.");
        }
    }

    private static void ValidateCredentialBounds(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > MaxUsernameLength ||
            string.IsNullOrEmpty(password) || password.Length > MaxPasswordLength)
        {
            throw new ArgumentException("SQL credentials are outside the supported bounds.");
        }
    }

    private static void ValidateEntries(IReadOnlyDictionary<string, string> entries)
    {
        if (entries.Count > MaxEntries)
        {
            throw new InvalidOperationException("The protected SQL secret store exceeds its bounded entry limit.");
        }

        foreach (var pair in entries)
        {
            ValidateOwnedReference(pair.Key);
            if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length > MaxProtectedPayloadLength)
            {
                throw new InvalidOperationException("The protected SQL secret store contains an invalid bounded payload.");
            }
        }
    }

    private static void ValidateOwnedReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) ||
            !reference.StartsWith(Prefix, StringComparison.Ordinal) ||
            reference.Length > MaxReferenceLength ||
            reference.Any(char.IsControl))
        {
            throw new InvalidOperationException("The protected SQL secret store contains an invalid Monitor-owned reference.");
        }
    }

    private sealed record SecretPayload(string Username, string Password);
    private sealed record SecretEnvelope(int Version, Dictionary<string, string> Entries);
}
