using System.Collections.Concurrent;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface IServerRegistrationRepository
{
    IReadOnlyList<ServerRegistration> GetAll();
    ServerRegistration? GetById(Guid id);
    void Upsert(ServerRegistration registration);
    bool Remove(Guid id);
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
}
