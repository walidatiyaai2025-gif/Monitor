using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum ServerTargetLifecycleStatus { Enabled, Disabled, AlreadyInState, NotFound }

public sealed record ServerTargetLifecycleResult(ServerTargetLifecycleStatus Status, string Message);

public interface IServerTargetLifecycleService
{
    ServerTargetLifecycleResult SetEnabled(Guid registrationId, bool enabled, string actor);
}

internal sealed class ServerTargetLifecycleService(
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache,
    ServerRegistrationMutationGate mutationGate,
    IAuditStore audit) : IServerTargetLifecycleService
{
    internal ServerTargetLifecycleService(
        IServerRegistrationRepository registrations,
        IServerHealthSnapshotCache cache,
        IAuditStore audit)
        : this(registrations, cache, new ServerRegistrationMutationGate(), audit)
    {
    }

    public ServerTargetLifecycleResult SetEnabled(Guid registrationId, bool enabled, string actor)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Actor is required.", nameof(actor));
        actor = actor.Trim();

        mutationGate.Wait();
        try
        {
            var current = registrations.GetById(registrationId);
            if (current is null) return new(ServerTargetLifecycleStatus.NotFound, "Server registration was not found.");
            if (current.IsEnabled == enabled)
                return new(ServerTargetLifecycleStatus.AlreadyInState, enabled ? "Monitoring is already enabled." : "Monitoring is already paused.");

            audit.Append(actor, "server.monitoring.request", current.Id.ToString("D"), enabled ? "enable" : "disable");
            var mutation = registrations.SetEnabled(registrationId, enabled);
            if (mutation.Status == ServerRegistrationFieldMutationStatus.NotFound)
            {
                return new(ServerTargetLifecycleStatus.NotFound, "Server registration was not found.");
            }
            if (mutation.Status == ServerRegistrationFieldMutationStatus.Unchanged)
            {
                if (!enabled) cache.Evict(registrationId);
                return new(ServerTargetLifecycleStatus.AlreadyInState, enabled ? "Monitoring is already enabled." : "Monitoring is already paused.");
            }
            if (mutation.Status == ServerRegistrationFieldMutationStatus.Conflict)
            {
                throw new InvalidOperationException("Server monitoring state could not be updated safely.");
            }

            if (!enabled) cache.Evict(registrationId);
            var outcome = enabled ? "enabled" : "disabled";
            audit.Append(actor, "server.monitoring", registrationId.ToString("D"), outcome);
            return new(
                enabled ? ServerTargetLifecycleStatus.Enabled : ServerTargetLifecycleStatus.Disabled,
                enabled
                    ? "Monitoring enabled. Test the connection before collecting a new snapshot."
                    : "Monitoring paused. Metadata and history are retained.");
        }
        finally
        {
            mutationGate.Release();
        }
    }
}
