using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Manage)]
public sealed class GovernanceController : Controller
{
    private readonly IGovernanceRetentionService _governance;
    private readonly IAuditStore _audit;

    public GovernanceController(
        IServerRegistrationRepository registrations,
        IHealthIncidentRepository incidents,
        IOperatorMetadataStore metadata,
        IAuditStore audit,
        TimeProvider timeProvider,
        IGovernancePruneStateStore? pruneState = null)
    {
        _audit = audit;
        _governance = new GovernanceRetentionService(
            registrations,
            incidents,
            metadata,
            audit,
            timeProvider,
            pruneState: pruneState ?? GovernancePruneStateMigration.CreateTransient(audit, metadata));
    }

    [HttpGet("/governance/retention")]
    public IActionResult Index() => View(_governance.DryRun());

    [HttpPost("/governance/retention/apply")]
    [ValidateAntiForgeryToken]
    public IActionResult Apply(string? confirmation)
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor)) return Forbid();

        if (!string.Equals(confirmation?.Trim(), "PRUNE", StringComparison.Ordinal))
        {
            _audit.Append(actor, "governance.cleanup", "operator-metadata", "confirmation-rejected");
            TempData["GovernanceStatus"] = "Type PRUNE exactly to confirm the reviewed retention plan.";
            return RedirectToAction(nameof(Index));
        }

        var count = _governance.Apply(actor);
        TempData["GovernanceStatus"] = $"Applied {count} bounded governance prune receipt(s).";
        return RedirectToAction(nameof(Index));
    }
}
