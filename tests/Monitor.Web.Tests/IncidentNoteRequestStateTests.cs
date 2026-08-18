using System.Security.Cryptography;
using System.Text;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class IncidentNoteRequestStateTests
{
    [Fact]
    public void AppliedRequest_RemainsDuplicateAfterAuditEvictionAndFileRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"monitor-note-state-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 18, 19, 0, 0, TimeSpan.Zero));
            var metadata = new InMemoryOperatorMetadataStore(time);
            var rawAudit = new FileAuditStore(Path.Combine(directory, "audit.json"), time);
            var state = new FileIncidentNoteRequestStateStore(directory);
            IAuditStore audit = new CoordinatedIncidentNoteAuditStore(
                rawAudit,
                new UnusedSharedStateStore(),
                time,
                useSharedOperationalState: false,
                state);
            var service = new IncidentCollaborationService(metadata, audit, time);
            const string incidentId = "11111111111111111111111111111111:RULE-EVICTION";
            const string requestKey = "request-eviction-12345678";

            Assert.True(service.TryAddNote(incidentId, "operator", "Durable investigation note.", requestKey));
            Assert.Single(metadata.GetIncident(incidentId).Notes);

            for (var index = 0; index < 1001; index++)
                rawAudit.Append("operator", "test.audit.noise", index.ToString(), "applied");

            Assert.DoesNotContain(ReadAll(rawAudit), item =>
                item.Action is "incident.note.write.commit" or "incident.note.request");

            var restartedState = new FileIncidentNoteRequestStateStore(directory);
            IAuditStore restartedAudit = new CoordinatedIncidentNoteAuditStore(
                new FileAuditStore(Path.Combine(directory, "audit.json"), time),
                new UnusedSharedStateStore(),
                time,
                useSharedOperationalState: false,
                restartedState);
            var restartedService = new IncidentCollaborationService(metadata, restartedAudit, time);

            Assert.False(restartedService.TryAddNote(incidentId, "operator", "Durable investigation note.", requestKey));
            Assert.Single(metadata.GetIncident(incidentId).Notes);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DurableAppliedState_WinsWhenFinalAuditEvidenceFailsAndArmedReceiptRemains()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 18, 20, 0, 0, TimeSpan.Zero));
        var metadata = new InMemoryOperatorMetadataStore(time);
        var rawAudit = new FailFinalAppliedAuditStore(time);
        var state = new InMemoryIncidentNoteRequestStateStore();
        IAuditStore audit = new CoordinatedIncidentNoteAuditStore(
            rawAudit,
            new UnusedSharedStateStore(),
            time,
            useSharedOperationalState: false,
            state);
        var service = new IncidentCollaborationService(metadata, audit, time);
        const string incidentId = "11111111111111111111111111111111:RULE-FINAL-AUDIT";
        const string requestKey = "request-final-audit-12345678";

        var firstFailure = Assert.Throws<IOException>(() =>
            service.TryAddNote(incidentId, "operator", "Applied before final audit evidence.", requestKey));

        Assert.Contains("final note receipt unavailable", firstFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(metadata.GetIncident(incidentId).Notes);
        Assert.Contains(ReadAll(rawAudit), item =>
            item.Action == "incident.note.write.commit" && item.Outcome == "armed");
        Assert.DoesNotContain(ReadAll(rawAudit), item =>
            item.Action == "incident.note.request" && item.Outcome == "applied");

        Assert.False(service.TryAddNote(incidentId, "operator", "Applied before final audit evidence.", requestKey));
        Assert.Single(metadata.GetIncident(incidentId).Notes);
    }

    [Fact]
    public void ArmedRequest_RemainsAmbiguousAfterAuditEvictionAndFileRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"monitor-note-armed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 18, 19, 0, 0, TimeSpan.Zero));
            var rawAudit = new FileAuditStore(Path.Combine(directory, "audit.json"), time);
            var state = new FileIncidentNoteRequestStateStore(directory);
            var audit = new CoordinatedIncidentNoteAuditStore(
                rawAudit,
                new UnusedSharedStateStore(),
                time,
                useSharedOperationalState: false,
                state);
            const string receiptTarget = "incident-armed:0123456789ABCDEF01234567";

            Assert.Equal(IncidentNoteClaimResult.Claimed, audit.TryClaimIncidentNote("operator", receiptTarget));

            for (var index = 0; index < 1001; index++)
                rawAudit.Append("operator", "test.audit.noise", index.ToString(), "applied");

            Assert.DoesNotContain(ReadAll(rawAudit), item =>
                item.Action == "incident.note.write.commit" && item.Target == receiptTarget);

            var restarted = new CoordinatedIncidentNoteAuditStore(
                new FileAuditStore(Path.Combine(directory, "audit.json"), time),
                new UnusedSharedStateStore(),
                time,
                useSharedOperationalState: false,
                new FileIncidentNoteRequestStateStore(directory));

            Assert.Equal(IncidentNoteClaimResult.Ambiguous, restarted.TryClaimIncidentNote("operator", receiptTarget));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void IndependentFileStores_CannotReclaimPeerArmedRequest()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"monitor-note-peer-claim-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var targets = FindTargetsInSameShard(2);
            var first = new FileIncidentNoteRequestStateStore(directory);
            var second = new FileIncidentNoteRequestStateStore(directory);

            Assert.Equal(IncidentNoteClaimResult.Claimed, first.TryClaim(targets[0]));
            Assert.Equal(IncidentNoteClaimResult.Claimed, second.TryClaim(targets[1]));
            Assert.Equal(IncidentNoteClaimResult.Ambiguous, first.TryClaim(targets[1]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void IndependentFileStores_DoNotLosePeerShardEntries()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"monitor-note-peer-entry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var targets = FindTargetsInSameShard(3);
            var first = new FileIncidentNoteRequestStateStore(directory);
            var second = new FileIncidentNoteRequestStateStore(directory);

            Assert.Equal(IncidentNoteClaimResult.Claimed, first.TryClaim(targets[0]));
            Assert.Equal(IncidentNoteClaimResult.Claimed, second.TryClaim(targets[1]));
            Assert.Equal(IncidentNoteClaimResult.Claimed, first.TryClaim(targets[2]));

            var restarted = new FileIncidentNoteRequestStateStore(directory);
            Assert.Equal(IncidentNoteClaimResult.Ambiguous, restarted.TryClaim(targets[0]));
            Assert.Equal(IncidentNoteClaimResult.Ambiguous, restarted.TryClaim(targets[1]));
            Assert.Equal(IncidentNoteClaimResult.Ambiguous, restarted.TryClaim(targets[2]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void IndependentFileStores_CannotDowngradeAppliedState()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"monitor-note-peer-applied-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var targets = FindTargetsInSameShard(3);
            var first = new FileIncidentNoteRequestStateStore(directory);
            var second = new FileIncidentNoteRequestStateStore(directory);

            Assert.Equal(IncidentNoteClaimResult.Claimed, first.TryClaim(targets[0]));
            Assert.Equal(IncidentNoteClaimResult.Claimed, second.TryClaim(targets[1]));
            second.MarkApplied(targets[0]);
            Assert.Equal(IncidentNoteClaimResult.Claimed, first.TryClaim(targets[2]));

            var restarted = new FileIncidentNoteRequestStateStore(directory);
            Assert.Equal(IncidentNoteClaimResult.AlreadyApplied, restarted.TryClaim(targets[0]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FileStore_WaitsForCrossInstanceMutationLeaseBeforeClaiming()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"monitor-note-peer-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        FileStream? blocker = null;
        try
        {
            blocker = new FileStream(
                Path.Combine(directory, "incident-note-requests.lock"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            var store = new FileIncidentNoteRequestStateStore(directory);
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var claim = Task.Run(() =>
            {
                started.TrySetResult(true);
                return store.TryClaim("incident-lock-test:0123456789ABCDEF01234567");
            });

            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var completedWhileBlocked = await Task.WhenAny(claim, Task.Delay(TimeSpan.FromMilliseconds(150)));
            Assert.NotSame(claim, completedWhileBlocked);

            blocker.Dispose();
            blocker = null;
            Assert.Equal(
                IncidentNoteClaimResult.Claimed,
                await claim.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            blocker?.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LegacyAuditReceipts_MaterializeWithAppliedWinningOverArmed()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 18, 19, 0, 0, TimeSpan.Zero));
        var audit = new InMemoryAuditStore(time);
        var state = new InMemoryIncidentNoteRequestStateStore();
        const string appliedTarget = "incident-applied:0123456789ABCDEF01234567";
        const string armedTarget = "incident-ambiguous:0123456789ABCDEF01234567";

        audit.Append("operator", "incident.note.write.commit", appliedTarget, "armed");
        audit.Append("operator", "incident.note.request", appliedTarget, "applied");
        audit.Append("operator", "incident.note.write.commit", armedTarget, "armed");

        IncidentNoteRequestStateMigration.MaterializeRetainedAuditReceipts(state, audit);

        Assert.Equal(IncidentNoteClaimResult.AlreadyApplied, state.TryClaim(appliedTarget));
        Assert.Equal(IncidentNoteClaimResult.Ambiguous, state.TryClaim(armedTarget));
    }

    private static string[] FindTargetsInSameShard(int count)
    {
        var byShard = new Dictionary<int, List<string>>();
        for (var index = 0; index < 10000; index++)
        {
            var target = $"incident-peer-{index:D5}:0123456789ABCDEF01234567";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(target));
            var shard = hash[0] & 63;
            if (!byShard.TryGetValue(shard, out var targets))
            {
                targets = [];
                byShard[shard] = targets;
            }

            targets.Add(target);
            if (targets.Count == count)
                return targets.ToArray();
        }

        throw new InvalidOperationException("Unable to find deterministic incident-note targets in one shard.");
    }

    private static IReadOnlyList<AuditEvent> ReadAll(IAuditStore audit)
    {
        var events = new List<AuditEvent>();
        for (var offset = 0; offset < 1000; offset += 100)
        {
            var page = audit.Read(offset, 100);
            events.AddRange(page);
            if (page.Count < 100) break;
        }
        return events;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FailFinalAppliedAuditStore(TimeProvider timeProvider) : IAuditStore
    {
        private readonly InMemoryAuditStore _inner = new(timeProvider);

        public void Append(string actor, string action, string target, string outcome)
        {
            if (action == "incident.note.request" && outcome == "applied")
                throw new IOException("Final note receipt unavailable.");

            _inner.Append(actor, action, target, outcome);
        }

        public IReadOnlyList<AuditEvent> Read(int offset, int limit) => _inner.Read(offset, limit);
    }

    private sealed class UnusedSharedStateStore : ISharedStateDocumentStore
    {
        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Shared state must not be used in this SingleNode test.");

        public Task<SharedStateWriteResult> CompareExchangeAsync(
            string key,
            long expectedVersion,
            string payloadJson,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Shared state must not be used in this SingleNode test.");
    }
}
