using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class WriteAheadAuditMutationTests
{
    [Fact]
    public void IncidentAssignment_AuditFailureLeavesOwnerUnchanged()
    {
        var metadata = new InMemoryOperatorMetadataStore(TimeProvider.System);
        const string incidentId = "11111111111111111111111111111111:RULE-001";
        metadata.AssignIncident(incidentId, "original-owner");
        var service = new IncidentCollaborationService(metadata, new ThrowingAuditStore(), TimeProvider.System);

        var exception = Assert.Throws<IOException>(() =>
            service.Assign(incidentId, "next-owner", "operator"));

        Assert.Contains("audit unavailable", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("original-owner", metadata.GetIncident(incidentId).Assignee);
    }

    [Fact]
    public void IncidentNote_AuditFailureLeavesNotesUnchanged()
    {
        var metadata = new InMemoryOperatorMetadataStore(TimeProvider.System);
        const string incidentId = "11111111111111111111111111111111:RULE-002";
        var service = new IncidentCollaborationService(metadata, new ThrowingAuditStore(), TimeProvider.System);

        var exception = Assert.Throws<IOException>(() =>
            service.TryAddNote(incidentId, "operator", "Investigating failover evidence.", "request-12345678"));

        Assert.Contains("audit unavailable", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(metadata.GetIncident(incidentId).Notes);
    }

    [Fact]
    public void IncidentNote_DurableIntentWithoutAppliedReceipt_FailsClosedOnRetry()
    {
        var metadata = new InMemoryOperatorMetadataStore(TimeProvider.System);
        const string incidentId = "11111111111111111111111111111111:RULE-003";
        const string requestKey = "request-87654321";
        var audit = new RecordThenFailOnceAuditStore();
        var service = new IncidentCollaborationService(metadata, audit, TimeProvider.System);

        var firstFailure = Assert.Throws<IOException>(() =>
            service.TryAddNote(incidentId, "operator", "Ambiguous after durable intent.", requestKey));

        Assert.Contains("audit unavailable", firstFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(metadata.GetIncident(incidentId).Notes);
        Assert.Single(audit.Read(0, 20), item =>
            item.Action == "incident.note.write.request" && item.Outcome == "requested");
        Assert.DoesNotContain(audit.Read(0, 20), item =>
            item.Action == "incident.note.request" && item.Outcome == "applied");

        var retryFailure = Assert.Throws<IncidentNoteRequestAmbiguousException>(() =>
            service.TryAddNote(incidentId, "operator", "Ambiguous after durable intent.", requestKey));

        Assert.Contains("unresolved outcome", retryFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(metadata.GetIncident(incidentId).Notes);
        Assert.Single(audit.Read(0, 20), item =>
            item.Action == "incident.note.write.request" && item.Outcome == "requested");
    }

    [Fact]
    public void IncidentNote_AppliedReceipt_RemainsPositiveDedupeEvidence()
    {
        var metadata = new InMemoryOperatorMetadataStore(TimeProvider.System);
        var audit = new InMemoryAuditStore(TimeProvider.System);
        var service = new IncidentCollaborationService(metadata, audit, TimeProvider.System);
        const string incidentId = "11111111111111111111111111111111:RULE-004";
        const string requestKey = "request-applied-1234";

        Assert.True(service.TryAddNote(incidentId, "operator", "Applied once.", requestKey));
        Assert.False(service.TryAddNote(incidentId, "operator", "Applied once.", requestKey));

        Assert.Single(metadata.GetIncident(incidentId).Notes);
        Assert.Single(audit.Read(0, 20), item =>
            item.Action == "incident.note.write.request" && item.Outcome == "requested");
        Assert.Single(audit.Read(0, 20), item =>
            item.Action == "incident.note.request" && item.Outcome == "applied");
    }

    [Fact]
    public async Task CredentialLocalReplacement_AuditFailurePreventsSecretWriteTestAndRegistrationMutation()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var originalReference = new ConnectionSecretReference("local:v1:original");
        var registration = Registration(originalReference);
        registrations.Upsert(registration);
        var secrets = new TrackingSecretStore();
        secrets.AddOwned(originalReference, new SqlLoginSecret("old-user", "old-password"));
        var tester = new TrackingTester();
        var inner = new CredentialLifecycleService(
            registrations,
            secrets,
            tester,
            new InMemoryAuditStore(TimeProvider.System),
            new CredentialPolicyOptions { AllowLocalOwnedCredentials = true });
        ICredentialLifecycleService service = new WriteAheadAuditedCredentialLifecycleService(inner, new ThrowingAuditStore());

        var exception = await Assert.ThrowsAsync<IOException>(async () =>
            await service.ReplaceWithLocalCredentialAsync(registration.Id, "new-user", "new-password", "operator"));

        Assert.Contains("audit unavailable", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, secrets.StoreCalls);
        Assert.Equal(0, tester.Calls);
        Assert.Equal(originalReference, registrations.GetById(registration.Id)!.SecretReference);
        Assert.True(secrets.ContainsOwned(originalReference));
    }

    [Fact]
    public async Task CredentialCleanup_AuditFailurePreventsOwnedSecretDeletion()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var orphan = new ConnectionSecretReference("local:v1:orphan");
        var secrets = new TrackingSecretStore();
        secrets.AddOwned(orphan, new SqlLoginSecret("orphan-user", "orphan-password"));
        var inner = new CredentialLifecycleService(
            registrations,
            secrets,
            new TrackingTester(),
            new InMemoryAuditStore(TimeProvider.System),
            new CredentialPolicyOptions { AllowLocalOwnedCredentials = true });
        ICredentialLifecycleService service = new WriteAheadAuditedCredentialLifecycleService(inner, new ThrowingAuditStore());

        var exception = await Assert.ThrowsAsync<IOException>(async () =>
            await service.CleanupOrphanedOwnedSecretsAsync("operator"));

        Assert.Contains("audit unavailable", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(secrets.ContainsOwned(orphan));
        Assert.Equal(0, secrets.DeleteCalls);
    }

    private static ServerRegistration Registration(ConnectionSecretReference reference) => new(
        Guid.NewGuid(),
        "Finance SQL",
        new SqlServerEndpoint("sql.internal", 1433),
        SqlAuthenticationMode.SqlLogin,
        reference,
        true,
        DateTimeOffset.UtcNow);

    private sealed class ThrowingAuditStore : IAuditStore
    {
        public void Append(string actor, string action, string target, string outcome) =>
            throw new IOException("audit unavailable");

        public IReadOnlyList<AuditEvent> Read(int offset, int limit) => [];
    }

    private sealed class RecordThenFailOnceAuditStore : IAuditStore
    {
        private readonly InMemoryAuditStore _inner = new(TimeProvider.System);
        private bool _failed;

        public void Append(string actor, string action, string target, string outcome)
        {
            _inner.Append(actor, action, target, outcome);
            if (!_failed)
            {
                _failed = true;
                throw new IOException("audit unavailable after durable intent write");
            }
        }

        public IReadOnlyList<AuditEvent> Read(int offset, int limit) => _inner.Read(offset, limit);
    }

    private sealed class TrackingTester : IServerConnectionTester
    {
        public int Calls { get; private set; }

        public Task<ConnectionTestResult> TestAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new ConnectionTestResult(ConnectionTestStatus.Succeeded, "Connection succeeded.", 1));
        }
    }

    private sealed class TrackingSecretStore : IConnectionSecretStore, IOwnedConnectionSecretStore, IRuntimeCredentialWriter
    {
        private readonly Dictionary<string, SqlLoginSecret> _owned = new(StringComparer.Ordinal);

        public int StoreCalls { get; private set; }
        public int DeleteCalls { get; private set; }

        public void AddOwned(ConnectionSecretReference reference, SqlLoginSecret secret) =>
            _owned[reference.Value] = secret;

        public bool ContainsOwned(ConnectionSecretReference reference) =>
            _owned.ContainsKey(reference.Value);

        public bool Owns(ConnectionSecretReference reference) =>
            reference.Value.StartsWith("local:v1:", StringComparison.Ordinal);

        public IReadOnlyList<ConnectionSecretReference> GetOwnedReferences() =>
            _owned.Keys.Select(value => new ConnectionSecretReference(value)).ToArray();

        public ValueTask<ConnectionSecretReference> StoreAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            StoreCalls++;
            var reference = new ConnectionSecretReference($"local:v1:{Guid.NewGuid():N}");
            _owned[reference.Value] = new SqlLoginSecret(username, password);
            return ValueTask.FromResult(reference);
        }

        public ValueTask DeleteOwnedAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            _owned.Remove(reference.Value);
            return ValueTask.CompletedTask;
        }

        public ValueTask<SqlLoginSecret?> ResolveAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_owned.TryGetValue(reference.Value, out var secret) ? secret : null);
    }
}
