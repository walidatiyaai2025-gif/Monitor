using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class RealServerJourneyTests
{
    [Fact]
    public async Task RegisterSuccess_TestsCollectsObservesAndRedirectsToRealServers()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var writer = new FakeCredentialWriter();
        var cache = new FakeCache();
        var observer = new FakeObserver();
        var controller = new ConnectionLabController(repository, new SuccessfulTester(), writer, cache, observer);
        var input = SqlInput();

        var action = await controller.Register(input, default);

        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal("Servers", redirect.ActionName);
        Assert.Equal("Operations", redirect.ControllerName);
        Assert.Equal(1, writer.CallCount);
        Assert.Equal(1, cache.RefreshCount);
        Assert.Equal(1, observer.CallCount);
        var registration = Assert.Single(repository.GetAll());
        Assert.DoesNotContain("canary-password", System.Text.Json.JsonSerializer.Serialize(registration), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedTest_RegistersButDoesNotCollectOrEchoPassword()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var cache = new FakeCache();
        var controller = new ConnectionLabController(repository, new FailedTester(), new FakeCredentialWriter(), cache, new FakeObserver());
        var input = SqlInput();

        var action = await controller.Register(input, default);

        Assert.IsType<ViewResult>(action);
        Assert.Null(input.SqlPassword);
        Assert.Equal(0, cache.RefreshCount);
        Assert.Single(repository.GetAll());
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

    private static ConnectionLabRegistrationInput SqlInput() => new()
    {
        DisplayName = "Real SQL", Host = "sql.internal", Port = 1433,
        AuthenticationMode = SqlAuthenticationMode.SqlLogin, SqlUsername = "monitor_reader",
        SqlPassword = "canary-password", Encrypt = true
    };

    private static ServerRegistration Registration(Guid id, string name) => new(
        id, name, new SqlServerEndpoint($"{name.Replace(" ", "").ToLowerInvariant()}.internal"),
        SqlAuthenticationMode.IntegratedSecurity, null, true, DateTimeOffset.UtcNow);

    private sealed class FakeCredentialWriter : IRuntimeCredentialWriter
    {
        public int CallCount { get; private set; }
        public ValueTask<ConnectionSecretReference> StoreAsync(string username, string password, CancellationToken cancellationToken = default)
        { CallCount++; return ValueTask.FromResult(new ConnectionSecretReference("runtime-safe-reference")); }
    }

    private sealed class SuccessfulTester : IServerConnectionTester
    {
        public Task<ConnectionTestResult> TestAsync(ServerRegistration registration, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectionTestResult(ConnectionTestStatus.Succeeded, "Connection succeeded.", 10, "17.0"));
    }

    private sealed class FailedTester : IServerConnectionTester
    {
        public Task<ConnectionTestResult> TestAsync(ServerRegistration registration, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectionTestResult(ConnectionTestStatus.AuthenticationFailed, "Authentication failed.", 10));
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
