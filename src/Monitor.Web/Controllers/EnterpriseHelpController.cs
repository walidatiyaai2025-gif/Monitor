using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class EnterpriseHelpController : Controller
{
    private readonly IEnterprisePersistenceReadinessService _readiness;

    public EnterpriseHelpController(IOperatorMetadataStore metadata, TimeProvider timeProvider)
    {
        _readiness = new EnterprisePersistenceReadinessService(metadata, timeProvider);
    }

    [HttpGet("/enterprise/help")]
    public IActionResult Help() => View();

    [HttpGet("/enterprise/readiness")]
    public IActionResult Readiness() => View(_readiness.Read());
}
