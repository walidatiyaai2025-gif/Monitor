using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Roles = "Administrator")]
public sealed class ServerConnectionsController(
    IServerRegistrationRepository registrations,
    IServerConnectionTester tester) : ControllerBase
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
}
