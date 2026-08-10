using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Roles = "Administrator")]
public sealed class OperationsController : Controller
{
    private readonly IDemoMonitorService _monitor;

    public OperationsController(IDemoMonitorService monitor)
    {
        _monitor = monitor;
    }

    [HttpGet("/dashboard")]
    public IActionResult Dashboard() => View(_monitor.GetDashboard());

    [HttpGet("/servers")]
    public IActionResult Servers() => View(_monitor.GetServers());

    [HttpGet("/servers/{id}")]
    public IActionResult ServerDetails(string id)
    {
        var model = _monitor.GetServer(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet("/database-health")]
    public IActionResult DatabaseHealth() => View(_monitor.GetServers());

    [HttpGet("/memory-health")]
    public IActionResult MemoryHealth() => View(_monitor.GetServers());

    [HttpGet("/alerts")]
    public IActionResult Alerts() => View(_monitor.GetIncidents());

    [HttpGet("/settings")]
    public IActionResult Settings() => View();
}
