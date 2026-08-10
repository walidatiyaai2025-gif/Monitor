using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Roles = "Administrator")]
public sealed class OperationsController : Controller
{
    private readonly IDemoMonitorService _monitor;
    private readonly IServerRegistrationService _registrations;

    public OperationsController(
        IDemoMonitorService monitor,
        IServerRegistrationService registrations)
    {
        _monitor = monitor;
        _registrations = registrations;
    }

    [HttpGet("/dashboard")]
    public IActionResult Dashboard() => View(_monitor.GetDashboard());

    [HttpGet("/servers")]
    public IActionResult Servers() => View(_monitor.GetServers());

    [HttpGet("/servers/register")]
    public IActionResult RegisterServer() => View(BuildRegistrationPage(new RegisterServerInput()));

    [HttpPost("/servers/register")]
    [ValidateAntiForgeryToken]
    public IActionResult RegisterServer(RegisterServerInput input)
    {
        if (!Enum.IsDefined(input.AuthenticationMode))
        {
            ModelState.AddModelError(nameof(input.AuthenticationMode), "Select a supported authentication mode.");
        }

        if (input.AuthenticationMode == SqlAuthenticationMode.SqlLogin)
        {
            if (string.IsNullOrWhiteSpace(input.Username))
            {
                ModelState.AddModelError(nameof(input.Username), "SQL login is required when SQL authentication is selected.");
            }

            if (string.IsNullOrWhiteSpace(input.Password))
            {
                ModelState.AddModelError(nameof(input.Password), "Password is required when SQL authentication is selected.");
            }
        }
        else
        {
            input.Username = null;
            input.Password = null;
        }

        if (!ModelState.IsValid)
        {
            ClearPasswordAttempt(input);
            return View(BuildRegistrationPage(input));
        }

        var result = _registrations.Register(input);
        input.Password = null;

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The SQL target could not be registered.");
            ClearPasswordAttempt(input);
            return View(BuildRegistrationPage(input));
        }

        TempData["RegistrationCreated"] = $"{result.Registration!.DisplayName} registered. Test Connection is the next M1 step.";
        return RedirectToAction(nameof(RegisterServer));
    }

    [HttpGet("/servers/{id}")]
    public IActionResult ServerDetails(string id)
    {
        var model = _monitor.GetServer(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet("/database-health")]
    public IActionResult DatabaseHealth() => View(_monitor.GetServers());

    [HttpGet("/memory-health")]
    public IActionResult MemoryHealth() => View(_monitor.GetServers());

    [HttpGet("/alerts")]
    public IActionResult Alerts() => View(_monitor.GetIncidents());

    [HttpGet("/settings")]
    public IActionResult Settings() => View();

    private RegisterServerPageViewModel BuildRegistrationPage(RegisterServerInput input) => new()
    {
        Input = input,
        Registrations = _registrations.GetAll()
    };

    private void ClearPasswordAttempt(RegisterServerInput input)
    {
        var passwordKey = nameof(RegisterServerInput.Password);
        var errors = ModelState.TryGetValue(passwordKey, out var entry)
            ? entry.Errors.Select(error => error.ErrorMessage).Where(message => !string.IsNullOrWhiteSpace(message)).ToArray()
            : [];

        ModelState.Remove(passwordKey);
        foreach (var error in errors)
        {
            ModelState.AddModelError(passwordKey, error);
        }

        input.Password = null;
    }
}
