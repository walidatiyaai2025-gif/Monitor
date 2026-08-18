using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class GlobalRefreshAuthenticationTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public async Task CookieEvents_AjaxLoginChallenge_Returns401WithoutRedirect()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        var context = RedirectContext(http, "/login");

        await Events().RedirectToLogin(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, http.Response.StatusCode);
        Assert.False(http.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task CookieEvents_AjaxAccessDenied_Returns403WithoutRedirect()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        var context = RedirectContext(http, "/access-denied");

        await Events().RedirectToAccessDenied(context);

        Assert.Equal(StatusCodes.Status403Forbidden, http.Response.StatusCode);
        Assert.False(http.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task CookieEvents_NormalBrowserChallenge_PreservesCookieRedirectBehavior()
    {
        var http = new DefaultHttpContext();
        var context = RedirectContext(http, "/login?returnUrl=%2Fdashboard");

        await Events().RedirectToLogin(context);

        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.Equal("/login?returnUrl=%2Fdashboard", http.Response.Headers.Location.ToString());
    }

    [Fact]
    public void GlobalRefreshClient_RejectsAuthRedirectAndNonJsonSuccess()
    {
        var script = File.ReadAllText(Path.Combine(Root, "src", "Monitor.Web", "wwwroot", "js", "site.js"));

        Assert.Contains("'X-Requested-With': 'XMLHttpRequest'", script, StringComparison.Ordinal);
        Assert.Contains("'Accept': 'application/json'", script, StringComparison.Ordinal);
        Assert.Contains("response.status === 401 || response.status === 403", script, StringComparison.Ordinal);
        Assert.Contains("response.redirected", script, StringComparison.Ordinal);
        Assert.Contains("contentType.includes('application/json')", script, StringComparison.Ordinal);
        Assert.Contains("authorizationFailure", script, StringComparison.Ordinal);
        Assert.Contains("Session expired or authentication is required", script, StringComparison.Ordinal);
        Assert.Contains("Administrator permission is required", script, StringComparison.Ordinal);
    }

    private static AbsoluteSessionCookieEvents Events() => new(
        new WebSecurityOptions
        {
            SessionIdleMinutes = 30,
            SessionAbsoluteHours = 8
        },
        TimeProvider.System);

    private static RedirectContext<CookieAuthenticationOptions> RedirectContext(DefaultHttpContext http, string redirectUri)
    {
        var scheme = new AuthenticationScheme(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CookieAuthenticationDefaults.AuthenticationScheme,
            typeof(CookieAuthenticationHandler));
        return new RedirectContext<CookieAuthenticationOptions>(
            http,
            scheme,
            new CookieAuthenticationOptions(),
            new AuthenticationProperties(),
            redirectUri);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
