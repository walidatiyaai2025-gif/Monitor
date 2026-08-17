using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800WorkflowSafetyMatrixTests
{
    [Fact]
    public void EveryControllerPost_IsAntiforgeryProtected_AndExplicitlyAuthorizedOrAnonymous()
    {
        var posts = ControllerMethods()
            .Where(item => item.Method.GetCustomAttributes<HttpPostAttribute>().Any())
            .ToArray();

        Assert.NotEmpty(posts);
        var violations = new List<string>();

        foreach (var item in posts)
        {
            var antiforgery = item.Method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null ||
                              item.Controller.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null;
            if (!antiforgery)
                violations.Add($"POST {item.Controller.Name}.{item.Method.Name} is missing ValidateAntiForgeryToken.");

            var anonymous = item.Method.GetCustomAttribute<AllowAnonymousAttribute>() is not null ||
                            item.Controller.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
            var authorized = item.Method.GetCustomAttributes<AuthorizeAttribute>().Any() ||
                             item.Controller.GetCustomAttributes<AuthorizeAttribute>().Any();
            if (!anonymous && !authorized)
                violations.Add($"POST {item.Controller.Name}.{item.Method.Name} has neither authorization nor explicit AllowAnonymous.");
        }

        Assert.True(violations.Count == 0, "POST safety violations:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void OperationalPosts_UseAtLeastOneNamedPolicy()
    {
        var posts = ControllerMethods()
            .Where(item => item.Controller != typeof(AccountController))
            .Where(item => item.Method.GetCustomAttributes<HttpPostAttribute>().Any())
            .Where(item => item.Method.GetCustomAttribute<AllowAnonymousAttribute>() is null)
            .ToArray();
        var unnamed = new List<string>();

        foreach (var item in posts)
        {
            var policies = item.Controller.GetCustomAttributes<AuthorizeAttribute>()
                .Concat(item.Method.GetCustomAttributes<AuthorizeAttribute>())
                .Select(attribute => attribute.Policy)
                .Where(policy => !string.IsNullOrWhiteSpace(policy))
                .ToArray();

            if (policies.Length == 0)
                unnamed.Add($"{item.Controller.Name}.{item.Method.Name}");
        }

        Assert.True(unnamed.Count == 0, "Operational POST actions without a named authorization policy:\n" + string.Join("\n", unnamed));
    }

    [Fact]
    public void GetEndpoints_AreNeverUsedAsMutationAliases()
    {
        var violations = new List<string>();
        foreach (var item in ControllerMethods())
        {
            var isGet = item.Method.GetCustomAttributes<HttpGetAttribute>().Any();
            if (!isGet) continue;

            if (item.Method.GetCustomAttributes<HttpPostAttribute>().Any())
                violations.Add($"GET {item.Controller.Name}.{item.Method.Name} is also marked HttpPost.");
            if (item.Method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null)
                violations.Add($"GET {item.Controller.Name}.{item.Method.Name} carries mutation antiforgery metadata.");
        }

        Assert.True(violations.Count == 0, "GET/mutation alias violations:\n" + string.Join("\n", violations));
    }

    private static IEnumerable<(Type Controller, MethodInfo Method)> ControllerMethods()
    {
        var assembly = typeof(OperationsController).Assembly;
        return assembly.GetTypes()
            .Where(type => type.Namespace == typeof(OperationsController).Namespace)
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(
                controller => controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Select(method => (Controller: controller, Method: method)));
    }
}
