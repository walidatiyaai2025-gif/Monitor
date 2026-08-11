using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class EnterpriseReportsController : Controller
{
    private readonly IEnterpriseReportingService _reports;
    private readonly TimeProvider _timeProvider;

    public EnterpriseReportsController(
        IServerRegistrationRepository registrations,
        IServerHealthSnapshotCache cache,
        IOperatorMetadataStore operatorMetadata,
        IHealthIncidentRepository incidents,
        ISnapshotHistoryStore history,
        IAuditStore audit,
        TimeProvider timeProvider)
    {
        _reports = new EnterpriseReportingService(registrations, cache, operatorMetadata, incidents, history, audit, timeProvider);
        _timeProvider = timeProvider;
    }

    [HttpGet("/reports/servers-v2.csv")]
    public IActionResult Servers(ServerEnvironmentClass? environment = null, string? group = null, string? tag = null)
    {
        var bytes = _reports.Servers(new(environment, group, tag));
        return File(bytes, "text/csv; charset=utf-8", FileName("servers"));
    }

    [HttpGet("/reports/incidents.csv")]
    public IActionResult Incidents(string? assignee = null, bool? suppressed = null)
    {
        var bytes = _reports.Incidents(new(assignee, suppressed));
        return File(bytes, "text/csv; charset=utf-8", FileName("incidents"));
    }

    [HttpGet("/reports/history/{registrationId:guid}.csv")]
    public IActionResult History(Guid registrationId, string window = "6h")
    {
        if (!TryWindow(window, out var duration)) return BadRequest(new { message = "History export window must be 1h, 6h or 24h." });
        var bytes = _reports.History(registrationId, duration);
        return File(bytes, "text/csv; charset=utf-8", FileName("history"));
    }

    [HttpGet("/reports/audit.csv")]
    [Authorize(Policy = MonitorPolicies.Manage)]
    public IActionResult Audit()
    {
        var bytes = _reports.Audit();
        return File(bytes, "text/csv; charset=utf-8", FileName("audit"));
    }

    [HttpGet("/diagnostics/manifest.json")]
    [Authorize(Policy = MonitorPolicies.Manage)]
    public IActionResult Manifest() => File(_reports.Manifest(), "application/json; charset=utf-8", FileName("manifest", "json"));

    private string FileName(string subject, string extension = "csv") => $"monitor-{subject}-{_timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.{extension}";

    private static bool TryWindow(string value, out TimeSpan duration)
    {
        duration = value.ToLowerInvariant() switch
        {
            "1h" => TimeSpan.FromHours(1),
            "6h" => TimeSpan.FromHours(6),
            "24h" => TimeSpan.FromHours(24),
            _ => TimeSpan.Zero
        };
        return duration > TimeSpan.Zero;
    }
}
