using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class CredentialLifecycleConcurrencyTests
{
    [Fact]
    public async Task Cleanup_WaitsForLocalReplacementCommit_AndPreservesCandidateSecret()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var oldReference = new ConnectionSecretReference("local:v1:old");
        var registration = new ServerRegistration(
            Guid.NewGuid(),
            "Finance",
            new SqlServerEndpoint("sql.example.internal", 1433),
            SqlAuthenticationMode.SqlLogin,
            oldReference,
            true,
            DateTimeOffset.UtcNow);
        registrations.Upsert(registration);

        var secrets = new CoordinatedSecretStore();
        secrets.AddOwned(oldReference, new SqlLoginSecret("old-user", "old-password"));
        var tester = new BlockingSucceededTester();
        var service = new CredentialLifecycleService(
            registrations,
            secrets,
            tester,
            new InMemoryAuditStore(TimeProvider.System),
            new CredentialPolicyOptions { AllowLocalOwnedCredentials = true });

        var replacementTask = service.ReplaceWithLocalCredentialAsync(
            registration.Id,
            "new-user",
            "new-password",
            "Admin");

        await tester.Entered;
        var candidateReference = tester.LastRegistration!.SecretReference!.Value;
        Assert.True(secrets.ContainsOwned(candidateReference));

        var cleanupTask = service.CleanupOrphanedOwnedSecretsAsync("Admin");

        Assert.False(cleanupTask.IsCompleted);
        Assert.True(secrets.ContainsOwned(candidateReference));
        Assert.Equal(0, secrets.DeleteCalls);

        tester.Succeed();

        var replacement = await replacementTask;
        var removed = await cleanupTask;
        var updated = registrations.GetById(registration.Id)!;

        Assert.True(replacement.Applied);
        Assert.Equal(0, removed);
        Assert.Equal(candidateReference, updated.SecretReference!.Value);
        Assert.True(secrets.ContainsOwned(candidateReference));
        Assert.False(secrets.ContainsOwned(oldReference));
        Assert.Equal(1, secrets.DeleteCalls);
    }

    private sealed class BlockingSucceededTester : IServerConnectionTester
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<ConnectionTestResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;
        public ServerRegistration? LastRegistration { get; private set; }

        public Task<ConnectionTestResult> TestAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default)
        {
            LastRegistration = registration;
            _entered.TrySetResult();
            return _completion.Task.WaitAsync(cancellationToken);
        }

        public void Succeed() => _completion.TrySetResult(new ConnectionTestResult(
            ConnectionTestStatus.Succeeded,
            "Connection succeeded.",
            1));
    }

    private sealed class CoordinatedSecretStore : IConnectionSecretStore, IOwnedConnectionSecretStore, IRuntimeCredentialWriter
    {
        private readonly Dictionary<string, SqlLoginSecret> _owned = new(StringComparer.Ordinal);

        public int DeleteCalls { get; private set; }

        public void AddOwned(ConnectionSecretReference reference, SqlLoginSecret secret) =>
            _owned[reference.Value] = secret;

        public bool ContainsOwned(ConnectionSecretReference reference) =>
            _owned.ContainsKey(reference.Value);

        public bool Owns(ConnectionSecretReference reference) =>
            reference.Value.StartsWith("local:v1:", StringComparison.Ordinal);

        public IReadOnlyList<ConnectionSecretReference> GetOwnedReferences() =>
            _owned.Keys.Select(value => new ConnectionSecretReference(value)).ToArray();

        public ValueTask<ConnectionSecretReference> StoreAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reference = new ConnectionSecretReference($"local:v1:{Guid.NewGuid():N}");
            _owned[reference.Value] = new SqlLoginSecret(username, password);
            return ValueTask.FromResult(reference);
        }

        public ValueTask DeleteOwnedAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeleteCalls++;
            _owned.Remove(reference.Value);
            return ValueTask.CompletedTask;
        }

        public ValueTask<SqlLoginSecret?> ResolveAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_owned.TryGetValue(reference.Value, out var secret)
                ? secret
                : null);
        }
    }
}
