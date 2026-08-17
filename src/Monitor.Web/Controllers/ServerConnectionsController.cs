using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Manage)]
public sealed class ServerConnectionsController(
    IServerRegistrationRepository registrations,
    IServerConnectionTester tester,
    ISnapshotRefreshService refreshService) : ControllerBase
{
    [HttpPost("/servers/{id:guid}/test-connection")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ConnectionTestResult>> TestConnection(
        Guid id,
        CancellationToken cancellationToken)
    {
        var registration = registrations.GetById(id);
        if (registration is null)
        {
            return NotFound(new ConnectionTestResult(
                ConnectionTestStatus.RegistrationNotFound,
                "Server registration was not found.",
                0));
        }

        var result = await tester.TestAsync(registration, cancellationToken);
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
        var result = await refreshService.RefreshAsync(id, cancellationToken);
        return result.Status switch
        {
            SnapshotRefreshStatus.RegistrationNotFound => NotFound(result),
            SnapshotRefreshStatus.Disabled => Conflict(result),
            SnapshotRefreshStatus.Throttled => StatusCode(StatusCodes.Status429TooManyRequests, result),
            _ => Ok(result)
        };
    }
}
