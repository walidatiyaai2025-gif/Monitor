using System.Security.Claims;
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
    public async Task MissingAttributableActor_ForbidsWithoutCallingTesterOrRefresh()
    {
        var tester = new FakeTester();
        var refresh = new FakeRefreshService();
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var controller = CreateController(
            new InMemoryServerRegistrationRepository(),
            tester,
            refresh,
            audit,
            actor: null);

        var testResponse = await controller.TestConnection(Guid.NewGuid(), CancellationToken.None);
        var refreshResponse = await controller.RefreshSnapshot(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<ForbidResult>(testResponse.Result);
        Assert.IsType<ForbidResult>(refreshResponse.Result);
        Assert.Equal(0, tester.CallCount);
        Assert.Equal(0, refresh.CallCount);
        Assert.Empty(audit.Read(0, 100));
    }

    [Fact]
    public async Task UnknownRegistration_ReturnsSafeNotFoundWithoutCallingTesterAndAuditsActor()
    {
        var tester = new FakeTester();
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var id = Guid.NewGuid();
        var controller = CreateController(
            new InMemoryServerRegistrationRepository(),
            tester,
            new FakeRefreshService(),
            audit);

        var response = await controller.TestConnection(id, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(response.Result);
        var result = Assert.IsType<ConnectionTestResult>(notFound.Value);
        Assert.Equal(ConnectionTestStatus.RegistrationNotFound, result.Status);
        Assert.Equal(0, tester.CallCount);
        var events = audit.Read(0, 100).OrderBy(item => item.OccurredAtUtc).ToArray();
        Assert.Equal(2, events.Length);
        Assert.All(events, item => Assert.Equal("administrator", item.Actor));
        Assert.All(events, item => Assert.Equal("server.connection.test", item.Action));
        Assert.All(events, item => Assert.Equal(id.ToString("D"), item.Target));
        Assert.Equal("requested", events[0].Outcome);
        Assert.Equal("not-found", events[1].Outcome);
    }

    [Fact]
    public async Task SuccessfulConnectionTest_AuditsRequestedAndSafeStatus()
    {
        var id = Guid.NewGuid();
        var registrations = new InMemoryServerRegistrationRepository();
        registrations.Upsert(new ServerRegistration(
            id,
            "Test SQL",
            new SqlServerEndpoint("sql01"),
            SqlAuthenticationMode.IntegratedSecurity,
            null,
            true,
            DateTimeOffset.UtcNow));
        var tester = new FakeTester();
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var controller = CreateController(
            registrations,
            tester,
            new FakeRefreshService(),
            audit,
            actor: " DBA Operator ");

        var response = await controller.TestConnection(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.IsType<ConnectionTestResult>(ok.Value);
        Assert.Equal(1, tester.CallCount);
        var events = audit.Read(0, 100).OrderBy(item => item.OccurredAtUtc).ToArray();
        Assert.Equal(2, events.Length);
        Assert.All(events, item => Assert.Equal("DBA Operator", item.Actor));
        Assert.All(events, item => Assert.Equal("server.connection.test", item.Action));
        Assert.Equal("requested", events[0].Outcome);
        Assert.Equal(ConnectionTestStatus.Succeeded.ToString(), events[1].Outcome);
    }

    [Fact]
    public async Task RetainedStaleRefresh_ReturnsServiceUnavailableWithEvidencePayloadAndAudit()
    {
        var expected = new SnapshotRefreshResult(
            SnapshotRefreshStatus.RetainedStale,
            "Refresh failed; retained stale snapshot returned.",
            Freshness: SnapshotFreshness.Stale);
        var refresh = new FakeRefreshService(expected);
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var id = Guid.NewGuid();
        var controller = CreateController(
            new InMemoryServerRegistrationRepository(),
            new FakeTester(),
            refresh,
            audit);

        var response = await controller.RefreshSnapshot(id, CancellationToken.None);

        var unavailable = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
        Assert.Same(expected, unavailable.Value);
        Assert.Equal(1, refresh.CallCount);
        var events = audit.Read(0, 100).OrderBy(item => item.OccurredAtUtc).ToArray();
        Assert.Equal(2, events.Length);
        Assert.All(events, item => Assert.Equal("administrator", item.Actor));
        Assert.All(events, item => Assert.Equal("snapshot.refresh", item.Action));
        Assert.All(events, item => Assert.Equal(id.ToString("D"), item.Target));
        Assert.Equal("requested", events[0].Outcome);
        Assert.Equal(SnapshotRefreshStatus.RetainedStale.ToString(), events[1].Outcome);
    }

    private static ServerConnectionsController CreateController(
        IServerRegistrationRepository registrations,
        IServerConnectionTester tester,
        ISnapshotRefreshService refresh,
        IAuditStore audit,
        string? actor = "administrator")
    {
        var identity = actor is null
            ? new ClaimsIdentity(Array.Empty<Claim>(), "Test")
            : new ClaimsIdentity([new Claim(ClaimTypes.Name, actor)], "Test");
        return new ServerConnectionsController(registrations, tester, refresh, audit)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
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

        public int CallCount { get; private set; }

        public Task<SnapshotRefreshResult> RefreshAsync(
            Guid registrationId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }
}
