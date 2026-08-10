namespace Monitor.Web.Services;

public sealed class AuthenticationTelemetryMiddleware(
    RequestDelegate next,
    IMonitorTelemetry telemetry)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var observeLogin = HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path.Equals("/login", StringComparison.OrdinalIgnoreCase);

        await next(context);

        if (!observeLogin)
        {
            return;
        }

        if (context.Response.StatusCode == StatusCodes.Status429TooManyRequests)
        {
            telemetry.Login(SecurityTelemetryOutcome.Limited);
        }
        else if (context.Response.StatusCode is >= 300 and < 400)
        {
            telemetry.Login(SecurityTelemetryOutcome.Succeeded);
        }
        else
        {
            telemetry.Login(SecurityTelemetryOutcome.Rejected);
        }
    }
}
