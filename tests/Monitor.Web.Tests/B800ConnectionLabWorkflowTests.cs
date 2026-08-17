using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800ConnectionLabWorkflowTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void ConnectionLab_IsAdministratorOnlyThroughNamedManagePolicy()
    {
        var policy = typeof(ConnectionLabController).GetCustomAttributes<AuthorizeAttribute>().Single();
        Assert.Equal(MonitorPolicies.Manage, policy.Policy);

        foreach (var methodName in new[]
        {
            nameof(ConnectionLabController.Register),
            nameof(ConnectionLabController.Test),
            nameof(ConnectionLabController.ReplaceCredentialReference),
            nameof(ConnectionLabController.ReplaceLocalCredential),
            nameof(ConnectionLabController.CleanupOwnedCredentials),
            nameof(ConnectionLabController.Enable),
            nameof(ConnectionLabController.Disable)
        })
        {
            var method = typeof(ConnectionLabController).GetMethod(methodName)
                ?? throw new MissingMethodException(nameof(ConnectionLabController), methodName);
            Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }
    }

    [Fact]
    public void Register_TestsCandidateBeforeDurableRegistrationMutation()
    {
        var controller = Read("src/Monitor.Web/Controllers/ConnectionLabController.cs");
        var register = Slice(
            controller,
            "public async Task<IActionResult> Register",
            "[HttpPost(\"/servers/connections/{id:guid}/test\")]");

        var testIndex = register.IndexOf("tester.TestAsync(registration", StringComparison.Ordinal);
        var upsertIndex = register.IndexOf("registrations.Upsert(registration)", StringComparison.Ordinal);
        Assert.True(testIndex >= 0, "Candidate connection test is missing from registration workflow.");
        Assert.True(upsertIndex > testIndex, "Registration must not be persisted before the candidate connection test succeeds.");

        Assert.Contains("TryCleanupCandidateCredentialAsync", register, StringComparison.Ordinal);
        Assert.Contains("if (!testResult.Succeeded)", register, StringComparison.Ordinal);
        Assert.Contains("return View(\"Index\", BuildPage(input, testResult))", register, StringComparison.Ordinal);
        Assert.Contains("observer.Observe(await cache.RefreshAsync(registration", register, StringComparison.Ordinal);
        Assert.Contains("RedirectToAction(\"Servers\", \"Operations\")", register, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrationFailure_ClearsPasswordAndDoesNotRenderExistingSecretValues()
    {
        var controller = Read("src/Monitor.Web/Controllers/ConnectionLabController.cs");
        var view = Read("src/Monitor.Web/Views/ConnectionLab/Index.cshtml");

        Assert.Contains("input.SqlPassword = null", controller, StringComparison.Ordinal);
        Assert.Contains("type=\"password\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"@Model.Input.SqlPassword\"", view, StringComparison.Ordinal);
        Assert.Contains("Passwords are write-only", view, StringComparison.Ordinal);
        Assert.Contains("The current reference is never rendered", view, StringComparison.Ordinal);
        Assert.DoesNotContain("registration.SecretReference", view, StringComparison.Ordinal);
    }

    [Fact]
    public void VisibleConnectionLabControls_MapToProtectedControllerActions()
    {
        var view = Read("src/Monitor.Web/Views/ConnectionLab/Index.cshtml");
        foreach (var action in new[]
        {
            "Register",
            "Test",
            "ReplaceLocalCredential",
            "ReplaceCredentialReference",
            "CleanupOwnedCredentials"
        })
            Assert.Contains($"asp-action=\"{action}\"", view, StringComparison.Ordinal);

        Assert.Contains("asp-action=\"@(registration.IsEnabled ? \"Disable\" : \"Enable\")\"", view, StringComparison.Ordinal);
        Assert.Contains("Administrator only", view, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", view, StringComparison.OrdinalIgnoreCase);
    }

    private static string Slice(string value, string startToken, string endToken)
    {
        var start = value.IndexOf(startToken, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start token not found: {startToken}");
        var end = value.IndexOf(endToken, start, StringComparison.Ordinal);
        Assert.True(end > start, $"End token not found after {startToken}: {endToken}");
        return value[start..end];
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
