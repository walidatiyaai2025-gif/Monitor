using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class EnterpriseReportsController : Controller
{
    private readonly IEnterpriseReportingService _reports;
    private readonly IMonitorReadService _monitoring;
    private readonly TimeProvider _timeProvider;

    public EnterpriseReportsController(
        IServerRegistrationRepository registrations,
        IServerHealthSnapshotCache cache,
        IOperatorMetadataStore operatorMetadata,
        IHealthIncidentRepository incidents,
        ISnapshotHistoryStore history,
        IAuditStore audit,
        IMonitorReadService monitoring,
        TimeProvider timeProvider)
    {
        _reports = new EnterpriseReportingService(registrations, cache, operatorMetadata, incidents, history, audit, timeProvider);
        _monitoring = monitoring;
        _timeProvider = timeProvider;
    }

    [HttpGet("/reports/servers-v2.csv")]
    public IActionResult Servers(ServerEnvironmentClass? environment = null, string? group = null, string? tag = null)
    {
        EnterpriseSecurityPolicy.ValidateEnterpriseTextBudget(group, tag);
        var bytes = _reports.Servers(new(environment, group, tag));
        return Download(bytes, "text/csv; charset=utf-8", EnterpriseDownloadSubject.Servers, "csv");
    }

    [HttpGet("/reports/incidents.csv")]
    public IActionResult Incidents(string? assignee = null, bool? suppressed = null)
    {
        EnterpriseSecurityPolicy.ValidateEnterpriseTextBudget(assignee);
        var bytes = _reports.Incidents(new(assignee, suppressed));
        return Download(bytes, "text/csv; charset=utf-8", EnterpriseDownloadSubject.Incidents, "csv");
    }

    [HttpGet("/reports/history/{registrationId:guid}.csv")]
    public IActionResult History(Guid registrationId, string window = "6h")
    {
        EnterpriseSecurityPolicy.ValidateEnterpriseTextBudget(window);
        if (!TryWindow(window, out var duration)) return BadRequest(new { message = "History export window must be 1h, 6h or 24h." });
        var bytes = _reports.History(registrationId, duration);
        return Download(bytes, "text/csv; charset=utf-8", EnterpriseDownloadSubject.History, "csv");
    }

    [HttpGet("/reports/fleet-decision-support.csv")]
    public IActionResult FleetDecisionSupport()
    {
        var bytes = _reports.FleetDecisionSupport();
        return Download(bytes, "text/csv; charset=utf-8", EnterpriseDownloadSubject.FleetDecisionSupport, "csv");
    }

    [HttpGet("/reports/maintenance-decision-support/{registrationId:guid}.csv")]
    public IActionResult MaintenanceDecisionSupport(Guid registrationId, string? operation = null)
    {
        EnterpriseSecurityPolicy.ValidateEnterpriseTextBudget(operation);
        var bytes = _reports.MaintenanceDecision(registrationId, operation);
        if (bytes is null) return NotFound();
        return Download(bytes, "text/csv; charset=utf-8", EnterpriseDownloadSubject.MaintenanceDecisionSupport, "csv");
    }

    [HttpGet("/reports/server-intelligence/{registrationId:guid}.csv")]
    public async Task<IActionResult> ServerIntelligence(Guid registrationId, CancellationToken cancellationToken)
    {
        if (registrationId == Guid.Empty) return NotFound();
        var model = await _monitoring.GetServerAsync(registrationId.ToString("D"), cancellationToken);
        if (model is null) return NotFound();
        return Download(ServerIntelligenceExport.Build(model), "text/csv; charset=utf-8", EnterpriseDownloadSubject.ServerIntelligence, "csv");
    }

    [HttpGet("/reports/server-intelligence.csv")]
    public Task<IActionResult> ServerIntelligenceSelection(Guid registrationId, CancellationToken cancellationToken) =>
        ServerIntelligence(registrationId, cancellationToken);

    [HttpGet("/reports/audit.csv")]
    [Authorize(Policy = MonitorPolicies.Manage)]
    public IActionResult Audit()
    {
        var bytes = _reports.Audit();
        return Download(bytes, "text/csv; charset=utf-8", EnterpriseDownloadSubject.Audit, "csv");
    }

    [HttpGet("/diagnostics/manifest.json")]
    [Authorize(Policy = MonitorPolicies.Manage)]
    public IActionResult Manifest() => Download(_reports.Manifest(), "application/json; charset=utf-8", EnterpriseDownloadSubject.Manifest, "json");

    private FileContentResult Download(byte[] bytes, string contentType, EnterpriseDownloadSubject subject, string extension)
    {
        EnterpriseSecurityPolicy.ApplySecureDownloadHeaders(Response);
        var fileName = EnterpriseSecurityPolicy.SafeDownloadFileName(subject, _timeProvider.GetUtcNow(), extension);
        return File(bytes, contentType, fileName);
    }

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
