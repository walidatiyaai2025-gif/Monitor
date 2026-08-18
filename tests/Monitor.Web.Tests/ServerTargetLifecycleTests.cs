using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class ServerTargetLifecycleTests
{
    [Fact]
    public void Disable_PersistsState_EvictsSnapshot_AndAudits()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var registration = Registration(true);
        repository.Upsert(registration);
        var cache = new FakeCache();
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var service = new ServerTargetLifecycleService(repository, cache, audit);

        var result = service.SetEnabled(registration.Id, false, "operator");

        Assert.Equal(ServerTargetLifecycleStatus.Disabled, result.Status);
        Assert.False(repository.GetById(registration.Id)!.IsEnabled);
        Assert.Equal(registration.Id, cache.Evicted);
        var entries = audit.Read(0, 10);
        Assert.Contains(entries, entry =>
            entry.Action == "server.monitoring.request" &&
            entry.Target == registration.Id.ToString("D") &&
            entry.Outcome == "disable");
        var final = Assert.Single(entries, entry => entry.Action == "server.monitoring");
        Assert.Equal("disabled", final.Outcome);
    }

    [Fact]
    public void AuditFailure_PreventsRegistrationMutationAndSnapshotEviction()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var registration = Registration(true);
        repository.Upsert(registration);
        var cache = new FakeCache();
        var service = new ServerTargetLifecycleService(repository, cache, new ThrowingAuditStore());

        var exception = Assert.Throws<IOException>(() =>
            service.SetEnabled(registration.Id, false, "operator"));

        Assert.Contains("audit unavailable", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(repository.GetById(registration.Id)!.IsEnabled);
        Assert.Null(cache.Evicted);
    }

    [Fact]
    public void Enable_PreservesIdentityEndpointCredentialAndHistoryMetadata()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var registration = Registration(false);
        repository.Upsert(registration);
        var service = new ServerTargetLifecycleService(
            repository, new FakeCache(), new InMemoryAuditStore(TimeProvider.System));

        var result = service.SetEnabled(registration.Id, true, "administrator");
        var updated = repository.GetById(registration.Id)!;

        Assert.Equal(ServerTargetLifecycleStatus.Enabled, result.Status);
        Assert.Equal(registration.Id, updated.Id);
        Assert.Equal(registration.Endpoint, updated.Endpoint);
        Assert.Equal(registration.SecretReference, updated.SecretReference);
        Assert.Equal(registration.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.True(updated.IsEnabled);
    }

    [Fact]
    public void RepeatingSameState_IsIdempotentAndDoesNotAudit()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var registration = Registration(true);
        repository.Upsert(registration);
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var service = new ServerTargetLifecycleService(repository, new FakeCache(), audit);

        var result = service.SetEnabled(registration.Id, true, "operator");

        Assert.Equal(ServerTargetLifecycleStatus.AlreadyInState, result.Status);
        Assert.Empty(audit.Read(0, 10));
    }

    private static ServerRegistration Registration(bool enabled) => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Finance SQL",
        new SqlServerEndpoint("sql01.internal", 1433),
        SqlAuthenticationMode.SqlLogin,
        new ConnectionSecretReference("env:FINANCE"),
        enabled,
        new DateTimeOffset(2026, 8, 11, 5, 0, 0, TimeSpan.Zero));

    private sealed class FakeCache : IServerHealthSnapshotCache
    {
        public Guid? Evicted { get; private set; }
        public void Evict(Guid registrationId) => Evicted = registrationId;
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingAuditStore : IAuditStore
    {
        public void Append(string actor, string action, string target, string outcome) =>
            throw new IOException("audit unavailable");

        public IReadOnlyList<AuditEvent> Read(int offset, int limit) => [];
    }
}
