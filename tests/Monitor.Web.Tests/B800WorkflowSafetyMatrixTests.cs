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

        foreach (var item in posts)
        {
            var antiforgery = item.Method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null ||
                              item.Controller.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null;
            Assert.True(antiforgery, $"POST {item.Controller.Name}.{item.Method.Name} is missing ValidateAntiForgeryToken.");

            var anonymous = item.Method.GetCustomAttribute<AllowAnonymousAttribute>() is not null ||
                            item.Controller.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
            var authorized = item.Method.GetCustomAttributes<AuthorizeAttribute>().Any() ||
                             item.Controller.GetCustomAttributes<AuthorizeAttribute>().Any();
            Assert.True(anonymous || authorized, $"POST {item.Controller.Name}.{item.Method.Name} has neither authorization nor explicit AllowAnonymous.");
        }
    }

    [Fact]
    public void OperationalPosts_UseAtLeastOneNamedPolicy()
    {
        var posts = ControllerMethods()
            .Where(item => item.Controller != typeof(AccountController))
            .Where(item => item.Method.GetCustomAttributes<HttpPostAttribute>().Any())
            .Where(item => item.Method.GetCustomAttribute<AllowAnonymousAttribute>() is null)
            .ToArray();

        foreach (var item in posts)
        {
            var policies = item.Controller.GetCustomAttributes<AuthorizeAttribute>()
                .Concat(item.Method.GetCustomAttributes<AuthorizeAttribute>())
                .Select(attribute => attribute.Policy)
                .Where(policy => !string.IsNullOrWhiteSpace(policy))
                .ToArray();

            Assert.NotEmpty(policies);
        }
    }

    [Fact]
    public void GetEndpoints_AreNeverUsedAsMutationAliases()
    {
        foreach (var item in ControllerMethods())
        {
            var isGet = item.Method.GetCustomAttributes<HttpGetAttribute>().Any();
            if (!isGet) continue;

            Assert.Empty(item.Method.GetCustomAttributes<HttpPostAttribute>());
            Assert.Null(item.Method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }
    }

    private static IEnumerable<(Type Controller, MethodInfo Method)> ControllerMethods()
    {
        var assembly = typeof(OperationsController).Assembly;
        return assembly.GetTypes()
            .Where(type => type.Namespace == typeof(OperationsController).Namespace)
            .Where(type => !type.IsAbstract && typeof(Controller).IsAssignableFrom(type))
            .SelectMany(
                controller => controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Select(method => (Controller: controller, Method: method)));
    }
}
