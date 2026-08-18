using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class CredentialLifecycleSerializationTests
{
    [Fact]
    public async Task LocalReplacement_BlocksCleanupUntilCandidateReferenceIsCommitted()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var oldReference = new ConnectionSecretReference("local:v1:old");
        var registration = Registration(oldReference);
        registrations.Upsert(registration);

        var secrets = new ControlledSecretStore();
        secrets.AddOwned(oldReference, new SqlLoginSecret("old-user", "old-password"));
        var tester = new BlockingTester();
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var inner = new CredentialLifecycleService(
            registrations,
            secrets,
            tester,
            audit,
            new CredentialPolicyOptions { AllowLocalOwnedCredentials = true });
        ICredentialLifecycleService service = new WriteAheadAuditedCredentialLifecycleService(inner, audit);

        var replacementTask = service.ReplaceWithLocalCredentialAsync(
            registration.Id,
            "new-user",
            "new-password",
            "Admin");
        await tester.Started;

        var candidateReference = tester.LastRegistration!.SecretReference!.Value;
        var cleanupTask = service.CleanupOrphanedOwnedSecretsAsync("Admin");

        Assert.False(cleanupTask.IsCompleted);
        Assert.Contains(
            audit.Read(0, 20),
            item => item.Action == "credential.cleanup.request" && item.Outcome == "requested");
        Assert.True(secrets.ContainsOwned(candidateReference));

        tester.Release();
        var replacement = await replacementTask;
        var removed = await cleanupTask;
        var updated = registrations.GetById(registration.Id)!;

        Assert.True(replacement.Applied);
        Assert.Equal(0, removed);
        Assert.Equal(candidateReference, updated.SecretReference!.Value);
        Assert.True(secrets.ContainsOwned(candidateReference));
        Assert.False(secrets.ContainsOwned(oldReference));
    }

    [Fact]
    public async Task CleanupWaitingForReplacement_HonorsCancellationAfterWriteAheadAudit()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var oldReference = new ConnectionSecretReference("local:v1:old");
        var registration = Registration(oldReference);
        registrations.Upsert(registration);

        var secrets = new ControlledSecretStore();
        secrets.AddOwned(oldReference, new SqlLoginSecret("old-user", "old-password"));
        var tester = new BlockingTester();
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var inner = new CredentialLifecycleService(
            registrations,
            secrets,
            tester,
            audit,
            new CredentialPolicyOptions { AllowLocalOwnedCredentials = true });
        ICredentialLifecycleService service = new WriteAheadAuditedCredentialLifecycleService(inner, audit);

        var replacementTask = service.ReplaceWithLocalCredentialAsync(
            registration.Id,
            "new-user",
            "new-password",
            "Admin");
        await tester.Started;

        using var cancellation = new CancellationTokenSource();
        var cleanupTask = service.CleanupOrphanedOwnedSecretsAsync("Admin", cancellation.Token);
        Assert.False(cleanupTask.IsCompleted);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cleanupTask);
        Assert.Contains(
            audit.Read(0, 20),
            item => item.Action == "credential.cleanup.request" && item.Outcome == "requested");
        Assert.DoesNotContain(
            audit.Read(0, 20),
            item => item.Action == "credential.cleanup");

        tester.Release();
        var replacement = await replacementTask;

        Assert.True(replacement.Applied);
        Assert.True(secrets.ContainsOwned(registrations.GetById(registration.Id)!.SecretReference!.Value));
    }

    private static ServerRegistration Registration(ConnectionSecretReference reference) =>
        new(
            Guid.NewGuid(),
            "Finance",
            new SqlServerEndpoint("sql.example.internal", 1433),
            SqlAuthenticationMode.SqlLogin,
            reference,
            true,
            DateTimeOffset.UtcNow);

    private sealed class BlockingTester : IServerConnectionTester
    {
        private readonly TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => started.Task;
        public ServerRegistration? LastRegistration { get; private set; }

        public void Release() => release.TrySetResult(true);

        public async Task<ConnectionTestResult> TestAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default)
        {
            LastRegistration = registration;
            started.TrySetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return new ConnectionTestResult(ConnectionTestStatus.Succeeded, "Connection succeeded.", 1);
        }
    }

    private sealed class ControlledSecretStore : IConnectionSecretStore, IOwnedConnectionSecretStore, IRuntimeCredentialWriter
    {
        private readonly Dictionary<string, SqlLoginSecret> owned = new(StringComparer.Ordinal);

        public void AddOwned(ConnectionSecretReference reference, SqlLoginSecret secret) => owned[reference.Value] = secret;
        public bool ContainsOwned(ConnectionSecretReference reference) => owned.ContainsKey(reference.Value);
        public bool Owns(ConnectionSecretReference reference) => reference.Value.StartsWith("local:v1:", StringComparison.Ordinal);
        public IReadOnlyList<ConnectionSecretReference> GetOwnedReferences() =>
            owned.Keys.Select(value => new ConnectionSecretReference(value)).ToArray();

        public ValueTask<ConnectionSecretReference> StoreAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reference = new ConnectionSecretReference($"local:v1:{Guid.NewGuid():N}");
            owned[reference.Value] = new SqlLoginSecret(username, password);
            return ValueTask.FromResult(reference);
        }

        public ValueTask DeleteOwnedAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            owned.Remove(reference.Value);
            return ValueTask.CompletedTask;
        }

        public ValueTask<SqlLoginSecret?> ResolveAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(owned.TryGetValue(reference.Value, out var secret) ? secret : null);
        }
    }
}
