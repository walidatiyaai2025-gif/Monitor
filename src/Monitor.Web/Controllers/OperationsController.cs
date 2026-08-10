using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;
using Monitor.Web.Models;

namespace Monitor.Web.Controllers;

[Authorize(Roles = "Administrator")]
public sealed class OperationsController : Controller
{
    private readonly IDemoMonitorService _monitor;
    private readonly IMonitorReadService _readService;
    private readonly IIncidentWorkflowService? _workflow;
    private readonly ITrendReadService? _trends;

    public OperationsController(IDemoMonitorService monitor, IMonitorReadService readService, IIncidentWorkflowService? workflow = null, ITrendReadService? trends = null)
    {
        _monitor = monitor;
        _readService = readService;
        _workflow = workflow;
        _trends = trends;
    }

    [HttpGet("/dashboard")]
    public IActionResult Dashboard() => View(_monitor.GetDashboard());

    [HttpGet("/servers")]
    public async Task<IActionResult> Servers(CancellationToken cancellationToken) =>
        View(await _readService.GetServersAsync(cancellationToken));

    [HttpGet("/servers/{id}")]
    public async Task<IActionResult> ServerDetails(string id, CancellationToken cancellationToken)
    {
        var model = await _readService.GetServerAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet("/database-health")]
    public async Task<IActionResult> DatabaseHealth(CancellationToken cancellationToken) =>
        View(new HealthModulePageViewModel("Database & Backup Health", "Cached database states and full-backup coverage.", await _readService.GetHealthModulesAsync(cancellationToken)));

    [HttpGet("/backups")]
    public async Task<IActionResult> Backups(CancellationToken cancellationToken) =>
        View("HealthModules", new HealthModulePageViewModel("Backup Health", "Full-backup coverage from the shared cached snapshot.", await _readService.GetHealthModulesAsync(cancellationToken)));

    [HttpGet("/jobs")]
    public async Task<IActionResult> Jobs(CancellationToken cancellationToken) =>
        View("HealthModules", new HealthModulePageViewModel("SQL Agent Jobs", "Aggregate job outcomes; commands and step text are never collected.", await _readService.GetHealthModulesAsync(cancellationToken)));

    [HttpGet("/storage")]
    public async Task<IActionResult> Storage(CancellationToken cancellationToken) =>
        View("HealthModules", new HealthModulePageViewModel("Storage Allocation", "Allocated database bytes only; this is not disk capacity or free space.", await _readService.GetHealthModulesAsync(cancellationToken)));

    [HttpGet("/blocking")]
    public async Task<IActionResult> Blocking(CancellationToken cancellationToken) =>
        View("HealthModules", new HealthModulePageViewModel("Blocking", "Bounded blocking counts without SQL text, plans or client identity.", await _readService.GetHealthModulesAsync(cancellationToken)));

    [HttpGet("/memory-health")]
    public async Task<IActionResult> MemoryHealth(CancellationToken cancellationToken) =>
        View(await _readService.GetServersAsync(cancellationToken));

    [HttpGet("/alerts")]
    public async Task<IActionResult> Alerts(IncidentStatus? status, FindingSeverity? severity, string? ruleId, CancellationToken cancellationToken)
    {
        await _readService.GetIncidentsAsync(cancellationToken);
        return _workflow is null
            ? View(new IncidentCenterViewModel([], new(0, 0, 0, 0, 0), new(status, severity, ruleId)))
            : View(_workflow.Query(new(status, severity, ruleId)));
    }

    [HttpGet("/alerts/{id}")]
    public async Task<IActionResult> IncidentDetails(string id, CancellationToken cancellationToken)
    {
        if (_workflow is null) return NotFound();
        var model = await _workflow.GetDetailsAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("/alerts/{id}/acknowledge")]
    [ValidateAntiForgeryToken]
    public IActionResult AcknowledgeIncident(string id) => Transition(id, _workflow?.Acknowledge(id) == true);

    [HttpPost("/alerts/{id}/resolve")]
    [ValidateAntiForgeryToken]
    public IActionResult ResolveIncident(string id) => Transition(id, _workflow?.Resolve(id) == true);

    [HttpPost("/alerts/{id}/reopen")]
    [ValidateAntiForgeryToken]
    public IActionResult ReopenIncident(string id) => Transition(id, _workflow?.Reopen(id) == true);

    private IActionResult Transition(string id, bool changed) => changed
        ? RedirectToAction(nameof(IncidentDetails), new { id })
        : Conflict(new { message = "Incident state changed or the transition is not allowed." });

    [HttpGet("/history/{registrationId:guid}")]
    public IActionResult History(Guid registrationId, string window = "6h")
    {
        var model = _trends?.Read(registrationId, window);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet("/settings")]
    public IActionResult Settings() => View();
}
