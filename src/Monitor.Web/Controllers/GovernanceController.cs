using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Manage)]
public sealed class GovernanceController : Controller
{
    private readonly IGovernanceRetentionService _governance;

    public GovernanceController(
        IServerRegistrationRepository registrations,
        IHealthIncidentRepository incidents,
        IOperatorMetadataStore metadata,
        IAuditStore audit,
        TimeProvider timeProvider)
    {
        _governance = new GovernanceRetentionService(registrations, incidents, metadata, audit, timeProvider);
    }

    [HttpGet("/governance/retention")]
    public IActionResult Index() => View(_governance.DryRun());

    [HttpPost("/governance/retention/apply")]
    [ValidateAntiForgeryToken]
    public IActionResult Apply()
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor)) return Forbid();
        var count = _governance.Apply(actor);
        TempData["GovernanceStatus"] = $"Applied {count} bounded governance prune receipt(s).";
        return RedirectToAction(nameof(Index));
    }
}
