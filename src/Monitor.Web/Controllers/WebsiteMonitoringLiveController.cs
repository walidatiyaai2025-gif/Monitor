using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

public sealed record WebsiteMonitoringLivePageViewModel(
    IReadOnlyList<WebsiteMonitoringTargetRow> Targets,
    bool MonitoringEnabled,
    bool NotificationsEnabled,
    DateTimeOffset RenderedAtUtc,
    bool IsAdministrator);

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class WebsiteMonitoringLiveController(
    WebsiteMonitoringOptions monitoringOptions,
    WebsiteNotificationOptions notificationOptions,
    IWebsiteTargetStore targets,
    IWebsiteProbeHistoryStore history,
    IHealthIncidentRepository incidents,
    IWebsiteDependencyCorrelationService correlation,
    TimeProvider timeProvider) : Controller
{
    private const int MaxVisibleTargets = 100;

    [HttpGet("/websites/live")]
    public IActionResult Index()
    {
        var allIncidents = incidents.GetAll()
            .Where(item => item.Status != IncidentStatus.Resolved && !WebsiteLiveProjection.IsPolicyBlockedRule(item.RuleId))
            .ToLookup(item => item.RegistrationId);

        var rows = targets.GetAll()
            .Take(MaxVisibleTargets)
            .Select(target => BuildRow(target, allIncidents[target.Id]))
            .ToArray();

        return View(new WebsiteMonitoringLivePageViewModel(
            rows,
            monitoringOptions.Enabled,
            notificationOptions.Enabled,
            timeProvider.GetUtcNow(),
            User.IsInRole(MonitorRoles.Administrator)));
    }

    private WebsiteMonitoringTargetRow BuildRow(WebsiteTargetDefinition target, IEnumerable<HealthIncident> targetIncidents)
    {
        var points = history.Read(target.Id, TimeSpan.FromHours(24));
        var latest = WebsiteLiveProjection.NormalizeLatest(points.LastOrDefault());
        var availability = WebsiteLiveProjection.SummarizeAvailability(points);
        var active = targetIncidents
            .OrderByDescending(item => item.Severity)
            .ThenByDescending(item => item.LastSeenUtc)
            .FirstOrDefault();

        return new WebsiteMonitoringTargetRow(
            target,
            latest,
            active,
            correlation.Assess(target, latest),
            availability.Percentage,
            availability.KnownChecks,
            availability.UnknownChecks);
    }
}
