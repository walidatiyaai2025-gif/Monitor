using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

public sealed record RecommendationHubRow(HealthIncident Incident, RecommendationPlan Recommendation);
public sealed record RecommendationHubViewModel(
    IReadOnlyList<RecommendationHubRow> Items,
    int BoundedTotal,
    int Critical,
    int Warning,
    FindingSeverity? Severity,
    string? RuleId);

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
    public IActionResult Recommendations(FindingSeverity? severity = null, string? ruleId = null)
    {
        var normalizedRuleId = SecurityInput.NormalizeOptionalToken(ruleId, 80);
        var bounded = incidents.GetAll()
            .Where(item => item.Status != IncidentStatus.Resolved)
            .Select(item => (Incident: item, Plan: recommendations.Build(item)))
            .Where(item => item.Plan is not null)
            .Select(item => new RecommendationHubRow(item.Incident, item.Plan!))
            .OrderByDescending(item => item.Incident.Severity)
            .ThenByDescending(item => item.Incident.LastSeenUtc)
            .Take(100)
            .ToArray();

        var filtered = bounded
            .Where(item => !severity.HasValue || item.Incident.Severity == severity.Value)
            .Where(item => normalizedRuleId is null || string.Equals(item.Incident.RuleId, normalizedRuleId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return View(new RecommendationHubViewModel(
            filtered,
            bounded.Length,
            bounded.Count(item => item.Incident.Severity == FindingSeverity.Critical),
            bounded.Count(item => item.Incident.Severity == FindingSeverity.Warning),
            severity,
            normalizedRuleId));
    }

    [HttpGet("/reports")]
    public IActionResult Reports() => View();
}
