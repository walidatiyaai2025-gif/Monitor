using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class ErrorRouteMethodTests
{
    [Fact]
    public void ReExecuteErrorEndpoints_AreNotRestrictedToGet()
    {
        AssertVerbAgnosticRoute(nameof(ErrorController.ServerError), "/error");
        AssertVerbAgnosticRoute(nameof(ErrorController.Status), "/error/status/{statusCode:int}");
    }

    private static void AssertVerbAgnosticRoute(string methodName, string expectedTemplate)
    {
        var method = typeof(ErrorController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Missing action {methodName}.");

        Assert.Contains(method.GetCustomAttributes<RouteAttribute>(), route => route.Template == expectedTemplate);
        Assert.Empty(method.GetCustomAttributes<HttpGetAttribute>());
        Assert.Empty(method.GetCustomAttributes<HttpPostAttribute>());
    }
}
