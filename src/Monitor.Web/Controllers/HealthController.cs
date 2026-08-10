using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[ApiController]
public sealed class HealthController(
    IApplicationReadinessService readiness,
    IMonitorTelemetry telemetry) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("/health/live")]
    public IActionResult Live() => Ok(new
    {
        status = "Live"
    });

    [AllowAnonymous]
    [HttpGet("/health/ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var snapshot = await readiness.CheckAsync(cancellationToken);
        var body = new
        {
            status = snapshot.Status.ToString(),
            message = snapshot.Message,
            sharedState = snapshot.SharedStateStatus.ToString(),
            deploymentReady = snapshot.DeploymentReady,
            checkedAtUtc = snapshot.CheckedAtUtc
        };
        return snapshot.Status == ApplicationReadinessStatus.Ready
            ? Ok(body)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }

    [AllowAnonymous]
    [HttpGet("/health")]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        var snapshot = await readiness.CheckAsync(cancellationToken);
        var metrics = telemetry.Snapshot();
        var body = new
        {
            status = snapshot.Status.ToString(),
            sharedState = snapshot.SharedStateStatus.ToString(),
            deploymentReady = snapshot.DeploymentReady,
            collector = new
            {
                attempts = metrics.CollectorAttempts,
                succeeded = metrics.CollectorSucceeded,
                failed = metrics.CollectorFailed
            },
            checkedAtUtc = snapshot.CheckedAtUtc
        };
        return snapshot.Status == ApplicationReadinessStatus.Ready
            ? Ok(body)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}

[Authorize(Policy = MonitorPolicies.Manage)]
public sealed class ObservabilityController(
    IApplicationReadinessService readiness,
    IMonitorTelemetry telemetry) : Controller
{
    [HttpGet("/observability")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(new ObservabilityViewModel(
            await readiness.CheckAsync(cancellationToken),
            telemetry.Snapshot()));
}
