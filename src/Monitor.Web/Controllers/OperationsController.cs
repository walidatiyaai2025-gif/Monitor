using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

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
    public IActionResult DatabaseHealth() => View(_monitor.GetServers());

    [HttpGet("/memory-health")]
    public async Task<IActionResult> MemoryHealth(CancellationToken cancellationToken) =>
        View(await _readService.GetServersAsync(cancellationToken));

    [HttpGet("/alerts")]
    public IActionResult Alerts() => View(_monitor.GetIncidents());

    [HttpGet("/settings")]
    public IActionResult Settings() => View();
}
