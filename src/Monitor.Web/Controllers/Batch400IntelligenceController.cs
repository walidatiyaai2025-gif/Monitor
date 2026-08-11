using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class Batch400IntelligenceController : Controller
{
    [HttpGet("/intelligence/v2/contract")]
    public IActionResult Contract() => Json(Batch400ReleaseGate.ContractManifest());
}
