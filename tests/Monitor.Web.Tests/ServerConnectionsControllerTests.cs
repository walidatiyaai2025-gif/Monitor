using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        var refreshAction = typeof(ServerConnectionsController).GetMethod(nameof(ServerConnectionsController.RefreshSnapshot));

        Assert.Equal(MonitorPolicies.Manage, authorize.Policy);
        Assert.Null(authorize.Roles);
        Assert.NotNull(action);
        Assert.NotEmpty(action.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true));
        Assert.NotNull(refreshAction);
        Assert.NotEmpty(refreshAction.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true));
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

    [Fact]
    public async Task RetainedStaleRefresh_ReturnsServiceUnavailableWithEvidencePayload()
    {
        var expected = new SnapshotRefreshResult(
            SnapshotRefreshStatus.RetainedStale,
            "Refresh failed; retained stale snapshot returned.",
            Freshness: SnapshotFreshness.Stale);
        var controller = new ServerConnectionsController(
            new InMemoryServerRegistrationRepository(),
            new FakeTester(),
            new FakeRefreshService(expected));

        var response = await controller.RefreshSnapshot(Guid.NewGuid(), CancellationToken.None);

        var unavailable = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
        Assert.Same(expected, unavailable.Value);
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

    private sealed class FakeRefreshService(SnapshotRefreshResult? result = null) : ISnapshotRefreshService
    {
        private readonly SnapshotRefreshResult _result = result ?? new SnapshotRefreshResult(
            SnapshotRefreshStatus.RegistrationNotFound,
            "Server registration was not found.");

        public Task<SnapshotRefreshResult> RefreshAsync(
            Guid registrationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_result);
    }
}
