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
    IAuditStore audit) : IServerTargetLifecycleService
{
    private readonly object _gate = new();

    public ServerTargetLifecycleResult SetEnabled(Guid registrationId, bool enabled, string actor)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Actor is required.", nameof(actor));
        actor = actor.Trim();
        lock (_gate)
        {
            var current = registrations.GetById(registrationId);
            if (current is null) return new(ServerTargetLifecycleStatus.NotFound, "Server registration was not found.");
            if (current.IsEnabled == enabled)
                return new(ServerTargetLifecycleStatus.AlreadyInState, enabled ? "Monitoring is already enabled." : "Monitoring is already paused.");

            audit.Append(actor, "server.monitoring.request", current.Id.ToString("D"), enabled ? "enable" : "disable");
            registrations.Upsert(new ServerRegistration(
                current.Id, current.DisplayName, current.Endpoint, current.AuthenticationMode,
                current.SecretReference, enabled, current.CreatedAtUtc));
            if (!enabled) cache.Evict(current.Id);
            var outcome = enabled ? "enabled" : "disabled";
            audit.Append(actor, "server.monitoring", current.Id.ToString("D"), outcome);
            return new(
                enabled ? ServerTargetLifecycleStatus.Enabled : ServerTargetLifecycleStatus.Disabled,
                enabled
                    ? "Monitoring enabled. Test the connection before collecting a new snapshot."
                    : "Monitoring paused. Metadata and history are retained.");
        }
    }
}