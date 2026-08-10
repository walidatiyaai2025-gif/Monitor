using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly IAdminCredentialVerifier _credentialVerifier;
    private readonly ILoginAttemptLimiter? _limiter;
    private readonly IAuditStore? _audit;
    private readonly IServerRegistrationRepository? _registrations;

    public AccountController(IAdminCredentialVerifier credentialVerifier, ILoginAttemptLimiter? limiter = null, IAuditStore? audit = null, IServerRegistrationRepository? registrations = null)
    {
        _credentialVerifier = credentialVerifier;
        _limiter = limiter;
        _audit = audit;
        _registrations = registrations;
    }

    [AllowAnonymous]
    [HttpGet("/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Dashboard", "Operations");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [HttpPost("/login")]
    public async Task<IActionResult> Login(string? username, string? password, string? returnUrl = null)
    {
        var normalizedUsername = username ?? string.Empty;
        var normalizedPassword = password ?? string.Empty;
        var attemptKey = LoginAttemptKey.Create(HttpContext.Connection.RemoteIpAddress, normalizedUsername);

        if (_limiter?.IsAllowed(attemptKey) == false)
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            _audit?.Append("anonymous", "login", "development-admin", "locked");
            ModelState.AddModelError(string.Empty, "Too many login attempts. Try again later.");
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        if (!_credentialVerifier.Verify(normalizedUsername, normalizedPassword))
        {
            _limiter?.RecordFailure(attemptKey);
            _audit?.Append("anonymous", "login", "development-admin", "rejected");
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        _limiter?.RecordSuccess(attemptKey);
        _audit?.Append(normalizedUsername, "login", "development-admin", "success");

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, normalizedUsername),
            new Claim(ClaimTypes.Role, MonitorRoles.Administrator)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return _registrations?.GetAll().Any(item => item.IsEnabled) == true
            ? RedirectToAction("Dashboard", "Operations")
            : RedirectToAction("Index", "ConnectionLab");
    }

    [Authorize]
    [ValidateAntiForgeryToken]
    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
