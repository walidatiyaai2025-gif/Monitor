using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class AccountControllerSecurityControlTests
{
    [Fact]
    public async Task MissingLoginLimiter_ReturnsServiceUnavailableBeforeCredentialVerification()
    {
        var verifier = new AcceptingVerifier();
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var controller = Controller(verifier, limiter: null, audit);

        var result = await controller.Login("Admin", "password");

        Assert.IsType<ViewResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, controller.Response.StatusCode);
        Assert.Equal(0, verifier.Calls);
        Assert.Contains(audit.Read(0, 10), item => item.Outcome == "security-unavailable");
    }

    [Fact]
    public async Task MissingAuditStore_ReturnsServiceUnavailableBeforeCredentialVerification()
    {
        var verifier = new AcceptingVerifier();
        var controller = Controller(verifier, new LoginAttemptLimiter(TimeProvider.System), audit: null);

        var result = await controller.Login("Admin", "password");

        Assert.IsType<ViewResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, controller.Response.StatusCode);
        Assert.Equal(0, verifier.Calls);
    }

    private static AccountController Controller(
        IAdminCredentialVerifier verifier,
        ILoginAttemptLimiter? limiter,
        IAuditStore? audit)
    {
        var controller = new AccountController(verifier, limiter, audit)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }

    private sealed class AcceptingVerifier : IAdminCredentialVerifier
    {
        public int Calls { get; private set; }

        public bool Verify(string username, string password)
        {
            Calls++;
            return true;
        }
    }
}
