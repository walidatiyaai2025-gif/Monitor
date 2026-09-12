using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class DbaController(
    IDbaCommandCenterService commandCenter,
    IAuditStore audit) : Controller
{
    [HttpGet("/dba")]
    public IActionResult Index() => View(commandCenter.GetCached());

    [HttpPost("/dba/{id:guid}/inspect")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public async Task<IActionResult> Inspect(Guid id, CancellationToken cancellationToken)
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor)) return Forbid();

        audit.Append(actor, "dba.inspect.request", id.ToString("D"), "requested");
        var result = await commandCenter.InspectAsync(id, cancellationToken);
        audit.Append(actor, "dba.inspect", id.ToString("D"), result.Success ? "completed" : "unavailable");
        TempData["DbaInspection"] = result.Message;
        TempData["DbaInspectionStatus"] = result.Success ? "success" : "warning";
        return RedirectToAction(nameof(Index), new { server = id.ToString("D") });
    }

    [HttpGet("/dba/{id:guid}/backup-plan")]
    public IActionResult BackupPlan(Guid id, string? database = null)
    {
        try
        {
            var plan = commandCenter.BuildBackupManagementPlan(id, database);
            Response.Headers.CacheControl = "no-store, max-age=0";
            Response.Headers.Pragma = "no-cache";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Content(plan, "text/plain; charset=utf-8");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("/api/dba/summary")]
    public IActionResult Summary()
    {
        Response.Headers.CacheControl = "no-store, max-age=0";
        var model = commandCenter.GetCached();
        return Json(new
        {
            generatedAtUtc = model.GeneratedAtUtc,
            criticalOrHighRecommendations = model.CriticalOrHighRecommendations,
            queryHotspots = model.QueryHotspots,
            openTodoItems = model.OpenTodoItems,
            servers = model.Servers.Select(server => new
            {
                id = server.RegistrationId,
                name = server.DisplayName,
                endpoint = server.Endpoint,
                enabled = server.IsEnabled,
                collectedAtUtc = server.BaseSnapshot?.CollectedAtUtc,
                databasesOnline = server.BaseSnapshot?.DatabaseOnline,
                databasesTotal = server.BaseSnapshot?.DatabaseTotal,
                sqlMemoryPercent = server.BaseSnapshot?.Memory?.SqlProcessMemoryUtilizationPercent,
                availableMemoryKb = server.BaseSnapshot?.Memory?.AvailablePhysicalMemoryKb,
                blockedRequests = server.BaseSnapshot?.Blocking?.BlockedRequests,
                runnableTasks = server.BaseSnapshot?.Performance?.RunnableTasks,
                pendingIoRequests = server.BaseSnapshot?.Performance?.PendingIoRequests,
                fullBackupGaps = server.Diagnostics?.Databases.Count(database => database.FullBackupOverdue) ?? server.BaseSnapshot?.Backups?.MissingFullBackupLast24Hours ?? 0,
                queryHotspots = server.Diagnostics?.QueryHotspots.Count ?? 0,
                recommendations = server.Recommendations.Take(5).Select(item => new { item.Severity, item.Category, item.Title }),
                todos = server.TodoItems.Take(8).Select(item => new { item.Priority, item.What, item.When, item.Where })
            })
        });
    }
}
