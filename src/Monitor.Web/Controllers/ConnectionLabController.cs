using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Manage)]
public sealed class ConnectionLabController(
    IServerRegistrationRepository registrations,
    IServerConnectionTester tester,
    IRuntimeCredentialWriter credentialWriter,
    IServerHealthSnapshotCache cache,
    ISnapshotObserver observer,
    ICredentialLifecycleService? credentialLifecycle = null,
    ICredentialReadinessService? credentialReadiness = null,
    CredentialPolicyOptions? credentialPolicy = null,
    IServerTargetLifecycleService? targetLifecycle = null) : Controller
{
    private bool AllowsLocalCredentialEntry => credentialPolicy?.AllowLocalOwnedCredentials ?? true;

    [HttpGet("/servers/connections")]
    public IActionResult Index() => View(BuildPage(new ConnectionLabRegistrationInput()));

    [HttpPost("/servers/connections/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(ConnectionLabRegistrationInput input, CancellationToken cancellationToken)
    {
        Normalize(input);
        ValidateInput(input);

        if (!ModelState.IsValid)
        {
            input.SqlPassword = null;
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
                input.SqlPassword = null;
                return View("Index", BuildPage(input));
            }

            ConnectionSecretReference? secretReference = null;
            var createdCandidateCredential = false;
            if (input.AuthenticationMode == SqlAuthenticationMode.SqlLogin)
            {
                if (!string.IsNullOrWhiteSpace(input.SqlUsername) && !string.IsNullOrEmpty(input.SqlPassword))
                {
                    secretReference = await credentialWriter.StoreAsync(input.SqlUsername, input.SqlPassword, cancellationToken);
                    createdCandidateCredential = true;
                }
                else
                {
                    secretReference = new ConnectionSecretReference(input.SecretReference!);
                }
            }

            var registration = new ServerRegistration(
                Guid.NewGuid(),
                input.DisplayName,
                endpoint,
                input.AuthenticationMode,
                secretReference,
                true,
                DateTimeOffset.UtcNow);

            ConnectionTestResult testResult;
            try
            {
                testResult = await tester.TestAsync(registration, cancellationToken);
            }
            catch
            {
                input.SqlPassword = null;
                await TryCleanupCandidateCredentialAsync(secretReference, createdCandidateCredential);
                throw;
            }

            if (!testResult.Succeeded)
            {
                input.SqlPassword = null;
                var cleanupSucceeded = await TryCleanupCandidateCredentialAsync(secretReference, createdCandidateCredential);
                ModelState.AddModelError(
                    string.Empty,
                    cleanupSucceeded
                        ? testResult.Message
                        : "Connection failed and the temporary Monitor-owned credential could not be cleaned up automatically. Run owned credential cleanup before retrying.");
                return View("Index", BuildPage(input, testResult));
            }

            try
            {
                registrations.Upsert(registration);
            }
            catch
            {
                await TryCleanupCandidateCredentialAsync(secretReference, createdCandidateCredential);
                throw;
            }

            try
            {
                observer.Observe(await cache.RefreshAsync(registration, cancellationToken));
                if (ControllerContext.HttpContext is not null)
                {
                    TempData["ConnectionLabMessage"] = $"{registration.DisplayName} connected and its first real snapshot was collected.";
                }
                return RedirectToAction("Servers", "Operations");
            }
            catch (SnapshotCollectionException exception)
            {
                if (ControllerContext.HttpContext is not null)
                {
                    TempData["ConnectionLabMessage"] = $"Connection succeeded, but monitoring data is not available yet ({exception.Failure}). Review SQL monitoring permissions.";
                }
                return RedirectToAction(nameof(Index));
            }
        }
        catch (ArgumentException exception)
        {
            input.SqlPassword = null;
            ModelState.AddModelError(string.Empty, SafeDomainMessage(exception));
            return View("Index", BuildPage(input));
        }
        catch (InvalidOperationException)
        {
            input.SqlPassword = null;
            ModelState.AddModelError(string.Empty, "The selected credential mode is disabled by the current deployment policy.");
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

    [HttpPost("/servers/connections/{id:guid}/credential-reference")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplaceCredentialReference(
        Guid id,
        CredentialReferenceReplacementInput input,
        CancellationToken cancellationToken)
    {
        if (credentialLifecycle is null)
        {
            return NotFound();
        }

        var actor = User.Identity?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(actor))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData["ConnectionLabMessage"] = "Provide a valid external secret reference.";
            return RedirectToAction(nameof(Index));
        }

        var result = await credentialLifecycle.ReplaceWithExternalReferenceAsync(
            id,
            input.ExternalSecretReference,
            actor,
            cancellationToken);
        TempData["ConnectionLabMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/servers/connections/{id:guid}/credentials/local")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplaceLocalCredential(
        Guid id,
        LocalCredentialReplacementInput input,
        CancellationToken cancellationToken)
    {
        if (credentialLifecycle is null || !AllowsLocalCredentialEntry) return NotFound();
        var actor = User.Identity?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(actor)) return Forbid();
        if (!ModelState.IsValid)
        {
            TempData["ConnectionLabMessage"] = "Provide a valid SQL username and password.";
            return Redirect($"/servers/connections#target-{id:D}");
        }
        var result = await credentialLifecycle.ReplaceWithLocalCredentialAsync(
            id, input.SqlUsername, input.SqlPassword, actor, cancellationToken);
        input.SqlPassword = string.Empty;
        TempData["ConnectionLabMessage"] = result.Message;
        return Redirect($"/servers/connections#target-{id:D}");
    }

    [HttpPost("/servers/connections/credentials/cleanup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CleanupOwnedCredentials(CancellationToken cancellationToken)
    {
        if (credentialLifecycle is null)
        {
            return NotFound();
        }

        var actor = User.Identity?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(actor))
        {
            return Forbid();
        }

        var removed = await credentialLifecycle.CleanupOrphanedOwnedSecretsAsync(actor, cancellationToken);
        TempData["ConnectionLabMessage"] = removed == 0
            ? "No orphaned Monitor-owned SQL credentials were found."
            : $"Removed {removed} orphaned Monitor-owned credential entr{(removed == 1 ? "y" : "ies")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/servers/connections/{id:guid}/enable")]
    [ValidateAntiForgeryToken]
    public IActionResult Enable(Guid id) => SetEnabled(id, true);

    [HttpPost("/servers/connections/{id:guid}/disable")]
    [ValidateAntiForgeryToken]
    public IActionResult Disable(Guid id) => SetEnabled(id, false);

    private IActionResult SetEnabled(Guid id, bool enabled)
    {
        if (targetLifecycle is null) return NotFound();
        var actor = User.Identity?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(actor)) return Forbid();
        var result = targetLifecycle.SetEnabled(id, enabled, actor);
        if (result.Status == ServerTargetLifecycleStatus.NotFound) return NotFound();
        TempData["ConnectionLabMessage"] = result.Message;
        return Redirect($"/servers/connections#target-{id:D}");
    }

    private async ValueTask<bool> TryCleanupCandidateCredentialAsync(
        ConnectionSecretReference? secretReference,
        bool createdCandidateCredential)
    {
        if (!createdCandidateCredential || secretReference is null) return true;
        var reference = secretReference.Value;
        try
        {
            await credentialWriter.DeleteAsync(reference, CancellationToken.None);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private ConnectionLabViewModel BuildPage(
        ConnectionLabRegistrationInput input,
        ConnectionTestResult? result = null,
        Guid? testedId = null) => new()
    {
        Input = input,
        Registrations = registrations.GetAll().Select(ToSummary).ToArray(),
        TestResult = result,
        TestedRegistrationId = testedId,
        JourneyStep = registrations.GetAll().Count == 0 ? 1 : result?.Succeeded == true ? 3 : 2,
        AllowsLocalCredentialEntry = AllowsLocalCredentialEntry,
        CredentialReadiness = credentialReadiness?.Get()
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
        var localOwned = registration.SecretReference?.Value.StartsWith("local:v1:", StringComparison.Ordinal) == true;

        return new ConnectionLabRegistrationSummary(
            registration.Id,
            registration.DisplayName,
            target,
            registration.AuthenticationMode,
            registration.SecretReference is not null,
            registration.IsEnabled,
            endpoint.Encrypt,
            endpoint.TrustServerCertificate,
            registration.CreatedAtUtc,
            localOwned);
    }

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
        var localOwned = registration.SecretReference?.Value.StartsWith("local:v1:", StringComparison.Ordinal) == true;

        return new ConnectionLabRegistrationSummary(
            registration.Id,
            registration.DisplayName,
            target,
            registration.AuthenticationMode,
            registration.SecretReference is not null,
            registration.IsEnabled,
            endpoint.Encrypt,
            endpoint.TrustServerCertificate,
            registration.CreatedAtUtc,
            localOwned);
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

        if (input.AuthenticationMode == SqlAuthenticationMode.SqlLogin)
        {
            var suppliedLocalCredential = !string.IsNullOrWhiteSpace(input.SqlUsername) || !string.IsNullOrEmpty(input.SqlPassword);
            if (!AllowsLocalCredentialEntry && suppliedLocalCredential)
            {
                ModelState.AddModelError(nameof(input.SqlUsername), "Local SQL credential entry is disabled. Provide an external secret reference.");
            }

            if (string.IsNullOrWhiteSpace(input.SecretReference) &&
                (string.IsNullOrWhiteSpace(input.SqlUsername) || string.IsNullOrEmpty(input.SqlPassword)))
            {
                ModelState.AddModelError(nameof(input.SqlUsername), "Enter a SQL username/password for this session or provide an external secret reference.");
            }
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
        input.SqlUsername = string.IsNullOrWhiteSpace(input.SqlUsername) ? null : input.SqlUsername.Trim();
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
