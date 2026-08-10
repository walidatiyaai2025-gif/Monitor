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

    public OperationsController(IDemoMonitorService monitor, IMonitorReadService readService)
    {
        _monitor = monitor;
        _readService = readService;
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
    public async Task<IActionResult> Alerts(CancellationToken cancellationToken) =>
        View(await _readService.GetIncidentsAsync(cancellationToken));

    [HttpGet("/settings")]
    public IActionResult Settings() => View();
}
