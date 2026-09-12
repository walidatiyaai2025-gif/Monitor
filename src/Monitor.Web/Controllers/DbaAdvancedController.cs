using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class DbaAdvancedController : Controller
{
    private readonly DbaAdvancedDiagnosticsService _diagnostics;
    private readonly IAuditStore _audit;

    public DbaAdvancedController(
        IServerRegistrationRepository registrations,
        IServiceProvider services,
        IAuditStore audit,
        TimeProvider timeProvider)
    {
        _diagnostics = new DbaAdvancedDiagnosticsService(registrations, services.GetRequiredService<IConnectionSecretStore>(), timeProvider);
        _audit = audit;
    }

    [HttpGet("/dba/advanced/{id:guid}")]
    public IActionResult Index(Guid id)
    {
        try
        {
            return View("~/Views/Dba/Advanced.cshtml", _diagnostics.GetCached(id));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("/dba/advanced/{id:guid}/inspect")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public async Task<IActionResult> Inspect(Guid id, CancellationToken cancellationToken)
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor)) return Forbid();

        _audit.Append(actor, "dba.advanced.inspect.request", id.ToString("D"), "requested");
        try
        {
            var result = await _diagnostics.InspectAsync(id, cancellationToken);
            _audit.Append(actor, "dba.advanced.inspect", id.ToString("D"), result.IsAvailable ? "completed" : "unavailable");
            TempData["DbaAdvancedInspection"] = result.StatusMessage;
            TempData["DbaAdvancedInspectionStatus"] = result.IsAvailable ? "success" : "warning";
            return RedirectToAction(nameof(Index), new { id });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
