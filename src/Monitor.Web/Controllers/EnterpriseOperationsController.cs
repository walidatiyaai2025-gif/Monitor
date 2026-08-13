using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

public sealed record EnterpriseOperationsFilter(
    ServerEnvironmentClass? Environment,
    string? Group,
    string? Tag,
    string? Assignee,
    bool? Suppressed);

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
    bool RecommendationAcknowledged,
    bool AlertSuppressed,
    IncidentSlaBucket SlaBucket);

public sealed record EnterpriseOperationsViewModel(
    IReadOnlyList<EnterpriseServerOperatorRow> Servers,
    IReadOnlyList<EnterpriseIncidentOperatorRow> Incidents,
    EnterpriseOperationsFilter Filter);

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
    public IActionResult Overview(
        ServerEnvironmentClass? environment = null,
        string? group = null,
        string? tag = null,
        string? assignee = null,
        bool? suppressed = null)
    {
        EnterpriseSecurityPolicy.ValidateEnterpriseTextBudget(group, tag, assignee);
        var filter = new EnterpriseOperationsFilter(
            NormalizeFilter(environment is null ? null : environment.ToString(), 32) is null ? null : environment,
            NormalizeFilter(group, EnterpriseOperatorValidation.MaxGroupLength),
            NormalizeFilter(tag, 32),
            NormalizeFilter(assignee, EnterpriseOperatorValidation.MaxAssigneeLength),
            suppressed);
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
            .Where(row => filter.Environment is null || row.Metadata.Environment == filter.Environment)
            .Where(row => filter.Group is null || string.Equals(row.Metadata.Group, filter.Group, StringComparison.OrdinalIgnoreCase))
            .Where(row => filter.Tag is null || row.Metadata.Tags.Contains(filter.Tag, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        var collaboration = Collaboration();
        var collaborationRows = collaboration.QueryByAssignee(_incidents.GetAll(), filter.Assignee);
        var incidents = collaborationRows
            .Select(item =>
            {
                var incident = item.Incident;
                var metadata = _operatorMetadata.GetIncident(incident.Id);
                var recommendation = _recommendations.Build(incident);
                var key = recommendation is null ? null : RecommendationAcknowledgmentKey.Create(recommendation);
                var acknowledged = key is not null && metadata.AcknowledgedRecommendationKeys.Contains(key, StringComparer.Ordinal);
                var serverMetadata = _operatorMetadata.GetServer(incident.RegistrationId);
                var isSuppressed = EnterpriseOperatorPolicy.IsAlertSuppressed(serverMetadata, now);
                return new EnterpriseIncidentOperatorRow(incident, metadata, recommendation, key, acknowledged, isSuppressed, item.SlaBucket);
            })
            .Where(row => filter.Suppressed is null || row.AlertSuppressed == filter.Suppressed)
            .ToArray();

        return View(new EnterpriseOperationsViewModel(servers, incidents, filter));
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
            EnterpriseSecurityPolicy.ValidateEnterpriseTextBudget(group, tags, maintenanceReason, suppressionReason);
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
            return Reject("server.operator-profile", id.ToString("D"), exception);
        }
    }

    [HttpPost("/alerts/{id}/owner")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public IActionResult AssignIncident(string id, string? assignee)
    {
        try
        {
            id = EnterpriseSecurityPolicy.NormalizeIncidentRouteId(id);
            EnterpriseSecurityPolicy.ValidateEnterpriseTextBudget(assignee);
        }
        catch (ArgumentException exception)
        {
            return Reject("incident.owner", id, exception);
        }
        if (_incidents.GetById(id) is null) return NotFound();
        try
        {
            Collaboration().Assign(id, assignee, Actor());
            TempData["OperatorStatus"] = "Incident owner updated.";
            return RedirectToAction(nameof(Overview));
        }
        catch (ArgumentException exception)
        {
            return Reject("incident.owner", id, exception);
        }
    }

    [HttpPost("/alerts/{id}/notes")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public IActionResult AddIncidentNote(string id, string note, string requestKey)
    {
        try
        {
            id = EnterpriseSecurityPolicy.NormalizeIncidentRouteId(id);
            EnterpriseSecurityPolicy.ValidateEnterpriseTextBudget(note, requestKey);
        }
        catch (ArgumentException exception)
        {
            return Reject("incident.note", id, exception);
        }
        if (_incidents.GetById(id) is null) return NotFound();
        try
        {
            var added = Collaboration().TryAddNote(id, Actor(), note, requestKey);
            TempData[added ? "OperatorStatus" : "OperatorError"] = added ? "Incident note added." : "This note request was already applied.";
            return RedirectToAction(nameof(Overview));
        }
        catch (ArgumentException exception)
        {
            return Reject("incident.note", id, exception);
        }
    }

    [HttpPost("/alerts/{id}/recommendation/{recommendationKey}/acknowledge")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public IActionResult AcknowledgeRecommendation(string id, string recommendationKey, bool acknowledged = true)
    {
        try
        {
            id = EnterpriseSecurityPolicy.NormalizeIncidentRouteId(id);
            EnterpriseSecurityPolicy.ValidateEnterpriseTextBudget(recommendationKey);
        }
        catch (ArgumentException exception)
        {
            return Reject("recommendation.acknowledgment", id, exception);
        }
        var incident = _incidents.GetById(id);
        if (incident is null) return NotFound();

        var recommendation = _recommendations.Build(incident);
        if (recommendation is null)
        {
            _audit.Append(Actor(), "recommendation.acknowledgment", id, "rejected:no-current-recommendation");
            TempData["OperatorError"] = "The incident has no current deterministic recommendation.";
            return RedirectToAction(nameof(Overview));
        }

        var currentKey = RecommendationAcknowledgmentKey.Create(recommendation);
        if (!string.Equals(currentKey, recommendationKey, StringComparison.Ordinal))
        {
            _audit.Append(Actor(), "recommendation.acknowledgment", id, "rejected:stale-recommendation");
            TempData["OperatorError"] = "The recommendation changed. Reloaded state is required before acknowledgment.";
            return RedirectToAction(nameof(Overview));
        }

        try
        {
            _operatorMetadata.SetRecommendationAcknowledged(id, currentKey, acknowledged);
            _audit.Append(Actor(), "recommendation.acknowledgment", id, acknowledged ? "acknowledged" : "reopened");
            TempData["OperatorStatus"] = acknowledged ? "Recommendation acknowledged." : "Recommendation review reopened.";
            return RedirectToAction(nameof(Overview));
        }
        catch (ArgumentException exception)
        {
            return Reject("recommendation.acknowledgment", id, exception);
        }
    }

    [HttpGet("/reports/servers.csv")]
    public IActionResult ServerCsv()
    {
        var bytes = _csv.BuildServerReport();
        EnterpriseSecurityPolicy.ApplySecureDownloadHeaders(Response);
        return File(bytes, "text/csv; charset=utf-8", EnterpriseSecurityPolicy.SafeDownloadFileName(EnterpriseDownloadSubject.Servers, _timeProvider.GetUtcNow(), "csv"));
    }

    [HttpGet("/diagnostics/package")]
    [Authorize(Policy = MonitorPolicies.Manage)]
    public async Task<IActionResult> Diagnostics(CancellationToken cancellationToken)
    {
        var bytes = await new BoundedDiagnosticsRunner(_diagnostics).BuildAsync(cancellationToken);
        _audit.Append(Actor(), "diagnostics.package", "application", "generated");
        EnterpriseSecurityPolicy.ApplySecureDownloadHeaders(Response);
        return File(bytes, "application/zip", EnterpriseSecurityPolicy.SafeDownloadFileName(EnterpriseDownloadSubject.Diagnostics, _timeProvider.GetUtcNow(), "zip"));
    }

    private IActionResult Reject(string action, string target, ArgumentException exception)
    {
        _audit.Append(Actor(), action, SecurityInput.NormalizeAuditField(target, 160), "rejected");
        TempData["OperatorError"] = SecurityInput.NormalizeAuditField(exception.Message, 180);
        return RedirectToAction(nameof(Overview));
    }

    private IIncidentCollaborationService Collaboration() => new IncidentCollaborationService(_operatorMetadata, _audit, _timeProvider);

    private string Actor()
    {
        var actor = User.Identity?.Name;
        return string.IsNullOrWhiteSpace(actor) ? "unknown" : EnterpriseOperatorValidation.NormalizeActor(actor);
    }

    private static string? NormalizeFilter(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength || normalized.Any(char.IsControl)) return null;
        return normalized;
    }
}
