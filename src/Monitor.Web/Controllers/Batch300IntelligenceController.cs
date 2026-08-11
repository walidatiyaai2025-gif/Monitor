using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class Batch300IntelligenceController : Controller
{
    [HttpGet("/intelligence/contract")]
    public IActionResult Contract() => Json(Batch300ReleaseGate.ContractManifest());
}
