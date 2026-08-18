using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class CredentialHaServicesTests
{
    [Fact]
    public void SharedKeyRing_RoundTripsAcrossDataProtectionProviderInstances()
    {
        var store = new MemoryDocumentStore();
        var kek = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        using var firstServices = DataProtectionServices(new SharedEncryptedDataProtectionXmlRepository(store, kek));
        var first = firstServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("canary-purpose");
        var protectedValue = first.Protect("canary-secret-value");

        using var secondServices = DataProtectionServices(new SharedEncryptedDataProtectionXmlRepository(store, kek));
        var second = secondServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("canary-purpose");

        Assert.Equal("canary-secret-value", second.Unprotect(protectedValue));
    }

    [Fact]
    public async Task SharedKeyRing_PersistsCiphertextOnly()
    {
        var store = new MemoryDocumentStore();
        var kekBytes = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var kek = Convert.ToBase64String(kekBytes);
        var repository = new SharedEncryptedDataProtectionXmlRepository(store, kek);
        var xml = XElement.Parse("<key id=\"canary-key-id\"><secret>canary-key-material</secret></key>");

        repository.StoreElement(xml, "key-canary-name");
        var document = await store.ReadAsync("monitor:dataprotection:keyring:v1");

        Assert.NotNull(document);
        Assert.DoesNotContain("canary-key-material", document!.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("<key", document.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(kek, document.PayloadJson, StringComparison.Ordinal);
        Assert.Equal("canary-key-material", repository.GetAllElements().Single().Element("secret")!.Value);
    }

    [Fact]
    public void SharedKeyRing_WrongKekFailsClosed()
    {
        var store = new MemoryDocumentStore();
        var first = new SharedEncryptedDataProtectionXmlRepository(store, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        first.StoreElement(XElement.Parse("<key id=\"a\"><secret>material</secret></key>"), "key-a");
        var second = new SharedEncryptedDataProtectionXmlRepository(store, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        var exception = Assert.Throws<InvalidOperationException>(() => second.GetAllElements());

        Assert.Contains("cannot be decrypted", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("material", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64")]
    public void SharedKeyRing_MissingOrInvalidKekFailsClosed(string key)
    {
        Assert.Throws<InvalidOperationException>(() => new SharedEncryptedDataProtectionXmlRepository(new MemoryDocumentStore(), key));
    }

    [Fact]
    public async Task LocalCredentialCreationPolicy_FailsClosedWithoutWriting()
    {
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "secrets.json");
        var store = new ProtectedFileConnectionSecretStore(
            file,
            new EphemeralDataProtectionProvider(),
            new ConfigurationBuilder().Build(),
            [],
            new CredentialPolicyOptions { AllowLocalOwnedCredentials = false });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.StoreAsync("canary-user", "canary-password"));

        Assert.Contains("disabled", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task LocalReplacement_MissingPolicyFailsClosedBeforeSecretWriteTestOrMutation()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var oldReference = new ConnectionSecretReference("local:v1:old");
        var registration = Registration(oldReference);
        registrations.Upsert(registration);
        var secrets = new FakeSecretStore();
        secrets.AddOwned(oldReference, new SqlLoginSecret("old-user", "old-password"));
        var tester = new FakeTester(ConnectionTestStatus.Succeeded);
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var service = new CredentialLifecycleService(registrations, secrets, tester, audit);

        var result = await service.ReplaceWithLocalCredentialAsync(registration.Id, "new-user", "new-password", "Admin");

        Assert.Equal(CredentialReplacementStatus.Failed, result.Status);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, secrets.StoreCalls);
        Assert.Equal(0, tester.Calls);
        Assert.Equal(oldReference, registrations.GetById(registration.Id)!.SecretReference);
        Assert.Equal("local-policy-disabled", audit.Read(0, 10).Single().Outcome);
    }

    [Fact]
    public async Task LocalReplacement_DisabledPolicyFailsClosedBeforeSecretWriteTestOrMutation()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var oldReference = new ConnectionSecretReference("local:v1:old");
        var registration = Registration(oldReference);
        registrations.Upsert(registration);
        var secrets = new FakeSecretStore();
        secrets.AddOwned(oldReference, new SqlLoginSecret("old-user", "old-password"));
        var tester = new FakeTester(ConnectionTestStatus.Succeeded);
        var service = new CredentialLifecycleService(
            registrations,
            secrets,
            tester,
            new InMemoryAuditStore(TimeProvider.System),
            new CredentialPolicyOptions { AllowLocalOwnedCredentials = false });

        var result = await service.ReplaceWithLocalCredentialAsync(registration.Id, "new-user", "new-password", "Admin");

        Assert.Equal(CredentialReplacementStatus.Failed, result.Status);
        Assert.Equal(0, secrets.StoreCalls);
        Assert.Equal(0, tester.Calls);
        Assert.Equal(oldReference, registrations.GetById(registration.Id)!.SecretReference);
    }

    [Fact]
    public async Task ExternalReplacement_TestsCandidateBeforeCommit_CleansOldOwnedSecret_AndAuditsMetadataOnly()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var oldReference = new ConnectionSecretReference("local:v1:old");
        var nextReference = new ConnectionSecretReference("env:FINANCE_V2");
        var registration = Registration(oldReference);
        registrations.Upsert(registration);
        var secrets = new FakeSecretStore();
        secrets.AddOwned(oldReference, new SqlLoginSecret("old-user", "old-password"));
        secrets.AddExternal(nextReference, new SqlLoginSecret("new-user", "new-password"));
        var tester = new FakeTester(ConnectionTestStatus.Succeeded);
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var service = new CredentialLifecycleService(registrations, secrets, tester, audit);

        var result = await service.ReplaceWithExternalReferenceAsync(registration.Id, nextReference.Value, "Admin", default);

        Assert.True(result.Applied);
        Assert.Equal(nextReference.Value, registrations.GetById(registration.Id)!.SecretReference!.Value.Value);
        Assert.Equal(nextReference.Value, tester.LastRegistration!.SecretReference!.Value.Value);
        Assert.False(secrets.ContainsOwned(oldReference));
        var item = audit.Read(0, 10).Single();
        Assert.Equal("credential.reference.replace", item.Action);
        Assert.Equal(registration.Id.ToString("D"), item.Target);
        Assert.Equal("applied", item.Outcome);
        var auditText = string.Join('|', item.Actor, item.Action, item.Target, item.Outcome);
        Assert.DoesNotContain(nextReference.Value, auditText, StringComparison.Ordinal);
        Assert.DoesNotContain("new-password", auditText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedReplacement_DoesNotMutateRegistrationOrDeleteOwnedSecret()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var oldReference = new ConnectionSecretReference("local:v1:old");
        var nextReference = new ConnectionSecretReference("env:BAD_V2");
        var registration = Registration(oldReference);
        registrations.Upsert(registration);
        var secrets = new FakeSecretStore();
        secrets.AddOwned(oldReference, new SqlLoginSecret("old-user", "old-password"));
        secrets.AddExternal(nextReference, new SqlLoginSecret("bad-user", "bad-password"));
        var tester = new FakeTester(ConnectionTestStatus.AuthenticationFailed);
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var service = new CredentialLifecycleService(registrations, secrets, tester, audit);

        var result = await service.ReplaceWithExternalReferenceAsync(registration.Id, nextReference.Value, "Admin", default);

        Assert.Equal(CredentialReplacementStatus.ConnectionRejected, result.Status);
        Assert.Equal(oldReference.Value, registrations.GetById(registration.Id)!.SecretReference!.Value.Value);
        Assert.True(secrets.ContainsOwned(oldReference));
        Assert.Contains("test-AuthenticationFailed", audit.Read(0, 10).Single().Outcome, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalReplacement_TestsThenCommitsSameRegistration_AndDeletesOldOwnedSecret()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var oldReference = new ConnectionSecretReference("local:v1:old");
        var registration = Registration(oldReference);
        registrations.Upsert(registration);
        var secrets = new FakeSecretStore();
        secrets.AddOwned(oldReference, new SqlLoginSecret("old-user", "old-password"));
        var tester = new FakeTester(ConnectionTestStatus.Succeeded);
        var service = new CredentialLifecycleService(
            registrations,
            secrets,
            tester,
            new InMemoryAuditStore(TimeProvider.System),
            new CredentialPolicyOptions { AllowLocalOwnedCredentials = true });

        var result = await service.ReplaceWithLocalCredentialAsync(registration.Id, "new-user", "new-password", "Admin");
        var updated = registrations.GetById(registration.Id)!;

        Assert.True(result.Applied);
        Assert.Equal(1, secrets.StoreCalls);
        Assert.Equal(registration.Id, updated.Id);
        Assert.Equal(registration.Endpoint, updated.Endpoint);
        Assert.NotEqual(oldReference.Value, updated.SecretReference!.Value.Value);
        Assert.Equal(updated.SecretReference, tester.LastRegistration!.SecretReference);
        Assert.False(secrets.ContainsOwned(oldReference));
        Assert.True(secrets.ContainsOwned(updated.SecretReference.Value));
    }

    [Fact]
    public async Task FailedLocalReplacement_CompensatesCandidateAndKeepsExistingReference()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var oldReference = new ConnectionSecretReference("local:v1:old");
        var registration = Registration(oldReference);
        registrations.Upsert(registration);
        var secrets = new FakeSecretStore();
        secrets.AddOwned(oldReference, new SqlLoginSecret("old-user", "old-password"));
        var service = new CredentialLifecycleService(
            registrations,
            secrets,
            new FakeTester(ConnectionTestStatus.AuthenticationFailed),
            new InMemoryAuditStore(TimeProvider.System),
            new CredentialPolicyOptions { AllowLocalOwnedCredentials = true });

        var result = await service.ReplaceWithLocalCredentialAsync(registration.Id, "bad-user", "bad-password", "Admin");

        Assert.Equal(CredentialReplacementStatus.ConnectionRejected, result.Status);
        Assert.Equal(oldReference, registrations.GetById(registration.Id)!.SecretReference);
        Assert.Single(secrets.GetOwnedReferences());
        Assert.True(secrets.ContainsOwned(oldReference));
    }

    [Fact]
    public async Task UnavailableReplacementReference_DoesNotInvokeTestOrMutate()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var oldReference = new ConnectionSecretReference("local:v1:old");
        var registration = Registration(oldReference);
        registrations.Upsert(registration);
        var tester = new FakeTester(ConnectionTestStatus.Succeeded);
        var service = new CredentialLifecycleService(registrations, new FakeSecretStore(), tester, new InMemoryAuditStore(TimeProvider.System));

        var result = await service.ReplaceWithExternalReferenceAsync(registration.Id, "env:MISSING", "Admin", default);

        Assert.Equal(CredentialReplacementStatus.SecretUnavailable, result.Status);
        Assert.Equal(0, tester.Calls);
        Assert.Equal(oldReference.Value, registrations.GetById(registration.Id)!.SecretReference!.Value.Value);
    }

    [Fact]
    public async Task Cleanup_RemovesOnlyOrphanedOwnedReferences()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var referenced = new ConnectionSecretReference("local:v1:referenced");
        var orphan = new ConnectionSecretReference("local:v1:orphan");
        registrations.Upsert(Registration(referenced));
        var secrets = new FakeSecretStore();
        secrets.AddOwned(referenced, new SqlLoginSecret("a", "b"));
        secrets.AddOwned(orphan, new SqlLoginSecret("c", "d"));
        var service = new CredentialLifecycleService(registrations, secrets, new FakeTester(ConnectionTestStatus.Succeeded), new InMemoryAuditStore(TimeProvider.System));

        var removed = await service.CleanupOrphanedOwnedSecretsAsync("Admin");

        Assert.Equal(1, removed);
        Assert.True(secrets.ContainsOwned(referenced));
        Assert.False(secrets.ContainsOwned(orphan));
    }

    [Fact]
    public void CredentialReadiness_IsAggregateOnlyAndBlocksLocalOwnedReferences()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        registrations.Upsert(Registration(new ConnectionSecretReference("local:v1:one")));
        registrations.Upsert(new ServerRegistration(
            Guid.NewGuid(),
            "External",
            new SqlServerEndpoint("sql2.example.internal", 1433),
            SqlAuthenticationMode.SqlLogin,
            new ConnectionSecretReference("env:EXTERNAL"),
            true,
            DateTimeOffset.UtcNow));
        var service = new CredentialReadinessService(
            registrations,
            new DataProtectionKeyStoreOptions { Mode = DataProtectionKeyStoreMode.SharedState },
            new CredentialPolicyOptions { AllowLocalOwnedCredentials = false });

        var readiness = service.Get();
        var text = readiness.Message + readiness.Status;

        Assert.Equal(2, readiness.SqlLoginRegistrations);
        Assert.Equal(1, readiness.LocalOwnedRegistrations);
        Assert.Equal(1, readiness.ExternalRegistrations);
        Assert.False(readiness.MultiNodeCredentialReady);
        Assert.DoesNotContain("env:EXTERNAL", text, StringComparison.Ordinal);
        Assert.DoesNotContain("local:v1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CredentialReadiness_ReadyWhenSharedRing_LocalCreationDisabled_AndAllReferencesExternal()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        registrations.Upsert(new ServerRegistration(
            Guid.NewGuid(),
            "External",
            new SqlServerEndpoint("sql2.example.internal", 1433),
            SqlAuthenticationMode.SqlLogin,
            new ConnectionSecretReference("env:EXTERNAL"),
            true,
            DateTimeOffset.UtcNow));
        var service = new CredentialReadinessService(
            registrations,
            new DataProtectionKeyStoreOptions { Mode = DataProtectionKeyStoreMode.SharedState },
            new CredentialPolicyOptions { AllowLocalOwnedCredentials = false });

        Assert.True(service.Get().MultiNodeCredentialReady);
    }

    private static ServiceProvider DataProtectionServices(Microsoft.AspNetCore.DataProtection.Repositories.IXmlRepository repository)
    {
        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName("Monitor.SqlSecrets.v1")
            .AddKeyManagementOptions(options => options.XmlRepository = repository);
        return services.BuildServiceProvider();
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

    private sealed class FakeTester(ConnectionTestStatus status) : IServerConnectionTester
    {
        public int Calls { get; private set; }
        public ServerRegistration? LastRegistration { get; private set; }
        public Task<ConnectionTestResult> TestAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRegistration = registration;
            var succeeded = status == ConnectionTestStatus.Succeeded;
            return Task.FromResult(new ConnectionTestResult(status, succeeded ? "Connection succeeded." : "Authentication failed.", 1));
        }
    }

    private sealed class FakeSecretStore : IConnectionSecretStore, IOwnedConnectionSecretStore, IRuntimeCredentialWriter
    {
        private readonly Dictionary<string, SqlLoginSecret> _owned = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SqlLoginSecret> _external = new(StringComparer.Ordinal);

        public int StoreCalls { get; private set; }
        public void AddOwned(ConnectionSecretReference reference, SqlLoginSecret secret) => _owned[reference.Value] = secret;
        public void AddExternal(ConnectionSecretReference reference, SqlLoginSecret secret) => _external[reference.Value] = secret;
        public bool ContainsOwned(ConnectionSecretReference reference) => _owned.ContainsKey(reference.Value);
        public bool Owns(ConnectionSecretReference reference) => reference.Value.StartsWith("local:v1:", StringComparison.Ordinal);
        public IReadOnlyList<ConnectionSecretReference> GetOwnedReferences() => _owned.Keys.Select(value => new ConnectionSecretReference(value)).ToArray();
        public ValueTask<ConnectionSecretReference> StoreAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            StoreCalls++;
            var reference = new ConnectionSecretReference($"local:v1:{Guid.NewGuid():N}");
            _owned[reference.Value] = new SqlLoginSecret(username, password);
            return ValueTask.FromResult(reference);
        }
        public ValueTask DeleteOwnedAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default)
        {
            _owned.Remove(reference.Value);
            return ValueTask.CompletedTask;
        }
        public ValueTask<SqlLoginSecret?> ResolveAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default)
        {
            if (_owned.TryGetValue(reference.Value, out var owned)) return ValueTask.FromResult<SqlLoginSecret?>(owned);
            if (_external.TryGetValue(reference.Value, out var external)) return ValueTask.FromResult<SqlLoginSecret?>(external);
            return ValueTask.FromResult<SqlLoginSecret?>(null);
        }
    }

    private sealed class MemoryDocumentStore : ISharedStateDocumentStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, SharedStateDocument> _documents = new(StringComparer.Ordinal);

        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_documents.TryGetValue(key, out var document) ? document : null);
            }
        }

        public Task<SharedStateWriteResult> CompareExchangeAsync(string key, long expectedVersion, string payloadJson, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (!_documents.TryGetValue(key, out var current))
                {
                    if (expectedVersion != 0) return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, null));
                    var created = new SharedStateDocument(key, 1, payloadJson, DateTimeOffset.UtcNow);
                    _documents[key] = created;
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, created));
                }

                if (current.Version != expectedVersion)
                {
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, current));
                }

                var updated = current with { Version = current.Version + 1, PayloadJson = payloadJson, UpdatedAtUtc = DateTimeOffset.UtcNow };
                _documents[key] = updated;
                return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, updated));
            }
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"monitor-credential-ha-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }
        public string Path { get; }
        public void Dispose() => Directory.Delete(Path, true);
    }
}
