using System.Collections.Concurrent;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum ServerRegistrationFieldMutationStatus
{
    Applied,
    NotFound,
    Conflict,
    Unchanged
}

public sealed record ServerRegistrationFieldMutationResult(
    ServerRegistrationFieldMutationStatus Status,
    ServerRegistration? Registration)
{
    public bool Applied => Status == ServerRegistrationFieldMutationStatus.Applied;
}

public interface IServerRegistrationRepository
{
    IReadOnlyList<ServerRegistration> GetAll();
    ServerRegistration? GetById(Guid id);
    void Upsert(ServerRegistration registration);
    bool Remove(Guid id);

    ServerRegistrationFieldMutationResult TryReplaceSecretReference(
        Guid id,
        ConnectionSecretReference? expectedReference,
        ConnectionSecretReference nextReference)
    {
        var current = GetById(id);
        if (current is null)
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

        var updated = CopyWithSecretReference(current, nextReference);
        Upsert(updated);
        return new(ServerRegistrationFieldMutationStatus.Applied, updated);
    }

    ServerRegistrationFieldMutationResult SetEnabled(Guid id, bool enabled)
    {
        var current = GetById(id);
        if (current is null)
        {
            return new(ServerRegistrationFieldMutationStatus.NotFound, null);
        }

        if (current.IsEnabled == enabled)
        {
            return new(ServerRegistrationFieldMutationStatus.Unchanged, current);
        }

        var updated = CopyWithEnabled(current, enabled);
        Upsert(updated);
        return new(ServerRegistrationFieldMutationStatus.Applied, updated);
    }

    private static bool SecretReferencesEqual(
        ConnectionSecretReference? left,
        ConnectionSecretReference? right) =>
        string.Equals(left?.Value, right?.Value, StringComparison.Ordinal);

    private static ServerRegistration CopyWithSecretReference(
        ServerRegistration current,
        ConnectionSecretReference nextReference) =>
        new(
            current.Id,
            current.DisplayName,
            current.Endpoint,
            current.AuthenticationMode,
            nextReference,
            current.IsEnabled,
            current.CreatedAtUtc);

    private static ServerRegistration CopyWithEnabled(ServerRegistration current, bool enabled) =>
        new(
            current.Id,
            current.DisplayName,
            current.Endpoint,
            current.AuthenticationMode,
            current.SecretReference,
            enabled,
            current.CreatedAtUtc);
}

public sealed class InMemoryServerRegistrationRepository : IServerRegistrationRepository
{
    private readonly ConcurrentDictionary<Guid, ServerRegistration> _registrations = new();

    public IReadOnlyList<ServerRegistration> GetAll() =>
        _registrations.Values.OrderBy(registration => registration.DisplayName).ToArray();

    public ServerRegistration? GetById(Guid id) =>
        _registrations.TryGetValue(id, out var registration) ? registration : null;

    public void Upsert(ServerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _registrations[registration.Id] = registration;
    }

    public bool Remove(Guid id) => _registrations.TryRemove(id, out _);

    public ServerRegistrationFieldMutationResult TryReplaceSecretReference(
        Guid id,
        ConnectionSecretReference? expectedReference,
        ConnectionSecretReference nextReference)
    {
        while (true)
        {
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
            if (_registrations.TryUpdate(id, updated, current))
            {
                return new(ServerRegistrationFieldMutationStatus.Applied, updated);
            }
        }
    }

    public ServerRegistrationFieldMutationResult SetEnabled(Guid id, bool enabled)
    {
        while (true)
        {
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
            if (_registrations.TryUpdate(id, updated, current))
            {
                return new(ServerRegistrationFieldMutationStatus.Applied, updated);
            }
        }
    }

    private static bool SecretReferencesEqual(
        ConnectionSecretReference? left,
        ConnectionSecretReference? right) =>
        string.Equals(left?.Value, right?.Value, StringComparison.Ordinal);
}
