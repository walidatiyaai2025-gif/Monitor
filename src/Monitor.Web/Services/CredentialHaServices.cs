using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum DataProtectionKeyStoreMode
{
    LocalFile,
    SharedState
}

public sealed class DataProtectionKeyStoreOptions
{
    public const string SectionName = "DataProtectionKeyStore";
    public DataProtectionKeyStoreMode Mode { get; set; } = DataProtectionKeyStoreMode.LocalFile;
    public string KeyEncryptionKeyEnvironmentVariable { get; set; } = "MONITOR_DP_KEK";

    public void Validate()
    {
        if (!Enum.IsDefined(Mode))
        {
            throw new InvalidOperationException("DataProtectionKeyStore:Mode is not supported.");
        }

        if (!IsSafeEnvironmentVariableName(KeyEncryptionKeyEnvironmentVariable))
        {
            throw new InvalidOperationException("DataProtectionKeyStore:KeyEncryptionKeyEnvironmentVariable is invalid.");
        }
    }

    private static bool IsSafeEnvironmentVariableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
            (!char.IsAsciiLetter(value[0]) && value[0] != '_'))
        {
            return false;
        }

        return value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
    }
}

public sealed class CredentialPolicyOptions
{
    public const string SectionName = "CredentialPolicy";
    public bool AllowLocalOwnedCredentials { get; set; } = true;
}

internal interface IOwnedConnectionSecretStore
{
    bool Owns(ConnectionSecretReference reference);
    IReadOnlyList<ConnectionSecretReference> GetOwnedReferences();
    ValueTask DeleteOwnedAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default);
}

public sealed class SharedEncryptedDataProtectionXmlRepository : IXmlRepository
{
    private const string DocumentKey = "monitor:dataprotection:keyring:v1";
    private const int FormatVersion = 1;
    private const int MaxEntries = 128;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly ISharedStateDocumentStore _store;
    private readonly byte[] _key;

    public SharedEncryptedDataProtectionXmlRepository(
        ISharedStateDocumentStore store,
        string keyEncryptionKeyBase64)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        if (string.IsNullOrWhiteSpace(keyEncryptionKeyBase64))
        {
            throw new InvalidOperationException("Shared Data Protection key encryption key is unavailable.");
        }

        try
        {
            _key = Convert.FromBase64String(keyEncryptionKeyBase64.Trim());
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Shared Data Protection key encryption key is invalid.", exception);
        }

        if (_key.Length != 32)
        {
            CryptographicOperations.ZeroMemory(_key);
            throw new InvalidOperationException("Shared Data Protection key encryption key must be 256 bits.");
        }
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        var state = SharedStateDocumentMutation.ReadState(_store, DocumentKey, Deserialize);
        var result = new List<XElement>(state.Count);
        foreach (var pair in state.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            result.Add(Decrypt(pair.Key, pair.Value));
        }

        return result;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        friendlyName = NormalizeFriendlyName(friendlyName);
        var encrypted = Encrypt(friendlyName, element.ToString(SaveOptions.DisableFormatting));

        SharedStateDocumentMutation.Mutate(
            _store,
            DocumentKey,
            Deserialize,
            state =>
            {
                if (!state.ContainsKey(friendlyName) && state.Count >= MaxEntries)
                {
                    throw new InvalidOperationException("Shared Data Protection key ring reached its bounded entry limit.");
                }

                state[friendlyName] = encrypted;
                return SharedStateDocumentMutation.MutationResult<Dictionary<string, EncryptedXmlElement>, bool>.Applied(state, true);
            },
            Serialize);
    }

    private EncryptedXmlElement Encrypt(string friendlyName, string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];
        var associatedData = Encoding.UTF8.GetBytes($"Monitor.DataProtection.v1|{friendlyName}");
        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag, associatedData);
            return new(
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(tag),
                Convert.ToBase64String(ciphertext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
            CryptographicOperations.ZeroMemory(associatedData);
        }
    }

    private XElement Decrypt(string friendlyName, EncryptedXmlElement encrypted)
    {
        try
        {
            var nonce = Convert.FromBase64String(encrypted.NonceBase64);
            var tag = Convert.FromBase64String(encrypted.TagBase64);
            var ciphertext = Convert.FromBase64String(encrypted.CiphertextBase64);
            if (nonce.Length != NonceSize || tag.Length != TagSize || ciphertext.Length == 0)
            {
                throw new CryptographicException();
            }

            var plaintext = new byte[ciphertext.Length];
            var associatedData = Encoding.UTF8.GetBytes($"Monitor.DataProtection.v1|{friendlyName}");
            try
            {
                using var aes = new AesGcm(_key, TagSize);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
                var xml = Encoding.UTF8.GetString(plaintext);
                return XElement.Parse(xml, LoadOptions.None);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
                CryptographicOperations.ZeroMemory(associatedData);
            }
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or System.Xml.XmlException)
        {
            throw new InvalidOperationException("Shared Data Protection key ring cannot be decrypted.", exception);
        }
    }

    private static Dictionary<string, EncryptedXmlElement> Deserialize(string? payload)
    {
        if (payload is null)
        {
            return new(StringComparer.Ordinal);
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<KeyRingEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared Data Protection key ring is invalid.");
            if (envelope.Version != FormatVersion || envelope.Entries is null || envelope.Entries.Count > MaxEntries)
            {
                throw new InvalidDataException("Shared Data Protection key ring format is invalid.");
            }

            var result = new Dictionary<string, EncryptedXmlElement>(StringComparer.Ordinal);
            foreach (var pair in envelope.Entries)
            {
                var name = NormalizeFriendlyName(pair.Key);
                if (!result.TryAdd(name, pair.Value) || string.IsNullOrWhiteSpace(pair.Value.CiphertextBase64))
                {
                    throw new InvalidDataException("Shared Data Protection key ring contains invalid entries.");
                }
            }

            return result;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Shared Data Protection key ring is corrupt.", exception);
        }
    }

    private static string Serialize(Dictionary<string, EncryptedXmlElement> state) =>
        JsonSerializer.Serialize(new KeyRingEnvelope(FormatVersion, state), SharedStateDocumentMutation.JsonOptions);

    private static string NormalizeFriendlyName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256 ||
            value.Any(character => char.IsControl(character)))
        {
            throw new ArgumentException("Data Protection key friendly name is invalid.", nameof(value));
        }

        return value.Trim();
    }

    private sealed record EncryptedXmlElement(string NonceBase64, string TagBase64, string CiphertextBase64);
    private sealed record KeyRingEnvelope(int Version, Dictionary<string, EncryptedXmlElement>? Entries);
}

public enum CredentialReplacementStatus
{
    Applied,
    RegistrationNotFound,
    NotSqlLogin,
    InvalidReference,
    SecretUnavailable,
    ConnectionRejected,
    Failed
}

public sealed record CredentialReplacementResult(
    CredentialReplacementStatus Status,
    string Message,
    ConnectionTestResult? TestResult = null)
{
    public bool Applied => Status == CredentialReplacementStatus.Applied;
}

public interface ICredentialLifecycleService
{
    Task<CredentialReplacementResult> ReplaceWithExternalReferenceAsync(
        Guid registrationId,
        string externalReference,
        string actor,
        CancellationToken cancellationToken = default);

    Task<int> CleanupOrphanedOwnedSecretsAsync(
        string actor,
        CancellationToken cancellationToken = default);
}

internal sealed class CredentialLifecycleService(
    IServerRegistrationRepository registrations,
    IConnectionSecretStore secrets,
    IServerConnectionTester tester,
    IAuditStore audit) : ICredentialLifecycleService
{
    private const string LocalPrefix = "local:v1:";

    public async Task<CredentialReplacementResult> ReplaceWithExternalReferenceAsync(
        Guid registrationId,
        string externalReference,
        string actor,
        CancellationToken cancellationToken = default)
    {
        actor = NormalizeActor(actor);
        var registration = registrations.GetById(registrationId);
        if (registration is null)
        {
            Audit(actor, registrationId, "not-found");
            return new(CredentialReplacementStatus.RegistrationNotFound, "Server registration was not found.");
        }

        if (registration.AuthenticationMode != SqlAuthenticationMode.SqlLogin)
        {
            Audit(actor, registrationId, "not-sql-login");
            return new(CredentialReplacementStatus.NotSqlLogin, "This registration does not use SQL Login authentication.");
        }

        ConnectionSecretReference nextReference;
        try
        {
            nextReference = new ConnectionSecretReference(externalReference);
            if (nextReference.Value.StartsWith(LocalPrefix, StringComparison.Ordinal) ||
                nextReference.Value.StartsWith("runtime-", StringComparison.Ordinal))
            {
                throw new ArgumentException("Reference is not external.", nameof(externalReference));
            }
        }
        catch (ArgumentException)
        {
            Audit(actor, registrationId, "invalid-reference");
            return new(CredentialReplacementStatus.InvalidReference, "Provide a valid external secret reference.");
        }

        var resolved = await secrets.ResolveAsync(nextReference, cancellationToken);
        if (resolved is null)
        {
            Audit(actor, registrationId, "secret-unavailable");
            return new(CredentialReplacementStatus.SecretUnavailable, "The replacement credential is unavailable.");
        }

        var candidate = new ServerRegistration(
            registration.Id,
            registration.DisplayName,
            registration.Endpoint,
            registration.AuthenticationMode,
            nextReference,
            registration.IsEnabled,
            registration.CreatedAtUtc);

        var test = await tester.TestAsync(candidate, cancellationToken);
        if (!test.Succeeded)
        {
            Audit(actor, registrationId, $"test-{test.Status}");
            return new(CredentialReplacementStatus.ConnectionRejected, "The replacement credential did not pass Test Connection.", test);
        }

        var previousReference = registration.SecretReference;
        try
        {
            registrations.Upsert(candidate);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or SharedStateConcurrencyException)
        {
            Audit(actor, registrationId, "commit-failed");
            return new(CredentialReplacementStatus.Failed, "The credential reference could not be committed safely.", test);
        }

        if (previousReference is not null &&
            !string.Equals(previousReference.Value.Value, nextReference.Value, StringComparison.Ordinal) &&
            secrets is IOwnedConnectionSecretStore owned && owned.Owns(previousReference.Value))
        {
            try
            {
                await owned.DeleteOwnedAsync(previousReference.Value, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                // The registration is already safe on the tested replacement reference.
                // A retained encrypted local entry is harmless and is removable by the orphan cleanup command.
            }
        }

        Audit(actor, registrationId, "applied");
        return new(CredentialReplacementStatus.Applied, "Credential reference replaced and Test Connection succeeded.", test);
    }

    public async Task<int> CleanupOrphanedOwnedSecretsAsync(string actor, CancellationToken cancellationToken = default)
    {
        actor = NormalizeActor(actor);
        if (secrets is not IOwnedConnectionSecretStore owned)
        {
            audit.Append(actor, "credential.cleanup", "owned-secrets", "unsupported");
            return 0;
        }

        var referenced = registrations.GetAll()
            .Where(item => item.SecretReference is not null)
            .Select(item => item.SecretReference!.Value.Value)
            .ToHashSet(StringComparer.Ordinal);
        var removed = 0;
        foreach (var reference in owned.GetOwnedReferences())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (referenced.Contains(reference.Value))
            {
                continue;
            }

            await owned.DeleteOwnedAsync(reference, cancellationToken);
            removed++;
        }

        audit.Append(actor, "credential.cleanup", "owned-secrets", removed == 0 ? "none" : "removed");
        return removed;
    }

    private void Audit(string actor, Guid registrationId, string outcome) =>
        audit.Append(actor, "credential.reference.replace", registrationId.ToString("D"), outcome);

    private static string NormalizeActor(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new InvalidOperationException("Authenticated actor identity is required.");
        }

        return actor.Trim();
    }
}

public sealed record CredentialReadinessViewModel(
    DataProtectionKeyStoreMode KeyRingMode,
    bool SharedKeyRingReady,
    int SqlLoginRegistrations,
    int LocalOwnedRegistrations,
    int ExternalRegistrations,
    bool MultiNodeCredentialReady,
    string Status,
    string Message);

public interface ICredentialReadinessService
{
    CredentialReadinessViewModel Get();
}

internal sealed class CredentialReadinessService(
    IServerRegistrationRepository registrations,
    DataProtectionKeyStoreOptions keyStoreOptions,
    CredentialPolicyOptions credentialPolicy) : ICredentialReadinessService
{
    public CredentialReadinessViewModel Get()
    {
        var sqlLogin = registrations.GetAll().Where(item => item.AuthenticationMode == SqlAuthenticationMode.SqlLogin).ToArray();
        var localOwned = sqlLogin.Count(item => item.SecretReference?.Value.StartsWith("local:v1:", StringComparison.Ordinal) == true);
        var external = sqlLogin.Length - localOwned;
        var sharedKeyRing = keyStoreOptions.Mode == DataProtectionKeyStoreMode.SharedState;
        var ready = sharedKeyRing && !credentialPolicy.AllowLocalOwnedCredentials && localOwned == 0;
        return new(
            keyStoreOptions.Mode,
            sharedKeyRing,
            sqlLogin.Length,
            localOwned,
            external,
            ready,
            ready ? "HA credential ready" : "HA credential blocked",
            ready
                ? "SQL Login registrations use external references and the Data Protection key ring is shared."
                : "Multi-node credential readiness requires a shared encrypted key ring, local-owned credential creation disabled, and zero local-owned registration references.");
    }
}
