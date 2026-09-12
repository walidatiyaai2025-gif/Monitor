using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Manage)]
public sealed class AdminUpgradeController(
    IWebHostEnvironment environment,
    TimeProvider timeProvider,
    IAuditStore audit) : Controller
{
    private UpgradePackageStaging Service => new(environment, timeProvider);

    [HttpGet("/admin/upgrades")]
    public IActionResult Index() => View(Service.GetDashboard());

    [HttpPost("/admin/upgrades/stage")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(UpgradePackageStaging.MaxUploadBytes + 1024 * 1024)]
    public async Task<IActionResult> Stage(IFormFile? package, string? sha256, CancellationToken cancellationToken)
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor)) return Forbid();
        if (package is null)
        {
            TempData["UpgradeMessage"] = "Select a Monitor .zip upgrade package.";
            TempData["UpgradeStatus"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        audit.Append(actor, "upgrade.stage.request", package.FileName, "requested");
        await using var stream = package.OpenReadStream();
        var result = await Service.StageAsync(stream, package.FileName, package.Length, sha256 ?? string.Empty, cancellationToken);
        audit.Append(actor, "upgrade.stage", package.FileName, result.Success ? "completed" : "rejected");
        TempData["UpgradeMessage"] = result.Message;
        TempData["UpgradeStatus"] = result.Success ? "success" : "warning";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/upgrades/request-apply")]
    [ValidateAntiForgeryToken]
    public IActionResult RequestApply()
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor)) return Forbid();
        audit.Append(actor, "upgrade.apply.request", "staged-package", "requested");
        var result = Service.RequestApply(actor);
        audit.Append(actor, "upgrade.apply.queue", "staged-package", result.Success ? "queued" : "rejected");
        TempData["UpgradeMessage"] = result.Message;
        TempData["UpgradeStatus"] = result.Success ? "success" : "warning";
        return RedirectToAction(nameof(Index));
    }
}
