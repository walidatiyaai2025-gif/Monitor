using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Manage)]
public sealed class ServerConnectionsController(
    IServerRegistrationRepository registrations,
    IServerConnectionTester tester,
    ISnapshotRefreshService refreshService,
    IAuditStore audit) : ControllerBase
{
    [HttpPost("/servers/{id:guid}/test-connection")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ConnectionTestResult>> TestConnection(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var actor)) return Forbid();

        var target = id.ToString("D");
        audit.Append(actor, "server.connection.test", target, "requested");

        var registration = registrations.GetById(id);
        if (registration is null)
        {
            audit.Append(actor, "server.connection.test", target, "not-found");
            return NotFound(new ConnectionTestResult(
                ConnectionTestStatus.RegistrationNotFound,
                "Server registration was not found.",
                0));
        }

        var result = await tester.TestAsync(registration, cancellationToken);
        audit.Append(actor, "server.connection.test", target, result.Status.ToString());
        return result.Status == ConnectionTestStatus.Disabled
            ? Conflict(result)
            : Ok(result);
    }

    [HttpPost("/servers/{id:guid}/refresh-snapshot")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<SnapshotRefreshResult>> RefreshSnapshot(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var actor)) return Forbid();

        var target = id.ToString("D");
        audit.Append(actor, "snapshot.refresh", target, "requested");

        var result = await refreshService.RefreshAsync(id, cancellationToken);
        audit.Append(actor, "snapshot.refresh", target, result.Status.ToString());
        return result.Status switch
        {
            SnapshotRefreshStatus.RegistrationNotFound => NotFound(result),
            SnapshotRefreshStatus.Disabled => Conflict(result),
            SnapshotRefreshStatus.Throttled => StatusCode(StatusCodes.Status429TooManyRequests, result),
            SnapshotRefreshStatus.RetainedStale => StatusCode(StatusCodes.Status503ServiceUnavailable, result),
            _ => Ok(result)
        };
    }

    private bool TryActor(out string actor)
    {
        var identityName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(identityName))
        {
            actor = string.Empty;
            return false;
        }

        actor = EnterpriseOperatorValidation.NormalizeActor(identityName);
        return true;
    }
}
