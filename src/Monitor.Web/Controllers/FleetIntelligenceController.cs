using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class FleetIntelligenceController : Controller
{
    private readonly IFleetIntelligenceService _fleet;

    public FleetIntelligenceController(
        IServerRegistrationRepository registrations,
        IServerHealthSnapshotCache cache,
        IOperatorMetadataStore operatorMetadata,
        IHealthIncidentRepository incidents,
        TimeProvider timeProvider)
    {
        _fleet = new FleetIntelligenceService(registrations, cache, operatorMetadata, incidents, timeProvider);
    }

    [HttpGet("/enterprise/fleet")]
    public IActionResult Index() => View(_fleet.Read());
}
