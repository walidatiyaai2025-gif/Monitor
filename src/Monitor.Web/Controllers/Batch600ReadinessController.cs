using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class Batch600ReadinessController : Controller
{
    [HttpGet("/production/v2/readiness-contract")]
    public IActionResult Contract() => Json(Batch600ReleaseGate.ContractManifest());
}
