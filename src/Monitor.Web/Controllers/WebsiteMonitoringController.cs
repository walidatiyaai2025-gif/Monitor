using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

public sealed record WebsiteMonitoringTargetRow(
    WebsiteTargetDefinition Target,
    WebsiteProbeHistoryPoint? Latest,
    HealthIncident? ActiveIncident,
    WebsiteCorrelationAssessment Correlation,
    double? Availability24Hours,
    int KnownChecks24Hours,
    int UnknownChecks24Hours);

public sealed class WebsiteTargetForm
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Environment { get; set; } = "production";
    public bool IsEnabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 15;
    public int ExpectedStatusMin { get; set; } = 200;
    public int ExpectedStatusMax { get; set; } = 399;
    public string? ExpectedContentMarker { get; set; }
    public bool FollowRedirects { get; set; } = true;
    public string? ExpectedFinalHost { get; set; }
    public int SlowThresholdMilliseconds { get; set; } = 3000;
    public int FailureConfirmationCount { get; set; } = 3;
    public int RecoveryConfirmationCount { get; set; } = 2;
    public string NotificationGroupIds { get; set; } = string.Empty;
    public string LinkedRegistrationIds { get; set; } = string.Empty;
}

public sealed class WebsiteNotificationGroupForm
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Recipients { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

public sealed record WebsiteMonitoringPageViewModel(
    IReadOnlyList<WebsiteMonitoringTargetRow> Targets,
    IReadOnlyList<WebsiteNotificationGroup> NotificationGroups,
    WebsiteTargetForm TargetForm,
    WebsiteNotificationGroupForm GroupForm,
    bool MonitoringEnabled,
    bool NotificationsEnabled,
    int AllowedPrivateHostCount,
    int PendingEmails,
    int DeadLetterEmails,
    bool IsAdministrator);

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class WebsiteMonitoringController(
    WebsiteMonitoringOptions monitoringOptions,
    WebsiteNotificationOptions notificationOptions,
    WebsiteOutboundPolicyOptions outboundPolicy,
    IWebsiteTargetStore targets,
    IWebsiteProbeHistoryStore history,
    IWebsiteNotificationGroupStore groups,
    IWebsiteNotificationOutbox outbox,
    IHealthIncidentRepository incidents,
    IServerRegistrationRepository registrations,
    IWebsiteDependencyCorrelationService correlation,
    IWebsiteProbeEngine probe,
    IWebsiteIncidentCoordinator incidentCoordinator,
    IWebsiteNotificationPlanner notificationPlanner,
    IAuditStore audit) : Controller
{
    private const int MaxVisibleTargets = 100;

    [HttpGet("/websites")]
    public IActionResult Index(Guid? editId = null)
    {
        var allIncidents = incidents.GetAll()
            .Where(item => item.Status != IncidentStatus.Resolved)
            .ToLookup(item => item.RegistrationId);
        var rows = targets.GetAll()
            .Take(MaxVisibleTargets)
            .Select(target => BuildRow(target, allIncidents[target.Id]))
            .ToArray();
        var outboxItems = User.IsInRole(MonitorRoles.Administrator) ? outbox.Snapshot() : Array.Empty<WebsiteNotificationOutboxItem>();
        var editing = editId.HasValue ? targets.Get(editId.Value) : null;
        return View(new WebsiteMonitoringPageViewModel(
            rows,
            groups.GetAll(),
            editing is null ? new WebsiteTargetForm() : ToForm(editing),
            new WebsiteNotificationGroupForm(),
            monitoringOptions.Enabled,
            notificationOptions.Enabled,
            outboundPolicy.AllowedPrivateHosts.Length,
            outboxItems.Count(item => item.Status == WebsiteNotificationDeliveryStatus.Pending),
            outboxItems.Count(item => item.Status == WebsiteNotificationDeliveryStatus.DeadLetter),
            User.IsInRole(MonitorRoles.Administrator)));
    }

    [Authorize(Policy = MonitorPolicies.Manage)]
    [ValidateAntiForgeryToken]
    [HttpPost("/websites/save")]
    public IActionResult SaveTarget(WebsiteTargetForm form)
    {
        var actor = Actor();
        if (actor is null) return Forbid();
        var groupIds = SplitTokens(form.NotificationGroupIds);
        if (groupIds.Any(id => groups.Get(id) is null))
        {
            TempData["WebsiteError"] = "One or more notification group IDs do not exist.";
            return RedirectToAction(nameof(Index), new { editId = form.Id });
        }

        if (!TryParseLinkedRegistrations(form.LinkedRegistrationIds, out var linkedRegistrationIds, out var linkedError))
        {
            TempData["WebsiteError"] = linkedError;
            return RedirectToAction(nameof(Index), new { editId = form.Id });
        }
        if (linkedRegistrationIds.Any(id => registrations.GetById(id) is null))
        {
            TempData["WebsiteError"] = "One or more linked server registration IDs do not exist.";
            return RedirectToAction(nameof(Index), new { editId = form.Id });
        }

        var target = new WebsiteTargetDefinition(
            form.Id is { } id && id != Guid.Empty ? id : Guid.NewGuid(),
            form.Name?.Trim() ?? string.Empty,
            form.Url?.Trim() ?? string.Empty,
            NormalizeEnvironment(form.Environment),
            form.IsEnabled,
            form.IntervalSeconds,
            form.TimeoutSeconds,
            form.ExpectedStatusMin,
            form.ExpectedStatusMax,
            NormalizeOptional(form.ExpectedContentMarker, WebsiteTargetValidator.MaxContentMarkerLength),
            form.FollowRedirects,
            NormalizeOptional(form.ExpectedFinalHost, 253),
            form.SlowThresholdMilliseconds,
            form.FailureConfirmationCount,
            form.RecoveryConfirmationCount,
            groupIds,
            linkedRegistrationIds);
        var validation = WebsiteTargetValidator.Validate(target);
        if (!validation.IsValid)
        {
            TempData["WebsiteError"] = string.Join(" ", validation.Errors);
            return RedirectToAction(nameof(Index), new { editId = form.Id });
        }

        audit.Append(actor, "website.target.upsert.requested", target.Id.ToString("D"), $"name={Bound(target.Name, 120)}; environment={target.Environment}; linkedDependencies={linkedRegistrationIds.Length}");
        targets.Upsert(target);
        audit.Append(actor, "website.target.upsert.completed", target.Id.ToString("D"), "Website target metadata saved; no probe executed by this action.");
        TempData["WebsiteSuccess"] = "Website target saved.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = MonitorPolicies.Manage)]
    [ValidateAntiForgeryToken]
    [HttpPost("/websites/{id:guid}/toggle")]
    public IActionResult Toggle(Guid id)
    {
        var actor = Actor();
        if (actor is null) return Forbid();
        var target = targets.Get(id);
        if (target is null) return NotFound();
        audit.Append(actor, "website.target.toggle.requested", id.ToString("D"), target.IsEnabled ? "disable" : "enable");
        targets.Upsert(target with { IsEnabled = !target.IsEnabled });
        audit.Append(actor, "website.target.toggle.completed", id.ToString("D"), target.IsEnabled ? "disabled" : "enabled");
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = MonitorPolicies.Manage)]
    [ValidateAntiForgeryToken]
    [HttpPost("/websites/{id:guid}/delete")]
    public IActionResult Delete(Guid id)
    {
        var actor = Actor();
        if (actor is null) return Forbid();
        var target = targets.Get(id);
        if (target is null) return NotFound();
        audit.Append(actor, "website.target.delete.requested", id.ToString("D"), $"name={Bound(target.Name, 120)}");
        if (!targets.Remove(id)) return Conflict();
        audit.Append(actor, "website.target.delete.completed", id.ToString("D"), "Website target removed; retained history/incidents are not silently rewritten.");
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = MonitorPolicies.Operate)]
    [ValidateAntiForgeryToken]
    [HttpPost("/websites/{id:guid}/check")]
    public async Task<IActionResult> CheckNow(Guid id, CancellationToken cancellationToken)
    {
        var actor = Actor();
        if (actor is null) return Forbid();
        if (!monitoringOptions.Enabled)
        {
            TempData["WebsiteError"] = "Website Monitoring is disabled. Enable it explicitly before any outbound probe can run.";
            return RedirectToAction(nameof(Index));
        }
        var target = targets.Get(id);
        if (target is null) return NotFound();
        audit.Append(actor, "website.probe.manual.requested", id.ToString("D"), $"host={new Uri(target.Url).DnsSafeHost}");
        var result = await probe.ProbeAsync(target, cancellationToken);
        history.Append(result);
        var observation = incidentCoordinator.Observe(target, result);
        _ = notificationPlanner.Queue(target, result, observation);
        audit.Append(actor, "website.probe.manual.completed", id.ToString("D"), $"rule={result.Classification.RuleId}; state={result.Classification.State}; http={result.Evidence.HttpStatusCode?.ToString() ?? "n/a"}");
        TempData["WebsiteSuccess"] = $"Check completed: {result.Classification.State} / {result.Classification.RuleId}.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = MonitorPolicies.Manage)]
    [ValidateAntiForgeryToken]
    [HttpPost("/websites/groups/save")]
    public IActionResult SaveGroup(WebsiteNotificationGroupForm form)
    {
        var actor = Actor();
        if (actor is null) return Forbid();
        var recipients = SplitRecipients(form.Recipients);
        var group = new WebsiteNotificationGroup(form.Id?.Trim() ?? string.Empty, form.Name?.Trim() ?? string.Empty, recipients, form.IsEnabled);
        try { WebsiteNotificationValidation.ValidateGroup(group); }
        catch (ArgumentException exception)
        {
            TempData["WebsiteError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
        audit.Append(actor, "website.notification-group.upsert.requested", Bound(group.Id, 80), $"recipientCount={group.Recipients.Count}");
        groups.Upsert(group);
        audit.Append(actor, "website.notification-group.upsert.completed", Bound(group.Id, 80), "Notification group saved without exposing recipients to audit detail.");
        TempData["WebsiteSuccess"] = "Notification group saved.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = MonitorPolicies.Manage)]
    [ValidateAntiForgeryToken]
    [HttpPost("/websites/groups/{id}/delete")]
    public IActionResult DeleteGroup(string id)
    {
        var actor = Actor();
        if (actor is null) return Forbid();
        var normalized = id?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 80) return BadRequest();
        audit.Append(actor, "website.notification-group.delete.requested", normalized, "delete");
        if (!groups.Remove(normalized)) return NotFound();
        audit.Append(actor, "website.notification-group.delete.completed", normalized, "Notification group removed.");
        return RedirectToAction(nameof(Index));
    }

    private WebsiteMonitoringTargetRow BuildRow(WebsiteTargetDefinition target, IEnumerable<HealthIncident> targetIncidents)
    {
        var points = history.Read(target.Id, TimeSpan.FromHours(24));
        var latest = points.LastOrDefault();
        var known = points.Where(point => point.State != WebsiteProbeState.Unknown).ToArray();
        var available = known.Count(point => point.State is WebsiteProbeState.Up or WebsiteProbeState.Degraded);
        double? availability = known.Length == 0 ? null : Math.Round(available * 100d / known.Length, 2);
        var active = targetIncidents.OrderByDescending(item => item.Severity).ThenByDescending(item => item.LastSeenUtc).FirstOrDefault();
        return new WebsiteMonitoringTargetRow(target, latest, active, correlation.Assess(target, latest), availability, known.Length, points.Count - known.Length);
    }

    private static WebsiteTargetForm ToForm(WebsiteTargetDefinition target) => new()
    {
        Id = target.Id,
        Name = target.Name,
        Url = target.Url,
        Environment = target.Environment,
        IsEnabled = target.IsEnabled,
        IntervalSeconds = target.IntervalSeconds,
        TimeoutSeconds = target.TimeoutSeconds,
        ExpectedStatusMin = target.ExpectedStatusMin,
        ExpectedStatusMax = target.ExpectedStatusMax,
        ExpectedContentMarker = target.ExpectedContentMarker,
        FollowRedirects = target.FollowRedirects,
        ExpectedFinalHost = target.ExpectedFinalHost,
        SlowThresholdMilliseconds = target.SlowThresholdMilliseconds,
        FailureConfirmationCount = target.FailureConfirmationCount,
        RecoveryConfirmationCount = target.RecoveryConfirmationCount,
        NotificationGroupIds = string.Join(", ", target.NotificationGroupIds ?? Array.Empty<string>()),
        LinkedRegistrationIds = string.Join(", ", target.LinkedRegistrationIds ?? Array.Empty<Guid>())
    };

    private string? Actor() => string.IsNullOrWhiteSpace(User.Identity?.Name) ? null : User.Identity!.Name!.Trim();
    private static string NormalizeEnvironment(string? value) => Batch300AlertRouting.NormalizeEnvironment(value);
    private static string? NormalizeOptional(string? value, int max)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        return normalized.Length <= max ? normalized : normalized[..max];
    }
    private static string[] SplitTokens(string? value) => (value ?? string.Empty)
        .Split([',', ';', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(WebsiteTargetValidator.MaxNotificationGroups)
        .ToArray();
    private static string[] SplitRecipients(string? value) => (value ?? string.Empty)
        .Split([',', ';', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(WebsiteNotificationValidation.MaxRecipientsPerGroup)
        .ToArray();
    private static bool TryParseLinkedRegistrations(string? value, out Guid[] ids, out string error)
    {
        var tokens = (value ?? string.Empty)
            .Split([',', ';', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > WebsiteTargetValidator.MaxLinkedRegistrations)
        {
            ids = [];
            error = $"A target may link at most {WebsiteTargetValidator.MaxLinkedRegistrations} server registrations.";
            return false;
        }
        var materialized = new List<Guid>(tokens.Length);
        foreach (var token in tokens)
        {
            if (!Guid.TryParse(token, out var id) || id == Guid.Empty)
            {
                ids = [];
                error = "Linked server registration IDs must be valid non-empty GUIDs.";
                return false;
            }
            if (!materialized.Contains(id)) materialized.Add(id);
        }
        ids = materialized.ToArray();
        error = string.Empty;
        return true;
    }
    private static string Bound(string value, int max) => value.Length <= max ? value : value[..max];
}
