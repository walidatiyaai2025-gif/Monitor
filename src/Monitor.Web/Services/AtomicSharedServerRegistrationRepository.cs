using System.Text.Json;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed class AtomicSharedServerRegistrationRepository : IServerRegistrationRepository
{
    private const string DocumentKey = "monitor:registrations:v1";
    private const int FormatVersion = 1;
    private readonly ISharedStateDocumentStore _store;
    private readonly SharedServerRegistrationRepository _inner;

    public AtomicSharedServerRegistrationRepository(ISharedStateDocumentStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _inner = new SharedServerRegistrationRepository(store);
    }

    public IReadOnlyList<ServerRegistration> GetAll() => _inner.GetAll();

    public ServerRegistration? GetById(Guid id) => _inner.GetById(id);

    public void Upsert(ServerRegistration registration) => _inner.Upsert(registration);

    public bool Remove(Guid id) => _inner.Remove(id);

    public bool ImportIfEmpty(IEnumerable<ServerRegistration> registrations) =>
        _inner.ImportIfEmpty(registrations);

    public ServerRegistrationFieldMutationResult TryReplaceSecretReference(
        Guid id,
        ConnectionSecretReference? expectedReference,
        ConnectionSecretReference nextReference) =>
        SharedStateDocumentMutation.Mutate(
            _store,
            DocumentKey,
            Deserialize,
            state =>
            {
                if (!state.TryGetValue(id, out var current))
                {
                    return Unchanged(state, ServerRegistrationFieldMutationStatus.NotFound, null);
                }

                if (!SecretReferencesEqual(current.SecretReference, expectedReference))
                {
                    return Unchanged(state, ServerRegistrationFieldMutationStatus.Conflict, current);
                }

                if (SecretReferencesEqual(current.SecretReference, nextReference))
                {
                    return Unchanged(state, ServerRegistrationFieldMutationStatus.Unchanged, current);
                }

                var updated = new ServerRegistration(
                    current.Id,
                    current.DisplayName,
                    current.Endpoint,
                    current.AuthenticationMode,
                    nextReference,
                    current.IsEnabled,
                    current.CreatedAtUtc);
                state[id] = updated;
                return Applied(state, updated);
            },
            Serialize);

    public ServerRegistrationFieldMutationResult SetEnabled(Guid id, bool enabled) =>
        SharedStateDocumentMutation.Mutate(
            _store,
            DocumentKey,
            Deserialize,
            state =>
            {
                if (!state.TryGetValue(id, out var current))
                {
                    return Unchanged(state, ServerRegistrationFieldMutationStatus.NotFound, null);
                }

                if (current.IsEnabled == enabled)
                {
                    return Unchanged(state, ServerRegistrationFieldMutationStatus.Unchanged, current);
                }

                var updated = new ServerRegistration(
                    current.Id,
                    current.DisplayName,
                    current.Endpoint,
                    current.AuthenticationMode,
                    current.SecretReference,
                    enabled,
                    current.CreatedAtUtc);
                state[id] = updated;
                return Applied(state, updated);
            },
            Serialize);

    private static SharedStateDocumentMutation.MutationResult<Dictionary<Guid, ServerRegistration>, ServerRegistrationFieldMutationResult> Applied(
        Dictionary<Guid, ServerRegistration> state,
        ServerRegistration registration) =>
        SharedStateDocumentMutation.MutationResult<Dictionary<Guid, ServerRegistration>, ServerRegistrationFieldMutationResult>.Applied(
            state,
            new ServerRegistrationFieldMutationResult(ServerRegistrationFieldMutationStatus.Applied, registration));

    private static SharedStateDocumentMutation.MutationResult<Dictionary<Guid, ServerRegistration>, ServerRegistrationFieldMutationResult> Unchanged(
        Dictionary<Guid, ServerRegistration> state,
        ServerRegistrationFieldMutationStatus status,
        ServerRegistration? registration) =>
        SharedStateDocumentMutation.MutationResult<Dictionary<Guid, ServerRegistration>, ServerRegistrationFieldMutationResult>.Unchanged(
            state,
            new ServerRegistrationFieldMutationResult(status, registration));

    private static bool SecretReferencesEqual(
        ConnectionSecretReference? left,
        ConnectionSecretReference? right) =>
        string.Equals(left?.Value, right?.Value, StringComparison.Ordinal);

    private static Dictionary<Guid, ServerRegistration> Deserialize(string? payload)
    {
        if (payload is null)
        {
            return [];
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<RegistrationEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared registration state is empty or invalid.");
            if (envelope.Version != FormatVersion || envelope.Registrations is null)
            {
                throw new InvalidDataException("Shared registration state format is not supported.");
            }

            var state = new Dictionary<Guid, ServerRegistration>();
            foreach (var item in envelope.Registrations)
            {
                var registration = item.ToDomain();
                ValidateRegistration(registration);
                if (!state.TryAdd(registration.Id, registration))
                {
                    throw new InvalidDataException("Shared registration state contains duplicate IDs.");
                }
            }

            return state;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException("Shared registration state is corrupt.", exception);
        }
    }

    private static string Serialize(Dictionary<Guid, ServerRegistration> state)
    {
        var envelope = new RegistrationEnvelope(
            FormatVersion,
            state.Values
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id)
                .Select(PersistedRegistration.FromDomain)
                .ToArray());
        return JsonSerializer.Serialize(envelope, SharedStateDocumentMutation.JsonOptions);
    }

    private static void ValidateRegistration(ServerRegistration registration)
    {
        if (registration.Id == Guid.Empty || string.IsNullOrWhiteSpace(registration.DisplayName) ||
            registration.CreatedAtUtc == default)
        {
            throw new InvalidDataException("Shared registration is outside the allowed metadata contract.");
        }

        if (registration.AuthenticationMode == SqlAuthenticationMode.SqlLogin && registration.SecretReference is null)
        {
            throw new InvalidDataException("SQL Login registration is missing its opaque secret reference.");
        }
    }

    private sealed record RegistrationEnvelope(int Version, PersistedRegistration[]? Registrations);

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
            var reference = string.IsNullOrWhiteSpace(SecretReference)
                ? (ConnectionSecretReference?)null
                : new ConnectionSecretReference(SecretReference);

            return new ServerRegistration(
                Id,
                DisplayName,
                new SqlServerEndpoint(Host, Port, InstanceName, Encrypt, TrustServerCertificate),
                AuthenticationMode,
                reference,
                IsEnabled,
                CreatedAtUtc);
        }
    }
}
