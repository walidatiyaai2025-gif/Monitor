using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class Batch500ProductionController : Controller
{
    [HttpGet("/production/v1/acceptance-contract")]
    public IActionResult Contract() => Json(Batch500ReleaseGate.ContractManifest());
}
