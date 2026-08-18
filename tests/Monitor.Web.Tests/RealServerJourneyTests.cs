using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class RealServerJourneyTests
{
    [Fact]
    public async Task RegisterSuccess_TestsCandidateBeforeCommit_CollectsObservesAndRedirectsToRealServers()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var writer = new FakeCredentialWriter();
        var cache = new FakeCache();
        var observer = new FakeObserver();
        var tester = new RecordingTester(repository, succeed: true);
        var controller = CreateController(
            repository, tester, writer, cache, observer,
            credentialPolicy: LocalCredentialPolicy());
        var input = SqlInput();

        var action = await controller.Register(input, default);

        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal("Servers", redirect.ActionName);
        Assert.Equal("Operations", redirect.ControllerName);
        Assert.Equal(1, writer.CallCount);
        Assert.Equal(0, writer.DeleteCount);
        Assert.Equal(0, tester.RegistrationCountObservedDuringTest);
        Assert.Equal(1, cache.RefreshCount);
        Assert.Equal(1, observer.CallCount);
        var registration = Assert.Single(repository.GetAll());
        Assert.DoesNotContain("canary-password", System.Text.Json.JsonSerializer.Serialize(registration), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingActor_RegisterFailsClosedBeforeCredentialTestCommitOrCollection()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var writer = new FakeCredentialWriter();
        var cache = new FakeCache();
        var observer = new FakeObserver();
        var tester = new RecordingTester(repository, succeed: true);
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var controller = CreateController(
            repository, tester, writer, cache, observer,
            audit: audit,
            actor: null,
            credentialPolicy: LocalCredentialPolicy());

        var action = await controller.Register(SqlInput(), default);

        Assert.IsType<ForbidResult>(action);
        Assert.Equal(0, writer.CallCount);
        Assert.Equal(-1, tester.RegistrationCountObservedDuringTest);
        Assert.Equal(0, cache.RefreshCount);
        Assert.Equal(0, observer.CallCount);
        Assert.Empty(repository.GetAll());
        Assert.Empty(audit.Read(0, 100));
    }

    [Fact]
    public async Task MissingActor_TestFailsClosedBeforeConnectionProbe()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var registration = Registration(Guid.NewGuid(), "SQL One");
        repository.Upsert(registration);
        var tester = new RecordingTester(repository, succeed: true);
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var controller = CreateController(
            repository, tester, new FakeCredentialWriter(), new FakeCache(), new FakeObserver(),
            audit: audit,
            actor: null);

        var action = await controller.Test(registration.Id, default);

        Assert.IsType<ForbidResult>(action);
        Assert.Equal(-1, tester.RegistrationCountObservedDuringTest);
        Assert.Empty(audit.Read(0, 100));
    }

    [Fact]
    public async Task RegisterSuccess_AuditsAttributedRequestedAndConnectedWithoutSecretMaterial()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var controller = CreateController(
            repository,
            new RecordingTester(repository, succeed: true),
            new FakeCredentialWriter(),
            new FakeCache(),
            new FakeObserver(),
            audit: audit,
            actor: " DBA Operator ",
            credentialPolicy: LocalCredentialPolicy());

        var action = await controller.Register(SqlInput(), default);

        Assert.IsType<RedirectToActionResult>(action);
        var events = audit.Read(0, 100).OrderBy(item => item.OccurredAtUtc).ToArray();
        Assert.Equal(2, events.Length);
        Assert.All(events, item => Assert.Equal("DBA Operator", item.Actor));
        Assert.All(events, item => Assert.Equal("server.registration", item.Action));
        Assert.Equal("requested", events[0].Outcome);
        Assert.Equal("connected", events[1].Outcome);
        var serialized = System.Text.Json.JsonSerializer.Serialize(events);
        Assert.DoesNotContain("monitor_reader", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("canary-password", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("sql.internal", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedInitialTest_DoesNotPersistCollectOrEchoPassword_AndDeletesCandidateCredential()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var writer = new FakeCredentialWriter();
        var cache = new FakeCache();
        var tester = new RecordingTester(repository, succeed: false);
        var controller = CreateController(
            repository, tester, writer, cache, new FakeObserver(),
            credentialPolicy: LocalCredentialPolicy());
        var input = SqlInput();

        var action = await controller.Register(input, default);

        Assert.IsType<ViewResult>(action);
        Assert.Null(input.SqlPassword);
        Assert.Equal(0, tester.RegistrationCountObservedDuringTest);
        Assert.Equal(0, cache.RefreshCount);
        Assert.Empty(repository.GetAll());
        Assert.Equal(1, writer.CallCount);
        Assert.Equal(1, writer.DeleteCount);
    }

    [Fact]
    public async Task CancelledInitialTest_DoesNotPersistOrCollect_AndDeletesCandidateCredential()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var writer = new FakeCredentialWriter();
        var cache = new FakeCache();
        var controller = CreateController(
            repository, new CancelledTester(), writer, cache, new FakeObserver(),
            credentialPolicy: LocalCredentialPolicy());
        var input = SqlInput();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Register(input, default));

        Assert.Null(input.SqlPassword);
        Assert.Empty(repository.GetAll());
        Assert.Equal(0, cache.RefreshCount);
        Assert.Equal(1, writer.CallCount);
        Assert.Equal(1, writer.DeleteCount);
    }

    [Fact]
    public async Task FailedExternalReferenceTest_DoesNotMutateExternalSecretOrPersistTarget()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var writer = new FakeCredentialWriter();
        var cache = new FakeCache();
        var controller = CreateController(repository, new FailedTester(), writer, cache, new FakeObserver());
        var input = SqlInput();
        input.SqlUsername = null;
        input.SqlPassword = null;
        input.SecretReference = "env:FINANCE_PROD";

        var action = await controller.Register(input, default);

        Assert.IsType<ViewResult>(action);
        Assert.Empty(repository.GetAll());
        Assert.Equal(0, cache.RefreshCount);
        Assert.Equal(0, writer.CallCount);
        Assert.Equal(0, writer.DeleteCount);
    }

    [Fact]
    public async Task IntegratedSecuritySuccess_NeverCreatesCredential_AndCommitsAfterTest()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var writer = new FakeCredentialWriter();
        var cache = new FakeCache();
        var tester = new RecordingTester(repository, succeed: true);
        var controller = CreateController(repository, tester, writer, cache, new FakeObserver());
        var input = IntegratedInput();

        var action = await controller.Register(input, default);

        Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(0, tester.RegistrationCountObservedDuringTest);
        Assert.Equal(0, writer.CallCount);
        Assert.Equal(0, writer.DeleteCount);
        var registration = Assert.Single(repository.GetAll());
        Assert.Equal(SqlAuthenticationMode.IntegratedSecurity, registration.AuthenticationMode);
        Assert.Null(registration.SecretReference);
    }

    [Fact]
    public async Task EstateRead_ReturnsEveryRealRegistrationAndNeverMixesDemoCards()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var first = Registration(Guid.Parse("11111111-1111-1111-1111-111111111111"), "SQL One");
        var second = Registration(Guid.Parse("22222222-2222-2222-2222-222222222222"), "SQL Two");
        repository.Upsert(first);
        repository.Upsert(second);
        var service = new MonitorReadService(new DemoMonitorService(), repository, new KeyedCache(first.Id));

        var servers = await service.GetServersAsync();
        var dashboard = await service.GetDashboardAsync();

        Assert.Equal(2, servers.Count);
        Assert.Contains(servers, item => item.Source == ServerDataSource.LiveFresh);
        Assert.Contains(servers, item => item.Source == ServerDataSource.RegisteredUnavailable);
        Assert.DoesNotContain(servers, item => item.Source == ServerDataSource.Demo);
        Assert.Equal(2, dashboard.Servers.Count);
    }

    private static ConnectionLabController CreateController(
        IServerRegistrationRepository repository,
        IServerConnectionTester tester,
        IRuntimeCredentialWriter writer,
        IServerHealthSnapshotCache cache,
        ISnapshotObserver observer,
        IAuditStore? audit = null,
        string? actor = "administrator",
        CredentialPolicyOptions? credentialPolicy = null)
    {
        audit ??= new InMemoryAuditStore(TimeProvider.System);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(actor is null
                ? new ClaimsIdentity(Array.Empty<Claim>(), "Test")
                : new ClaimsIdentity([new Claim(ClaimTypes.Name, actor)], "Test"))
        };
        var controller = new ConnectionLabController(
            repository, tester, writer, cache, observer, audit,
            credentialPolicy: credentialPolicy)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
        return controller;
    }

    private static CredentialPolicyOptions LocalCredentialPolicy() => new()
    {
        AllowLocalOwnedCredentials = true
    };

    private static ConnectionLabRegistrationInput SqlInput() => new()
    {
        DisplayName = "Real SQL", Host = "sql.internal", Port = 1433,
        AuthenticationMode = SqlAuthenticationMode.SqlLogin, SqlUsername = "monitor_reader",
        SqlPassword = "canary-password", Encrypt = true
    };

    private static ConnectionLabRegistrationInput IntegratedInput() => new()
    {
        DisplayName = "Integrated SQL", Host = "sql-integrated.internal", Port = 1433,
        AuthenticationMode = SqlAuthenticationMode.IntegratedSecurity, Encrypt = true
    };

    private static ServerRegistration Registration(Guid id, string name) => new(
        id, name, new SqlServerEndpoint($"{name.Replace(" ", "").ToLowerInvariant()}.internal"),
        SqlAuthenticationMode.IntegratedSecurity, null, true, DateTimeOffset.UtcNow);

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class FakeCredentialWriter : IRuntimeCredentialWriter
    {
        public int CallCount { get; private set; }
        public int DeleteCount { get; private set; }

        public ValueTask<ConnectionSecretReference> StoreAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(new ConnectionSecretReference("runtime-safe-reference"));
        }

        public ValueTask DeleteAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default)
        {
            DeleteCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingTester(IServerRegistrationRepository repository, bool succeed) : IServerConnectionTester
    {
        public int RegistrationCountObservedDuringTest { get; private set; } = -1;

        public Task<ConnectionTestResult> TestAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            RegistrationCountObservedDuringTest = repository.GetAll().Count;
            return Task.FromResult(succeed
                ? new ConnectionTestResult(ConnectionTestStatus.Succeeded, "Connection succeeded.", 10, "17.0")
                : new ConnectionTestResult(ConnectionTestStatus.AuthenticationFailed, "Authentication failed.", 10));
        }
    }

    private sealed class FailedTester : IServerConnectionTester
    {
        public Task<ConnectionTestResult> TestAsync(ServerRegistration registration, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectionTestResult(ConnectionTestStatus.AuthenticationFailed, "Authentication failed.", 10));
    }

    private sealed class CancelledTester : IServerConnectionTester
    {
        public Task<ConnectionTestResult> TestAsync(ServerRegistration registration, CancellationToken cancellationToken = default) =>
            Task.FromException<ConnectionTestResult>(new OperationCanceledException());
    }

    private sealed class FakeCache : IServerHealthSnapshotCache
    {
        private SnapshotCacheResult? _latest;
        public int RefreshCount { get; private set; }
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => RefreshAsync(registration, cancellationToken);
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        { RefreshCount++; _latest = Result(registration.Id, registration.DisplayName); return Task.FromResult(_latest); }
        public SnapshotCacheResult? Peek(Guid registrationId) => _latest?.Snapshot.RegistrationId == registrationId ? _latest : null;
    }

    private sealed class KeyedCache(Guid successfulId) : IServerHealthSnapshotCache
    {
        public SnapshotCacheResult? Peek(Guid registrationId) => registrationId == successfulId ? Result(registrationId, "REAL SQL") : null;
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) =>
            registration.Id == successfulId ? Task.FromResult(Result(registration.Id, registration.DisplayName)) : Task.FromException<SnapshotCacheResult>(new SnapshotCollectionException(SnapshotCollectionFailure.NetworkUnavailable, "Unavailable"));
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => GetAsync(registration, cancellationToken);
    }

    private sealed class FakeObserver : ISnapshotObserver { public int CallCount { get; private set; } public void Observe(SnapshotCacheResult result) => CallCount++; }

    private static SnapshotCacheResult Result(Guid id, string name) => new(
        new ServerHealthSnapshot(id, name, "17", "Enterprise", null, 100, 2, 2, DateTimeOffset.UtcNow), SnapshotFreshness.Fresh, TimeSpan.Zero);
}
