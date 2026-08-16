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
    private readonly ISharedStateReadinessService? _sharedStateReadiness;
    private readonly ICredentialReadinessService? _credentialReadiness;
    private readonly IOperationalBackupService? _backupService;
    private readonly PerformanceScaleOptions _performance;
    private readonly IDbaOperationsSurfaceService? _dbaSurface;
    private readonly IOperatorMetadataStore? _operatorMetadata;
    private readonly TimeProvider _timeProvider;

    public OperationsController(
        IDemoMonitorService monitor,
        IMonitorReadService readService,
        IIncidentWorkflowService? workflow = null,
        ITrendReadService? trends = null,
        IAuditStore? audit = null,
        IAdvisorRequestService? advisorRequests = null,
        IHealthIncidentRepository? incidentRepository = null,
        ISnapshotRefreshService? snapshotRefresh = null,
        DeploymentReadinessViewModel? deploymentReadiness = null,
        ISharedStateReadinessService? sharedStateReadiness = null,
        ICredentialReadinessService? credentialReadiness = null,
        IOperationalBackupService? backupService = null,
        PerformanceScaleOptions? performance = null,
        IDbaOperationsSurfaceService? dbaSurface = null,
        IOperatorMetadataStore? operatorMetadata = null,
        TimeProvider? timeProvider = null)
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
        _sharedStateReadiness = sharedStateReadiness;
        _credentialReadiness = credentialReadiness;
        _backupService = backupService;
        _performance = performance ?? new PerformanceScaleOptions();
        _performance.Validate();
        _dbaSurface = dbaSurface;
        _operatorMetadata = operatorMetadata;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    [HttpGet("/dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var model = await _readService.GetDashboardAsync(cancellationToken);
        ViewData["DbaOperationsSurface"] = _dbaSurface is null
            ? null
            : await _dbaSurface.GetAsync(cancellationToken);
        return View(model);
    }

    [HttpGet("/servers")]
    public async Task<IActionResult> Servers(int offset = 0, int limit = 0, CancellationToken cancellationToken = default)
    {
        var page = await _readService.GetServersPageAsync(offset, limit, cancellationToken);
        ViewData["ServerTotal"] = page.TotalCount;
        ViewData["ServerOffset"] = page.Offset;
        ViewData["ServerLimit"] = page.Limit;
        ViewData["ServerHasPrevious"] = page.HasPrevious;
        ViewData["ServerHasNext"] = page.HasNext;
        ViewData["ServerPreviousOffset"] = page.PreviousOffset;
        ViewData["ServerNextOffset"] = page.NextOffset;

        var policy = PolicyService();
        if (policy is not null)
        {
            var ids = page.Items.Select(item => Guid.TryParse(item.Id, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToArray();
            var states = policy.GetServers(ids);
            ViewData["ServerPolicyStates"] = states;
            ViewData["MaintenanceActiveCount"] = states.Values.Count(item => item.PolicyReadable && item.MaintenanceActive);
            ViewData["OperatorPolicyUnavailableCount"] = states.Values.Count(item => !item.PolicyReadable);
        }

        return View(page.Items);
    }

    [HttpGet("/servers/{id}")]
    public async Task<IActionResult> ServerDetails(string id, CancellationToken cancellationToken)
    {
        var model = await _readService.GetServerAsync(id, cancellationToken);
        if (model is null) return NotFound();

        if (_operatorMetadata is not null && Guid.TryParse(id, out var registrationId))
        {
            var state = PolicyService()!.GetServer(registrationId);
            ViewData["MaintenanceActive"] = state.MaintenanceActive;
            ViewData["AlertSuppressed"] = state.AlertSuppressed;
            ViewData["OperatorPolicyUnavailable"] = !state.PolicyReadable;
            if (state.PolicyReadable)
            {
                try
                {
                    ViewData["ServerOperatorMetadata"] = _operatorMetadata.GetServer(registrationId);
                }
                catch (InvalidDataException)
                {
                    ViewData["OperatorPolicyUnavailable"] = true;
                }
                catch (SharedStateStoreUnavailableException)
                {
                    ViewData["OperatorPolicyUnavailable"] = true;
                }
            }
        }

        return View(model);
    }

    [HttpPost("/servers/{id:guid}/refresh")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public async Task<IActionResult> RefreshServer(Guid id, CancellationToken cancellationToken)
    {
        if (_snapshotRefresh is null) return NotFound();

        var policyState = PolicyService()?.GetServer(id);
        var maintenanceOverride = policyState is { PolicyReadable: true, MaintenanceActive: true };
        var policyUnavailable = policyState is { PolicyReadable: false };
        if (maintenanceOverride)
        {
            _audit?.Append(Actor(), "snapshot.refresh.maintenance-override", id.ToString("D"), "requested");
        }
        else if (policyUnavailable)
        {
            _audit?.Append(Actor(), "snapshot.refresh.policy-unavailable", id.ToString("D"), "manual-requested");
        }

        var result = await _snapshotRefresh.RefreshAsync(id, cancellationToken);
        if (maintenanceOverride)
        {
            _audit?.Append(Actor(), "snapshot.refresh.maintenance-override", id.ToString("D"), result.Status.ToString());
            TempData["SnapshotMaintenanceOverride"] = "Manual refresh was explicitly requested during an active maintenance window.";
        }

        TempData["SnapshotRefresh"] = result.Message;
        TempData["SnapshotRefreshStatus"] = result.Status.ToString();
        TempData["SnapshotRefreshFreshness"] = result.Freshness?.ToString() ?? string.Empty;
        return result.Status == SnapshotRefreshStatus.RegistrationNotFound
            ? NotFound()
            : RedirectToAction(nameof(ServerDetails), new { id = id.ToString("D") });
    }

    [HttpGet("/database-health")]
    public async Task<IActionResult> DatabaseHealth(CancellationToken cancellationToken) => View(new HealthModulePageViewModel("Database Health", "Cached database states from the shared snapshot. Missing database detail remains explicit.", await _readService.GetHealthModulesAsync(cancellationToken)));

    [HttpGet("/backups")]
    public async Task<IActionResult> Backups(CancellationToken cancellationToken) => View(new HealthModulePageViewModel("Backup Health", "Full-backup coverage from the shared cached snapshot.", await _readService.GetHealthModulesAsync(cancellationToken)));

    [HttpGet("/jobs")]
    public async Task<IActionResult> Jobs(CancellationToken cancellationToken) => View(new HealthModulePageViewModel("SQL Agent Jobs", "Aggregate job outcomes; commands and step text are never collected.", await _readService.GetHealthModulesAsync(cancellationToken)));

    [HttpGet("/storage")]
    public async Task<IActionResult> Storage(CancellationToken cancellationToken) => View(new HealthModulePageViewModel("Storage Allocation", "Allocated database bytes only; this is not disk capacity or free space.", await _readService.GetHealthModulesAsync(cancellationToken)));

    [HttpGet("/blocking")]
    public async Task<IActionResult> Blocking(CancellationToken cancellationToken) => View(new HealthModulePageViewModel("Blocking", "Bounded blocking counts without SQL text, plans or client identity.", await _readService.GetHealthModulesAsync(cancellationToken)));

    [HttpGet("/memory-health")]
    public async Task<IActionResult> MemoryHealth(CancellationToken cancellationToken) => View(await _readService.GetServersAsync(cancellationToken));

    [HttpGet("/alerts")]
    public async Task<IActionResult> Alerts(IncidentStatus? status, FindingSeverity? severity, string? ruleId, int offset = 0, int limit = 50, CancellationToken cancellationToken = default)
    {
        await _readService.GetIncidentsAsync(cancellationToken);
        var query = new IncidentQuery(
            status,
            severity,
            NormalizeRuleId(ruleId),
            PerformanceScaleOptions.BoundOffset(offset),
            _performance.BoundIncidentLimit(limit));
        var model = _workflow is null
            ? new IncidentCenterViewModel([], new(0, 0, 0, 0, 0), query)
            : _workflow.Query(query);

        var policy = PolicyService();
        if (policy is not null)
        {
            var states = policy.GetIncidents(model.Items);
            ViewData["IncidentPolicyStates"] = states;
            ViewData["ActionableIncidentCount"] = states.Values.Count(item => item.PolicyReadable && !item.AlertSuppressed);
            ViewData["SuppressedIncidentCount"] = states.Values.Count(item => item.PolicyReadable && item.AlertSuppressed);
            ViewData["IncidentPolicyUnavailableCount"] = states.Values.Count(item => !item.PolicyReadable);
        }

        return View(model);
    }

    [HttpGet("/alerts/{id}")]
    public async Task<IActionResult> IncidentDetails(string id, CancellationToken cancellationToken)
    {
        if (_workflow is null) return NotFound();
        var model = await _workflow.GetDetailsAsync(id, cancellationToken);
        if (model is null) return NotFound();

        if (_operatorMetadata is not null)
        {
            try
            {
                var metadata = _operatorMetadata.GetIncident(id);
                var recommendationKey = model.Recommendation is null ? null : RecommendationAcknowledgmentKey.Create(model.Recommendation);
                ViewData["IncidentOperatorMetadata"] = metadata;
                ViewData["RecommendationKey"] = recommendationKey;
                ViewData["RecommendationAcknowledged"] = recommendationKey is not null && metadata.AcknowledgedRecommendationKeys.Contains(recommendationKey, StringComparer.Ordinal);
            }
            catch (InvalidDataException)
            {
                ViewData["OperatorPolicyUnavailable"] = true;
            }
            catch (SharedStateStoreUnavailableException)
            {
                ViewData["OperatorPolicyUnavailable"] = true;
            }

            var state = PolicyService()!.GetServer(model.Incident.RegistrationId);
            ViewData["IncidentAlertSuppressed"] = state.PolicyReadable && state.AlertSuppressed;
            if (!state.PolicyReadable) ViewData["OperatorPolicyUnavailable"] = true;
        }

        return View(model);
    }

    [HttpPost("/alerts/{id}/acknowledge")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public IActionResult AcknowledgeIncident(string id) => Transition(id, workflow => workflow.Acknowledge(id));

    [HttpPost("/alerts/{id}/resolve")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public IActionResult ResolveIncident(string id) => Transition(id, workflow => workflow.Resolve(id));

    [HttpPost("/alerts/{id}/reopen")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public IActionResult ReopenIncident(string id) => Transition(id, workflow => workflow.Reopen(id));

    private IActionResult Transition(string id, Func<IIncidentWorkflowService, bool> transition)
    {
        if (_workflow is null) return NotFound();
        var actor = User.Identity?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(actor)) return Forbid();
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

    private static string BuildTransitionAuditOutcome(bool changed, HealthIncident? before, HealthIncident? after, bool repositoryAvailable)
    {
        if (!repositoryAvailable) return changed ? "applied" : "conflict";
        if (changed) return before is not null && after is not null ? $"{before.Status}->{after.Status}" : "applied";
        if (before is null) return "rejected:not-found";
        var current = after?.Status ?? before.Status;
        return $"rejected:current={current}";
    }

    [HttpGet("/audit")]
    [Authorize(Policy = MonitorPolicies.Manage)]
    public IActionResult Audit(int offset = 0, int limit = 50) =>
        View(_audit?.Read(PerformanceScaleOptions.BoundOffset(offset), _performance.BoundAuditLimit(limit)) ?? []);

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
    public IActionResult History(Guid registrationId, string window = "6h", int offset = 0, int limit = 100)
    {
        var model = _trends?.Read(registrationId, window, PerformanceScaleOptions.BoundOffset(offset), _performance.BoundHistoryLimit(limit));
        return model is null ? NotFound() : View(model);
    }

    [HttpGet("/settings")]
    [Authorize(Policy = MonitorPolicies.Manage)]
    public async Task<IActionResult> Settings(CancellationToken cancellationToken)
    {
        var sharedState = _sharedStateReadiness is null
            ? SharedStateReadinessViewModel.Disabled()
            : await _sharedStateReadiness.GetAsync(cancellationToken);
        var credentials = _credentialReadiness?.Get() ?? new CredentialReadinessViewModel(
            DataProtectionKeyStoreMode.LocalFile,
            false,
            0,
            0,
            0,
            false,
            "HA credential readiness unavailable",
            "Credential readiness service is unavailable.");
        var backups = _backupService?.GetReadiness();
        return View(new SettingsViewModel(_deploymentReadiness, sharedState, credentials, backups));
    }

    private IOperatorPolicyReadService? PolicyService() =>
        _operatorMetadata is null ? null : new OperatorPolicyReadService(_operatorMetadata, _timeProvider);

    private string Actor()
    {
        var actor = User.Identity?.Name?.Trim();
        return string.IsNullOrWhiteSpace(actor) ? "unknown" : EnterpriseOperatorValidation.NormalizeActor(actor);
    }

    private static string? NormalizeRuleId(string? ruleId) => SecurityInput.NormalizeOptionalToken(ruleId, 80);
}
