using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

public sealed record RecommendationHubRow(HealthIncident Incident, RecommendationPlan Recommendation);
public sealed record RecommendationHubViewModel(IReadOnlyList<RecommendationHubRow> Items);

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class PortalController(
    IMonitorReadService monitoring,
    IHealthIncidentRepository incidents,
    IRecommendationEngine recommendations) : Controller
{
    [HttpGet("/performance-health")]
    public async Task<IActionResult> Performance(CancellationToken cancellationToken) =>
        View(new HealthModulePageViewModel(
            "Performance Health",
            "Bounded request, runnable-task and pending-I/O facts from the shared cached snapshot.",
            await monitoring.GetHealthModulesAsync(cancellationToken)));

    [HttpGet("/recommendations")]
    public IActionResult Recommendations()
    {
        var rows = incidents.GetAll()
            .Where(item => item.Status != IncidentStatus.Resolved)
            .Select(item => (Incident: item, Plan: recommendations.Build(item)))
            .Where(item => item.Plan is not null)
            .Select(item => new RecommendationHubRow(item.Incident, item.Plan!))
            .OrderByDescending(item => item.Incident.Severity)
            .ThenByDescending(item => item.Incident.LastSeenUtc)
            .Take(100)
            .ToArray();
        return View(new RecommendationHubViewModel(rows));
    }

    [HttpGet("/reports")]
    public IActionResult Reports() => View();
}
