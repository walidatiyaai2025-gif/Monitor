using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Roles = "Administrator")]
public sealed class ConnectionLabController(
    IServerRegistrationRepository registrations,
    IServerConnectionTester tester) : Controller
{
    [HttpGet("/servers/connections")]
    public IActionResult Index() => View(BuildPage(new ConnectionLabRegistrationInput()));

    [HttpPost("/servers/connections/register")]
    [ValidateAntiForgeryToken]
    public IActionResult Register(ConnectionLabRegistrationInput input)
    {
        Normalize(input);
        ValidateInput(input);

        if (!ModelState.IsValid)
        {
            return View("Index", BuildPage(input));
        }

        try
        {
            var endpoint = new SqlServerEndpoint(
                input.Host,
                input.Port,
                input.InstanceName,
                input.Encrypt,
                input.TrustServerCertificate);

            if (IsDuplicate(endpoint))
            {
                ModelState.AddModelError(string.Empty, "This SQL Server endpoint is already registered.");
                return View("Index", BuildPage(input));
            }

            ConnectionSecretReference? secretReference = input.AuthenticationMode == SqlAuthenticationMode.SqlLogin
                ? new ConnectionSecretReference(input.SecretReference!)
                : null;

            var registration = new ServerRegistration(
                Guid.NewGuid(),
                input.DisplayName,
                endpoint,
                input.AuthenticationMode,
                secretReference,
                true,
                DateTimeOffset.UtcNow);

            registrations.Upsert(registration);
            TempData["ConnectionLabMessage"] = $"{registration.DisplayName} registered. Test Connection is ready.";
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, SafeDomainMessage(exception));
            return View("Index", BuildPage(input));
        }
    }

    [HttpPost("/servers/connections/{id:guid}/test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(Guid id, CancellationToken cancellationToken)
    {
        var registration = registrations.GetById(id);
        ConnectionTestResult result;

        if (registration is null)
        {
            result = new ConnectionTestResult(
                ConnectionTestStatus.RegistrationNotFound,
                "Server registration was not found.",
                0);
        }
        else
        {
            result = await tester.TestAsync(registration, cancellationToken);
        }

        return View("Index", BuildPage(new ConnectionLabRegistrationInput(), result, id));
    }

    private ConnectionLabViewModel BuildPage(
        ConnectionLabRegistrationInput input,
        ConnectionTestResult? result = null,
        Guid? testedId = null) => new()
    {
        Input = input,
        Registrations = registrations.GetAll().Select(ToSummary).ToArray(),
        TestResult = result,
        TestedRegistrationId = testedId
    };

    private bool IsDuplicate(SqlServerEndpoint endpoint) => registrations.GetAll().Any(item =>
        item.Endpoint.Host.Equals(endpoint.Host, StringComparison.OrdinalIgnoreCase) &&
        item.Endpoint.Port == endpoint.Port &&
        string.Equals(item.Endpoint.InstanceName, endpoint.InstanceName, StringComparison.OrdinalIgnoreCase));

    private static ConnectionLabRegistrationSummary ToSummary(ServerRegistration registration)
    {
        var endpoint = registration.Endpoint;
        var target = endpoint.Port.HasValue
            ? $"{endpoint.Host},{endpoint.Port.Value}"
            : endpoint.InstanceName is null
                ? endpoint.Host
                : $"{endpoint.Host}\\{endpoint.InstanceName}";

        return new ConnectionLabRegistrationSummary(
            registration.Id,
            registration.DisplayName,
            target,
            registration.AuthenticationMode,
            registration.SecretReference is not null,
            registration.IsEnabled,
            endpoint.Encrypt,
            endpoint.TrustServerCertificate,
            registration.CreatedAtUtc);
    }

    private void ValidateInput(ConnectionLabRegistrationInput input)
    {
        if (!Enum.IsDefined(input.AuthenticationMode))
        {
            ModelState.AddModelError(nameof(input.AuthenticationMode), "Select a supported authentication mode.");
        }

        if (input.Port.HasValue && !string.IsNullOrWhiteSpace(input.InstanceName))
        {
            ModelState.AddModelError(nameof(input.Port), "Specify either a TCP port or a named instance, not both.");
        }

        if (input.AuthenticationMode == SqlAuthenticationMode.SqlLogin && string.IsNullOrWhiteSpace(input.SecretReference))
        {
            ModelState.AddModelError(nameof(input.SecretReference), "SQL Login authentication requires an external secret reference.");
        }
    }

    private static void Normalize(ConnectionLabRegistrationInput input)
    {
        input.DisplayName = input.DisplayName?.Trim() ?? string.Empty;
        input.Host = input.Host?.Trim() ?? string.Empty;
        input.InstanceName = string.IsNullOrWhiteSpace(input.InstanceName) ? null : input.InstanceName.Trim();
        input.SecretReference = input.AuthenticationMode == SqlAuthenticationMode.SqlLogin && !string.IsNullOrWhiteSpace(input.SecretReference)
            ? input.SecretReference.Trim()
            : null;
    }

    private static string SafeDomainMessage(ArgumentException exception) => exception.ParamName switch
    {
        "host" => "Server / host is required.",
        "port" => "TCP port must be between 1 and 65535.",
        "displayName" => "Display name is required.",
        "secretReference" or "value" => "The selected SQL Login profile requires a valid external secret reference.",
        _ => "The SQL Server registration is invalid. Review the supplied metadata."
    };
}
