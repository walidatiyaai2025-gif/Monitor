using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

public sealed record EnterpriseServerOperatorRow(
    ServerRegistration Registration,
    ServerOperatorMetadata Metadata,
    bool MaintenanceActive,
    bool AlertSuppressed);

public sealed record EnterpriseIncidentOperatorRow(
    HealthIncident Incident,
    IncidentOperatorMetadata Metadata,
    RecommendationPlan? Recommendation,
    string? RecommendationKey,
    bool RecommendationAcknowledged);

public sealed record EnterpriseOperationsViewModel(
    IReadOnlyList<EnterpriseServerOperatorRow> Servers,
    IReadOnlyList<EnterpriseIncidentOperatorRow> Incidents);

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class EnterpriseOperationsController : Controller
{
    private readonly IOperatorMetadataStore _operatorMetadata;
    private readonly IServerRegistrationRepository _registrations;
    private readonly IHealthIncidentRepository _incidents;
    private readonly IRecommendationEngine _recommendations;
    private readonly ISafeCsvReportService _csv;
    private readonly IRedactedDiagnosticsPackageService _diagnostics;
    private readonly IAuditStore _audit;
    private readonly TimeProvider _timeProvider;

    public EnterpriseOperationsController(
        IOperatorMetadataStore operatorMetadata,
        IServerRegistrationRepository registrations,
        IHealthIncidentRepository incidents,
        IRecommendationEngine recommendations,
        ISafeCsvReportService csv,
        IRedactedDiagnosticsPackageService diagnostics,
        IAuditStore audit,
        TimeProvider timeProvider)
    {
        _operatorMetadata = operatorMetadata;
        _registrations = registrations;
        _incidents = incidents;
        _recommendations = recommendations;
        _csv = csv;
        _diagnostics = diagnostics;
        _audit = audit;
        _timeProvider = timeProvider;
    }

    [HttpGet("/enterprise")]
    public IActionResult Overview()
    {
        var now = _timeProvider.GetUtcNow();
        var servers = _registrations.GetAll()
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .Select(registration =>
            {
                var metadata = _operatorMetadata.GetServer(registration.Id);
                return new EnterpriseServerOperatorRow(
                    registration,
                    metadata,
                    EnterpriseOperatorPolicy.IsMaintenanceActive(metadata, now),
                    EnterpriseOperatorPolicy.IsAlertSuppressed(metadata, now));
            })
            .ToArray();

        var incidents = _incidents.GetAll()
            .OrderBy(item => item.Status)
            .ThenByDescending(item => item.Severity)
            .ThenByDescending(item => item.LastSeenUtc)
            .Select(incident =>
            {
                var metadata = _operatorMetadata.GetIncident(incident.Id);
                var recommendation = _recommendations.Build(incident);
                var key = recommendation is null ? null : RecommendationAcknowledgmentKey.Create(recommendation);
                var acknowledged = key is not null && metadata.AcknowledgedRecommendationKeys.Contains(key, StringComparer.Ordinal);
                return new EnterpriseIncidentOperatorRow(incident, metadata, recommendation, key, acknowledged);
            })
            .ToArray();

        return View(new EnterpriseOperationsViewModel(servers, incidents));
    }

    [HttpPost("/servers/{id:guid}/operator-profile")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Manage)]
    public IActionResult UpdateServerProfile(
        Guid id,
        ServerEnvironmentClass environment,
        string? group,
        string? tags,
        DateTimeOffset? maintenanceStartUtc,
        DateTimeOffset? maintenanceEndUtc,
        string? maintenanceReason,
        DateTimeOffset? suppressionStartUtc,
        DateTimeOffset? suppressionEndUtc,
        string? suppressionReason)
    {
        if (!_registrations.GetAll().Any(item => item.Id == id)) return NotFound();

        try
        {
            var metadata = new ServerOperatorMetadata(
                id,
                environment,
                group,
                EnterpriseOperatorValidation.ParseTags(tags),
                EnterpriseOperatorValidation.BuildWindow(maintenanceStartUtc, maintenanceEndUtc, maintenanceReason),
                EnterpriseOperatorValidation.BuildWindow(suppressionStartUtc, suppressionEndUtc, suppressionReason),
                _timeProvider.GetUtcNow());
            _operatorMetadata.UpsertServer(metadata);
            _audit.Append(Actor(), "server.operator-profile", id.ToString("D"), "updated");
            TempData["OperatorStatus"] = "Server operations metadata updated.";
            return RedirectToAction(nameof(Overview));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = SecurityInput.NormalizeAuditField(exception.Message, 180) });
        }
    }

    [HttpPost("/alerts/{id}/owner")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public IActionResult AssignIncident(string id, string? assignee)
    {
        if (_incidents.GetById(id) is null) return NotFound();
        try
        {
            _operatorMetadata.AssignIncident(id, assignee);
            _audit.Append(Actor(), "incident.owner", id, string.IsNullOrWhiteSpace(assignee) ? "cleared" : "assigned");
            TempData["OperatorStatus"] = "Incident owner updated.";
            return RedirectToAction(nameof(Overview));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = SecurityInput.NormalizeAuditField(exception.Message, 180) });
        }
    }

    [HttpPost("/alerts/{id}/notes")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public IActionResult AddIncidentNote(string id, string note)
    {
        if (_incidents.GetById(id) is null) return NotFound();
        try
        {
            _operatorMetadata.AddIncidentNote(id, Actor(), note);
            _audit.Append(Actor(), "incident.note", id, "added");
            TempData["OperatorStatus"] = "Incident note added.";
            return RedirectToAction(nameof(Overview));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = SecurityInput.NormalizeAuditField(exception.Message, 180) });
        }
    }

    [HttpPost("/alerts/{id}/recommendation/{recommendationKey}/acknowledge")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public IActionResult AcknowledgeRecommendation(string id, string recommendationKey, bool acknowledged = true)
    {
        var incident = _incidents.GetById(id);
        if (incident is null) return NotFound();

        var recommendation = _recommendations.Build(incident);
        if (recommendation is null) return Conflict(new { message = "The incident has no current deterministic recommendation." });

        var currentKey = RecommendationAcknowledgmentKey.Create(recommendation);
        if (!string.Equals(currentKey, recommendationKey, StringComparison.Ordinal))
            return Conflict(new { message = "The recommendation changed. Reload the enterprise operations view before acknowledging it." });

        try
        {
            _operatorMetadata.SetRecommendationAcknowledged(id, currentKey, acknowledged);
            _audit.Append(Actor(), "recommendation.acknowledgment", id, acknowledged ? "acknowledged" : "reopened");
            TempData["OperatorStatus"] = acknowledged ? "Recommendation acknowledged." : "Recommendation review reopened.";
            return RedirectToAction(nameof(Overview));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = SecurityInput.NormalizeAuditField(exception.Message, 180) });
        }
    }

    [HttpGet("/reports/servers.csv")]
    public IActionResult ServerCsv()
    {
        var bytes = _csv.BuildServerReport();
        return File(bytes, "text/csv; charset=utf-8", $"monitor-servers-{_timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.csv");
    }

    [HttpGet("/diagnostics/package")]
    [Authorize(Policy = MonitorPolicies.Manage)]
    public async Task<IActionResult> Diagnostics(CancellationToken cancellationToken)
    {
        var bytes = await _diagnostics.BuildAsync(cancellationToken);
        _audit.Append(Actor(), "diagnostics.package", "application", "generated");
        return File(bytes, "application/zip", $"monitor-diagnostics-{_timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.zip");
    }

    private string Actor()
    {
        var actor = User.Identity?.Name;
        return string.IsNullOrWhiteSpace(actor) ? "unknown" : EnterpriseOperatorValidation.NormalizeActor(actor);
    }
}
