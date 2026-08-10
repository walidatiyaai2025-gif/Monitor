using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;
using Monitor.Web.Models;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class OperationsController : Controller
{
    private readonly IDemoMonitorService _monitor;
    private readonly IMonitorReadService _readService;
    private readonly IIncidentWorkflowService? _workflow;
    private readonly ITrendReadService? _trends;
    private readonly IAuditStore? _audit;
    private readonly IAdvisorRequestService? _advisorRequests;
    private readonly IHealthIncidentRepository? _incidentRepository;
    private readonly ISnapshotRefreshService? _snapshotRefresh;
    private readonly DeploymentReadinessViewModel _deploymentReadiness;

    public OperationsController(
        IDemoMonitorService monitor,
        IMonitorReadService readService,
        IIncidentWorkflowService? workflow = null,
        ITrendReadService? trends = null,
        IAuditStore? audit = null,
        IAdvisorRequestService? advisorRequests = null,
        IHealthIncidentRepository? incidentRepository = null,
        ISnapshotRefreshService? snapshotRefresh = null,
        DeploymentReadinessViewModel? deploymentReadiness = null)
    {
        _monitor = monitor;
        _readService = readService;
        _workflow = workflow;
        _trends = trends;
        _audit = audit;
        _advisorRequests = advisorRequests;
        _incidentRepository = incidentRepository;
        _snapshotRefresh = snapshotRefresh;
        _deploymentReadiness = deploymentReadiness ?? DeploymentReadinessViewModel.SafeDefault();
    }

    [HttpGet("/dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken) => View(await _readService.GetDashboardAsync(cancellationToken));

    [HttpGet("/servers")]
    public async Task<IActionResult> Servers(CancellationToken cancellationToken) =>
        View(await _readService.GetServersAsync(cancellationToken));

    [HttpGet("/servers/{id}")]
    public async Task<IActionResult> ServerDetails(string id, CancellationToken cancellationToken)
    {
        var model = await _readService.GetServerAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("/servers/{id:guid}/refresh")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public async Task<IActionResult> RefreshServer(Guid id, CancellationToken cancellationToken)
    {
        if (_snapshotRefresh is null) return NotFound();
        var result = await _snapshotRefresh.RefreshAsync(id, cancellationToken);
        TempData["SnapshotRefresh"] = result.Message;
        return result.Status == SnapshotRefreshStatus.RegistrationNotFound
            ? NotFound()
            : RedirectToAction(nameof(ServerDetails), new { id = id.ToString("D") });
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
    [Authorize(Policy = MonitorPolicies.Operate)]
    public IActionResult AcknowledgeIncident(string id) =>
        Transition(id, workflow => workflow.Acknowledge(id));

    [HttpPost("/alerts/{id}/resolve")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public IActionResult ResolveIncident(string id) =>
        Transition(id, workflow => workflow.Resolve(id));

    [HttpPost("/alerts/{id}/reopen")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public IActionResult ReopenIncident(string id) =>
        Transition(id, workflow => workflow.Reopen(id));

    private IActionResult Transition(string id, Func<IIncidentWorkflowService, bool> transition)
    {
        if (_workflow is null)
        {
            return NotFound();
        }

        var actor = User.Identity?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(actor))
        {
            return Forbid();
        }

        var repositoryAvailable = _incidentRepository is not null;
        var before = _incidentRepository?.GetById(id);
        var changed = transition(_workflow);
        var after = _incidentRepository?.GetById(id);

        var outcome = BuildTransitionAuditOutcome(changed, before, after, repositoryAvailable);
        _audit?.Append(actor, "incident.transition", id, outcome);

        return changed
            ? RedirectToAction(nameof(IncidentDetails), new { id })
            : Conflict(new { message = "Incident state changed or the transition is not allowed." });
    }

    private static string BuildTransitionAuditOutcome(
        bool changed,
        HealthIncident? before,
        HealthIncident? after,
        bool repositoryAvailable)
    {
        if (!repositoryAvailable)
        {
            return changed ? "applied" : "conflict";
        }

        if (changed)
        {
            return before is not null && after is not null
                ? $"{before.Status}->{after.Status}"
                : "applied";
        }

        if (before is null)
        {
            return "rejected:not-found";
        }

        var current = after?.Status ?? before.Status;
        return $"rejected:current={current}";
    }

    [HttpGet("/audit")]
    [Authorize(Policy = MonitorPolicies.Manage)]
    public IActionResult Audit(int offset = 0, int limit = 50) => View(_audit?.Read(offset, limit) ?? []);

    [HttpPost("/alerts/{id}/advisor")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Advisor)]
    public async Task<IActionResult> RequestAdvisor(string id, CancellationToken cancellationToken)
    {
        if (_advisorRequests is null) return NotFound();
        var result = await _advisorRequests.RequestAsync(id, User.Identity?.Name ?? "unknown", cancellationToken);
        TempData["AdvisorStatus"] = result.Message;
        return RedirectToAction(nameof(IncidentDetails), new { id });
    }

    [HttpGet("/history/{registrationId:guid}")]
    public IActionResult History(Guid registrationId, string window = "6h")
    {
        var model = _trends?.Read(registrationId, window);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet("/settings")]
    [Authorize(Policy = MonitorPolicies.Manage)]
    public IActionResult Settings() => View(_deploymentReadiness);
}
