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
        lock (_gate)
        {
            var current = registrations.GetById(registrationId);
            if (current is null) return new(ServerTargetLifecycleStatus.NotFound, "Server registration was not found.");
            if (current.IsEnabled == enabled)
                return new(ServerTargetLifecycleStatus.AlreadyInState, enabled ? "Monitoring is already enabled." : "Monitoring is already paused.");

            var auditActor = actor.Trim();
            var auditTarget = current.Id.ToString("D");
            audit.Append(auditActor, "server.monitoring", auditTarget, "requested");
            registrations.Upsert(new ServerRegistration(
                current.Id, current.DisplayName, current.Endpoint, current.AuthenticationMode,
                current.SecretReference, enabled, current.CreatedAtUtc));
            if (!enabled) cache.Evict(current.Id);
            var outcome = enabled ? "enabled" : "disabled";
            audit.Append(auditActor, "server.monitoring", auditTarget, outcome);
            return new(
                enabled ? ServerTargetLifecycleStatus.Enabled : ServerTargetLifecycleStatus.Disabled,
                enabled
                    ? "Monitoring enabled. Test the connection before collecting a new snapshot."
                    : "Monitoring paused. Metadata and history are retained.");
        }
    }
}
