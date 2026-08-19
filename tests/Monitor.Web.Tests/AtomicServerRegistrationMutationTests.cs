using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class AtomicServerRegistrationMutationTests
{
    [Fact]
    public async Task SharedSecretMutation_RetriesAgainstLatestState_AndPreservesConcurrentDisable()
    {
        var inner = new MemoryDocumentStore();
        var blocking = new BlockingCompareExchangeStore(inner);
        var nodeA = new AtomicSharedServerRegistrationRepository(blocking);
        var nodeB = new AtomicSharedServerRegistrationRepository(inner);
        var oldReference = new ConnectionSecretReference("env:OLD");
        var nextReference = new ConnectionSecretReference("env:NEW");
        var registration = Registration(oldReference);
        nodeB.Upsert(registration);

        var replaceTask = Task.Run(() =>
            nodeA.TryReplaceSecretReference(registration.Id, oldReference, nextReference));
        await blocking.FirstWriteStarted;

        var disable = nodeB.SetEnabled(registration.Id, false);
        Assert.True(disable.Applied);
        blocking.Release();

        var replace = await replaceTask;
        var final = nodeB.GetById(registration.Id)!;

        Assert.True(replace.Applied);
        Assert.False(final.IsEnabled);
        Assert.Equal(nextReference, final.SecretReference);
        Assert.True(blocking.ConflictObserved);
    }

    [Fact]
    public async Task SharedSecretMutation_ExpectedReferenceChangedOnOtherNode_FailsClosed()
    {
        var inner = new MemoryDocumentStore();
        var blocking = new BlockingCompareExchangeStore(inner);
        var nodeA = new AtomicSharedServerRegistrationRepository(blocking);
        var nodeB = new AtomicSharedServerRegistrationRepository(inner);
        var oldReference = new ConnectionSecretReference("env:OLD");
        var nodeAReference = new ConnectionSecretReference("env:NODE_A");
        var nodeBReference = new ConnectionSecretReference("env:NODE_B");
        var registration = Registration(oldReference);
        nodeB.Upsert(registration);

        var nodeATask = Task.Run(() =>
            nodeA.TryReplaceSecretReference(registration.Id, oldReference, nodeAReference));
        await blocking.FirstWriteStarted;

        var nodeBResult = nodeB.TryReplaceSecretReference(registration.Id, oldReference, nodeBReference);
        Assert.True(nodeBResult.Applied);
        blocking.Release();

        var nodeAResult = await nodeATask;
        var final = nodeB.GetById(registration.Id)!;

        Assert.Equal(ServerRegistrationFieldMutationStatus.Conflict, nodeAResult.Status);
        Assert.Equal(nodeBReference, final.SecretReference);
        Assert.True(blocking.ConflictObserved);
    }

    [Fact]
    public async Task AtomicCredentialLifecycle_ConcurrentReferenceChange_ReturnsFailureWithoutOverwrite()
    {
        var inner = new MemoryDocumentStore();
        var blocking = new BlockingCompareExchangeStore(inner);
        var nodeARepository = new AtomicSharedServerRegistrationRepository(blocking);
        var nodeBRepository = new AtomicSharedServerRegistrationRepository(inner);
        var oldReference = new ConnectionSecretReference("env:OLD");
        var candidateReference = new ConnectionSecretReference("env:CANDIDATE");
        var competingReference = new ConnectionSecretReference("env:COMPETING");
        var registration = Registration(oldReference);
        nodeBRepository.Upsert(registration);
        var secrets = new ExternalSecretStore(candidateReference);
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var service = new AtomicCredentialLifecycleService(
            nodeARepository,
            secrets,
            new SuccessfulTester(),
            audit,
            new CredentialPolicyOptions());

        var replacementTask = Task.Run(async () =>
            await service.ReplaceWithExternalReferenceAsync(
                registration.Id,
                candidateReference.Value,
                "Admin"));
        await blocking.FirstWriteStarted;

        Assert.True(nodeBRepository.TryReplaceSecretReference(
            registration.Id,
            oldReference,
            competingReference).Applied);
        blocking.Release();

        var result = await replacementTask;
        var final = nodeBRepository.GetById(registration.Id)!;

        Assert.Equal(CredentialReplacementStatus.Failed, result.Status);
        Assert.Contains("concurrently", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(competingReference, final.SecretReference);
        Assert.Contains(audit.Read(0, 10), item => item.Outcome == "conflict");
    }

    [Fact]
    public void InMemoryFieldMutations_PreserveIndependentLatestFields()
    {
        var repository = new InMemoryServerRegistrationRepository();
        var oldReference = new ConnectionSecretReference("env:OLD");
        var nextReference = new ConnectionSecretReference("env:NEW");
        var registration = Registration(oldReference);
        repository.Upsert(registration);

        Assert.True(repository.SetEnabled(registration.Id, false).Applied);
        Assert.True(repository.TryReplaceSecretReference(registration.Id, oldReference, nextReference).Applied);

        var final = repository.GetById(registration.Id)!;
        Assert.False(final.IsEnabled);
        Assert.Equal(nextReference, final.SecretReference);
        Assert.Equal(
            ServerRegistrationFieldMutationStatus.Conflict,
            repository.TryReplaceSecretReference(registration.Id, oldReference, new ConnectionSecretReference("env:STALE")).Status);
    }

    [Fact]
    public void FileFieldMutations_PreserveIndependentLatestFields()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"monitor-registration-atomic-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "registrations.json");
            var repository = new FileServerRegistrationRepository(path);
            var oldReference = new ConnectionSecretReference("env:OLD");
            var nextReference = new ConnectionSecretReference("env:NEW");
            var registration = Registration(oldReference);
            repository.Upsert(registration);

            Assert.True(repository.SetEnabled(registration.Id, false).Applied);
            Assert.True(repository.TryReplaceSecretReference(registration.Id, oldReference, nextReference).Applied);

            var reopened = new FileServerRegistrationRepository(path);
            var final = reopened.GetById(registration.Id)!;
            Assert.False(final.IsEnabled);
            Assert.Equal(nextReference, final.SecretReference);
            Assert.Equal(
                ServerRegistrationFieldMutationStatus.Conflict,
                reopened.TryReplaceSecretReference(registration.Id, oldReference, new ConnectionSecretReference("env:STALE")).Status);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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

    private sealed class SuccessfulTester : IServerConnectionTester
    {
        public Task<ConnectionTestResult> TestAsync(
            ServerRegistration registration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectionTestResult(ConnectionTestStatus.Succeeded, "Connection succeeded.", 1));
    }

    private sealed class ExternalSecretStore(ConnectionSecretReference availableReference) : IConnectionSecretStore
    {
        public ValueTask<SqlLoginSecret?> ResolveAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SqlLoginSecret?>(
                reference.Value == availableReference.Value
                    ? new SqlLoginSecret("external-user", "external-password")
                    : null);
    }

    private sealed class MemoryDocumentStore : ISharedStateDocumentStore
    {
        private readonly object gate = new();
        private readonly Dictionary<string, SharedStateDocument> documents = new(StringComparer.Ordinal);

        public Task<SharedStateDocument?> ReadAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                return Task.FromResult(documents.TryGetValue(key, out var document) ? document : null);
            }
        }

        public Task<SharedStateWriteResult> CompareExchangeAsync(
            string key,
            long expectedVersion,
            string payloadJson,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                if (!documents.TryGetValue(key, out var current))
                {
                    if (expectedVersion != 0)
                    {
                        return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, null));
                    }

                    var created = new SharedStateDocument(key, 1, payloadJson, DateTimeOffset.UtcNow);
                    documents[key] = created;
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, created));
                }

                if (current.Version != expectedVersion)
                {
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, current));
                }

                var updated = current with
                {
                    Version = current.Version + 1,
                    PayloadJson = payloadJson,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
                documents[key] = updated;
                return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, updated));
            }
        }
    }

    private sealed class BlockingCompareExchangeStore(MemoryDocumentStore inner) : ISharedStateDocumentStore
    {
        private readonly TaskCompletionSource<bool> firstWriteStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int remainingBlocks = 1;

        public Task FirstWriteStarted => firstWriteStarted.Task;
        public bool ConflictObserved { get; private set; }

        public void Release() => release.TrySetResult(true);

        public Task<SharedStateDocument?> ReadAsync(
            string key,
            CancellationToken cancellationToken = default) =>
            inner.ReadAsync(key, cancellationToken);

        public async Task<SharedStateWriteResult> CompareExchangeAsync(
            string key,
            long expectedVersion,
            string payloadJson,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref remainingBlocks, 0) == 1)
            {
                firstWriteStarted.TrySetResult(true);
                await release.Task.WaitAsync(cancellationToken);
            }

            var result = await inner.CompareExchangeAsync(key, expectedVersion, payloadJson, cancellationToken);
            if (result.Status == SharedStateWriteStatus.Conflict)
            {
                ConflictObserved = true;
            }

            return result;
        }
    }
}
