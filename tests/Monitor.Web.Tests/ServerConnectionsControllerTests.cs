using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class ServerConnectionsControllerTests
{
    [Fact]
    public void Endpoint_RequiresAdministratorManagePolicyAndAntiforgery()
    {
        var authorize = Assert.Single(typeof(ServerConnectionsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        var action = typeof(ServerConnectionsController).GetMethod(nameof(ServerConnectionsController.TestConnection));

        Assert.Equal(MonitorPolicies.Manage, authorize.Policy);
        Assert.Null(authorize.Roles);
        Assert.NotNull(action);
        Assert.NotEmpty(action.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true));
    }

    [Fact]
    public async Task UnknownRegistration_ReturnsSafeNotFoundWithoutCallingTester()
    {
        var tester = new FakeTester();
        var controller = new ServerConnectionsController(
            new InMemoryServerRegistrationRepository(),
            tester,
            new FakeRefreshService());

        var response = await controller.TestConnection(Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(response.Result);
        var result = Assert.IsType<ConnectionTestResult>(notFound.Value);
        Assert.Equal(ConnectionTestStatus.RegistrationNotFound, result.Status);
        Assert.Equal(0, tester.CallCount);
    }

    private sealed class FakeTester : IServerConnectionTester
    {
        public int CallCount { get; private set; }

        public Task<ConnectionTestResult> TestAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ConnectionTestResult(
                ConnectionTestStatus.Succeeded,
                "Connection succeeded.",
                1));
        }
    }

    private sealed class FakeRefreshService : ISnapshotRefreshService
    {
        public Task<SnapshotRefreshResult> RefreshAsync(
            Guid registrationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SnapshotRefreshResult(
                SnapshotRefreshStatus.RegistrationNotFound,
                "Server registration was not found."));
    }
}
